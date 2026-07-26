using BusinessEntityLayer;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using Org.BouncyCastle.Asn1.Ocsp;
using Sensel.XmlDB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using System.Web.Services.Description;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using static Portal.New.ISenselRestService;
using static Portal.New.OutputColumn;

namespace Portal.New
{
    //404 Error --> In IIS 8 have to add MIMIE Type and Http Handler.If Still Problem,have to Install Role and Featuers in Server .NET 4.5->WCF Services->HTTP
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "STLService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select STLService.svc or STLService.svc.cs at the Solution Explorer and start debugging.
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
    public class SenselRestService : ISenselRestService
    {

        protected XmlDBAccess dbx = new XmlDBAccess();
        string sHostName = getHostName();
        string sUserIP = GetUser_IP();
        enum headersIndex { trucksIndex, timesIndex, latitudeIndex, longitudeIndex, speedIndex, unixtsIndex, infoIndex, StopIndex };

        public string AuthenticateAPIKey(string API_Key, string MethodId, string format, string clientId)
        {

            if (string.Equals("xml", format, StringComparison.OrdinalIgnoreCase))
            {
                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Xml;
            }
            if (string.IsNullOrEmpty(API_Key))
            {
                try
                {
                    AdminService.Service admser = new AdminService.Service();
                    admser.WebServiceRequestLog(MethodId, sHostName, API_Key, "API_KEY_MISSING", "", "", sUserIP);
                }
                catch { }
                ErrorMessage customError = new ErrorMessage("404", "FAIL:API_KEY_PARAM_MISSING");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }

            if (clientId == null)
                clientId = string.Empty;

            string access = dbx.AuthenticateAPIKey(API_Key, MethodId, sUserIP, clientId);
            if (access.Contains("FAIL:"))
            {
                try
                {
                    AdminService.Service admser = new AdminService.Service();
                    admser.WebServiceRequestLog(MethodId, sHostName, API_Key, access, "", "", sUserIP);
                }
                catch { }

                ErrorMessage customError = new ErrorMessage("400", access);
                if (access.Contains("INVALID") || access.Contains("ACCESS_DENIED"))
                {
                    customError = new ErrorMessage("401", access);
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.Unauthorized);
                }
                if (access.Contains("OVER_QUERY_LIMIT"))
                {
                    customError = new ErrorMessage("429", access);
                    throw new WebFaultException<ErrorMessage>(customError, (HttpStatusCode)429);
                }
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            else
            {
                try
                {
                    string username = access.Split(',')[0];
                    AdminService.Service admser = new AdminService.Service();
                    admser.WebServiceRequestLog(MethodId, sHostName, API_Key, "", username, "", sUserIP);
                }
                catch { }
            }
            return access;
        }
        //Modified for BIAL VIOLATION REPORT SAHANA -10/07/2024
        public ViolationData BIALGetViolationReport(string key, string clientId, string vehicleId, string viotype, string timePeriod, string format)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "32", format, clientId);

            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string AccountName = objbll.GetAccountName(sessionId);
                string allviolations = "";
                string usersvehicles = objbll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");

                int groupid = objbll.getGroupId(sessionId);

                if (groupid == 0)
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:USER NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                else if (string.IsNullOrEmpty(vehicleId))
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                string Vehicles = "";
                if (vehicleId == "All")
                {
                    Vehicles = usersvehicles;
                }
                else
                {
                    Vehicles = vehicleId;
                }
                if (viotype == "All")
                {
                    allviolations = "'OVERSPEEDING','HARSH BRAKING','HIGH ACCELERATION','SHORT RUNTIME','TOTAL RUNTIME','NIGHT DRIVING','MAIN DISCONNECTED DRIVING','DUTY TIME'";
                }
                else
                {
                    allviolations = viotype;
                }

                DateTime Date = DateTime.Now;
                DateTime toDate = Date.AddHours(-1);

                if (string.IsNullOrEmpty(timePeriod))
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:TIMEPERIOD_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }

                DateTime fromDate = toDate.AddMinutes(-Convert.ToInt32(timePeriod));
                List<VioData> Vioreports = new List<VioData>();

                DataTable data = objbll.GetAPIViolationReport(fromDate.ToString("yyyy'-'MM'-'dd HH:mm:ss"), toDate.ToString("yyyy'-'MM'-'dd HH:mm:ss"), allviolations, Vehicles, sessionId);

                if (data != null && data.Rows.Count > 0)
                {
                    foreach (DataRow row in data.Rows)
                    {
                        VioData vioreport = new VioData
                        {
                            vehicle = row["vehicleId"].ToString(),
                            uid = row["uid"].ToString(),
                            alertType = row["type"].ToString(),
                            alertDateTime = row["datetime"].ToString(),
                            location = row["Location"].ToString(),
                            latitude = row["latitude"].ToString(),
                            longitude = row["longitude"].ToString(),
                            alertThreshold = row["alertThreshold"].ToString(),
                        };

                        // Check the value of the 'type' column and add corresponding columns
                        string type = row["type"].ToString();
                        switch (type)
                        {
                            case "OVERSPEEDING":
                                vioreport.speed = row["remarks"].ToString();
                                break;
                            case "HARSH BRAKING":
                                vioreport.hbvalue = row["remarks"].ToString();
                                break;
                            case "HIGH ACCELERATION":
                                vioreport.havalue = row["remarks"].ToString();
                                break;
                            case "NIGHT DRIVING":
                                vioreport.duration = row["duration"].ToString() + " mins";
                                break;
                            case "CONTINUOUS DRIVING":
                                vioreport.remarks = row["remarks"].ToString();
                                break;
                            default:
                                // Handle any other types here
                                break;
                        }

                        Vioreports.Add(vioreport);
                    }

                    ViolationData violationdata = new ViolationData
                    {
                        DATAELEMENTS = Vioreports
                    };
                    return violationdata;
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATA NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
            }
            catch (WebFaultException<ErrorMessage> ex)
            {
                // Rethrow WebFaultException with custom error message
                throw ex;
            }
            catch (Exception ex)
            {
                // Handle other exceptions with a generic error message
                ErrorMessage customError = new ErrorMessage("500", "FAIL:INTERNAL SERVER ERROR");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }
        public List<PositionData> LastPositionData(string Key, string VehicleId, string Geofence, string Transporter, string format, string clientId, string dctrip, string Temperature)
        {
            string access = AuthenticateAPIKey(Key, "1", format, clientId);
            try
            {
                List<PositionData> res = new List<PositionData>();
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = access.Split(',')[0];
                string SessionId = access.Split(',')[1];
                Object[] selectedValues = dbx.getLastPositions(SessionId, null);
                DateTime now = DateTime.UtcNow.AddSeconds(dbx.GetTimeZoneDiff(SessionId));
                DataTable accconfig = objbll.GetAccountGroupConfig(SessionId);
                string BatteryStatusinLand = "0";
                string ShowAuxInputinLanding = "0";
                if (accconfig.Rows.Count > 0)
                {
                    BatteryStatusinLand = Convert.ToString(accconfig.Rows[0]["ShowBatteryStatusinLand"]);
                    ShowAuxInputinLanding = Convert.ToString(accconfig.Rows[0]["ShowAuxInputinLanding"]);
                }
                if (!string.IsNullOrEmpty(VehicleId))
                    VehicleId = VehicleId.ToLower().Replace(" ", "");

                #region Find Data
                for (int i = 0; i < selectedValues.Length; i++)
                {
                    string veh = (((Object[])selectedValues[i])[(int)headersIndex.trucksIndex]).ToString();
                    if (!string.IsNullOrEmpty(VehicleId) && VehicleId != "all")
                    {
                        if (!VehicleId.Contains(veh.ToLower().Replace(" ", "")))
                            continue;
                    }
                    PositionData p = new PositionData();
                    #region General Data
                    string latStr = (((Object[])selectedValues[i])[(int)headersIndex.latitudeIndex]).ToString();
                    string longStr = (((Object[])selectedValues[i])[(int)headersIndex.longitudeIndex]).ToString();
                    double speed = Convert.ToDouble(((Object[])selectedValues[i])[(int)headersIndex.speedIndex]);
                    string timestamp = (((Object[])selectedValues[i])[(int)headersIndex.timesIndex]).ToString();
                    string vehinfo = (((Object[])selectedValues[i])[(int)headersIndex.infoIndex]).ToString();
                    string stop = (((Object[])selectedValues[i])[(int)headersIndex.StopIndex]).ToString();
                    string hvinp = "0";
                    if ((((Object[])selectedValues[i])[13]) != null)
                        hvinp = (((Object[])selectedValues[i])[13]).ToString();

                    string gvinp = "0";
                    if ((((Object[])selectedValues[i])[14]) != null)
                        gvinp = (((Object[])selectedValues[i])[14]).ToString();

                    string direction = "0";
                    if ((((Object[])selectedValues[i])[8]) != null)
                    {
                        direction = (Convert.ToInt32((((Object[])selectedValues[i])[8]).ToString()) % 360).ToString();
                    }

                    string positionsTxt = string.Empty;
                    if (access.Split(',')[4] == "1")
                    {
                        object[] PositiontxtData = dbx.getLatestPositiontxt(SessionId);
                        if (PositiontxtData != null)
                        {
                            int len = PositiontxtData.Length;
                            for (int k = 0; k < len; k++)
                            {
                                if ((((Object[])PositiontxtData[k])[0]).ToString() == veh)
                                {
                                    try
                                    {
                                        if (Convert.ToDateTime(timestamp) < Convert.ToDateTime((((Object[])PositiontxtData[k])[2]).ToString()).AddMinutes(5))
                                            positionsTxt = (((Object[])PositiontxtData[k])[1]).ToString();
                                        else
                                            positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                                    }
                                    catch { }
                                    break;
                                }
                            }
                            if (positionsTxt == "")
                            {
                                positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                            }
                        }
                        else
                        {
                            positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                        }
                    }
                    #endregion

                    #region Geofence
                    if (Geofence == "true")
                    {
                        DataTable dtgeo = dbx.GetLastGeofenceEntry(veh);
                        string GeofenceName = "N/A";
                        string GeofenceInOrOut = "N/A";
                        string GeofenceTime = "N/A";
                        if (dtgeo.Rows.Count > 0)
                        {
                            GeofenceName = dtgeo.Rows[0]["locationstr"].ToString();
                            GeofenceInOrOut = (dtgeo.Rows[0]["outtimedone"].ToString() == "0") ? "IN" : "OUT";
                            GeofenceTime = (dtgeo.Rows[0]["outtimedone"].ToString() == "0") ? Convert.ToDateTime(dtgeo.Rows[0]["intimestamp"].ToString()).ToString("yyyy'-'MM'-'dd HH:mm:ss") : Convert.ToDateTime(dtgeo.Rows[0]["intimestamp"].ToString()).ToString("yyyy'-'MM'-'dd HH:mm:ss");
                        }
                        p.geofencelocation = GeofenceName;
                        p.geofenceinout = GeofenceInOrOut;
                        p.geofencedatetime = GeofenceTime;
                    }
                    #endregion
                    //Added by Madhuri- for Temperature flag-04-04-2020
                    #region Temperature
                    if (Temperature == "true")
                    {
                        string temp = dbx.GetLastTempEntry(veh, timestamp);
                        if (string.IsNullOrEmpty(temp))
                            temp = "";
                        p.Temperature = temp;
                    }
                    #endregion

                    #region Distance
                    string dist = "0";
                    if (username.ToLower().Contains("ramco"))//Implemented ONLY For Ramco Cements.
                    {
                        string runtime = "";
                        object[] distobj = objbll.accumulateDistance(veh, now.ToString("yyyy-MM-dd") + " 00:00", objbll.formatDBDate(now), null);
                        if (distobj.Length > 0)
                        {
                            dist = distobj[0].ToString();
                            runtime = distobj[2].ToString();
                        }
                    }
                    #endregion

                    #region Transporter
                    if (Transporter == "true")
                    {
                        DataTable info = dbx.GetVehicleInfoData(veh);
                        string TransName = string.Empty;
                        string odometer = "0";
                        if (info.Rows.Count > 0)
                        {
                            TransName = info.Rows[0]["TransName"].ToString();
                            string OdometerInitialVal = info.Rows[0]["OdometerInitialVal"].ToString();
                            string OdometerInitialValDate = info.Rows[0]["OdometerInitialValDate"].ToString();
                            string GPSDistance = info.Rows[0]["GPSDistance"].ToString();
                            string GPSDistanceFrom = info.Rows[0]["GPSDistanceFrom"].ToString();
                            string GPSDistanceTo = info.Rows[0]["GPSDistanceTo"].ToString();
                            if (string.IsNullOrEmpty(GPSDistanceFrom) && !string.IsNullOrEmpty(OdometerInitialValDate))
                            {
                                if (username.ToLower().Contains("ramco"))//Implemented ONLY For Ramco Cements.
                                {
                                    object[] gpsdistobj = objbll.accumulateDistance(veh, objbll.formatDBDate(Convert.ToDateTime(OdometerInitialValDate)), now.ToString("yyyy-MM-dd") + " 00:00", null);
                                    if (gpsdistobj.Length > 0)
                                    {
                                        dbx.UpdateGPSDistAndDateinVehicleInfo(veh, objbll.formatDBDate(Convert.ToDateTime(OdometerInitialValDate)), now.ToString("yyyy-MM-dd") + " 00:00", gpsdistobj[0].ToString());
                                    }
                                }
                            }
                            else if (Convert.ToDateTime(GPSDistanceTo).ToString("yyyy-MM-dd") != now.ToString("yyyy-MM-dd"))
                            {
                                if (username.ToLower().Contains("ramco"))//Implemented ONLY For Ramco Cements.
                                {
                                    object[] gpsdistobj = objbll.accumulateDistance(veh, objbll.formatDBDate(Convert.ToDateTime(GPSDistanceTo)), now.ToString("yyyy-MM-dd") + " 00:00", null);
                                    if (gpsdistobj.Length > 0)
                                    {
                                        if (!string.IsNullOrEmpty(GPSDistance))
                                            GPSDistance = Math.Round(Convert.ToDecimal(GPSDistance) + Convert.ToDecimal(gpsdistobj[0].ToString())).ToString();
                                        dbx.UpdateGPSDistAndDateinVehicleInfo(veh, null, now.ToString("yyyy-MM-dd") + " 00:00", GPSDistance);
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(GPSDistance))
                            {
                                odometer = Math.Round(Convert.ToDecimal(OdometerInitialVal) + Convert.ToDecimal(GPSDistance) + Math.Round(Convert.ToDecimal(dist))).ToString();
                            }
                            else
                            {
                                odometer = dist;
                            }
                        }
                        p.transporter = TransName;
                        p.odometer = Math.Round(Convert.ToDecimal(odometer)).ToString();
                    }
                    #endregion

                    int uid = dbx.getUidFromConfigdata(veh);
                    #region Form data
                    p.vehicle = veh;
                    p.uid = uid.ToString();
                    p.vehicleinfo = vehinfo;
                    p.gpsupdatedtime = Convert.ToDateTime(timestamp).ToString("yyyy'-'MM'-'dd HH:mm:ss");
                    p.latitude = latStr;
                    p.longitude = longStr;
                    p.location = positionsTxt;
                    p.speed = speed.ToString();
                    p.stop = stop;
                    p.hvinp = hvinp;
                    p.gvinp = gvinp;
                    p.direction = direction;
					//string Lastpanic = dbx.Getvehiclestatus(veh);
                    string Lastpanic = dbx.getLastPanicInfo(veh, "");
                    if (Lastpanic == "MAIN CONNECTED")
					{
						p.MainDisConnected = "CONNECTED";
					}
					else
					{
						p.MainDisConnected = "DISCONNECTED";
					}
					if (BatteryStatusinLand == "1")
                    {
                        string batVal = (((Object[])selectedValues[i])[15]).ToString();//Battery Voltage BV
                        p.battery = objbll.GetBatteryPercentageByVoltage(Convert.ToInt32(batVal), uid).ToString();
                    }
                    if (ShowAuxInputinLanding == "1")
                    {
                        string auxinput = objbll.GetAuxInputByVehicle(veh, p.gpsupdatedtime);
                        p.auxinput = auxinput;
                    }
                    if (username.ToLower().Contains("ramco"))//Implemented ONLY For Ramco Cements.
                    {
                        p.ditancetoday = Math.Round(Convert.ToDecimal(dist)).ToString();
                    }
                    #endregion

                    #region Fuel Data
                    object[] fuelCalibData = dbx.getFuelCalibrationData(veh);
                    if (fuelCalibData.Length > 0)
                    {
                        DataTable dtfuel = dbx.GetFuelLevelInLtrs(veh, Convert.ToDateTime(p.gpsupdatedtime).AddMinutes(-5).ToString("yyyy-MM-dd HH:mm:ss"));
                        if (dtfuel.Rows.Count > 0)
                        {
                            p.fuel = dtfuel.Rows[0]["fuelInLtrs"].ToString();
                        }
                        else
                        {
                            fuelPortal.ShowFuelValue sfv = new fuelPortal.ShowFuelValue(dbx);
                            string fuelInfo = sfv.showFuelValue1(veh, p.gpsupdatedtime);
                            String[] fuelList;
                            if (!String.IsNullOrEmpty(fuelInfo))
                            {
                                fuelList = fuelInfo.Split(',');
                                p.fuel = fuelList[0];
                                string fuellevel = (((Object[])selectedValues[i])[17]).ToString();
                                dbx.InsertFuelLevelInLtrs(veh, p.gpsupdatedtime, fuellevel, p.fuel);
                            }
                        }
                    }
                    #endregion
                    if (dctrip == "1")
                    {
                        p.capacity = dbx.GetCapacity(veh);
                        DataTable driverinfo = dbx.GetDriverInfoByVehicle(veh);
                        if (driverinfo.Rows.Count > 0)
                        {
                            p.driverId = driverinfo.Rows[0]["DriverId"].ToString();
                            p.driverName = driverinfo.Rows[0]["Driver"].ToString();
                            p.driverLicense = driverinfo.Rows[0]["LicenceNo"].ToString();
                        }
                        DataTable dcDetails = dbx.GetCurrentDcTripDetails(veh);
                        if (dcDetails.Rows.Count > 0)
                        {
                            p.tripStatus = "In Trip";
                            p.tripEntry = Convert.ToDateTime(dcDetails.Rows[0]["DateTime"]).ToString("yyyy-MM-dd HH:mm:ss");
                            p.loadingPlaceName = dcDetails.Rows[0]["LoadingPlaceName"].ToString();
                            List<TripDetails> td = new List<TripDetails>();
                            DateTime nowdt = DateTime.Now;
                            for (int j = 0; j < dcDetails.Rows.Count; j++)
                            {
                                TripDetails t = new TripDetails();
                                t.unloadingPlaceName = dcDetails.Rows[j]["Name"].ToString();
                                t.distanceFromPlant = dcDetails.Rows[j]["RTKm"].ToString();
                                t.quantity = dcDetails.Rows[j]["Quantity"].ToString();
                                if (dcDetails.Rows[j]["IsDelivered"].ToString() == "True")
                                {
                                    t.deliveredQty = dcDetails.Rows[j]["DeliveredQty"].ToString();
                                    t.deliveredTime = Convert.ToDateTime(dcDetails.Rows[j]["DeliveredOn"]).ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else if (!string.IsNullOrEmpty(dcDetails.Rows[j]["Latitude"].ToString()) && !string.IsNullOrEmpty(dcDetails.Rows[j]["Longitude"].ToString()))
                                {
                                    long distance = Convert.ToInt64(objbll.getgoogleDistance(latStr, longStr, dcDetails.Rows[j]["Latitude"].ToString(), dcDetails.Rows[j]["Longitude"].ToString()));
                                    object[] tripparams = objbll.getConfgParamsbySession(dbx.GetSessionIdByVehicleId(veh), veh);
                                    string ETA = objbll.findETA(nowdt, distance, 33, Convert.ToInt32(tripparams[2].ToString()), tripparams[4].ToString(), tripparams[3].ToString(), Convert.ToInt32(tripparams[10].ToString()), Convert.ToInt32(tripparams[11].ToString()), Convert.ToInt32(tripparams[14].ToString().Split(',')[0]) * 60, 0);
                                    nowdt = Convert.ToDateTime(ETA.Split(',')[0]).AddHours(2);//Default unloading time is adding
                                    t.ETA = ETA.Split(',')[0];
                                }
                                td.Add(t);
                            }
                            p.tripDetails = td;
                        }
                        else
                        {
                            p.tripStatus = "No Trip";
                        }
                    }
                    //Added by Madhuri- for TamperDetect flag-07-10-2021
                    #region TamperDetect
                    if (!string.IsNullOrEmpty(veh))
                    {
                        DataTable dtTamperDefect = dbx.getPrevPanicState(veh);
                        string TamperDefect = "";
                        if (dtTamperDefect.Rows.Count > 0)
                        {
                            TamperDefect = dtTamperDefect.Rows[0]["start"].ToString();
                        }
                        p.TamperDetect = TamperDefect;
                    }
                    #endregion
                    res.Add(p);
                }
                #endregion
                return res;

            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", ex.ToString());
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }
        public List<DistanceData> DistanceReport(string Key, string VehicleId, string uid, string fromDate, string toDate, string format, string clientId)
        {
            string access = AuthenticateAPIKey(Key, "2", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (string.IsNullOrEmpty(VehicleId) && string.IsNullOrEmpty(uid))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    if (uid.ToLower() == "all")
                    {
                        VehicleId = usersvehicles;
                    }
                    else
                    {
                        string[] uids = uid.Split(',');
                        for (int i = 0; i < uids.Length; i++)
                        {
                            try
                            {
                                VehicleId += dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + ",";
                            }
                            catch { }
                        }
                    }
                }
                else if (VehicleId.ToLower() == "all")
                {
                    VehicleId = usersvehicles;
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }

                if ((to - from).TotalDays > 15)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<DistanceData> dlist = new List<DistanceData>();
                string[] vehicleids = VehicleId.Split(',');
                for (int i = 0; i < vehicleids.Length; i++)
                {
                    VehicleId = vehicleids[i];
                    if (!string.IsNullOrEmpty(VehicleId))
                    {
                        string[] splitVeh = usersvehicles.Split(',');
                        bool isFound = false;
                        for (int j = 0; j < splitVeh.Length; j++)
                        {
                            if (splitVeh[j].Replace(" ", "") == vehicleids[i].Replace(" ", ""))
                            {
                                isFound = true;
                                VehicleId = splitVeh[j];
                                break;
                            }
                        }
                        if (isFound)
                        {
                            try
                            {
                                object[] dist = objBll.accumulateDistance(VehicleId, from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), null);
                                DistanceData d = new DistanceData();
                                d.uid = dbx.getUidFromConfigdata(VehicleId).ToString();
                                d.vehicle = VehicleId;
                                d.fromdate = from.ToString("yyyy-MM-dd HH:mm:ss");
                                d.todate = to.ToString("yyyy-MM-dd HH:mm:ss");
                                d.distance = dist[0].ToString();
                                d.runtime = dist[2].ToString();
                                d.startlatlng = dist[3].ToString();
                                d.endlatlng = dist[4].ToString();
                                dlist.Add(d);
                            }
                            catch { }
                        }
                    }
                }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND_CHECK_VEHICLE_ID");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }
        public List<RouteReport> RouteReport(string Key, string VehicleId, string uid, string fromDate, string toDate, string calc_Distance, string format, string clientId)
        {
            string access = AuthenticateAPIKey(Key, "3", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (string.IsNullOrEmpty(VehicleId) && string.IsNullOrEmpty(uid))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    if (uid.ToLower() == "all")
                    {
                        VehicleId = usersvehicles;
                    }
                    else
                    {
                        string[] uids = uid.Split(',');
                        for (int i = 0; i < uids.Length; i++)
                        {
                            try
                            {
                                VehicleId += dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + ",";
                            }
                            catch { }
                        }
                    }
                }
                else if (VehicleId.ToLower() == "all")
                {
                    VehicleId = usersvehicles;
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                if (VehicleId.Contains(","))
                    VehicleId = VehicleId.TrimEnd(',');

                int range_limit = 168;
                if (VehicleId.Contains(","))
                    range_limit = 24;
                if ((to - from).TotalHours > range_limit)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<RouteReport> dlist = new List<RouteReport>();
                string[] vehicleids = VehicleId.Split(',');
                for (int i = 0; i < vehicleids.Length; i++)
                {
                    VehicleId = vehicleids[i];
                    if (!string.IsNullOrEmpty(VehicleId) && usersvehicles.Contains(VehicleId))
                    {
                        try
                        {
                            object[] values = objBll.getRouteData(from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), VehicleId, false);
                            RouteReport d = new RouteReport();
                            d.uid = dbx.getUidFromConfigdata(VehicleId).ToString();
                            d.vehicle = VehicleId;
                            d.fromdate = from.ToString("yyyy-MM-dd HH:mm:ss");
                            d.todate = to.ToString("yyyy-MM-dd HH:mm:ss");

                            if (calc_Distance != null)
                            {
                                if (calc_Distance.ToLower() == "true" || calc_Distance.ToLower() == "1")
                                {
                                    object[] dist = objBll.accumulateDistance(VehicleId, from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), null, values);
                                    d.distance = dist[0].ToString();
                                    d.runtime = dist[2].ToString();
                                    d.startlatlng = dist[3].ToString();
                                    d.endlatlng = dist[4].ToString();
                                }
                            }
                            //Latitude/Longtide Points
                            List<RouteData> rd = new List<RouteData>();
                            for (int j = 0; j < values.Length; j++)
                            {
                                RouteData r = new RouteData();
                                r.latitude = (((Object[])values[j])[(int)(BusinessLogicLayer.BLL.headers.latitude)]).ToString();
                                r.longitude = (((Object[])values[j])[(int)(BusinessLogicLayer.BLL.headers.longitude)]).ToString();
                                r.speed = (((Object[])values[j])[(int)(BusinessLogicLayer.BLL.headers.speed)]).ToString();
                                r.stop = (((Object[])values[j])[(int)(BusinessLogicLayer.BLL.headers.stop)]).ToString();
                                r.timestamp = Convert.ToDateTime((((Object[])values[j])[(int)(BusinessLogicLayer.BLL.headers.timestamp)]).ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                                rd.Add(r);
                            }
                            d.RouteData = rd;

                            dlist.Add(d);
                        }
                        catch { }
                    }
                }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND_CHECK_VEHICLE_ID");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }
        public List<RFIDData> RFIDReport(string Key, string VehicleId, string uid, string fromDate, string toDate, string format, string clientId)
        {
            string access = AuthenticateAPIKey(Key, "4", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (string.IsNullOrEmpty(VehicleId) && string.IsNullOrEmpty(uid))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    if (uid.ToLower() == "all")
                    {
                        VehicleId = usersvehicles;
                    }
                    else
                    {
                        string[] uids = uid.Split(',');
                        for (int i = 0; i < uids.Length; i++)
                        {
                            try
                            {
                                VehicleId += dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + ",";
                            }
                            catch { }
                        }
                    }
                }
                else if (VehicleId.ToLower() == "all")
                {
                    VehicleId = usersvehicles;
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                int range_limit = 268;
                if (VehicleId.Contains(","))
                    range_limit = 48;
                if ((to - from).TotalHours > range_limit)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<RFIDData> dlist = new List<RFIDData>();
                try
                {
                    Object[] values = objBll.getRFID_data("'" + VehicleId.Replace(",", "','") + "'", from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), "1");
                    List<RFIDData> rd = new List<RFIDData>();
                    for (int j = 0; j < values.Length; j++)
                    {
                        RFIDData r = new RFIDData();
                        r.vehicle = (((Object[])values[j])[0]).ToString();
                        r.uid = dbx.getUidFromConfigdata(r.vehicle).ToString();
                        r.timestamp = Convert.ToDateTime((((Object[])values[j])[1]).ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                        r.latitude = (((Object[])values[j])[2]).ToString();
                        r.longitude = (((Object[])values[j])[3]).ToString();
                        r.rfid = (((Object[])values[j])[4]).ToString();
                        dlist.Add(r);
                    }
                }
                catch { }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }
        public List<TripData> TripReport(string Key, string VehicleId, string uid, string fromDate, string toDate, string violations, string format, string clientId)
        {
            string access = AuthenticateAPIKey(Key, "5", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    if (uid.ToLower() == "all")
                    {
                        VehicleId = usersvehicles;
                    }
                    else
                    {
                        string[] uids = uid.Split(',');
                        for (int i = 0; i < uids.Length; i++)
                        {
                            try
                            {
                                VehicleId += "'" + dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + "',";
                            }
                            catch { }
                        }
                    }
                }
                else if (string.IsNullOrEmpty(VehicleId))
                {
                    VehicleId = usersvehicles;
                }
                else if (VehicleId.ToLower() == "all")
                {
                    VehicleId = usersvehicles;
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                int range_limit = 360; //15 Days
                if (VehicleId.Contains(","))
                    range_limit = 360;
                if ((to - from).TotalHours > range_limit)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                string violtypes = "'OVERSPEEDING','HARSH BRAKING','HIGH ACCELERATION'";
                List<TripData> dlist = new List<TripData>();
                try
                {
                    DataTable dt = objBll.GetVehicleDCTrip("'" + VehicleId.Replace(",", "','") + "'", from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"));
                    List<RFIDData> rd = new List<RFIDData>();
                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        TripData r = new TripData();
                        string dcOn = dateformat(dt.Rows[j]["DateTime"].ToString());
                        string tripEnd = dateformat(dt.Rows[j]["TripEndsOn"].ToString());
                        string delvdOn = dateformat(dt.Rows[j]["DeliveredOn"].ToString());
                        string veh = dt.Rows[j]["VehicleID"].ToString();
                        r.vehicle = veh;
                        r.driver = objBll.getdrivername(veh, dcOn).Split(',')[0];
                        r.starttime = dcOn;
                        r.deliveredtime = delvdOn;
                        r.endtime = tripEnd;
                        r.source = dt.Rows[j]["Source"].ToString();
                        r.destination = dt.Rows[j]["Destination"].ToString();
                        if (!string.IsNullOrEmpty(violations))
                        {
                            if (violations.ToLower() == "true" || violations.ToLower() == "1")
                            {
                                string todate = (tripEnd == "" && delvdOn == "") ? DateTime.Now.ToString("yyyy-MM-dd HH:mm") : tripEnd;
                                todate = (todate == "" && delvdOn != "") ? delvdOn : DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                                DataTable dtViol = objBll.GetViolationsByVehicleIDs("'" + veh + "'", dcOn, todate, violtypes);
                                List<Violation> vd = new List<Violation>();
                                for (int k = 0; k < dtViol.Rows.Count; k++)
                                {
                                    Violation v = new Violation();
                                    v.type = dtViol.Rows[k]["type"].ToString();
                                    v.latitude = dtViol.Rows[k]["latitude"].ToString();
                                    v.longitude = dtViol.Rows[k]["longitude"].ToString();
                                    v.remarks = dtViol.Rows[k]["remarks"].ToString();
                                    v.timestamp = dateformat(dtViol.Rows[k]["timestamp"].ToString());
                                    vd.Add(v);
                                }
                                r.violations = vd;
                            }
                        }
                        dlist.Add(r);
                    }
                }
                catch { }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }
        public List<FuelReport> FuelReport(string Key, string VehicleId, string fromDate, string toDate, string format, string clientId)
        {
            string access = AuthenticateAPIKey(Key, "6", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (string.IsNullOrEmpty(VehicleId))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                int range_limit = 745;
                if (VehicleId.Contains(","))
                    range_limit = 361;
                if ((to - from).TotalHours > range_limit)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<FuelReport> dlist = new List<FuelReport>();
                if (!string.IsNullOrEmpty(VehicleId))
                {
                    try
                    {
                        object[] values = dbx.getFuelData(VehicleId, dbx.formatDBDate(from), dbx.formatDBDate(to));
                        FuelReport d = new FuelReport();
                        d.uid = dbx.getUidFromConfigdata(VehicleId).ToString();
                        d.vehicle = VehicleId;
                        d.fromdate = from.ToString("yyyy-MM-dd HH:mm:ss");
                        d.todate = to.ToString("yyyy-MM-dd HH:mm:ss");

                        //Latitude/Longtide Points
                        List<FuelData> rd = new List<FuelData>();
                        for (int j = 0; j < values.Length; j++)
                        {
                            FuelData r = new FuelData();
                            r.timestamp = Convert.ToDateTime((((Object[])values[j])[1]).ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                            r.latitude = (((Object[])values[j])[2]).ToString();
                            r.longitude = (((Object[])values[j])[3]).ToString();
                            r.location = objBll.getPositionTxt((((Object[])values[j])[2]).ToString(), (((Object[])values[j])[3]).ToString(), -1);
                            r.before_filling = ((Object[])(values[j]))[4].ToString();
                            r.estimated_filled = (Convert.ToDouble(((Object[])(values[j]))[5]) - Convert.ToDouble(((Object[])(values[j]))[4])).ToString();
                            r.actual_filled = ((Object[])(values[j]))[6].ToString();
                            r.distance_travelled = ((Object[])(values[j]))[7].ToString();
                            r.mileage = ((Object[])(values[j]))[8].ToString();
                            rd.Add(r);
                        }
                        d.FuelData = rd;

                        dlist.Add(d);
                    }
                    catch { }
                }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND_CHECK_VEHICLE_ID");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }
        public List<TripData> TripTracking(string Key, string VehicleId, string uid, string fromDate, string toDate, string violations, string format, string clientId)
        {
            string access = AuthenticateAPIKey(Key, "5", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    if (uid.ToLower() == "all")
                    {
                        VehicleId = usersvehicles;
                    }
                    else
                    {
                        string[] uids = uid.Split(',');
                        for (int i = 0; i < uids.Length; i++)
                        {
                            try
                            {
                                VehicleId += "'" + dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + "',";
                            }
                            catch { }
                        }
                    }
                }
                else if (string.IsNullOrEmpty(VehicleId))
                {
                    VehicleId = usersvehicles;
                }
                else if (VehicleId.ToLower() == "all")
                {
                    VehicleId = usersvehicles;
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                int range_limit = 360; //15 Days
                if (VehicleId.Contains(","))
                    range_limit = 360;
                if ((to - from).TotalHours > range_limit)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                string violtypes = "'OVERSPEEDING','HARSH BRAKING','HIGH ACCELERATION'";
                List<TripData> dlist = new List<TripData>();
                try
                {
                    DataTable dt = objBll.GetVehicleDCTrip("'" + VehicleId.Replace(",", "','") + "'", from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"));
                    List<RFIDData> rd = new List<RFIDData>();
                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        TripData r = new TripData();
                        string dcOn = dateformat(dt.Rows[j]["DateTime"].ToString());
                        string tripEnd = dateformat(dt.Rows[j]["TripEndsOn"].ToString());
                        string delvdOn = dateformat(dt.Rows[j]["DeliveredOn"].ToString());
                        string veh = dt.Rows[j]["VehicleID"].ToString();
                        r.vehicle = veh;
                        r.driver = objBll.getdrivername(veh, dcOn).Split(',')[0];
                        r.starttime = dcOn;
                        r.deliveredtime = delvdOn;
                        r.endtime = tripEnd;
                        r.source = dt.Rows[j]["Source"].ToString();
                        r.destination = dt.Rows[j]["Destination"].ToString();
                        if (!string.IsNullOrEmpty(violations))
                        {
                            if (violations.ToLower() == "true" || violations.ToLower() == "1")
                            {
                                string todate = (tripEnd == "" && delvdOn == "") ? DateTime.Now.ToString("yyyy-MM-dd HH:mm") : tripEnd;
                                todate = (todate == "" && delvdOn != "") ? delvdOn : DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                                DataTable dtViol = objBll.GetViolationsByVehicleIDs("'" + veh + "'", dcOn, todate, violtypes);
                                List<Violation> vd = new List<Violation>();
                                for (int k = 0; k < dtViol.Rows.Count; k++)
                                {
                                    Violation v = new Violation();
                                    v.type = dtViol.Rows[k]["type"].ToString();
                                    v.latitude = dtViol.Rows[k]["latitude"].ToString();
                                    v.longitude = dtViol.Rows[k]["longitude"].ToString();
                                    v.remarks = dtViol.Rows[k]["remarks"].ToString();
                                    v.timestamp = dateformat(dtViol.Rows[k]["timestamp"].ToString());
                                    vd.Add(v);
                                }
                                r.violations = vd;
                            }
                        }
                        dlist.Add(r);
                    }
                }
                catch { }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }

        public List<PositionData> GetVehicleDetails(string key, string clientId, string plantId)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(key, "7", "Json", clientId);
            try
            {
                string SessionId = access.Split(',')[1];
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                DataTable vehDt = objbll.GetVehicleDetailsByPlantId(plantId, SessionId);
                List<PositionData> res = new List<PositionData>();
                for (int i = 0; i < vehDt.Rows.Count; i++)
                {
                    string status = vehDt.Rows[i]["Status"].ToString();
                    string eta = Convert.ToString(vehDt.Rows[i]["ETA"]);
                    if (status == "In Plant")
                        status = "0";
                    else if (status == "In Trip")
                        status = "1";
                    else if (status == "Towards Plant")
                        status = "2";
                    else if (status == "No DC")
                    {
                        status = "3";
                        eta = "NA";
                    }
                    else if (status == "Not Reachable")
                    {
                        status = "4";
                        eta = "NA";
                    }
                    if (eta != "" && eta != "NA")
                        eta = objbll.formatDBDate(Convert.ToDateTime(eta));
                    res.Add(new PositionData()
                    {
                        vehicle = vehDt.Rows[i]["VehicleId"].ToString(),
                        gpsupdatedtime = Convert.ToString(vehDt.Rows[i]["timestamp"]) != "" ?
                                         objbll.formatDBDate(Convert.ToDateTime(Convert.ToString(vehDt.Rows[i]["timestamp"]))) : "",
                        latitude = vehDt.Rows[i]["Latitude"].ToString(),
                        longitude = vehDt.Rows[i]["Longitude"].ToString(),
                        status = status,
                        eta = eta,
                        totalKms = vehDt.Rows[i]["distance"].ToString()
                    });
                }
                return res;
            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", ex.ToString());
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }
        public List<CustomerStock> GetCustomerStockDetails(string key, string clientId, string plantId)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(key, "8", "Json", clientId);
            try
            {
                string SessionId = access.Split(',')[1];
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                DataTable custDt = objbll.GetCustomerDetailsByPlantId(plantId);
                List<CustomerStock> res = new List<CustomerStock>();
                for (int i = 0; i < custDt.Rows.Count; i++)
                {
                    res.Add(new CustomerStock()
                    {
                        customerId = custDt.Rows[i]["CustomerCode"].ToString(),
                        stockLevel = custDt.Rows[i]["StockLevel"].ToString(),
                        next3DaysUsage = custDt.Rows[i]["Next3DaysExpected"].ToString(),
                        updtTime = objbll.formatDBDate(Convert.ToDateTime(custDt.Rows[i]["timestamp"].ToString()))
                    });
                }
                return res;
            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", ex.ToString());
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }

        public Result InsertVehicleEvents(string key, string clientId, string vehicleId, string eventName, string eventType, string timestamp, string duration, string source)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(key, "9", "Json", clientId);
            try
            {
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                int res = objBll.insertVehicleEvents(vehicleId, eventName, eventType, timestamp, duration, source);
                Result result = new Result();
                if (res > 0)
                {
                    result.result = "Success";
                    result.statuscode = "200";
                }
                else
                {
                    result.result = "Error";
                    result.statuscode = "500";
                }
                return result;
            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", ex.ToString());
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }

        public AppDetails GetAppCurrentVersion(string packageName)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL(System.Web.Configuration.WebConfigurationManager.AppSettings["adminPanelDBConnectionString"]);
            DataTable table = objbll.GetAppCurrentVersion(packageName);
            AppDetails appDetails = new AppDetails();
            if (table != null && table.Rows.Count > 0)
            {
                appDetails.AppName = table.Rows[0]["AppName"].ToString();
                appDetails.VersionCode = table.Rows[0]["VersionCode"].ToString();
                appDetails.Priority = table.Rows[0]["Priority"].ToString();
                appDetails.StableVersion = table.Rows[0]["StableVersion"].ToString();
                appDetails.DomainUrl = table.Rows[0]["DomainUrl"].ToString();
                appDetails.GoogleApiClientId = table.Rows[0]["GoogleApiClientId"].ToString();
            }
            return appDetails;
        }

        /// <summary>
        /// Help to Turn ON and Turn OFF the Ignition
        /// Added By : Harish on 31/08/2020
        /// </summary>
        /// <param name="command">Indicate the ignition state (START /STOP) </param>
        public List<Result> StartorStopVehicle(string key, string clientId, string vehicleId, string command)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(key, "10", "Json", clientId);
            List<Result> Finalres = new List<Result>();
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = access.Split(',')[0];
                string SessionId = access.Split(',')[1];

                if (string.IsNullOrEmpty(vehicleId))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }

                if (string.IsNullOrEmpty(command))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL: COMMAND_DETAILS IS_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }

                string tempCmd = command.ToUpper().Replace(" ", string.Empty);
                if (tempCmd == "START" || tempCmd == "ON" || tempCmd == "1")
                    command = "Start";
                else if (tempCmd == "STOP" || tempCmd == "OFF" || tempCmd == "0")
                    command = "Stop";

                if (command == "Start" || command == "Stop")
                {
                    string usersvehicles = objbll.GetVehicles(SessionId).Replace("'", "").Replace(",Select", "").ToUpper();
                    //string[] vehicleList = Regex.Replace(vehicleId, @"\s+", "").Split(',');
                    string[] vehicleList = vehicleId.Split(',');
                    foreach (string vehicle in vehicleList)
                    {
                        Result res = new Result();
                        string vehexit = objbll.GetCorrectVehicleNumber(vehicle);
                        if (!string.IsNullOrEmpty(vehexit))
                        {
                            if (usersvehicles.Contains(vehexit.ToUpper()))
                            {
                                res.result = objbll.StartorStopVehicle(vehicleId, command, SessionId);
                                res.statuscode = "200";
                            }
                            else
                            {
                                res.result = vehicle + "- VEHICLE_iS_NOT_AVAILABLE_IN_LOGIN";
                                res.statuscode = "404";
                            }
                        }
                        else
                        {
                            res.result = vehicle + "- INVALID_VEHICLE_NUMBER";
                            res.statuscode = "404";
                        }
                        Finalres.Add(res);
                    }
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL: INVALID_COMMAND_(ENTER START/STOP/ON/OFF)");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
                }
                return Finalres;
            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", ex.ToString());
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
            return Finalres;
        }

        /// <summary>
        /// Share Entered Vehicle Panic Details
        /// Added By : Harish on 31/08/2020
        /// </summary>
        /// <param name="vehicleId">Enter panic details required vehicle number </param>
        public List<PanicData> PanicReport(string key, string clientId, string vehicleId, string fromDate, string toDate)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(key, "11", "Json", clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (string.IsNullOrEmpty(vehicleId))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");

                if (string.IsNullOrEmpty(vehicleId))
                    vehicleId = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");

                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }

                if (to < from)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:TO_DATE_IS_LESSTHEN_FROMDATE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                StringBuilder sb = new StringBuilder();
                string vehicleid;
                while (vehicleId != "")
                {
                    int index = vehicleId.IndexOf(",");
                    if (index >= 0)
                    {
                        vehicleid = vehicleId.Substring(0, index);
                        vehicleId = vehicleId.Substring(++index);
                    }
                    else
                    {
                        vehicleid = vehicleId.Substring(0);
                        vehicleId = "";
                    }
                    string correctVehicleNo = objBll.GetCorrectVehicleNumber(vehicleid);
                    if (string.IsNullOrEmpty(vehicleid))
                        correctVehicleNo = vehicleid;
                    if (vehicleId != "")
                        sb.Append("vehicleid='" + correctVehicleNo.Replace("'", "").Replace("\"", "") + "' or ");
                    else
                        sb.Append("vehicleid='" + correctVehicleNo.Replace("'", "").Replace("\"", "") + "'");
                }
                DataTable PanicTbl = objBll.getPanicInfo(fromDate, toDate, sb.ToString()).Tables[0];
                List<PanicData> pinfo = new List<PanicData>();

                foreach (DataRow dr in PanicTbl.Rows)
                {
                    string positionTxt = "";
                    Object[] latlong = null;
                    latlong = dbx.getLatLongByVehicle(dr["vehicleId"].ToString(), dr["alertTimestamp"].ToString());
                    if (latlong.Length > 0)
                        positionTxt = objBll.getPositionTxt((((Object[])(latlong[0]))[0]).ToString(), (((Object[])(latlong[0]))[1]).ToString(), -1);

                    PanicData pi = new PanicData();
                    pi.VehicleId = Convert.ToString(dr["vehicleId"]);
                    pi.Date = Convert.ToDateTime(dr["alertTimestamp"]).ToString("dd/MM/yyyy");
                    pi.Time = Convert.ToDateTime(dr["alertTimestamp"]).ToString("HH:mm:ss");
                    pi.Location = positionTxt;
                    pi.Latitude = (((Object[])(latlong[0]))[0]).ToString();
                    pi.Longitude = (((Object[])(latlong[0]))[1]).ToString();
                    pinfo.Add(pi);
                }
                if (pinfo.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND_CHECK_VEHICLE_ID");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return pinfo;
            }
            catch (Exception e)
            {
                ErrorMessage customError = new ErrorMessage("400", e.Message);
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
            }
            finally
            {
                dbx.close();
            }
        }


        public string PushPduPayload(string pdu)
        {
            String response = null;
            response = writeBT.pduParser(pdu);
            return (response);
        }

        #region Metro-Services
        public string PostTripInvoiceData(string Key, string clientId, string data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(Key, "6", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                if (data.Contains("TESTSENSELIN"))
                    return data;
                objbll.STL_RecordErrorMessage(data);
                try
                {
                    data = data.Replace("userId", "StoreNo");
                    data = data.Replace("}{", "},{");
                    string SessionId = access.Split(',')[1];
                    string accountid = objbll.GetAccountId(SessionId).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<Metro_Invoices> objMetroInvoiceList = jsonSerializer.Deserialize<List<Metro_Invoices>>(data);
                    for (int i = 0; i < objMetroInvoiceList.Count; i++)
                    {
                        objbll.Update_Metro_Invoices(objMetroInvoiceList[i], accountid);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Post Invoice Data Upload Failed", "MetroDataUploadStatus");
                    objbll.ExceptionLogging("PostTripInvoiceData", "ui.vehicle-tracking", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Post Invoice Data Upload Failed", "MetroDataUploadStatus");
                    objbll.ExceptionLogging("PostTripInvoiceData", "ui.vehicle-tracking", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }

                ////Push Data to Azure Server
                //for (int r = 0; r < 5; r++)
                //{
                //    try
                //    {
                //        var iData = new JavaScriptSerializer().Serialize(data);
                //        string postData = "{\"Key\":\"" + Key + "\",\"clientId\":\"" + clientId + "\",\"data\":" + iData + "}";
                //        string res = objbll.MakeHttpRequest("https://metro.vehicle-tracking.co.in/SenselRestService.svc/rest/v3/PostTripInvoiceData", "POST", "application/json", postData);
                //        break;
                //    }
                //    catch (Exception ex)
                //    {
                //        objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Azure Post Invoice Data Upload Failed", "MetroDataUploadStatus");
                //        objbll.ExceptionLogging("PostTripInvoiceData", "metro.vehicle-tracking.co.in", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                //    }
                //}

                //{
                //    DataTable dtGetLocationDetails = objbll.GetAdaniLocationDetails();
                //    //if (DateTime.Now > startTime && DateTime.Now < endTime)
                //    //{
                //    if (dtGetLocationDetails.Rows.Count > 0)
                //    {
                //        for (int i = 0; i < dtGetLocationDetails.Rows.Count; i++)
                //        {

                //            string location = objbll.getMMIGeoCodeLatLng(dtGetLocationDetails.Rows[i]["Address"].ToString());
                //            if (!string.IsNullOrEmpty(location) && location != "No Data" && location.Contains(","))
                //            {
                //                int res = objbll.UpdateAdaniLatitudeLongitudeDetails(location.Split(',')[0], location.Split(',')[1], dtGetLocationDetails.Rows[i]["ID"].ToString());
                //            }
                //        }
                //    }
                //}
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Post Invoice Data Upload Failed", "MetroDataUploadStatus");
                objbll.ExceptionLogging("PostTripInvoiceData", "ui.vehicle-tracking", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                return "Upload Failed";
            }
        }
        public string UploadTripInvoiceFile(string Key, string clientId, string InvoiceNum, string fileName, Stream filedata)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(Key, "7", "json", clientId);
            try
            {
                string FilePath = Path.Combine(HostingEnvironment.MapPath("~/Uploads/"), fileName);

                MultipartParser parser = new MultipartParser(filedata);
                if (parser.Success)
                {
                    File.WriteAllBytes(FilePath, parser.FileContents);
                }
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                objbll.MetroUpdateInvoiceFileName(InvoiceNum, fileName);
                BusinessLogicLayer.AmazonS3Upload.UploadFiles(HostingEnvironment.MapPath("~/Uploads/"), fileName, "db-flatfile-backup", "", "PublicRead");
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }
        public UserData GetLoginDetails(string username, string password)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            //string sHost = getHostName();
            string sHost = objbll.GetBaseURL();
            string sUserIp = GetUser_IP();
            DataTable dt = objbll.GetLoginDetails(username, password);
            UserData ud = new UserData();
            if (dt.Rows.Count > 0)
            {
                ud.name = dt.Rows[0]["Name"].ToString();
                ud.sessionid = dt.Rows[0]["SessionID"].ToString();
                ud.accountid = dt.Rows[0]["AccountID"].ToString();
                ud.result = "success";
                if (string.IsNullOrEmpty(dt.Rows[0][2].ToString()))
                {
                    ud.domain1 = sHost;
                    ud.domain2 = sHost;
                }
                else
                {
                    ud.domain1 = dt.Rows[0]["Domain1"].ToString();
                    ud.domain2 = dt.Rows[0]["Domain2"].ToString();
                }
            }
            else
            {
                ud.result = "UserName or Password is incorrect";
            }
            return ud;
        }
        public AccountConfig GetLoginConfigData(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            string sHost = getHostName();
            string sUserIp = GetUser_IP();
            string username = objBll.getUserId(sessionid);
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetLoginConfigData", "Metro-app", "", "", sessionid, sHost, sUserIp));

            string globalid = objBll.GetGlobalAccountIdBySession(sessionid);
            string Transpoterid = objBll.getTransporterIdByLogin(username);
            DataTable table = null;
            if (!string.IsNullOrEmpty(globalid))
                table = objBll.GetBlockingDetails(username, globalid);

            AccountConfig account = new AccountConfig();
            DataTable dt = objBll.GetAccountGroupConfig(sessionid);
            if (dt.Rows.Count > 0)
            {
                account.transpoterid = Transpoterid;
                account.map_refreshrate = dt.Rows[0]["refreshrate"].ToString();
                account.map_type = dt.Rows[0]["maprequest"].ToString();
                account.map_view = dt.Rows[0]["MapView"].ToString();
                account.dasboard_refreshrate = dt.Rows[0]["dashboardrefresh"].ToString();
                account.landing_refreshrate = dt.Rows[0]["landingrefresh"].ToString();
                account.aux_input = dt.Rows[0]["ShowAuxInputinLanding"].ToString();
                account.map_zoom_level = Convert.ToInt16(dt.Rows[0]["MapZoomLevel"].ToString());
                account.showbatteryinland = Convert.ToInt16(dt.Rows[0]["ShowBatteryStatusinLand"].ToString());
                account.reportDateRange1 = dt.Rows[0]["ReportDateRange1"].ToString();
                account.reportDateRange2 = dt.Rows[0]["ReportDateRange2"].ToString();
                account.result = "Success";
            }
            else
                account.result = "Data is not present for this sessionid!";

            if (table != null)
            {
                if (table.Rows.Count > 0)
                {
                    account.blocked_reason = table.Rows[0]["Reason"].ToString();
                    account.blocked_date = table.Rows[0]["Blockeddate"].ToString();
                }
            }
            rlog.Start();
            return account;
        }
        public List<TransportersList> GetTransportersList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            //DataTable table = ObjBll.GetContractorTripDetails(sessionid).Tables[0];
            DataTable table = ObjBll.GetMetroTransporterDetails(sessionid).Tables[0];
            List<TransportersList> transporters = new List<TransportersList>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var transporter = new TransportersList();
                {
                    transporter.TranspoterId = row["TransporterId"].ToString();
                    transporter.TranspoterName = row["Name"].ToString();
                    transporter.ShortName = row["ShortName"].ToString();
                }
                ;
                transporters.Add(transporter);
            }
            return transporters;
        }

        public List<CustomersList> MetroGetCustomersList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.GetMetro_Customers(sessionid).Tables[0];
            List<CustomersList> dt = new List<CustomersList>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var data = new CustomersList();
                {
                    data.CustomerId = row["CustomerId"].ToString();
                    data.CustomerName = row["CustomerName"].ToString();
                    data.custType = row["custType"].ToString();
                }
                ;
                dt.Add(data);
            }
            return dt;
        }
        public List<MetroRoutesList> MetroGetRoutesList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetRouteDetails(sessionid).Tables[0];
            List<MetroRoutesList> routes = new List<MetroRoutesList>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var route = new MetroRoutesList();
                {
                    route.RouteId = row["RouteId"].ToString();
                    route.RouteName = row["RouteName"].ToString();
                }
                ;
                routes.Add(route);
            }
            return routes;
        }
        public List<MetroInvoicesList> MetroGetInvoicesList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string username = ObjBll.getUserId(sessionid);
            string Transpoterid = ObjBll.getTransporterIdByLogin(username);
            DataTable table = ObjBll.MetroGetInvoicesDetails(sessionid, Transpoterid).Tables[0];
            List<MetroInvoicesList> invoices = new List<MetroInvoicesList>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var invoice = new MetroInvoicesList();
                {
                    invoice.RouteId = row["RouteId"].ToString();
                    invoice.CustomerId = row["CustomerId"].ToString();
                    invoice.CustomerName = row["CustomerName"].ToString();
                    invoice.InvoiceNum = row["InvoiceNum"].ToString();
                    invoice.InvoiceDate = row["OrderDateTime"].ToString();
                    invoice.Priority = row["Priority"].ToString();
                    //invoice.InvoiceType = row["InvoiceType"].ToString();
                    invoice.Address = row["Address"].ToString();
                    invoice.custType = row["custType"].ToString();
                    invoice.TransporterId = row["TransporterId"].ToString();
                    invoice.TransporterName = row["Name"].ToString();
                    invoice.StoreNO = "DC" + row["StoreNo"].ToString();
                }
                ;
                invoices.Add(invoice);
            }
            return invoices;
        }
        public List<MetroTripSheet> MetroGetTripSheet(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string username = ObjBll.getUserId(sessionid);
            string Transpoterid = ObjBll.getTransporterIdByLogin(username);
            DataTable table = ObjBll.MetroGetTripSheet(sessionid, Transpoterid).Tables[0];
            List<MetroTripSheet> metrotrips = new List<MetroTripSheet>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var trip = new MetroTripSheet();
                {
                    trip.Trip_No = row["Trip_No"].ToString();
                    trip.TripSheetNum = row["TripSheetNum"].ToString();
                    trip.DateTime = row["DateTime"].ToString();
                    trip.No_Of_Customer = row["No_Of_Customer"].ToString();
                    //trip.RouteName = row["RouteName"].ToString();
                    trip.RouteName = row["Status"].ToString();
                    trip.VehicleID = row["VehicleID"].ToString();
                    //trip.Driver = row["Driver"].ToString();
                }
                ;
                metrotrips.Add(trip);
            }
            return metrotrips;
        }
        public List<MetroTripDetails> MetroGetTripDetails(string sessionid, string TripNo)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetTripDetails(sessionid, TripNo).Tables[0];
            List<MetroTripDetails> metrotripdetails = new List<MetroTripDetails>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var tripdetails = new MetroTripDetails();
                {
                    tripdetails.CustomerId = row["CustomerId"].ToString();
                    tripdetails.CustomerName = row["CustomerName"].ToString();
                    tripdetails.InvoiceNum = row["InvoiceNum"].ToString();
                    //tripdetails.InvoiceType = row["InvoiceType"].ToString();
                    // tripdetails.Address = row["Address"].ToString();
                    tripdetails.Address = row["Delivery_Status"].ToString();
                    tripdetails.VehicleNo = row["VehicleID"].ToString();
                }
                ;
                metrotripdetails.Add(tripdetails);
            }
            return metrotripdetails;
        }
        public List<MetroTripDetails> MetroGetTripDetailsByTripSheetNo(string sessionid, string tripsheetno)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetTripDetailsByTripSheetNo(sessionid, tripsheetno).Tables[0];
            List<MetroTripDetails> metrotripdetails = new List<MetroTripDetails>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var tripdetails = new MetroTripDetails();
                {
                    tripdetails.CustomerId = row["CustomerId"].ToString();
                    tripdetails.CustomerName = row["CustomerName"].ToString();
                    tripdetails.InvoiceNum = row["InvoiceNum"].ToString();
                    // tripdetails.Address = row["Address"].ToString();
                    tripdetails.Status = row["Delivery_Status"].ToString();
                    tripdetails.VehicleNo = row["VehicleID"].ToString();
                    tripdetails.InvoiceType = row["InvoiceType"].ToString();
                    tripdetails.OrderType = row["OrderType"].ToString();
                }
                ;
                metrotripdetails.Add(tripdetails);
            }
            return metrotripdetails;
        }
        public string MetroGenerateTripSheet(string sessionid, Metro_TripInfo data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            return "Generation Failed";
            //BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            //string username = objbll.getUserId(sessionid);
            //string exepurl = HttpContext.Current.Request.Url.ToString();
            //try
            //{
            //    string result = objbll.MapsAPICall(data);

            //    Rootobject rootobject = JsonConvert.DeserializeObject<Rootobject>(result);
            //    double[,] resultDistanceMatrix = objbll.CreateDistanceMatrix(rootobject, data);
            //    string shortestPath = objbll.GetShortestPathBtwnAllPoints(resultDistanceMatrix);
            //    int estimatedTripDistance = Convert.ToInt32(objbll.CalculateTripDistance(resultDistanceMatrix, shortestPath));
            //    string TripSheetNo = objbll.GenerateTripSheet(sessionid, data, shortestPath, estimatedTripDistance, "");
            //    return TripSheetNo + "-Generated Successfully";
            //}
            //catch (Exception ex)
            //{
            //    objbll.ExceptionLogging("MetroGenerateTripSheet", "ui.vehicle-tracking", "", username, sessionid, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
            //    return "Generation Failed";
            //}

        }
        public string MetroDeleteTripSheet(string sessionid, string TripNo, string reason)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                objbll.MetroDeleteTripSheet(username, TripNo, reason);
                return "Deleted Successful";
            }
            catch (Exception e)
            {
                return "Deletion Failed";
            }
        }
        public List<MetroGetVehicles> MetroGetVehicles(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetVehicles(sessionid).Tables[0];
            table.Columns.Add("ETA");
            List<DataTable> tables = ObjBll.SplitTable(table, 25);
            int countTotalRows = table.Rows.Count;
            int countTables = tables.Count;
            int countRows = 0;
            int k = 0;
            int totalRows = 25;
            for (int m = 0; m < countTables; m++)
            {
                int index = 0;
                string result = ObjBll.MapsDistanceAPICall(tables[m], sessionid);
                Rootobject rootobject = JsonConvert.DeserializeObject<Rootobject>(result);
                List<string> arrivalTimes = ObjBll.ETADataTable(rootobject, table);

                for (k = countRows; k < totalRows; k++)
                {
                    if (k < countTotalRows)
                    {
                        DataRow dtrow = table.Rows[k];
                        if (index == 24)
                        {
                            index = 0;
                        }
                        dtrow["ETA"] = arrivalTimes[index];
                        index++;
                    }
                }
                countRows = countRows + 25;
                totalRows = totalRows + 25;

            }

            List<MetroGetVehicles> vehicles = new List<MetroGetVehicles>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var vehicle = new MetroGetVehicles();
                {
                    vehicle.Vehicle = row["VehicleId"].ToString();
                    vehicle.ETA = row["ETA"].ToString();
                }
                ;
                vehicles.Add(vehicle);
            }

            DataTable tableMarketVehicles = ObjBll.MetroGetMarketVehicles(sessionid).Tables[0];
            for (int i = 0; i < tableMarketVehicles.Rows.Count; i++)
            {
                DataRow row = tableMarketVehicles.Rows[i];
                var vehicle = new MetroGetVehicles();
                {
                    vehicle.Vehicle = row["VehicleId"].ToString();
                    vehicle.ETA = "NA";
                }
                ;
                vehicles.Add(vehicle);
            }
            return vehicles;
        }
        public string MetroAsignInvoiceToTransporter(string sessionid, string[] InvoiceNum, string TranspoterID)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                objbll.MetroAsignInvoiceToTransporter(sessionid, InvoiceNum, TranspoterID);
                return "Updated Successfully";
            }
            catch (Exception e)
            {
                return "Updation Failed";
            }
        }
        public string MetroDeleteInvoice(string sessionid, string InvoiceNum, string reason)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                objbll.MetroDeleteInvoice(username, InvoiceNum, reason);
                return "Deleted Successful";
            }
            catch (Exception e)
            {
                return "Deletion Failed";
            }
        }
        public string MetroRejectInvoice(string sessionid, string InvoiceNum, string reason)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                string Transpoterid = objbll.getTransporterIdByLogin(username);
                objbll.MetroRejectInvoice(sessionid, username, Transpoterid, InvoiceNum, reason);
                return "Rejected Successful";
            }
            catch (Exception e)
            {
                return "Rejected Failed";
            }
        }
        public List<MetroCustomerInfo> MetroGetCustomersInfo(string sessionid, string customerid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetCustomersInfo(sessionid, customerid).Tables[0];
            List<MetroCustomerInfo> customersInfo = new List<MetroCustomerInfo>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var customerInfo = new MetroCustomerInfo();
                {
                    customerInfo.CustomerName = row["CustomerName"].ToString();
                    customerInfo.custType = row["custType"].ToString();
                    customerInfo.MobileNo = row["MobileNo"].ToString();
                    customerInfo.Email = row["EmailId"].ToString();
                    List<MetroCustomerAddress> customersAddress = new List<MetroCustomerAddress>();
                    DataTable table1 = ObjBll.MetroGetCustomersAddressByCustomerId(sessionid, customerid).Tables[0];
                    for (int j = 0; j < table1.Rows.Count; j++)
                    {
                        DataRow rows = table1.Rows[j];
                        var customerAddress = new MetroCustomerAddress();
                        {
                            customerAddress.AddressId = rows["AddressId"].ToString();
                            customerAddress.Address = rows["Address"].ToString();
                            customerAddress.Latitude = rows["Latitude"].ToString();
                            customerAddress.Longitude = rows["Longitude"].ToString();
                            customerAddress.DetentionTime = rows["DetentionTime"].ToString();
                        }
                        ;
                        customersAddress.Add(customerAddress);
                    }
                    ;
                    customerInfo.Addresses = customersAddress;
                }
                customersInfo.Add(customerInfo);
            }
            return customersInfo;

        }
        public string MetroAsignInvoiceToCustomerAddress(string sessionid, string InvoiceNum, string AddressId)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                objbll.MetroAsignInvoiceToCustomerAddress(sessionid, InvoiceNum, AddressId);
                return "Updated Successfully";
            }
            catch (Exception e)
            {
                return "Updation Failed";
            }
        }
        public string MetroUpdateCustomersInfo(string sessionid, Metro_CustomerInfo data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                objbll.Update_Metro_CustomerInfo(sessionid, data);

                return "Updated Successfully";
            }
            catch (Exception e)
            {
                return "Updation Failed";
            }

        }
        public string MetroInsertCustomersAddress(string sessionid, Metro_CustomerAddress data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                objbll.Insert_Metro_CustomerAddress(sessionid, data);

                return "Inserted Successfully";
            }
            catch (Exception e)
            {
                return "Insertion Failed";
            }
        }
        public Stream MetroDownlaodTripSheet(string sessionid, string TripNo)
        {
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            int IsTripSheetGeneratedByMetroSystem = Convert.ToInt16(objbll.GetTripSheetGeneratedBy(TripNo));
            if (IsTripSheetGeneratedByMetroSystem == 1)
            {
                DataTable table = objbll.MetroGetTripSheetInfo(TripNo).Tables[0];
                DataRow row = table.Rows[0];
                Metro_TripInfo tripSheetInfo = new Metro_TripInfo();

                tripSheetInfo.TransporterId = "";
                tripSheetInfo.VehicleId = row["VehicleId"].ToString();
                tripSheetInfo.RouteId = row["RouteId"].ToString();
                tripSheetInfo.ShipperBox = row["NoOfShipperBoxes"].ToString();
                List<Metro_TripInvoices> tripInvoices = new List<Metro_TripInvoices>();
                DataTable table1 = objbll.MetroTripSheetInvoiceData(row["TripId"].ToString());
                for (int j = 0; j < table1.Rows.Count; j++)
                {
                    DataRow rows = table1.Rows[j];
                    var tripInvoice = new Metro_TripInvoices();
                    {
                        tripInvoice.InvoiceNum = rows["InvoiceNum"].ToString();
                    }
                    ;
                    tripInvoices.Add(tripInvoice);
                }
                tripSheetInfo.Invoices = tripInvoices;

                string result = "";//objbll.MapsAPICall(tripSheetInfo);
                Rootobject rootobject = JsonConvert.DeserializeObject<Rootobject>(result);
                double[,] resultDistanceMatrix = objbll.CreateDistanceMatrix(rootobject, tripSheetInfo);
                string shortestPath = objbll.GetShortestPathBtwnAllPoints(resultDistanceMatrix);
                int estimatedTripDistance = Convert.ToInt32(objbll.CalculateTripDistance(resultDistanceMatrix, shortestPath));
                string TripSheetNo = objbll.GenerateTripSheet(sessionid, tripSheetInfo, shortestPath, estimatedTripDistance, TripNo);
            }

            string downloadFilePath = Path.Combine(HostingEnvironment.MapPath("~/Uploads/"), TripNo + "_TripSheet.pdf");
            WebOperationContext.Current.OutgoingResponse.ContentType = "application/pdf";
            return File.OpenRead(downloadFilePath);
        }
        public Stream MetroDownlaodInvoice(string sessionid, string InvoiceNum)
        {
            string downloadFilePath = Path.Combine(HostingEnvironment.MapPath("~/Uploads/"), InvoiceNum + ".pdf");
            WebOperationContext.Current.OutgoingResponse.ContentType = "application/pdf";
            return File.OpenRead(downloadFilePath);
        }
        public Stream DownlaodTripSheet(string Key, string clientId, string TripSheetNo)
        {
            try
            {
                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
                string access = AuthenticateAPIKey(Key, "9", "json", clientId);
                string downloadFilePath = Path.Combine(HostingEnvironment.MapPath("~/Uploads/"), TripSheetNo + "_TripSheet.pdf");
                try
                {
                    if (!File.Exists(downloadFilePath))
                        BusinessLogicLayer.AmazonS3Upload.DownloadDbFiles(TripSheetNo + "_TripSheet.pdf", "", HostingEnvironment.MapPath("~/Uploads/"));
                }
                catch { }
                WebOperationContext.Current.OutgoingResponse.ContentType = "application/pdf";
                return File.OpenRead(downloadFilePath);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public string MetroInsertMarketVehicle(string sessionid, Metro_MarketVehicleInfo data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                string Transpoterid = objbll.getTransporterIdByLogin(username);
                objbll.Insert_Metro_MarketVehicle(sessionid, Transpoterid, data);

                return "Inserted Successfully";
            }
            catch (Exception e)
            {
                return "Insertion Failed";
            }
        }
        public List<MetroGetVehicleCapacities> MetroGetVehicleCapacity(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetVehicleCapacity(sessionid).Tables[0];
            List<MetroGetVehicleCapacities> capacities = new List<MetroGetVehicleCapacities>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var capacity = new MetroGetVehicleCapacities();
                {
                    capacity.Capacity = row["Capacity"].ToString();
                }
                ;
                capacities.Add(capacity);
            }
            return capacities;
        }
        public string MetroAddDriverDetails(string sessionid, Metro_AddDriverInfo data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                string Transpoterid = objbll.getTransporterIdByLogin(username);
                objbll.Insert_Metro_DriverInfo(sessionid, Transpoterid, data);

                return "Inserted Successfully";
            }
            catch (Exception e)
            {
                return "Insertion Failed";
            }
        }
        public List<MetroTripSheetInfo> GetTripInvoiceStatus(string Key, string clientId, string storeno, string tripdate)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(Key, "8", "json", clientId);
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroGetTripSheetDetails(storeno, tripdate).Tables[0];
            List<MetroTripSheetInfo> tripSheetsInfo = new List<MetroTripSheetInfo>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var tripSheetInfo = new MetroTripSheetInfo();
                {
                    tripSheetInfo.TripNo = row["TripNum"].ToString();
                    tripSheetInfo.TripDate = row["TripDate"].ToString();
                    tripSheetInfo.VehicleNo = row["VehicleId"].ToString();
                    tripSheetInfo.VehicleType = row["VehicleType"].ToString();
                    // tripSheetInfo.Capacity = row["Capacity"].ToString();
                    tripSheetInfo.DriverName = row["DriverName"].ToString();
                    tripSheetInfo.DriverPhoneNo = row["Mobile"].ToString();
                    // tripSheetInfo.DispatchMode = row["DispatchMode"].ToString();
                    // tripSheetInfo.Route = row["RouteId"].ToString();
                    tripSheetInfo.EstDistance = row["Distance"].ToString();
                    // tripSheetInfo.NoOfCustomers = row["NoOfCustomers"].ToString();
                    // tripSheetInfo.NoOfInvoices = row["NoOfInvoices"].ToString();
                    // tripSheetInfo.TotalAmount = row["TotalAmount"].ToString();
                    // tripSheetInfo.TotalDeliveryCharge = row["TotalDeliveryCharge"].ToString();
                    // tripSheetInfo.ShipperBoxes = row["NoOfShipperBoxes"].ToString();
                    tripSheetInfo.Transporter = row["Transporter"].ToString();
                    // tripSheetInfo.StoreNo = row["StoreNo"].ToString();
                    tripSheetInfo.TripStatus = row["Trip_Status"].ToString();
                    tripSheetInfo.TripStartDate = row["Trip_Status_Updated_On"].ToString();
                    tripSheetInfo.TripEndDate = row["Trip_Closed_On"].ToString();
                    tripSheetInfo.TripClosedBy = row["Trip_Closed_By"].ToString();
                    if (row["IsByMetro"].ToString() == "0")
                    {
                        tripSheetInfo.TripGeneratedBy = "MobileApp";
                    }
                    else if (row["IsByMetro"].ToString() == "1")
                    {
                        tripSheetInfo.TripGeneratedBy = "TMS";
                    }
                    else if (row["IsByMetro"].ToString() == "2")
                    {
                        tripSheetInfo.TripGeneratedBy = "AutoRouting";
                    }

                    List<MetroTripSheetCustomers> tripCustomers = new List<MetroTripSheetCustomers>();
                    DataTable dtTripCustomers = ObjBll.MetroTripSheetCustomerData(row["TripId"].ToString());
                    for (int c = 0; c < dtTripCustomers.Rows.Count; c++)
                    {
                        DataRow customerRows = dtTripCustomers.Rows[c];
                        var tripCustomer = new MetroTripSheetCustomers();
                        {
                            tripCustomer.CustomerID = customerRows["CustomerId"].ToString();
                            tripCustomer.CustomerName = customerRows["CustomerName"].ToString();
                            tripCustomer.Address = customerRows["Address"].ToString();
                            tripCustomer.Pincode = customerRows["Pincode"].ToString();
                            tripCustomer.Latitude = customerRows["Latitude"].ToString();
                            tripCustomer.Longitude = customerRows["Longitude"].ToString();
                            tripCustomer.TotalItems = customerRows["TotalItems"].ToString();
                            tripCustomer.TotalReturnQty = customerRows["ReturnQty"].ToString();
                            tripCustomer.TotalCashCollected = customerRows["CashCollected"].ToString();
                            tripCustomer.OtpAcknowledgement = customerRows["IsVerified"].ToString();
                            //tripCustomer.DeliveryTime = customerRows["DeliveryTime"].ToString();

                            List<MetroTripSheetInvoices> tripInvoices = new List<MetroTripSheetInvoices>();
                            DataTable table1 = ObjBll.MetroTripSheetInvoiceData(row["TripId"].ToString(), customerRows["CustomerId"].ToString());
                            for (int j = 0; j < table1.Rows.Count; j++)
                            {
                                DataRow rows = table1.Rows[j];
                                var tripInvoice = new MetroTripSheetInvoices();
                                {
                                    //tripInvoice.CustomerID = rows["CustomerId"].ToString();
                                    tripInvoice.InvoiceNum = rows["InvoiceNum"].ToString();
                                    tripInvoice.InvoiceDate = rows["OrderDateTime"].ToString();
                                    tripInvoice.Amount = rows["TotalPrice"].ToString();
                                    tripInvoice.DeliveryCharge = rows["DeliveryCharge"].ToString();
                                    tripInvoice.InvoiceStatus = rows["Delivery_Status"].ToString();
                                    tripInvoice.DeliveredDate = rows["DeliveredDate"].ToString();
                                    tripInvoice.AssignedTransporter = rows["Transporter"].ToString();
                                    tripInvoice.TotalDeliveryChargeCollected = rows["DeliveryChargeCollected"].ToString();
                                    tripInvoice.TotalInvoiceAmountCollected = rows["InvoiceAmountCollected"].ToString();
                                    tripInvoice.IsVerified = rows["IsVerified"].ToString();
                                    //tripInvoice.VerifiedOTP = rows["VerifiedOTP"].ToString();
                                    //tripInvoice.VerifiedMobile = rows["VerifiedMobileNo"].ToString();
                                    //tripInvoice.VerifiedOn = rows["OTPVerifiedOn"].ToString();
                                    //tripInvoice.Latitude = rows["Latitude"].ToString();
                                    //tripInvoice.Longitude = rows["Longitude"].ToString();
                                    //tripInvoice.TotalItems = rows["TotalItems"].ToString();
                                    //tripInvoice.TotalReturnQty = rows["ReturnQty"].ToString();
                                    //tripInvoice.TotalCashCollected = rows["CashCollected"].ToString();

                                    List<MetroTripInvoicesProducts> tripInvoiceProducts = new List<MetroTripInvoicesProducts>();
                                    DataTable table2 = ObjBll.MetroTripSheetInvoiceProductData(rows["InvoiceNum"].ToString());
                                    for (int k = 0; k < table2.Rows.Count; k++)
                                    {
                                        DataRow productRows = table2.Rows[k];
                                        var tripInvoiceProduct = new MetroTripInvoicesProducts();
                                        {
                                            tripInvoiceProduct.ProductId = productRows["ProductId"].ToString();
                                            tripInvoiceProduct.Description = productRows["Description"].ToString();
                                            tripInvoiceProduct.TotalQuantity = productRows["TotalQuantity"].ToString();
                                            tripInvoiceProduct.DeliveredQty = productRows["DeliveredQty"].ToString();
                                            tripInvoiceProduct.ReturnedQty = productRows["ReturnedQty"].ToString();
                                        }
                                        ;
                                        tripInvoiceProducts.Add(tripInvoiceProduct);
                                    }
                                    tripInvoice.InvoiceProducts = tripInvoiceProducts;
                                }
                                ;
                                tripInvoices.Add(tripInvoice);
                            }
                            tripCustomer.Invoices = tripInvoices;
                        }
                        ;
                        tripCustomers.Add(tripCustomer);
                    }
                    tripSheetInfo.Customers = tripCustomers;
                }
                tripSheetsInfo.Add(tripSheetInfo);
            }
            return tripSheetsInfo;
        }
        public List<MetroPendingInvoices> GetPendingInvoices(string Key, string clientId, string storeno)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(Key, "9", "json", clientId);
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable table = ObjBll.MetroPendingInvoices(storeno);
            List<MetroPendingInvoices> pendingInvoices = new List<MetroPendingInvoices>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var pendingInvoice = new MetroPendingInvoices();
                {
                    pendingInvoice.CustomerName = row["CustomerName"].ToString();
                    pendingInvoice.InvoiceNum = row["TaxInvoiceNum"].ToString();
                    pendingInvoice.InvoiceDate = row["OrderDateTime"].ToString();
                    pendingInvoice.Amount = row["TotalPrice"].ToString();
                    pendingInvoice.DeliveryCharge = row["DeliveryCharge"].ToString();
                    pendingInvoice.DeliveryTime = row["DeliveryTime"].ToString();
                    pendingInvoice.InvoiceAge = row["Age"].ToString();
                }
                ;
                pendingInvoices.Add(pendingInvoice);
            }
            return pendingInvoices;
        }
        public string PostVehiclesData(string Key, string clientId, string data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(Key, "6", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                if (data.Contains("TESTSENSELIN"))
                    return data;
                objbll.STL_RecordErrorMessage(data);
                try
                {
                    data = data.Replace("}{", "},{");
                    string SessionId = access.Split(',')[1];
                    string accountid = objbll.GetAccountId(SessionId).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<Metro_MarketVehicleInfo> objMetroInvoiceList = jsonSerializer.Deserialize<List<Metro_MarketVehicleInfo>>(data);
                    for (int i = 0; i < objMetroInvoiceList.Count; i++)
                    {
                        objbll.Update_Metro_Vehicles(objMetroInvoiceList[i], accountid);
                    }
                    Thread rlog = new Thread(() => objbll.WebServiceRequestlog("PostVehiclesData", "ui.vehicle-tracking", "", "", clientId, sHostName, sUserIP));
                }
                catch (System.ArgumentException ex)
                {
                    objbll.ExceptionLogging("PostVehiclesData", "ui.vehicle-tracking", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Post Vehicle Data Upload Failed", "MetroDataUploadStatus");
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.ExceptionLogging("PostVehiclesData", "ui.vehicle-tracking", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Post Vehicle Data Upload Failed", "MetroDataUploadStatus");
                }
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                objbll.ExceptionLogging("PostVehiclesData", "ui.vehicle-tracking", clientId, Key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Metro Post Vehicle Data Upload Failed", "MetroDataUploadStatus");
                return "Upload Failed";
            }
        }
        public string MetroCloseTrip(string sessionid, string TripNo, string reason)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                objbll.MetroCloseTripByFSDManager(username, TripNo, reason);
                return "Trip Closed Successful";
            }
            catch (Exception e)
            {
                return "Trip Closed Failed";
            }
        }
        public string MetroAddWalkInCustomerInvoice(string sessionid, Metro_Walkin_Invoice data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = objbll.getUserId(sessionid);
                string Transpoterid = objbll.getTransporterIdByLogin(username);
                objbll.MetroAddWalkInCustomerInvoice(sessionid, data);

                return "Inserted Successfully";
            }
            catch (Exception e)
            {
                return "Insertion Failed";
            }
        }
        public string MetroTripInvoiceStatus(string sessionid, string taxinvoiceno, string status, string reason)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string r = "-1";
                string username = objbll.getUserId(sessionid);
                string Transpoterid = objbll.getTransporterIdByLogin(username);
                if (status == "Cancel")
                {
                    //Commented By Tusar On 13/10/2023- Store is miss using the feature and used to cancel all the invoices from trip.
                    DataTable dtInvoiceCancelled = objbll.GetMetroInvoicesCancelled(taxinvoiceno);
                    if (dtInvoiceCancelled.Rows.Count > 0)
                    {
                        for (int i = 0; i < dtInvoiceCancelled.Rows.Count; i++)
                        {
                            objbll.InsertMetroTripStatus(dtInvoiceCancelled.Rows[i]["StoreNo"].ToString(), dtInvoiceCancelled.Rows[i]["TripSheetNo"].ToString(), dtInvoiceCancelled.Rows[i]["TaxInvoiceNum"].ToString(), Convert.ToDateTime(dtInvoiceCancelled.Rows[i]["OrderDateTime"]), "Cancelled", reason, Convert.ToDateTime(DateTime.Now), Transpoterid, sessionid);
                        }
                    }
                    r = objbll.MetroUpdateTripInvoiceStatus(taxinvoiceno, status, reason);
                }
                else
                {
                    r = objbll.MetroUpdateTripInvoiceStatus(taxinvoiceno, status, reason);


                    //Log all the Status update happened using store app
                    try
                    {
                        DataTable dtInvoiceCancelled = objbll.GetMetroInvoicesCancelled(taxinvoiceno);
                        if (dtInvoiceCancelled.Rows.Count > 0)
                        {
                            for (int i = 0; i < dtInvoiceCancelled.Rows.Count; i++)
                            {
                                objbll.InsertMetroTripStatus(dtInvoiceCancelled.Rows[i]["StoreNo"].ToString(), dtInvoiceCancelled.Rows[i]["TripSheetNo"].ToString(), dtInvoiceCancelled.Rows[i]["TaxInvoiceNum"].ToString(), Convert.ToDateTime(dtInvoiceCancelled.Rows[i]["OrderDateTime"]), status, reason, Convert.ToDateTime(DateTime.Now), Transpoterid, sessionid);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
                if (r != "-1")
                {
                    return "Inserted Successfully";
                }
                else
                {
                    return "Insertion Failed";
                }
            }
            catch (Exception e)
            {
                return "Insertion Failed";
            }
        }
        public List<MetroSelfDeliveredStores> GetSelfDeliveredInvoices(string Key, string clientId, string orderdate)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string access = AuthenticateAPIKey(Key, "9", "json", clientId);
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();

            DataTable table = ObjBll.MetroSelfDeliveredInvoices("", orderdate);
            List<MetroSelfDeliveredStores> selfDeliveredStoreNos = new List<MetroSelfDeliveredStores>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                var selfDeliveredStoreNo = new MetroSelfDeliveredStores();
                {
                    selfDeliveredStoreNo.StoreNo = row["StoreNo"].ToString();

                    List<MetroSelfDeliveredInvoices> selfDeliveredInvoices = new List<MetroSelfDeliveredInvoices>();
                    DataTable table1 = ObjBll.MetroSelfDeliveredInvoices(row["StoreNo"].ToString(), orderdate);
                    for (int j = 0; j < table1.Rows.Count; j++)
                    {
                        DataRow row1 = table1.Rows[j];
                        var selfDeliveredInvoice = new MetroSelfDeliveredInvoices();
                        {
                            //selfDeliveredInvoice.StoreNo = row["StoreNo"].ToString();
                            selfDeliveredInvoice.InvoiceNum = row1["TaxInvoiceNum"].ToString();
                            selfDeliveredInvoice.InvoiceDate = row1["OrderDateTime"].ToString();
                            selfDeliveredInvoice.DeliveryDate = row1["Delivery_Status_Updated_On"].ToString();
                        }
                        ;
                        selfDeliveredInvoices.Add(selfDeliveredInvoice);
                    }
                    selfDeliveredStoreNo.SelfDeliveredInvoices = selfDeliveredInvoices;
                }
                selfDeliveredStoreNos.Add(selfDeliveredStoreNo);
            }
            return selfDeliveredStoreNos;
        }
        #endregion

        #region Vehicle Vetting App
        public Result insertVehicleVettingDetails(string sessionid, Vehicle_Vetting_Fields vehicle_Vetting_Fields, List<Vehicle_Vetting_Checklist> vehicle_Vetting_Checklist)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertVehicleVettingDetails(sessionid, vehicle_Vetting_Fields, vehicle_Vetting_Checklist);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Inspection details submitted successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }
        #endregion

        #region Vehicle Vetting App
        public List<string> getTransporterList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            List<string> lst = new List<string>();
            DataTable trans_lst = objBll.GetTransportersList(sessionid);
            if (trans_lst != null && trans_lst.Rows.Count > 0)
            {
                for (int i = 0; i < trans_lst.Rows.Count; i++)
                {
                    lst.Add(trans_lst.Rows[i]["Name"].ToString());
                }
            }
            return lst;
        }

        public Result insertTechnicalInvariantDetails(string sessionid, Technical_Invariant_Fields technical_Invariant_Fields, List<Technical_Invariant_Checklist> technical_Invariant_Checklists)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertTechnicalInvariantDetails(sessionid, technical_Invariant_Fields, technical_Invariant_Checklists);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Inspection details submitted successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }
        #endregion

        #region Vetting App
        public Vehicle_Header GetVehicles(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetVehiclesBySessionId(sessionid).Tables[0];
            List<Vehicle_List> vehicle_Lists = new List<Vehicle_List>();
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                vehicle_Lists.Add(new Vehicle_List()
                {
                    vehicleid = dt.Rows[i]["VehicleID"].ToString(),
                    vehicleInfo = dt.Rows[i]["VehicleInfo"].ToString()
                });
            }
            Vehicle_Header vehicle_Header = new Vehicle_Header() { vehicles = vehicle_Lists };
            return vehicle_Header;
        }
        public Vetting_Report_Header GetVettingReport(string sessionId, string vehicleType, string fromDt, string toDt)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetVettingReport(sessionId, vehicleType, fromDt, toDt);
            List<Vetting_Report> vetting_Reports = new List<Vetting_Report>();
            DataView view = new DataView(dt);
            DataTable vettingList = view.ToTable(true, "Id");
            for (int i = 0; i < vettingList.Rows.Count; i++)
            {
                DataRow[] dr = dt.Select("Id='" + vettingList.Rows[i]["Id"] + "'");
                List<Vetting_Checklist_Values> vetting_Checklist_Values = new List<Vetting_Checklist_Values>();
                for (int j = 0; j < dr.Length; j++)
                {
                    vetting_Checklist_Values.Add(new Vetting_Checklist_Values()
                    {
                        checklistHeader = dr[j]["Name"].ToString(),
                        obtainedScore = Convert.ToInt16(dr[j]["obtainedScore"].ToString()),
                        totalScore = Convert.ToInt16(dr[j]["totalScore"].ToString()),
                        percentage = (Convert.ToInt16(dr[j]["obtainedScore"].ToString()) * 100) / Convert.ToInt16(dr[j]["totalScore"].ToString())
                    });
                }
                string filename = dr[0]["FileName"].ToString();
                string folderPath = HostingEnvironment.MapPath("~/Uploads/");
                try
                {
                    if (string.IsNullOrEmpty(filename))
                    {
                        filename = ObjBll.SaveVettingReportAsPdf(dr[0]["Id"].ToString());
                    }
                    else if (!File.Exists(folderPath + filename))
                        BusinessLogicLayer.AmazonS3Upload.DownloadDbFiles(filename, "", folderPath);
                }
                catch { }
                vetting_Reports.Add(new Vetting_Report()
                {
                    vettingId = dr[0]["Id"].ToString(),
                    vehicleId = dr[0]["VehicleId"].ToString(),
                    vehicleType = dr[0]["VehicleType"].ToString(),
                    dateTime = Convert.ToDateTime(dr[0]["VettingDate"].ToString()).ToString("yyyy-MM-dd"),
                    checklist_Values = vetting_Checklist_Values,
                    avg_score = vetting_Checklist_Values.Sum(x => x.percentage) / vetting_Checklist_Values.Count,
                    filePath = filename
                });
            }
            Vetting_Report_Header vetting_Report_Header = new Vetting_Report_Header() { vetting_Reports = vetting_Reports };
            return vetting_Report_Header;
        }

        public List<Vet_Report> GetVetReport(string sessionId, string appId, string fromDt, string toDt)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetVetReport(sessionId, appId, fromDt, toDt);
            List<Vet_Report> vet_Reports = new List<Vet_Report>();
            DataView view = new DataView(dt);
            DataTable vetList = view.ToTable(true, "Id");
            for (int i = 0; i < vetList.Rows.Count; i++)
            {
                DataRow[] dr = dt.Select("Id='" + vetList.Rows[i]["Id"] + "'");
                string filename = dr[0]["FileName"].ToString();
                string folderPath = HostingEnvironment.MapPath("~/Uploads/");
                try
                {
                    if (string.IsNullOrEmpty(filename))
                    {
                        filename = ObjBll.SaveVetReportAsPdf(dr[0]["Id"].ToString());
                    }
                    else if (!File.Exists(folderPath + filename))
                        BusinessLogicLayer.AmazonS3Upload.DownloadDbFiles(filename, "", folderPath);
                }
                catch { }
                string data = "{";
                for (int j = 0; j < dr.Length; j++)
                {
                    if (data != "{")
                        data += ",";
                    data += "'" + dr[j]["FieldName"].ToString() + "':'" + dr[j]["Value"].ToString() + "'";
                }
                data += "}";

                vet_Reports.Add(new Vet_Report()
                {

                    Data = data,
                    DateTime = Convert.ToDateTime(dr[0]["DateTime"].ToString()).ToString("yyyy-MM-dd HH:mm:ss"),
                    Score = ObjBll.GetVetScore(dr[0]["Id"].ToString()),
                    Link = filename
                });
            }
            return vet_Reports;
        }

        public List<DDLValues> GetVetDropDownList(string sessionId, string appId, string fieldId, string imei)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable ddlDt = ObjBll.GetVetDropDownList(sessionId, appId, fieldId, imei);
            List<DDLValues> ddlList = new List<DDLValues>();
            if (ddlDt != null && ddlDt.Rows.Count > 0)
            {
                for (int i = 0; i < ddlDt.Rows.Count; i++)
                {
                    ddlList.Add(new DDLValues()
                    {
                        Value = ddlDt.Rows[i]["Value"].ToString(),
                        DisplayValue = ddlDt.Rows[i]["DisplayValue"].ToString()
                    });
                }
            }
            return ddlList;
        }

        public string GetVetMainDetails(string sessionId, string appId, string fieldId, string value)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable ddlDt = ObjBll.GetVetMainDetails(sessionId, appId, fieldId, value);
            string data = "{";
            if (ddlDt != null && ddlDt.Rows.Count > 0)
            {
                for (int i = 0; i < ddlDt.Rows.Count; i++)
                {
                    if (data != "{")
                        data += ",";
                    data += "'" + ddlDt.Rows[i][0].ToString() + "':'" + ddlDt.Rows[i][1].ToString() + "'";
                }
            }
            data += "}";
            return data;
        }

        public List<Vet_LoginConfig> GetVetLoginConfig(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetVetLoginConfig(sessionid);
            List<Vet_LoginConfig> vet_LoginConfigs = new List<Vet_LoginConfig>();
            if (dt != null && dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    Vet_LoginConfig vlc = new Vet_LoginConfig()
                    {
                        appId = dt.Rows[i]["AppId"].ToString(),
                        name = dt.Rows[i]["AppName"].ToString()
                    };
                    vet_LoginConfigs.Add(vlc);
                }
            }
            return vet_LoginConfigs;
        }

        public Vetting_Header GetVetCheckList(string sessionid, string appId, string imei)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetVetChecklist(sessionid, appId, imei);
            List<Vetting_CheckList> checklists = new List<Vetting_CheckList>();
            DataView view = new DataView(dt);
            DataTable chklists = view.ToTable(true, "HeaderName");
            for (int i = 0; i < chklists.Rows.Count; i++)
            {
                DataRow[] dr = dt.Select("HeaderName='" + chklists.Rows[i]["HeaderName"].ToString() + "'");
                List<Vetting_Rules> rules = new List<Vetting_Rules>();
                for (int j = 0; j < dr.Length; j++)
                {
                    List<Vetting_Options> options = new List<Vetting_Options>();
                    string[] strOptions = dr[j]["Options"].ToString().Substring(3).Split(new string[] { ",&@&" }, StringSplitOptions.None);
                    for (int k = 0; k < strOptions.Length; k++)
                    {
                        Vetting_Options option = new Vetting_Options
                        {
                            optionName = strOptions[k].Split(new string[] { "@&@" }, StringSplitOptions.None)[0],
                            score = strOptions[k].Split(new string[] { "@&@" }, StringSplitOptions.None)[1]
                        };
                        options.Add(option);
                    }
                    if (options.Count > 0)
                    {
                        Vetting_Rules rule = new Vetting_Rules()
                        {
                            ruleId = dr[j]["Id"].ToString(),
                            ruleName = dr[j]["RuleName"].ToString(),
                            coefficient = dr[j]["Coefficient"].ToString(),
                            options = options
                        };
                        rules.Add(rule);
                    }
                }
                if (rules.Count > 0)
                {
                    Vetting_CheckList chklst = new Vetting_CheckList()
                    {
                        checklistName = chklists.Rows[i]["HeaderName"].ToString(),
                        rules = rules
                    };
                    checklists.Add(chklst);
                }
            }

            dt = ObjBll.GetVetFields(sessionid, appId);
            view = new DataView(dt);
            chklists = view.ToTable(true, "HeaderName");
            List<Vet_Fields> fields = new List<Vet_Fields>();
            for (int i = 0; i < chklists.Rows.Count; i++)
            {
                DataRow[] dr = dt.Select("HeaderName='" + chklists.Rows[i]["HeaderName"].ToString() + "'");
                List<Vet_FieldConfig> fieldValues = new List<Vet_FieldConfig>();
                for (int j = 0; j < dr.Length; j++)
                {
                    Vet_FieldConfig v = new Vet_FieldConfig()
                    {
                        id = dr[j]["Id"].ToString(),
                        fieldName = dr[j]["FieldName"].ToString(),
                        fieldType = dr[j]["FieldType"].ToString(),
                        validation = dr[j]["Validation"].ToString(),
                        value = dr[j]["Value"].ToString(),
                        mandatory = dr[j]["Mandatory"].ToString()
                    };
                    fieldValues.Add(v);
                }
                if (fieldValues.Count > 0)
                {
                    fields.Add(new Vet_Fields()
                    {
                        headerName = dr[0]["HeaderName"].ToString(),
                        fieldValues = fieldValues
                    });
                }
            }
            Vetting_Header vetting_Header = new Vetting_Header()
            {
                checklists = checklists,
                checklistcount = checklists.Sum(x => x.rules.Count),
                fields = fields
            };
            return vetting_Header;
        }

        public Vetting_Header GetVettingCheckList(string sessionid, string truckType)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetVettingCheckList(sessionid, truckType);
            List<Vetting_CheckList> checklists = new List<Vetting_CheckList>();
            DataView view = new DataView(dt);
            DataTable chklists = view.ToTable(true, "Name");
            for (int i = 0; i < chklists.Rows.Count; i++)
            {
                DataRow[] dr = dt.Select("Name='" + chklists.Rows[i]["Name"].ToString() + "'");
                List<Vetting_Rules> rules = new List<Vetting_Rules>();
                for (int j = 0; j < dr.Length; j++)
                {
                    List<Vetting_Options> options = new List<Vetting_Options>();
                    string[] strOptions = dr[j]["Options"].ToString().Substring(3).Split(new string[] { ",&@&" }, StringSplitOptions.None);
                    for (int k = 0; k < strOptions.Length; k++)
                    {
                        Vetting_Options option = new Vetting_Options
                        {
                            optionName = strOptions[k].Split(new string[] { "@&@" }, StringSplitOptions.None)[0],
                            score = strOptions[k].Split(new string[] { "@&@" }, StringSplitOptions.None)[1]
                        };
                        options.Add(option);
                    }
                    if (options.Count > 0)
                    {
                        Vetting_Rules rule = new Vetting_Rules()
                        {
                            ruleId = dr[j]["Id"].ToString(),
                            ruleName = dr[j]["RuleName"].ToString(),
                            coefficient = dr[j]["Coefficient"].ToString(),
                            options = options
                        };
                        rules.Add(rule);
                    }
                }
                if (rules.Count > 0)
                {
                    Vetting_CheckList chklst = new Vetting_CheckList()
                    {
                        checklistName = chklists.Rows[i]["Name"].ToString(),
                        rules = rules
                    };
                    checklists.Add(chklst);
                }
            }
            Vetting_Header vetting_Header = new Vetting_Header() { checklists = checklists, checklistcount = checklists.Sum(x => x.rules.Count) };
            return vetting_Header;
        }

        public Result insertVetDetails(string sessionid, Vetting_StaticDetails staticDetails, List<Vetting_ChecklistValues> checklistValues, List<Vet_FieldValues> fieldValues)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertVetDetails(sessionid, staticDetails, checklistValues, fieldValues);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Inspection details submitted successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }

        public Result insertVettingCheckList(string sessionid, Vetting_StaticDetails staticDetails, List<Vetting_ChecklistValues> checklistValues)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertVettingCheckList(sessionid, staticDetails, checklistValues);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Vetting Inspection submitted Successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }
        public Result insertFirebaseToken(string AppName, string sessionid, string Token)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertFirebaseToken(AppName, sessionid, Token);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Token Submitted Successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }
        public Notification_Header GetVettingNotificationsList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable result = ObjBll.GetVettingNotificationsList(sessionid);
            List<Notifications> notifications = new List<Notifications>();
            for (int i = 0; i < result.Rows.Count; i++)
            {
                notifications.Add(new Notifications()
                {
                    subject = result.Rows[i]["Subject"].ToString(),
                    info = result.Rows[i]["Info"].ToString(),
                    datetime = result.Rows[i]["DateTime"].ToString(),
                    isnotified = result.Rows[i]["IsNotified"].ToString()
                });
            }
            Notification_Header notification_Header = new Notification_Header() { notifications = notifications };
            return notification_Header;
        }
        public string UploadFile(string sessionid, string fileName, Stream filedata)
        {
            try
            {
                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                string username = ObjBll.getUserId(sessionid);
                Thread rlog = new Thread(() => ObjBll.WebServiceRequestlog("SenselRestService", "UploadFile", username, username, sessionid, sHostName, sUserIP));
                rlog.Start();
                string FilePath = Path.Combine(HostingEnvironment.MapPath("~/Uploads/placeinfo/"), fileName);
                MultipartParser parser = new MultipartParser(filedata);
                if (parser.Success)
                {
                    File.WriteAllBytes(FilePath, parser.FileContents);
                }
                else
                {
                    return "failed,file missing";
                }
                // The files will be uploaded to S3. And this path is passed to UploadFiles method below.
                String S3Path = System.Web.Configuration.WebConfigurationManager.AppSettings["unloadQtyPath"];
                if (String.IsNullOrEmpty(S3Path))
                    S3Path = "fleetsmart3.sensel.in/App Files/sensel/Sensel.in/fleetsmart3.ui.sensel.in/Uploads/placeinfo";

                BusinessLogicLayer.AmazonS3Upload.UploadFiles(HostingEnvironment.MapPath("~/Uploads/placeinfo/"), fileName, "db-flatfile-backup", S3Path, "PublicRead");
                return "success";
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }
        #endregion

        #region Passenger App
        public Authenticate PassengerApp_Authenticate(string mobileno, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            if (type.ToLower().Trim() == "otp_request")
            {
                auth.result = objbll.PassengerApp_OTPRequest(mobileno);
            }
            else if (type.ToLower().Trim() == "otp_validate")
            {
                DataTable dt = objbll.PassengerApp_OTPValidate(mobileno, otp);
                if (dt.Rows.Count > 0)
                {
                    auth.result = "Login Success";
                    auth.userid = dt.Rows[0]["userid"].ToString();
                    auth.mobileno = dt.Rows[0]["MobileNo"].ToString();
                    auth.sessionid = dt.Rows[0]["sessionid"].ToString();
                    auth.accountid = dt.Rows[0]["AccountId"].ToString();
                    auth.username = dt.Rows[0]["PsngrName"].ToString();
                    auth.usertype = dt.Rows[0]["Type"].ToString();
                    auth.vehicleid = dt.Rows[0]["AssignedVehicleId"].ToString();
                    auth.vehsessionid = objbll.getSessionIdByVehicle(auth.vehicleid);
                }
                else
                {
                    auth.result = "Invalid OTP";
                }
            }
            else
            {
                auth.result = "Invalid Request Type";
            }
            return auth;
        }

        public List<Notifications> GetPassengerNotifications(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            List<Notifications> passenger_Notifications = new List<Notifications>();
            DataTable dt = objbll.GetPassengerNotifications(sessionid);
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                passenger_Notifications.Add(new Notifications
                {
                    subject = dt.Rows[i]["Subject"].ToString(),
                    info = dt.Rows[i]["Info"].ToString(),
                    datetime = dt.Rows[i]["DateTime"].ToString(),
                    isnotified = dt.Rows[i]["IsNotified"].ToString(),
                    notifiedtime = dt.Rows[i]["NotifiedTime"].ToString(),
                    priority = dt.Rows[i]["Priority"].ToString()
                });
            }
            return passenger_Notifications;
        }

        public Result PassengerNotificationNotified(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            Result result = new Result();
            objbll.PassengerNotificationNotified(sessionid);
            result.result = "Success";
            result.statuscode = "200";
            return result;
        }

        #endregion

        #region Metro Driver App
        public Authenticate DriverApp_Authenticate(string mobileno, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            if (type.ToLower().Trim() == "otp_request")
            {
                auth.result = objbll.DriverApp_OTPRequest(mobileno);
            }
            else if (type.ToLower().Trim() == "otp_validate")
            {
                DataTable dt = objbll.DriverApp_OTPValidate(mobileno, otp);
                if (dt.Rows.Count > 0)
                {
                    auth.result = "Login Success";
                    auth.userid = dt.Rows[0]["userid"].ToString();
                    auth.mobileno = dt.Rows[0]["MobileNo"].ToString();
                    auth.sessionid = dt.Rows[0]["sessionid"].ToString();
                    auth.accountid = dt.Rows[0]["AccountId"].ToString();
                    auth.username = dt.Rows[0]["Name"].ToString();
                    auth.usertype = "Driver";
                }
                else
                {
                    auth.result = "Invalid OTP";
                }
            }
            else
            {
                auth.result = "Invalid Request Type";
            }
            return auth;
        }
        public List<Metro_Trip_List> DriverApp_GetTripInvoices(string sessionid)
        {
            bool flag = false;
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            List<Metro_Trip_List> li = new List<Metro_Trip_List>();
            List<Metro_Customers> ci = new List<Metro_Customers>();
            List<Metro_Trip_Invoices> ml = new List<Metro_Trip_Invoices>();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            Metro_Trip_List mt = new Metro_Trip_List();
            Metro_Customers mc = new Metro_Customers();

            if (dtuser.Rows.Count > 0)
            {
                try
                {
                    DataTable dt = objbll.Metro_DriverApp_GetTrips(sessionid);
                    string prev_customerId = dt.Rows[0]["CustomerId"].ToString();


                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        mt.TripNum = dt.Rows[j]["TripNum"].ToString();
                        mt.Trip_Status = dt.Rows[j]["Trip_Status"].ToString();
                        string isVerified = "False";
                        DataTable dtVerifyCustomer = objbll.CustomerVerifiedForTrip(dt.Rows[j]["CustomerId"].ToString(), mt.TripNum);
                        if (dtVerifyCustomer.Rows.Count > 0)
                        {
                            isVerified = "True";
                        }

                        if (prev_customerId != dt.Rows[j]["CustomerId"].ToString())
                        {
                            ci.Add(mc);
                            mc = new Metro_Customers();
                            ml = new List<Metro_Trip_Invoices>();
                        }
                        mc.CustomerId = dt.Rows[j]["CustomerId"].ToString();
                        mc.CustomerName = dt.Rows[j]["CustomerName"].ToString();
                        mc.CustomerMobile = dt.Rows[j]["MobileNo"].ToString();
                        mc.AddressId = dt.Rows[j]["CustomerAddressId"].ToString();
                        mc.Address = dt.Rows[j]["Address"].ToString();
                        mc.Latitude = dt.Rows[j]["Latitude"].ToString();
                        mc.Longitude = dt.Rows[j]["Longitude"].ToString();
                        mc.IsVerified = isVerified;

                        var metroInvoice = new Metro_Trip_Invoices();
                        {
                            metroInvoice.TaxInvoiceNum = dt.Rows[j]["TaxInvoiceNum"].ToString();
                            metroInvoice.TaxInvoiceType = dt.Rows[j]["InvoiceType"].ToString();
                            metroInvoice.OrderDateTime = Convert.ToDateTime(dt.Rows[j]["OrderDateTime"].ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                            metroInvoice.OrderType = dt.Rows[j]["OrderType"].ToString();
                            metroInvoice.AlternativeMobileNo = dt.Rows[j]["AlternativeMobileNo"].ToString();
                            metroInvoice.Delivery_Status = dt.Rows[j]["Delivery_Status"].ToString();
                            metroInvoice.DeliveryCharge = dt.Rows[j]["DeliveryCharge"].ToString();
                            metroInvoice.AmoutCollected = dt.Rows[j]["DeliveryChargeCollected"].ToString();
                            metroInvoice.TotalPrice = dt.Rows[j]["TotalPrice"].ToString();
                        }
                        ;
                        ml.Add(metroInvoice);

                        mc.Invoice = ml;
                        prev_customerId = dt.Rows[j]["CustomerId"].ToString();
                    }
                    ci.Add(mc);
                    mt.Customers = ci;
                    li.Add(mt);
                }
                catch (Exception e)
                {

                }
            }
            else
            {
                ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            return li;
        }
        public List<Metro_Trip_List> DriverApp_GetTripInvoicesList(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            List<Metro_Trip_List> li = new List<Metro_Trip_List>();
            List<Metro_Customers> ci = new List<Metro_Customers>();
            List<Metro_Trip_Invoices> ml = new List<Metro_Trip_Invoices>();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            Metro_Trip_List mt = new Metro_Trip_List();
            Metro_Customers mc = new Metro_Customers();

            if (dtuser.Rows.Count > 0)
            {
                try
                {
                    DataTable dt = objbll.Metro_DriverApp_GetTrips(sessionid);
                    string prev_customerId = dt.Rows[0]["CustomerId"].ToString();


                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        mt.TripNum = dt.Rows[j]["TripNum"].ToString();
                        mt.Trip_Status = dt.Rows[j]["Trip_Status"].ToString();
                        string isVerified = "False";
                        DataTable dtVerifyCustomer = objbll.CustomerVerifiedForTrip(dt.Rows[j]["CustomerId"].ToString(), mt.TripNum);
                        if (dtVerifyCustomer.Rows.Count > 0)
                        {
                            isVerified = "True";
                        }

                        if (prev_customerId != dt.Rows[j]["CustomerId"].ToString())
                        {
                            ci.Add(mc);
                            mc = new Metro_Customers();
                            ml = new List<Metro_Trip_Invoices>();
                        }
                        mc.CustomerId = dt.Rows[j]["CustomerId"].ToString();
                        mc.CustomerName = dt.Rows[j]["CustomerName"].ToString();
                        mc.CustomerMobile = dt.Rows[j]["MobileNo"].ToString();
                        mc.AddressId = dt.Rows[j]["CustomerAddressId"].ToString();
                        mc.Address = dt.Rows[j]["Address"].ToString();
                        mc.Latitude = dt.Rows[j]["Latitude"].ToString();
                        mc.Longitude = dt.Rows[j]["Longitude"].ToString();
                        mc.IsVerified = isVerified;

                        var metroInvoice = new Metro_Trip_Invoices();
                        {
                            metroInvoice.TaxInvoiceNum = dt.Rows[j]["TaxInvoiceNum"].ToString();
                            metroInvoice.TaxInvoiceType = dt.Rows[j]["InvoiceType"].ToString();
                            metroInvoice.OrderDateTime = Convert.ToDateTime(dt.Rows[j]["OrderDateTime"].ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                            metroInvoice.OrderType = dt.Rows[j]["OrderType"].ToString();
                            metroInvoice.PaymentType = dt.Rows[j]["PaymentType"].ToString();
                            metroInvoice.PaymentMethod = dt.Rows[j]["PaymentMethod"].ToString();
                            metroInvoice.AlternativeMobileNo = dt.Rows[j]["AlternativeMobileNo"].ToString();
                            metroInvoice.Delivery_Status = dt.Rows[j]["Delivery_Status"].ToString();
                            metroInvoice.DeliveryCharge = dt.Rows[j]["DeliveryCharge"].ToString();
                            metroInvoice.AmoutCollected = dt.Rows[j]["DeliveryChargeCollected"].ToString();
                            metroInvoice.TotalPrice = dt.Rows[j]["TotalPrice"].ToString();
                            metroInvoice.InvoiceAmountCollected = dt.Rows[j]["InvoiceAmountCollected"].ToString();
                            metroInvoice.IsInvoiceVerified = dt.Rows[j]["IsVerified"].ToString();
                            //Added on 28/07/2023
                            metroInvoice.PaymentStatus = dt.Rows[j]["PaymentStatus"].ToString();
                            metroInvoice.OrderId = dt.Rows[j]["ConsignmentId"].ToString().Replace("cons0", "").Replace("_0", "");
                            metroInvoice.ReturnId = dt.Rows[j]["ReturnId"].ToString();

                            //Code Added By Tushar on 22092023 for POD orders if Payment method and Status were not updated from Hibris.
                            if (String.IsNullOrEmpty(metroInvoice.PaymentMethod) && metroInvoice.PaymentType == "POD")
                                metroInvoice.PaymentMethod = "POD";
                            if (String.IsNullOrEmpty(metroInvoice.PaymentStatus) && metroInvoice.PaymentType == "POD")
                                metroInvoice.PaymentStatus = "PAYMENT_PENDING";
                        }
                        ;
                        ml.Add(metroInvoice);

                        mc.Invoice = ml;
                        prev_customerId = dt.Rows[j]["CustomerId"].ToString();
                    }
                    ci.Add(mc);
                    mt.Customers = ci;
                    li.Add(mt);
                }
                catch (Exception e)
                {

                }
            }
            else
            {
                ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            return li;
        }
        public Metro_Invoice DriverApp_GetInvoiceProducts(string sessionid, string invoiceno)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Metro_Invoice li = new Metro_Invoice();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            if (dtuser.Rows.Count > 0)
            {
                DataTable dt = objbll.Metro_DriverApp_GetInvoiceProducts(invoiceno);
                List<Metro_Products> mt = new List<Metro_Products>();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    Metro_Products mi = new Metro_Products();
                    mi.ProductId = dt.Rows[i]["ProductId"].ToString();
                    mi.ProductName = dt.Rows[i]["productName"].ToString();
                    mi.Amount = dt.Rows[i]["Amount"].ToString();
                    mi.TaxAmount = dt.Rows[i]["TaxAmount"].ToString();
                    mi.TotalQuantity = dt.Rows[i]["TotalQuantity"].ToString();
                    mi.TotalWeight = dt.Rows[i]["TotalWeight"].ToString();
                    mi.TaxPercentage = dt.Rows[i]["TaxPercentage"].ToString();
                    mi.Barcode = dt.Rows[i]["Barcode"].ToString();
                    mi.TotalTaxAmount = dt.Rows[i]["TotalTaxAmount"].ToString();
                    //Added on 28/07/2023
                    mi.IsReturnable = dt.Rows[i]["IsReturnable"].ToString();
                    mi.RequestedReturnQty = dt.Rows[i]["RequestedReturnQty"].ToString();
                    mi.TotalReturnQty = dt.Rows[i]["TotalReturnQty"].ToString();
                    mt.Add(mi);
                }
                li.Products = mt;
                li.TaxInvoiceNum = invoiceno;
            }
            else
            {
                ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            return li;
        }
        //public Result DriverApp_UpdateTripStatus(string sessionid, string tripno, string status, string latlng)
        //{
        //    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
        //    WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
        //    Result res = new Result();
        //    BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        //    DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
        //    if (dtuser.Rows.Count > 0)
        //    {
        //        string r = objbll.Metro_DriverApp_UpdateTripStatus(dtuser.Rows[0]["userid"].ToString(), tripno, status, latlng);
        //        if (r != "-1")
        //        {
        //            //MetroGetInvoiceDetailsByTripNo
        //            DataTable dtTripInvoices = objbll.MetroGetInvoiceDetailsByTripNo(tripno);
        //            if (dtTripInvoices != null && (dtTripInvoices.Rows.Count > 0))
        //            {
        //                string accesstoken = "";
        //                try
        //                {
        //                    //To get Access Token
        //                    //https://api.sapqa.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
        //                    //https://api.sappreprod.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
        //                    //https://api.sapdev.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
        //                    string resultToken = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", "POST", "application/json", "");
        //                    MetroHybrisGetAccessToken objAccessToken = jsonSerializer.Deserialize<MetroHybrisGetAccessToken>(resultToken);
        //                    accesstoken = objAccessToken.access_token;
        //                    objbll.MetroInsertAPILog("AccessToken-DriverApp_UpdateTripStatus", "https://api.sapqa.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", resultToken);
        //                }
        //                catch (Exception ex)
        //                {

        //                }
        //                for (int i = 0; i < dtTripInvoices.Rows.Count; i++)
        //                {
        //                    Random rand = new Random();
        //                    string deliverycode = rand.Next(1000, 9999).ToString();


        //                    if (dtTripInvoices.Rows[i]["InvoiceType"].ToString() == "Delivery")
        //                    {
        //                        //Update Out For Delivery
        //                        objbll.Metro_DriverApp_UpdateInvoiceStatusByDriver(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), "Out For Delivery");

        //                        //Update the status to hybris
        //                        //Update the status to hybris For Ecommerce Order Only
        //                        if (dtTripInvoices.Rows[i]["OrderType"].ToString() == "Ecomm")
        //                        {
        //                            try
        //                            {
        //                                string deliverystatus = "OUT_FOR_DELIVERY";

        //                                string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                                string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[i]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[i]["Trip_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[i]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[i]["DriverInfo"].ToString() + "\",\"deliveryCodeExpired\":\"" + "false" + "\"}";
        //                                string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                String unescapedString = Regex.Unescape(cleaned);
        //                                //https://api.sappreprod.metro.co.in/metrooccservices/v2/metro/orderstatusupdate
        //                                //https://api.sapdev.metro.co.in//metrooccservices/v2/metro/orderstatusupdate
        //                                //https://api.sapqa.metro.co.in
        //                                string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);
        //                                MetroHybrisGetPaymentStatus resultPaymentStatus = jsonSerializer.Deserialize<MetroHybrisGetPaymentStatus>(result);
        //                                string value = resultPaymentStatus.value;
        //                                string paymentmethod = resultPaymentStatus.paymentType;
        //                                string paymentstatus = resultPaymentStatus.paymentStatus;

        //                                objbll.MetroInsertAPILog("orderstatusupdate-Delivery-DriverApp_UpdateTripStatus", unescapedString, result);

        //                                objbll.MetroUpdateInvoiceForPODPaymentUpdate(paymentmethod, paymentstatus, "", dtTripInvoices.Rows[i]["ConsignmentId"].ToString());


        //                                //Non Eligible Items Return API
        //                                string cons = dtTripInvoices.Rows[i]["ConsignmentId"].ToString();
        //                                postData = "{\"listofConsignmentId\":[\"" + cons + "\"]}";
        //                                cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                unescapedString = Regex.Unescape(cleaned);

        //                                //Commented For POD Live on 13032023 by Tusar
        //                                //result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/consignment/noneligiblereturnitems", "POST", "application/json", unescapedString, header);
        //                                //objbll.MetroInsertAPILog("orderstatusupdate-Delivery-noneligiblereturnitems-DriverApp_UpdateTripStatus", unescapedString, result);

        //                                try
        //                                {
        //                                    //Update in metro_product_returns -Pending-
        //                                    MetroNonEligibleItems resultNonEligibleItemsList = jsonSerializer.Deserialize<MetroNonEligibleItems>(result);
        //                                    if (resultNonEligibleItemsList.allowForceReturn == true)
        //                                    {
        //                                        List<MetroNonEligibleItemsList> mneil = resultNonEligibleItemsList.nonEligibleItemsList;
        //                                        if (mneil.Count > 0)
        //                                        {
        //                                            string consgniment = mneil[0].key;
        //                                            foreach (var productid in mneil[0].value)
        //                                            {
        //                                                //Update in metro_product_returns
        //                                                objbll.MetroIsReturnableProductUpdateByInvoice(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), productid);
        //                                            }
        //                                        }

        //                                    }
        //                                }
        //                                catch (Exception ex)
        //                                {
        //                                    objbll.MetroInsertAPILog("orderstatusupdate-Delivery-noneligiblereturnitems-DriverApp_UpdateTripStatus", unescapedString, ex.ToString());
        //                                }







        //                                //Create and Send OTP to hybris
        //                                header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                                postData = "{\"id\":\"" + dtTripInvoices.Rows[i]["ConsignmentId"].ToString() + "\",\"deliveryCode\":\"" + deliverycode + "\",\"deliveryFlag\":\"" + true + "\",\"deliveryCodeExpired\":\"" + false + "\"}";
        //                                cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                unescapedString = Regex.Unescape(cleaned);

        //                                //Commented For POD Live on 13032023 by Tusar
        //                                //result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/recieve/sensel/deliverycode", "POST", "application/json", unescapedString, header);

        //                                objbll.UpdateInvoiceOTPDetails(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), deliverycode);

        //                                //objbll.MetroInsertAPILog("deliverycode-otp-request-delivery-DriverApp_Authenticate_Invoice", unescapedString, result);

        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                objbll.MetroInsertAPILog("orderstatusupdate-OUT_FOR_DELIVERY-DriverApp_UpdateTripStatus", "", ex.ToString());
        //                            }
        //                        }


        //                    }
        //                    else
        //                    {
        //                        //Update Out For Pickup
        //                        objbll.Metro_DriverApp_UpdateInvoiceStatusByDriver(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), "Out For Pickup");

        //                        //Update the status to hybris
        //                        //Update the status to hybris For Ecommerce Order Only
        //                        if (dtTripInvoices.Rows[i]["OrderType"].ToString() == "Ecomm")
        //                        {
        //                            try
        //                            {
        //                                string pickupstatus = "OUT_FOR_PICKUP";
        //                                string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                                string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[i]["ConsignmentId"].ToString() + "\",\"status\":\"" + pickupstatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[i]["Trip_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"returnId\":\"" + dtTripInvoices.Rows[i]["ReturnId"].ToString() + "\",\"pickUpDetails\":\"" + dtTripInvoices.Rows[i]["DriverInfo"].ToString() + "\"}";
        //                                string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                String unescapedString = Regex.Unescape(cleaned);
        //                                string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/returnstatusupdate", "PUT", "application/json", unescapedString, header);

        //                                objbll.MetroInsertAPILog("orderstatusupdate-Return-DriverApp_UpdateTripStatus", unescapedString, result);




        //                                header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                                postData = "{\"id\":\"" + dtTripInvoices.Rows[i]["ReturnId"].ToString() + "\",\"deliveryCode\":\"" + deliverycode + "\",\"deliveryFlag\":\"" + false + "\",\"deliveryCodeExpired\":\"" + false + "\"}";
        //                                cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                unescapedString = Regex.Unescape(cleaned);
        //                                result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/recieve/sensel/deliverycode", "POST", "application/json", unescapedString, header);

        //                                objbll.UpdateInvoiceOTPDetails(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), deliverycode);

        //                                objbll.MetroInsertAPILog("deliverycode-otp-request-return-DriverApp_Authenticate_Invoice", unescapedString, result);

        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                objbll.MetroInsertAPILog("orderstatusupdate-OUT_FOR_PICKUP-DriverApp_UpdateTripStatus", "", ex.ToString());
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            //Metro_DriverApp_UpdateInvoiceStatusByDriver
        //            res.result = "success";
        //            res.statuscode = "200";
        //        }
        //        else
        //        {
        //            res.result = "failed,please try again";
        //            res.statuscode = "500";
        //        }
        //    }
        //    else
        //    {
        //        ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
        //        throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
        //    }
        //    return res;
        //}
        //public Result DriverApp_UpdateInvoiceStatus(string sessionid, string invoiceno, string status, string latlng, string reason, List<Metro_Products> returned, List<Metro_Products> delivered)
        //{
        //    string data = "DriverApp_UpdateInvoiceStatus- sessionid : " + sessionid + " invoiceno : " + invoiceno + " status : " + status + " latlng : " + latlng + " reason" + reason;
        //    WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
        //    Result res = new Result();
        //    BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        //    DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
        //    string loginSessionid = "09e43c8a2a9ad68b7a561fa6b4afc03379e7eb73";
        //    if (dtuser.Rows.Count > 0)
        //    {
        //        if (delivered != null)
        //        {
        //            for (int i = 0; i < delivered.Count; i++)
        //            {
        //                objbll.Metro_DriverApp_UpdateDeliveredQty(invoiceno, delivered[i].ProductId, delivered[i].DeliveredQty);
        //                //objbll.SendFormatedMail(loginSessionid, invoiceno + " is delivered successfully.", "Metro Driver App - Order Delivered", "DriverAppDeliveryStatus");
        //            }
        //        }
        //        if (status.ToLower() == "returned" || status.ToLower() == "partially picked up")
        //        {
        //            if (returned != null)
        //            {
        //                for (int i = 0; i < returned.Count; i++)
        //                {
        //                    objbll.Metro_DriverApp_UpdateReturnedQty(invoiceno, returned[i].ProductId, returned[i].ReturnedQty, returned[i].ReturnedReason);
        //                    //objbll.SendFormatedMail(loginSessionid, invoiceno + " is delivered successfully but return products are there.", "Metro Driver App - Returns", "DriverAppDeliveryStatus");
        //                }
        //            }
        //            else
        //            {
        //                res.result = "failed,Returned Qty not available";
        //                res.statuscode = "500";
        //            }
        //        }
        //        if (res.statuscode != "500")
        //        {
        //            string r = objbll.Metro_DriverApp_UpdateInvoiceStatus(dtuser.Rows[0]["userid"].ToString(), invoiceno, status, reason, latlng);

        //            try
        //            {
        //                //MetroGetInvoiceDetailsByTripNo
        //                DataTable dtTripInvoices = objbll.MetroGetInvoiceDetailsByTaxInvoiceNum(invoiceno);
        //                if (dtTripInvoices != null && (dtTripInvoices.Rows.Count > 0))
        //                {
        //                    //Update the status to hybris For Ecommerce Order Only
        //                    if (dtTripInvoices.Rows[0]["OrderType"].ToString() == "Ecomm")
        //                    {
        //                        //Call Hybis API for status update
        //                        // To get Access Token
        //                        string deliverystatus = "";
        //                        JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
        //                        string resultToken = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", "POST", "application/json", "");
        //                        MetroHybrisGetAccessToken objAccessToken = jsonSerializer.Deserialize<MetroHybrisGetAccessToken>(resultToken);
        //                        string accesstoken = objAccessToken.access_token;


        //                        if (status == "Delivered")
        //                        {
        //                            deliverystatus = "DELIVERED";
        //                            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                            string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\",\"deliveryCodeExpired\":\"" + "true" + "\"}";
        //                            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                            String unescapedString = Regex.Unescape(cleaned);
        //                            string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);
        //                            MetroHybrisGetPaymentStatus resultPaymentStatus = jsonSerializer.Deserialize<MetroHybrisGetPaymentStatus>(result);
        //                            string value = resultPaymentStatus.value;

        //                            objbll.MetroInsertAPILog("orderstatusupdate-DELIVERED-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                        }
        //                        else if (status == "UnDelivered")
        //                        {
        //                            deliverystatus = "UNDELIVERED";
        //                            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                            string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
        //                            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                            String unescapedString = Regex.Unescape(cleaned);
        //                            string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);


        //                            objbll.MetroInsertAPILog("orderstatusupdate-UNDELIVERED-DriverApp_UpdateInvoiceStatus", unescapedString, result);

        //                            if (!String.IsNullOrEmpty(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()))
        //                            {
        //                                if(dtTripInvoices.Rows[0]["PaymentStatus"].ToString() == "PAYMENT_SUCCESS")
        //                                {
        //                                    if (Convert.ToInt32(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()) >= 2)
        //                                    {
        //                                        postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + "DeliveryAttemptExceeded" + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
        //                                        cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                        unescapedString = Regex.Unescape(cleaned);
        //                                        result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);

        //                                        objbll.MetroInsertAPILog("orderstatusupdate-DeliveryAttemptExceeded-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                                    }
        //                                }
        //                                else if(dtTripInvoices.Rows[0]["PaymentMethod"].ToString() == "POD" || dtTripInvoices.Rows[0]["PaymentMethod"].ToString() == "COD")
        //                                {
        //                                    if (Convert.ToInt32(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()) >= 1)
        //                                    {
        //                                        postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + "DeliveryAttemptExceeded" + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
        //                                        cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                        unescapedString = Regex.Unescape(cleaned);
        //                                        result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);

        //                                        objbll.MetroInsertAPILog("orderstatusupdate-DeliveryAttemptExceeded-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                                    }
        //                                }
        //                                else
        //                                {
        //                                    if (Convert.ToInt32(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()) >= 2)
        //                                    {
        //                                        postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + "DeliveryAttemptExceeded" + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
        //                                        cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                                        unescapedString = Regex.Unescape(cleaned);
        //                                        result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);

        //                                        objbll.MetroInsertAPILog("orderstatusupdate-DeliveryAttemptExceeded-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                                    }
        //                                }  
        //                            }
        //                        }
        //                        else if (status == "Returned" || status == "Rejected")
        //                        {
        //                            MetroDeliveryReturnRequest d = GetMetroDeliveryReturnRequest(dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString(), status);
        //                            deliverystatus = "DELIVERED";
        //                            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                            var postData = JsonConvert.SerializeObject(d);
        //                            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                            String unescapedString = Regex.Unescape(cleaned);
        //                            string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/deliveryreturns?fields=FULL", "POST", "application/json", unescapedString, header);


        //                            objbll.MetroInsertAPILog("orderstatusupdate-Returned-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                        }
        //                        //else if (status == "Rejected")
        //                        //{
        //                        //    deliverystatus = "ORDER_RETURNED";
        //                        //}
        //                        else if (status == "Not Picked Up")
        //                        {
        //                            deliverystatus = "RETURN_NOT_PICKED_UP";
        //                            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                            string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"returnId\":\"" + dtTripInvoices.Rows[0]["ReturnId"].ToString() + "\",\"pickUpDetails\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
        //                            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                            String unescapedString = Regex.Unescape(cleaned);
        //                            string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/returnstatusupdate", "PUT", "application/json", unescapedString, header);

        //                            objbll.MetroInsertAPILog("orderstatusupdate-RETURN_NOT_PICKED_UP-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                        }
        //                        else if (status == "Return Picked Up" || status == "Partially Picked Up")
        //                        {
        //                            deliverystatus = "RETURN_PICKED_UP";
        //                            MetroPostDeliveryReturnDetails d = MetroPostDeliveryReturnDetails(dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString());
        //                            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
        //                            var postData = JsonConvert.SerializeObject(d);
        //                            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
        //                            String unescapedString = Regex.Unescape(cleaned);
        //                            string result = objbll.MakeHttpRequest("https://api.sapqa.metro.co.in/metrooccservices/v2/metro/returnstatusupdate", "PUT", "application/json", unescapedString, header);


        //                            objbll.MetroInsertAPILog("orderstatusupdate-RETURN_PICKED_UP-DriverApp_UpdateInvoiceStatus", unescapedString, result);
        //                        }
        //                        else if (status == "Return Cancelled")
        //                        {
        //                            deliverystatus = "UNDELIVERED";
        //                        }
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                objbll.MetroInsertAPILog("orderstatusupdate-DriverApp_UpdateInvoiceStatus", "", ex.ToString());
        //            }


        //            try
        //            {
        //                var jsonReturnedData = JsonConvert.SerializeObject(returned);
        //                var jsonDeliveredData = JsonConvert.SerializeObject(delivered);
        //                objbll.STL_RecordErrorMessage(data);
        //                objbll.MetroInsertAPILog("DriverApp_UpdateInvoiceStatus", jsonReturnedData.ToString(), jsonDeliveredData.ToString());
        //            }
        //            catch (Exception ex)
        //            {

        //            }

        //            try
        //            {
        //                if (status != "Undelivered")
        //                {
        //                    // objbll.Metro_DriverApp_UpdateCustomerAddress(invoiceno, latlng);
        //                }
        //            }
        //            catch (Exception ex) { }
        //            objbll.SendFormatedMail(loginSessionid, invoiceno + " is undelivered", "Metro Driver App - UnDelivered", "DriverAppDeliveryStatus");
        //            if (r != "-1")
        //            {
        //                res.result = "success";
        //                res.statuscode = "200";
        //            }
        //            else
        //            {
        //                res.result = "failed,please try again";
        //                res.statuscode = "500";
        //            }
        //            //string tripId = objbll.MetroGetTripSheetNoByInvoice(invoiceno);
        //            //objbll.MetroCloseTrip(tripId);
        //        }
        //    }
        //    else
        //    {
        //        ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
        //        throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
        //    }
        //    return res;
        //}
        public CustomerAuthenticate DriverApp_Customer_Authenticate(string customerid, string mobileno, string tripsheetno, string returnqty, string cash, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            CustomerAuthenticate auth = new CustomerAuthenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtTotalItems = objbll.GetCustomerTotalProductCount(customerid, tripsheetno);
            DataTable dtVerifyCustomer = objbll.CustomerVerifiedForTrip(customerid, tripsheetno);
            if (dtVerifyCustomer.Rows.Count > 0)
            {
                auth.isVerified = dtVerifyCustomer.Rows[0]["IsVerifiedByDriver"].ToString();
            }
            else
            {
                if (type.ToLower().Trim() == "otp_request")
                {
                    //auth.result = objbll.DriverApp_Customer_OTPRequest(mobileno);
                    if (dtTotalItems.Rows.Count > 0)
                    {
                        auth.totalitems = dtTotalItems.Rows[0]["totalitems"].ToString();
                        auth.isVerified = "False";
                    }
                }
                else if (type.ToLower().Trim() == "otp_validate")
                {
                    DataTable dt = objbll.DriverApp_Customer_OTPValidate(mobileno, otp);
                    if (dt.Rows.Count > 0)
                    {
                        auth.result = "Valid OTP";
                        auth.isVerified = "True";
                        objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, type);
                    }
                    else
                    {
                        auth.result = "Invalid OTP";
                    }
                }
                else
                {
                    objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, type);
                    auth.isVerified = "True";
                }
            }
            return auth;
        }
        public CustomerAuthenticate DriverApp_Authenticate_Customer(string customerid, string mobileno, string tripsheetno, string returnqty, string cash, string type, string reason, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            CustomerAuthenticate auth = new CustomerAuthenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string msg = "";
            //Get Invoices By Statuses
            string countProductsDelivered = objbll.GetTripInvoicesByCustomerAndStatus(customerid, tripsheetno, "Delivered");
            string countProductsReturned = objbll.GetTripInvoicesByCustomerAndStatus(customerid, tripsheetno, "Returned");
            string countProductsRejected = objbll.GetTripInvoicesByCustomerAndStatus(customerid, tripsheetno, "Rejected");
            if (!String.IsNullOrEmpty(countProductsDelivered))
            {
                msg += "Qty Deliverd : " + countProductsDelivered + ",";
            }
            if (!String.IsNullOrEmpty(countProductsReturned))
            {
                msg += "Qty Returned : " + countProductsReturned + ",";
            }
            if (!String.IsNullOrEmpty(countProductsRejected))
            {
                msg += "Qty Rejected : " + countProductsRejected;
            }
            msg = msg.TrimEnd(',');
            msg = " " + msg + " ";
            DataTable dtTotalItems = objbll.GetCustomerTotalProductCount(customerid, tripsheetno);
            DataTable dtVerifyCustomer = objbll.CustomerVerifiedForTrip(customerid, tripsheetno);
            if (dtVerifyCustomer.Rows.Count > 0)
            {
                auth.isVerified = dtVerifyCustomer.Rows[0]["IsVerifiedByDriver"].ToString();
            }
            else
            {
                if (type.ToLower().Trim() == "otp_request")
                {
                    auth.result = objbll.DriverApp_Customer_OTPRequest(mobileno, msg);
                    if (dtTotalItems.Rows.Count > 0)
                    {
                        auth.totalitems = dtTotalItems.Rows[0]["totalitems"].ToString();
                        auth.isVerified = "False";
                    }
                }
                else if (type.ToLower().Trim() == "otp_validate")
                {
                    DataTable dt = objbll.DriverApp_Customer_OTPValidate(mobileno, otp);
                    if (dt.Rows.Count > 0)
                    {
                        auth.result = "Valid OTP";
                        auth.isVerified = "True";
                        objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, type);
                        objbll.UpdateInvoiceAcknowledgement(tripsheetno, customerid);
                    }
                    else
                    {
                        auth.result = "Invalid OTP";
                    }
                }
                else if (type.ToLower().Trim() == "otp_request_temp")
                {
                    auth.result = objbll.DriverApp_Customer_OTPRequest_Temp(mobileno, customerid, msg);
                    auth.isVerified = "False";
                }
                else if (type.ToLower().Trim() == "otp_validate_temp")
                {
                    DataTable dt = objbll.DriverApp_Customer_OTPValidate_Temp(mobileno, otp);
                    if (dt.Rows.Count > 0)
                    {
                        auth.result = "Valid OTP";
                        auth.isVerified = "True";
                        objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, reason);
                        objbll.UpdateInvoiceAcknowledgement(tripsheetno, customerid);
                    }
                    else
                    {
                        auth.result = "Invalid OTP";
                    }
                }
                else
                {
                    objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, reason);
                    objbll.UpdateInvoiceAcknowledgement(tripsheetno, customerid);
                    auth.isVerified = "False";
                }
            }
            return auth;
        }
        public CustomerAuthenticate DriverApp_Authenticate_Invoice(string customerid, string mobileno, string taxinvoiceno, string returnqty, string cash, string type, string reason, string otp, string status, string invoiceamount)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            CustomerAuthenticate auth = new CustomerAuthenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string msg = "";
            string totalQty = "";
            DataTable dtTripInvoicesDetails = objbll.GetTripInvoicesDetailsByTaxInvoiceNum(taxinvoiceno);
            //For Hybris
            //if (type.ToLower().Trim() == "otp_request")
            //{
            //    if (dtTripInvoicesDetails.Rows.Count > 0)
            //    {
            //        // Call Hybis API for status update
            //        // To get Access Token
            //        JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
            //        //https://api.sappreprod.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
            //        string resultToken = objbll.MakeHttpRequest("https://api.sapdev.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", "POST", "application/json", "");
            //        MetroHybrisGetAccessToken objAccessToken = jsonSerializer.Deserialize<MetroHybrisGetAccessToken>(resultToken);
            //        string accesstoken = objAccessToken.access_token;


            //        Random rand = new Random();
            //        string deliverycode = rand.Next(1000, 9999).ToString();

            //        if (dtTripInvoicesDetails.Rows[0]["InvoiceType"].ToString() == "Delivery")
            //        {
            //            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
            //            string postData = "{\"id\":\"" + dtTripInvoicesDetails.Rows[0]["ConsignmentId"].ToString() + "\",\"deliveryCode\":\"" + deliverycode + "\",\"deliveryFlag\":\"" + true + "\",\"deliveryCodeExpired\":\"" + false + "\"}";
            //            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
            //            String unescapedString = Regex.Unescape(cleaned);
            //            string result = objbll.MakeHttpRequest("https://api.sapdev.metro.co.in/metrooccservices/v2/metro/recieve/sensel/deliverycode", "POST", "application/json", unescapedString, header);

            //            objbll.MetroInsertAPILog("deliverycode-otp-request-delivery-DriverApp_Authenticate_Invoice", unescapedString, result);
            //        }
            //        else if (dtTripInvoicesDetails.Rows[0]["InvoiceType"].ToString() == "Return")
            //        {
            //            string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
            //            string postData = "{\"id\":\"" + dtTripInvoicesDetails.Rows[0]["ReturnId"].ToString() + "\",\"deliveryCode\":\"" + deliverycode + "\",\"deliveryFlag\":\"" + false + "\",\"deliveryCodeExpired\":\"" + false + "\"}";
            //            string cleaned = postData.Replace("\n", "").Replace("\r", " ");
            //            String unescapedString = Regex.Unescape(cleaned);
            //            string result = objbll.MakeHttpRequest("https://api.sapdev.metro.co.in/metrooccservices/v2/metro/recieve/sensel/deliverycode", "POST", "application/json", unescapedString, header);

            //            objbll.MetroInsertAPILog("deliverycode-otp-request-return-DriverApp_Authenticate_Invoice", unescapedString, result);
            //        }
            //    }
            //}


            if (dtTripInvoicesDetails.Rows.Count > 0)
            {
                auth.isVerified = dtTripInvoicesDetails.Rows[0]["IsVerified"].ToString();
                if (status == "Returned" || status == "Partially Picked Up")
                {
                    //msg = " Order No " + taxinvoiceno + " attempting for " + status + " with Qty " + returnqty;
                    //msg = taxinvoiceno+" attempting for "+status+" with Qty "+ returnqty;
                }
                else
                {
                    totalQty = dtTripInvoicesDetails.Rows[0]["totalitems"].ToString();
                    //msg = " Order No " + taxinvoiceno + " attempting for " + status + " with Qty " + totalQty;
                    //msg = taxinvoiceno + " attempting for " + status + " with Qty " + totalQty;
                }
            }



            if (auth.isVerified == "True")
            {
                auth.isVerified = dtTripInvoicesDetails.Rows[0]["IsVerified"].ToString();
            }
            else
            {
                if (type.ToLower().Trim() == "otp_request")
                {
                    if (String.IsNullOrEmpty(dtTripInvoicesDetails.Rows[0]["VerifiedOTP"].ToString()))
                    {
                        auth.result = objbll.DriverApp_Customer_OTPRequest(mobileno, msg);
                    }
                    else if (dtTripInvoicesDetails.Rows[0]["VerifiedOTP"].ToString().Length > 1)
                    {
                        auth.result = objbll.DriverApp_Customer_OTPRequest_InvoiceLevel(mobileno, msg, dtTripInvoicesDetails.Rows[0]["VerifiedOTP"].ToString());
                    }
                    else
                    {
                        auth.result = objbll.DriverApp_Customer_OTPRequest(mobileno, msg);
                    }

                    if (dtTripInvoicesDetails.Rows.Count > 0)
                    {
                        auth.totalitems = dtTripInvoicesDetails.Rows[0]["totalitems"].ToString();
                        auth.isVerified = "False";
                    }
                }
                else if (type.ToLower().Trim() == "otp_validate")
                {
                    DataTable dt = objbll.DriverApp_Customer_OTPValidate(mobileno, otp);
                    if (dt.Rows.Count > 0)
                    {
                        auth.result = "Valid OTP";
                        auth.isVerified = "True";
                        //objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, type);
                        objbll.UpdateInvoiceVerificationDetails(taxinvoiceno, mobileno, otp, cash, invoiceamount);
                    }
                    else
                    {
                        auth.result = "Invalid OTP";
                    }
                }
                else if (type.ToLower().Trim() == "otp_request_temp")
                {
                    if (String.IsNullOrEmpty(dtTripInvoicesDetails.Rows[0]["VerifiedOTP"].ToString()))
                    {
                        auth.result = objbll.DriverApp_Customer_OTPRequest_Temp(mobileno, customerid, msg);
                    }
                    else if (dtTripInvoicesDetails.Rows[0]["VerifiedOTP"].ToString().Length > 1)
                    {
                        auth.result = objbll.DriverApp_Customer_OTPRequest_InvoiceLevel_Temp(customerid, mobileno, msg, dtTripInvoicesDetails.Rows[0]["VerifiedOTP"].ToString());
                    }
                    else
                    {
                        auth.result = objbll.DriverApp_Customer_OTPRequest_Temp(mobileno, customerid, msg);
                    }

                    auth.isVerified = "False";
                }
                else if (type.ToLower().Trim() == "otp_validate_temp")
                {
                    DataTable dt = objbll.DriverApp_Customer_OTPValidate_Temp(mobileno, otp);
                    if (dt.Rows.Count > 0)
                    {
                        auth.result = "Valid OTP";
                        auth.isVerified = "True";
                        //objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, reason);
                        objbll.UpdateInvoiceVerificationDetails(taxinvoiceno, mobileno, otp, cash, invoiceamount);
                    }
                    else
                    {
                        auth.result = "Invalid OTP";
                    }
                }
                else
                {
                    //objbll.InsertMetroTripReturn(tripsheetno, dtTotalItems.Rows[0]["totalitems"].ToString(), customerid, returnqty, cash, reason);
                    objbll.UpdateInvoiceVerificationDetails(taxinvoiceno, mobileno, otp, cash, invoiceamount);
                    auth.isVerified = "False";
                }
            }
            return auth;
        }
        public string DriverApp_UploadFile(string sessionid, string invoiceno, string fileName, Stream filedata)
        {
            try
            {
                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                string username = ObjBll.getUserId(sessionid);
                //Thread rlog = new Thread(() => ObjBll.WebServiceRequestlog("SenselRestService", "DriverApp_UploadFile", username, username, sessionid, sHostName, sUserIP));
                //rlog.Start();
                fileName = "mcc_" + invoiceno + "_" + fileName;
                string FilePath = Path.Combine(HostingEnvironment.MapPath("~/Uploads/"), fileName);
                MultipartParser parser = new MultipartParser(filedata);
                if (parser.Success)
                {
                    File.WriteAllBytes(FilePath, parser.FileContents);
                }
                else
                {
                    return "failed,file missing";
                }
                // The files will be uploaded to S3. And this path is passed to UploadFiles method below.
                String S3Path = System.Web.Configuration.WebConfigurationManager.AppSettings["AWSS3UploadFolder"];
                if (String.IsNullOrEmpty(S3Path))
                    S3Path = "fleetsmart3.sensel.in/App Files/sensel/Sensel.in/fleetsmart3.ui.sensel.in/Uploads";

                BusinessLogicLayer.AmazonS3Upload.UploadFiles(HostingEnvironment.MapPath("~/Uploads/"), fileName, "db-flatfile-backup", S3Path, "PublicRead");
                return "success";
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }
        public List<MetroOrderStatusReason> DriverApp_GetOrderStatusReason(string sessionid, string status)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            List<MetroOrderStatusReason> mt = new List<MetroOrderStatusReason>();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            if (dtuser.Rows.Count > 0)
            {
                DataTable dt = objbll.Metro_DriverApp_GetOrderStatusReason(status);
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    MetroOrderStatusReason mi = new MetroOrderStatusReason();
                    mi.Status = dt.Rows[i]["StatusType"].ToString();
                    mi.ReasonCode = dt.Rows[i]["ReasonCode"].ToString();

                    mt.Add(mi);
                }
            }
            else
            {
                ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            return mt;
        }
        public string DriverApp_DisableReturnForCODOrders(string sessionid)
        {
            string status = "Enable";
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            List<MetroOrderStatusReason> mt = new List<MetroOrderStatusReason>();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            if (dtuser.Rows.Count > 0)
            {
                status = "Disable";
            }

            return status;
        }

        //Backup Methods to be used in POD live by Tushar on
        public Result DriverApp_UpdateTripStatus(string sessionid, string tripno, string status, string latlng)
        {
            JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Result res = new Result();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            if (dtuser.Rows.Count > 0)
            {
                string r = objbll.Metro_DriverApp_UpdateTripStatus(dtuser.Rows[0]["userid"].ToString(), tripno, status, latlng);
                if (r != "-1")
                {
                    //MetroGetInvoiceDetailsByTripNo
                    DataTable dtTripInvoices = objbll.MetroGetInvoiceDetailsByTripNo(tripno);
                    if (dtTripInvoices != null && (dtTripInvoices.Rows.Count > 0))
                    {
                        string accesstoken = "";
                        try
                        {
                            //To get Access Token
                            //https://api.sapqa.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
                            //https://api.sappreprod.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
                            //https://api.sapdev.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
                            //https://online.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials
                            string resultToken = objbll.MakeHttpRequest("https://online.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", "POST", "application/json", "");
                            MetroHybrisGetAccessToken objAccessToken = jsonSerializer.Deserialize<MetroHybrisGetAccessToken>(resultToken);
                            accesstoken = objAccessToken.access_token;
                            objbll.MetroInsertAPILog("AccessToken-DriverApp_UpdateTripStatus", "https://online.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", resultToken);
                        }
                        catch (Exception ex)
                        {

                        }



                        for (int i = 0; i < dtTripInvoices.Rows.Count; i++)
                        {
                            Random rand = new Random();
                            string deliverycode = rand.Next(1000, 9999).ToString();


                            if (dtTripInvoices.Rows[i]["InvoiceType"].ToString() == "Delivery")
                            {
                                //Update Out For Delivery
                                objbll.Metro_DriverApp_UpdateInvoiceStatusByDriver(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), "Out For Delivery");

                                //Update the status to hybris
                                //Update the status to hybris For Ecommerce Order Only
                                if (dtTripInvoices.Rows[i]["OrderType"].ToString() == "Ecomm")
                                {
                                    try
                                    {
                                        string deliverystatus = "OUT_FOR_DELIVERY";

                                        string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                        string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[i]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[i]["Trip_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[i]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[i]["DriverInfo"].ToString() + "\",\"deliveryCodeExpired\":\"" + "false" + "\"}";
                                        string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                        String unescapedString = Regex.Unescape(cleaned);
                                        //https://api.sappreprod.metro.co.in/metrooccservices/v2/metro/orderstatusupdate
                                        //https://api.sapdev.metro.co.in//metrooccservices/v2/metro/orderstatusupdate
                                        //https://api.sapqa.metro.co.in
                                        //https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate
                                        string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);
                                        MetroHybrisGetPaymentStatus resultPaymentStatus = jsonSerializer.Deserialize<MetroHybrisGetPaymentStatus>(result);
                                        string value = resultPaymentStatus.value;
                                        string paymentmethod = resultPaymentStatus.paymentType;
                                        string paymentstatus = resultPaymentStatus.paymentStatus;

                                        objbll.MetroInsertAPILog("orderstatusupdate-Delivery-DriverApp_UpdateTripStatus", unescapedString, result);

                                        objbll.MetroUpdateInvoiceForPODPaymentUpdate(paymentmethod, paymentstatus, "", dtTripInvoices.Rows[i]["ConsignmentId"].ToString());


                                        //Non Eligible Items Return API
                                        string cons = dtTripInvoices.Rows[i]["ConsignmentId"].ToString();
                                        postData = "{\"listofConsignmentId\":[\"" + cons + "\"]}";
                                        cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                        unescapedString = Regex.Unescape(cleaned);

                                        //Commented For POD Live on 13032023 by Tusar
                                        result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/consignment/noneligiblereturnitems", "POST", "application/json", unescapedString, header);
                                        objbll.MetroInsertAPILog("orderstatusupdate-Delivery-noneligiblereturnitems-DriverApp_UpdateTripStatus", unescapedString, result);

                                        try
                                        {
                                            //Update in metro_product_returns -Pending-
                                            MetroNonEligibleItems resultNonEligibleItemsList = jsonSerializer.Deserialize<MetroNonEligibleItems>(result);
                                            if (resultNonEligibleItemsList.allowForceReturn == true)
                                            {
                                                List<MetroNonEligibleItemsList> mneil = resultNonEligibleItemsList.nonEligibleItemsList;
                                                if (mneil.Count > 0)
                                                {
                                                    string consgniment = mneil[0].key;
                                                    foreach (var productid in mneil[0].value)
                                                    {
                                                        //Update in metro_product_returns
                                                        objbll.MetroIsReturnableProductUpdateByInvoice(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), productid);
                                                    }
                                                }

                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            objbll.MetroInsertAPILog("orderstatusupdate-Delivery-noneligiblereturnitems-DriverApp_UpdateTripStatus", unescapedString, ex.ToString());
                                        }







                                        //Create and Send OTP to hybris
                                        header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                        postData = "{\"id\":\"" + dtTripInvoices.Rows[i]["ConsignmentId"].ToString() + "\",\"deliveryCode\":\"" + deliverycode + "\",\"deliveryFlag\":\"" + true + "\",\"deliveryCodeExpired\":\"" + false + "\"}";
                                        cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                        unescapedString = Regex.Unescape(cleaned);

                                        //Commented For POD Live on 13032023 by Tusar
                                        result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/recieve/sensel/deliverycode", "POST", "application/json", unescapedString, header);

                                        objbll.UpdateInvoiceOTPDetails(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), deliverycode);

                                        //objbll.MetroInsertAPILog("deliverycode-otp-request-delivery-DriverApp_Authenticate_Invoice", unescapedString, result);

                                    }
                                    catch (Exception ex)
                                    {
                                        objbll.MetroInsertAPILog("orderstatusupdate-OUT_FOR_DELIVERY-DriverApp_UpdateTripStatus", "", ex.ToString());
                                    }
                                }


                            }
                            else
                            {
                                //Update Out For Pickup
                                objbll.Metro_DriverApp_UpdateInvoiceStatusByDriver(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), "Out For Pickup");

                                //Update the status to hybris
                                //Update the status to hybris For Ecommerce Order Only
                                if (dtTripInvoices.Rows[i]["OrderType"].ToString() == "Ecomm")
                                {
                                    try
                                    {
                                        string pickupstatus = "OUT_FOR_PICKUP";
                                        string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                        string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[i]["ConsignmentId"].ToString() + "\",\"status\":\"" + pickupstatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[i]["Trip_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"returnId\":\"" + dtTripInvoices.Rows[i]["ReturnId"].ToString() + "\",\"pickUpDetails\":\"" + dtTripInvoices.Rows[i]["DriverInfo"].ToString() + "\"}";
                                        string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                        String unescapedString = Regex.Unescape(cleaned);
                                        string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/returnstatusupdate", "PUT", "application/json", unescapedString, header);

                                        objbll.MetroInsertAPILog("orderstatusupdate-Return-DriverApp_UpdateTripStatus", unescapedString, result);




                                        header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                        postData = "{\"id\":\"" + dtTripInvoices.Rows[i]["ReturnId"].ToString() + "\",\"deliveryCode\":\"" + deliverycode + "\",\"deliveryFlag\":\"" + false + "\",\"deliveryCodeExpired\":\"" + false + "\"}";
                                        cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                        unescapedString = Regex.Unescape(cleaned);
                                        result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/recieve/sensel/deliverycode", "POST", "application/json", unescapedString, header);

                                        objbll.UpdateInvoiceOTPDetails(dtTripInvoices.Rows[i]["TaxInvoiceNum"].ToString(), deliverycode);

                                        objbll.MetroInsertAPILog("deliverycode-otp-request-return-DriverApp_Authenticate_Invoice", unescapedString, result);

                                    }
                                    catch (Exception ex)
                                    {
                                        objbll.MetroInsertAPILog("orderstatusupdate-OUT_FOR_PICKUP-DriverApp_UpdateTripStatus", "", ex.ToString());
                                    }
                                }
                            }
                        }
                    }
                    //Metro_DriverApp_UpdateInvoiceStatusByDriver
                    res.result = "success";
                    res.statuscode = "200";
                }
                else
                {
                    res.result = "failed,please try again";
                    res.statuscode = "500";
                }
            }
            else
            {
                ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            return res;
        }
        public Result DriverApp_UpdateInvoiceStatus(string sessionid, string invoiceno, string status, string latlng, string reason, List<Metro_Products> returned, List<Metro_Products> delivered)
        {
            string data = "DriverApp_UpdateInvoiceStatus- sessionid : " + sessionid + " invoiceno : " + invoiceno + " status : " + status + " latlng : " + latlng + " reason" + reason;
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Result res = new Result();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverApp_GetUserDetails(sessionid);
            string loginSessionid = "09e43c8a2a9ad68b7a561fa6b4afc03379e7eb73";
            if (dtuser.Rows.Count > 0)
            {
                if (delivered != null)
                {
                    for (int i = 0; i < delivered.Count; i++)
                    {
                        objbll.Metro_DriverApp_UpdateDeliveredQty(invoiceno, delivered[i].ProductId, delivered[i].DeliveredQty);
                        //objbll.SendFormatedMail(loginSessionid, invoiceno + " is delivered successfully.", "Metro Driver App - Order Delivered", "DriverAppDeliveryStatus");
                    }
                }
                if (status.ToLower() == "returned" || status.ToLower() == "partially picked up")
                {
                    if (returned != null)
                    {
                        for (int i = 0; i < returned.Count; i++)
                        {
                            objbll.Metro_DriverApp_UpdateReturnedQty(invoiceno, returned[i].ProductId, returned[i].ReturnedQty, returned[i].ReturnedReason);
                            //objbll.SendFormatedMail(loginSessionid, invoiceno + " is delivered successfully but return products are there.", "Metro Driver App - Returns", "DriverAppDeliveryStatus");
                        }
                    }
                    else
                    {
                        res.result = "failed,Returned Qty not available";
                        res.statuscode = "500";
                    }
                }
                if (res.statuscode != "500")
                {
                    string r = objbll.Metro_DriverApp_UpdateInvoiceStatus(dtuser.Rows[0]["userid"].ToString(), invoiceno, status, reason, latlng);
                    try
                    {
                        //MetroGetInvoiceDetailsByTripNo
                        DataTable dtTripInvoices = objbll.MetroGetInvoiceDetailsByTaxInvoiceNum(invoiceno);
                        if (dtTripInvoices != null && (dtTripInvoices.Rows.Count > 0))
                        {
                            //Update the status to hybris For Ecommerce Order Only
                            if (dtTripInvoices.Rows[0]["OrderType"].ToString() == "Ecomm")
                            {
                                //Call Hybis API for status update
                                // To get Access Token
                                string deliverystatus = "";
                                JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                                string resultToken = objbll.MakeHttpRequest("https://online.metro.co.in/authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", "POST", "application/json", "");
                                MetroHybrisGetAccessToken objAccessToken = jsonSerializer.Deserialize<MetroHybrisGetAccessToken>(resultToken);
                                string accesstoken = objAccessToken.access_token;


                                if (status == "Delivered")
                                {
                                    deliverystatus = "DELIVERED";
                                    string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                    string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\",\"deliveryCodeExpired\":\"" + "true" + "\"}";
                                    string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                    String unescapedString = Regex.Unescape(cleaned);
                                    string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);
                                    MetroHybrisGetPaymentStatus resultPaymentStatus = jsonSerializer.Deserialize<MetroHybrisGetPaymentStatus>(result);
                                    string value = resultPaymentStatus.value;

                                    objbll.MetroInsertAPILog("orderstatusupdate-DELIVERED-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                }
                                else if (status == "UnDelivered")
                                {
                                    deliverystatus = "UNDELIVERED";
                                    string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                    string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
                                    string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                    String unescapedString = Regex.Unescape(cleaned);
                                    string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);


                                    objbll.MetroInsertAPILog("orderstatusupdate-UNDELIVERED-DriverApp_UpdateInvoiceStatus", unescapedString, result);

                                    if (!String.IsNullOrEmpty(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()))
                                    {
                                        if (dtTripInvoices.Rows[0]["PaymentStatus"].ToString() == "PAYMENT_SUCCESS")
                                        {
                                            if (Convert.ToInt32(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()) >= 2)
                                            {
                                                postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + "DeliveryAttemptExceeded" + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
                                                cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                                unescapedString = Regex.Unescape(cleaned);
                                                result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);

                                                objbll.MetroInsertAPILog("orderstatusupdate-DeliveryAttemptExceeded-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                            }
                                        }
                                        else if (dtTripInvoices.Rows[0]["PaymentMethod"].ToString() == "POD" || dtTripInvoices.Rows[0]["PaymentMethod"].ToString() == "COD")
                                        {
                                            if (Convert.ToInt32(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()) >= 1)
                                            {
                                                postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + "DeliveryAttemptExceeded" + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
                                                cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                                unescapedString = Regex.Unescape(cleaned);
                                                result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);

                                                objbll.MetroInsertAPILog("orderstatusupdate-DeliveryAttemptExceeded-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                            }
                                        }
                                        else
                                        {
                                            if (Convert.ToInt32(dtTripInvoices.Rows[0]["DeliveryAttemptCount"].ToString()) >= 2)
                                            {
                                                postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + "DeliveryAttemptExceeded" + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"taxInvoiceNum\":\"" + dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString() + "\",\"reason\":\"" + dtTripInvoices.Rows[0]["Reason"].ToString() + "\",\"driverInfo\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
                                                cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                                unescapedString = Regex.Unescape(cleaned);
                                                result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/orderstatusupdate", "PUT", "application/json", unescapedString, header);

                                                objbll.MetroInsertAPILog("orderstatusupdate-DeliveryAttemptExceeded-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                            }
                                        }
                                    }
                                }
                                else if (status == "Returned" || status == "Rejected")
                                {
                                    MetroDeliveryReturnRequest d = GetMetroDeliveryReturnRequest(dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString(), status);
                                    deliverystatus = "DELIVERED";
                                    string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                    var postData = JsonConvert.SerializeObject(d);
                                    string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                    String unescapedString = Regex.Unescape(cleaned);
                                    string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/deliveryreturns?fields=FULL", "POST", "application/json", unescapedString, header);


                                    objbll.MetroInsertAPILog("orderstatusupdate-Returned-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                }
                                //else if (status == "Rejected")
                                //{
                                //    deliverystatus = "ORDER_RETURNED";
                                //}
                                else if (status == "Not Picked Up")
                                {
                                    deliverystatus = "RETURN_NOT_PICKED_UP";
                                    string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                    string postData = "{\"consignmentId\":\"" + dtTripInvoices.Rows[0]["ConsignmentId"].ToString() + "\",\"status\":\"" + deliverystatus + "\",\"date\":\"" + Convert.ToDateTime(dtTripInvoices.Rows[0]["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt") + "\",\"returnId\":\"" + dtTripInvoices.Rows[0]["ReturnId"].ToString() + "\",\"pickUpDetails\":\"" + dtTripInvoices.Rows[0]["DriverInfo"].ToString() + "\"}";
                                    string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                    String unescapedString = Regex.Unescape(cleaned);
                                    string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/returnstatusupdate", "PUT", "application/json", unescapedString, header);

                                    objbll.MetroInsertAPILog("orderstatusupdate-RETURN_NOT_PICKED_UP-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                }
                                else if (status == "Return Picked Up" || status == "Partially Picked Up")
                                {
                                    deliverystatus = "RETURN_PICKED_UP";
                                    MetroPostDeliveryReturnDetails d = MetroPostDeliveryReturnDetails(dtTripInvoices.Rows[0]["TaxInvoiceNum"].ToString());
                                    string header = "Cookie, ROUTE=.api-544f669bc9-6psjg||Authorization, Bearer " + accesstoken;
                                    var postData = JsonConvert.SerializeObject(d);
                                    string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                                    String unescapedString = Regex.Unescape(cleaned);
                                    string result = objbll.MakeHttpRequest("https://online.metro.co.in/metrooccservices/v2/metro/returnstatusupdate", "PUT", "application/json", unescapedString, header);


                                    objbll.MetroInsertAPILog("orderstatusupdate-RETURN_PICKED_UP-DriverApp_UpdateInvoiceStatus", unescapedString, result);
                                }
                                else if (status == "Return Cancelled")
                                {
                                    deliverystatus = "UNDELIVERED";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        objbll.MetroInsertAPILog("orderstatusupdate-DriverApp_UpdateInvoiceStatus", "", ex.ToString());
                    }


                    try
                    {
                        var jsonReturnedData = JsonConvert.SerializeObject(returned);
                        var jsonDeliveredData = JsonConvert.SerializeObject(delivered);
                        objbll.STL_RecordErrorMessage(data);
                        objbll.MetroInsertAPILog("DriverApp_UpdateInvoiceStatus", jsonReturnedData.ToString(), jsonDeliveredData.ToString());
                    }
                    catch (Exception ex)
                    {

                    }

                    try
                    {
                        if (status != "Undelivered")
                        {
                            // objbll.Metro_DriverApp_UpdateCustomerAddress(invoiceno, latlng);
                        }
                    }
                    catch (Exception ex) { }
                    objbll.SendFormatedMail(loginSessionid, invoiceno + " is undelivered", "Metro Driver App - UnDelivered", "DriverAppDeliveryStatus");
                    if (r != "-1")
                    {
                        res.result = "success";
                        res.statuscode = "200";
                    }
                    else
                    {
                        res.result = "failed,please try again";
                        res.statuscode = "500";
                    }
                    //string tripId = objbll.MetroGetTripSheetNoByInvoice(invoiceno);
                    //objbll.MetroCloseTrip(tripId);

                    try
                    {
                        DataTable dtInvoiceCancelled = objbll.GetMetroInvoicesCancelled(invoiceno);
                        if (dtInvoiceCancelled.Rows.Count > 0)
                        {
                            for (int i = 0; i < dtInvoiceCancelled.Rows.Count; i++)
                            {
                                objbll.InsertMetroTripStatus(dtInvoiceCancelled.Rows[i]["StoreNo"].ToString(), dtInvoiceCancelled.Rows[i]["TripSheetNo"].ToString(), dtInvoiceCancelled.Rows[i]["TaxInvoiceNum"].ToString(), Convert.ToDateTime(dtInvoiceCancelled.Rows[i]["OrderDateTime"]), status, reason, Convert.ToDateTime(DateTime.Now), "", "DriverApp" + sessionid);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }

                }
            }
            else
            {
                ErrorMessage customError = new ErrorMessage("401", "FAIL:INVALID_SESSIONID");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            return res;
        }
        #endregion

        private static string dateformat(string date)
        {
            try { return Convert.ToDateTime(date).ToString("yyyy-MM-dd HH:mm:ss"); }
            catch { return ""; }
        }
        private static string getHostName()
        {
            try
            {
                return System.Web.HttpContext.Current.Request.Url.ToString().ToLower().Substring(0, System.Web.HttpContext.Current.Request.Url.ToString().ToLower().IndexOf("senselrestservice.svc"));
            }
            catch
            {
                return string.Empty;
            }
        }
        private static string GetUser_IP()
        {
            try
            {
                string VisitorsIPAddr = string.Empty;
                if (System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"] != null)
                {
                    VisitorsIPAddr = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"].ToString();
                }
                else if (System.Web.HttpContext.Current.Request.UserHostAddress.Length != 0)
                {
                    VisitorsIPAddr = System.Web.HttpContext.Current.Request.UserHostAddress;
                }
                return VisitorsIPAddr;
            }
            catch { return string.Empty; }
        }
        //Added By Madhuri for BIAL 17-03-2020
        public Authenticate DriverSmart_Track_Authenticate(string mobileno, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            if (type.ToLower().Trim() == "otp_request")
            {
                auth.result = objbll.DriversmartTrack_OTPRequest(mobileno);
                if (auth.result == "OTP Send Successfully")
                    auth.status = "Success";
                else
                    auth.status = "Failed";
            }
            else if (type.ToLower().Trim() == "otp_validate")
            {
                DataTable dt = objbll.DriverSmartTrack_OTPValidate(mobileno, otp);
                if (dt.Rows.Count > 0)
                {
                    auth.status = "Success";
                    auth.result = "Login Success";
                    auth.userid = dt.Rows[0]["userid"].ToString();
                    auth.mobileno = dt.Rows[0]["MobileNo"].ToString();
                    auth.sessionid = dt.Rows[0]["sessionid"].ToString();
                    auth.accountid = dt.Rows[0]["AccountId"].ToString();
                    auth.username = dt.Rows[0]["Name"].ToString();
                    auth.usertype = "Driver";
                }
                else
                {
                    auth.result = "Invalid OTP";
                    auth.status = "Failed";
                }
            }
            else
            {
                auth.result = "Invalid Request Type";
                auth.status = "Failed";
            }
            return auth;
        }
        public Driver_Trip_Details DriverSmartTrack_GetTripInvoices(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverSmartTrack_GetTripDetails(sessionid);
            Driver_Trip_Details lsDriver_Trip_Details = new Driver_Trip_Details();
            if (dtuser.Rows.Count > 0)
            {
                lsDriver_Trip_Details.TripId = dtuser.Rows[0]["TripId"].ToString();
                lsDriver_Trip_Details.TaskType = dtuser.Rows[0]["Task"].ToString();
                lsDriver_Trip_Details.ScheduleTime = Convert.ToDateTime(dtuser.Rows[0]["ScheduleTimeFrom"].ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                lsDriver_Trip_Details.VehicleId = dtuser.Rows[0]["VehicleID"].ToString();
                if (dtuser.Rows[0]["CheckListStatus"].ToString() == "Approved")
                {
                    lsDriver_Trip_Details.AdminRemarks = dtuser.Rows[0]["CheckListAdminRemarks"].ToString();
                }
                else
                {
                    lsDriver_Trip_Details.AdminRemarks = dtuser.Rows[0]["AdminRemarks"].ToString();
                }
                lsDriver_Trip_Details.Destination = dtuser.Rows[0]["Destination"].ToString();
                lsDriver_Trip_Details.TripStart = dtuser.Rows[0]["tripstart"].ToString();
                lsDriver_Trip_Details.LocationName = dtuser.Rows[0]["LocationName"].ToString();
                lsDriver_Trip_Details.ContactPerson = dtuser.Rows[0]["ContactPerson"].ToString();
                lsDriver_Trip_Details.MobileNo = dtuser.Rows[0]["MobileNo"].ToString();

                //Get CheckList Submitted or not
                DataTable dtTripCheckListCount = new DataTable();
                dtTripCheckListCount = objbll.GetCheckListDetailsCountByRequestId(Convert.ToInt32(dtuser.Rows[0]["TripId"].ToString())).Tables[0];
                if (dtTripCheckListCount.Rows.Count == 0)
                {
                    lsDriver_Trip_Details.ChecklistStatus = "0";
                }
                else if (dtTripCheckListCount.Rows.Count == 1)
                {
                    lsDriver_Trip_Details.ChecklistStatus = "1";
                }
                else if (dtTripCheckListCount.Rows.Count > 1)
                {
                    lsDriver_Trip_Details.ChecklistStatus = "2";
                }
            }
            return lsDriver_Trip_Details;
        }
        public Authenticate DriverSmartTrack_UpdateTripStatus(string sessionid, string tripid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverSmartTrack_GetUserDetails(sessionid);
            if (dtuser.Rows.Count > 0)
            {
                int r = objbll.DriverSmartTrack_UpdateStartTrip(tripid);
                if (r > 0)
                {
                    auth.result = "Updated Sucessfully";
                    auth.status = "Success";
                }
                else
                {
                    auth.result = "Failed to update";
                    auth.status = "Failed";
                }
            }
            else
            {
                auth.result = "Authentication Failed";
                auth.status = "Failed";
            }
            return auth;
        }
        public Authenticate DriverSmartTrack_EndTrip(string sessionid, string tripid, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            DataTable dtuser = objbll.DriverSmartTrack_GetUserDetails(sessionid);
            DataTable dtTripDetails = new DataTable();
            string VehicleId = "";
            if (dtuser != null && dtuser.Rows.Count > 0)
            {
                if (type.ToLower().Trim() == "otp_request")
                {
                    //Get Vehicle by TripNum
                    dtTripDetails = objbll.GetAllTripDetailsByTripID(Convert.ToInt32(tripid)).Tables[0];
                    if (dtTripDetails.Rows.Count > 0)
                    {
                        VehicleId = dtTripDetails.Rows[0]["VehicleId"].ToString();
                    }

                    Random rand = new Random();
                    otp = rand.Next(1000, 9999).ToString();
                    DataTable dtRequestDetails = objbll.GetBialVehicleRequestDetailsById(Convert.ToInt32(tripid));
                    DataTable dtMobileNo = objbll.GetMobileNoConfig(Convert.ToInt16(dtuser.Rows[0]["AccountId"].ToString()), "DriverApp");
                    if (dtMobileNo.Rows.Count > 0)
                    {
                        // Added By Tusar on 28/07/2021
                        //Sned OTP to Contact Person Mobile number
                        string MobileNumber = dtMobileNo.Rows[0]["MobileNos"].ToString();
                        if (dtRequestDetails.Rows.Count > 0)
                        {
                            MobileNumber = dtRequestDetails.Rows[0]["MobileNo"].ToString();
                        }
                        string msg = "OTP to verify trip satisfaction for the vehicle " + VehicleId.Replace(" ", "") + " is " + otp;
                        string globalid = dtMobileNo.Rows[0]["globalaccountid"].ToString();
                        string login = dtMobileNo.Rows[0]["SMS_Login"].ToString();
                        string MobileNos = MobileNumber;
                        int res = objbll.insertOTPvalidations(dtuser.Rows[0]["AccountId"].ToString(), tripid, "DriverApp", MobileNos, otp);
                        objbll.SendSMSByAdminService(globalid, login, MobileNos, msg, "Driver App OTP", "No");
                        auth.status = "Success";
                        auth.result = "OTP send Successfully";
                    }
                    else
                    {
                        auth.status = "Failed";
                        auth.result = "No store manager mobileno found";
                    }
                }
                else if (type.ToLower().Trim() == "otp_validate")
                {
                    if (otp == "0000")
                    {
                        int res = objbll.DriverSmartTrack_UpdateEndTrip(tripid, otp);
                        if (res > 0)
                        {
                            auth.status = "Success";
                            auth.result = "Updated Successfully";
                        }
                        else
                        {
                            auth.status = "Failed";
                            auth.result = "Failed to updated";
                        }
                    }
                    else
                    {
                        int res = objbll.ValidateOTP(dtuser.Rows[0]["AccountId"].ToString(), tripid, "DriverApp", otp);
                        if (res > 0)
                        {
                            res = objbll.DriverSmartTrack_UpdateEndTrip(tripid, otp);
                            if (res > 0)
                            {
                                auth.status = "Success";
                                auth.result = "Updated Successfully";
                            }
                            else
                            {
                                auth.status = "Failed";
                                auth.result = "Failed to updated";
                            }
                        }
                        else
                        {
                            auth.status = "Failed";
                            auth.result = "Invalid OTP";
                        }
                    }
                }
                else
                {
                    auth.status = "Failed";
                    auth.result = "Invalid Request";
                }
            }
            else
            {
                auth.status = "Failed";
                auth.result = "Invalid Sessionid";
            }
            return auth;
        }
        public Result DriverSmartTrack_NotificationNotified(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            Result result = new Result();
            objbll.DriverSmartTrackNotificationNotified(sessionid);
            result.result = "Success";
            result.statuscode = "200";
            return result;
        }
        public List<DriverSmartTrackNotification> GetDriverSmartTrack_Notifications(string sessionid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            List<DriverSmartTrackNotification> DriverTrack_Notifications = new List<DriverSmartTrackNotification>();
            DataTable dt = objbll.GetDriverSmartTrackNotifications(sessionid);
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DriverTrack_Notifications.Add(new DriverSmartTrackNotification
                {
                    subject = dt.Rows[i]["Subject"].ToString(),
                    info = dt.Rows[i]["Info"].ToString(),
                    datetime = dt.Rows[i]["Date"].ToString(),
                    isnotified = dt.Rows[i]["IsNotified"].ToString(),
                    notifiedtime = dt.Rows[i]["NotifiedTime"].ToString(),
                    priority = dt.Rows[i]["Priority"].ToString()
                });
            }
            return DriverTrack_Notifications;
        }
        public Result DriverSmartTrack_GetTripCheckListStatus(string sessionid, string tripid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            Result result = new Result();
            DataTable dtCheckListStatus = new DataTable();
            dtCheckListStatus = objbll.DriverSmartTrack_GetTripCheckListStatus(sessionid, tripid);
            if (dtCheckListStatus.Rows.Count > 0)
            {
                result.result = dtCheckListStatus.Rows[0]["CheckListStatus"].ToString();
                result.statuscode = "200";
            }
            return result;
        }
        public Authenticate GetVehicleidByQRCode(string qrcode)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.GetVehicleidByQRCode(qrcode);
            Authenticate auth = new Authenticate();
            auth.result = result;
            if (result == "Server Error" || result == "No vehicle" || result == "Invalid QRCode")
                auth.status = "Failed";
            else
                auth.status = "Success";
            return auth;
        }
        public List<GeofenceInoutData> GeofenceDetails(string key, string clientId, string vehicleid, string uid, string format, string fromdate, string todate)
        {
            string access = AuthenticateAPIKey(key, "1", format, clientId);
            if (string.IsNullOrEmpty(vehicleid) && string.IsNullOrEmpty(uid))
            {
                ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            if (string.IsNullOrEmpty(fromdate) || string.IsNullOrEmpty(todate))
            {
                ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            DateTime from = new DateTime();
            DateTime to = new DateTime();
            try
            {
                from = Convert.ToDateTime(fromdate);
                to = Convert.ToDateTime(todate);
            }
            catch
            {
                ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
            }
            if (from > to)
            {
                ErrorMessage customError = new ErrorMessage("412", "FAIL:TODATE_SHOULD_BE_GREATER_THAN_FROMDATE");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            string sessionId = access.Split(',')[1].ToString();
            string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
            if (!string.IsNullOrEmpty(uid))
            {
                if (uid.ToLower() == "all")
                {
                    vehicleid = usersvehicles;
                }
                else
                {
                    string[] uids = uid.Split(',');
                    for (int i = 0; i < uids.Length; i++)
                    {
                        try
                        {
                            vehicleid += dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + ",";
                        }
                        catch { }
                    }
                }
            }
            else if (vehicleid.ToLower() == "all")
            {
                vehicleid = usersvehicles;
            }
            try
            {
                //string strvehicleid = "'" + VehicleId.Replace(",", "','") + "'";
                List<GeofenceInoutData> res = new List<GeofenceInoutData>();
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string SessionId = access.Split(',')[1];
                string AccountId = access.Split(',')[3];
                if (!string.IsNullOrEmpty(vehicleid))
                {
                    DataTable dtgeo = dbx.GetLastGeofenceInOutDetails(vehicleid, fromdate, todate, SessionId, AccountId);
                    if (dtgeo.Rows.Count > 0)
                    {
                        foreach (DataRow item in dtgeo.Rows)
                        {
                            GeofenceInoutData p = new GeofenceInoutData();
                            string VehicleId = "N/A";
                            string intimestamp = "N/A";
                            string outtimestamp = "N/A";
                            string GeofenceName = "N/A";
                            vehicleid = item["vehicleid"].ToString();
                            intimestamp = item["intimestamp"].ToString();
                            outtimestamp = item["outtimestamp"].ToString();
                            GeofenceName = item["locationstr"].ToString();
                            p.vehicleId = vehicleid;
                            p.intimestamp = intimestamp;
                            p.outtimestamp = outtimestamp;
                            p.geofence = GeofenceName;
                            res.Add(p);
                        }
                    }
                    else
                    {
                        ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                        throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                    }
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return res;
            }
            //catch (Exception ex)
            //{
            //    ErrorMessage customError = new ErrorMessage("500", ex.ToString());
            //    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            //}
            finally
            {
                dbx.close();
            }
        }
        public List<ViolationsDetails> ViolationDetails(string key, string clientId, string vehicleid, string uid, string format, string fromdate, string todate)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            List<ViolationsDetails> res = new List<ViolationsDetails>();
            string access = AuthenticateAPIKey(key, "1", format, clientId);
            if (string.IsNullOrEmpty(vehicleid) && string.IsNullOrEmpty(uid))
            {
                ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            if (string.IsNullOrEmpty(fromdate) || string.IsNullOrEmpty(todate))
            {
                ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            DateTime from = new DateTime();
            DateTime to = new DateTime();
            try
            {
                from = Convert.ToDateTime(fromdate);
                to = Convert.ToDateTime(todate);
            }
            catch
            {
                ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
            }
            if (from > to)
            {
                ErrorMessage customError = new ErrorMessage("412", "FAIL:TODATE_SHOULD_BE_GREATER_THAN_FROMDATE");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
            }
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            string sessionId = access.Split(',')[1].ToString();
            string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
            if (!string.IsNullOrEmpty(uid))
            {
                if (uid.ToLower() == "all")
                {
                    vehicleid = usersvehicles;
                }
                else
                {
                    string[] uids = uid.Split(',');
                    for (int i = 0; i < uids.Length; i++)
                    {
                        try
                        {
                            vehicleid += dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + ",";
                        }
                        catch { }
                    }
                }
            }
            else if (vehicleid.ToLower() == "all")
            {
                vehicleid = usersvehicles;
            }
            try
            {
                string SessionId = access.Split(',')[1];
                string AccountId = access.Split(',')[3];
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                if (!string.IsNullOrEmpty(vehicleid))
                {
                    DataTable ViolationsDetails = dbx.GetViolationsDetails(vehicleid, fromdate, todate, AccountId);
                    if (ViolationsDetails.Rows.Count > 0)
                    {
                        foreach (DataRow item in ViolationsDetails.Rows)
                        {
                            ViolationsDetails p = new ViolationsDetails();
                            string VehicleId = "N/A";
                            string eventName = "N/A";
                            string timestamp = "N/A";
                            string duration = "N/A";
                            string location = "N/A";
                            vehicleid = item["vehicleid"].ToString();
                            eventName = item["type"].ToString();
                            timestamp = item["timestamp"].ToString();
                            duration = item["duration"].ToString();
                            location = item["location"].ToString();
                            p.vehicleId = vehicleid;
                            p.eventName = eventName;
                            p.timestamp = timestamp;
                            p.duration = duration;
                            p.location = location;
                            res.Add(p);
                        }
                    }
                    else
                    {
                        ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                        throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                    }
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                return res;
            }
            //catch (Exception ex)
            //{
            //    ErrorMessage customError = new ErrorMessage("500", ex.ToString());
            //    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            //}
            finally
            {
                dbx.close();
            }
        }
        //Added by suvarna on 22/10/2020 for outboundcall Modified on 2/11/2020 to store logs
        public String PlayOutBoundCall(string sessionid, insertCallDetails CallDetails)
        {
            string responseString = "";
            Dictionary<string, string> postValues = new Dictionary<string, string>();
            string msgencoded = HttpUtility.UrlEncode(CallDetails.calltext);
            string username = dbx.getUserId(sessionid);
            int NoofSMS = 0;
            DataTable dt = dbx.GetCallActivationDetails(username);
            string postResponse = null;
            String postString = "";
            if (dt.Rows.Count > 0)
            {
                if (string.IsNullOrEmpty(CallDetails.accounttype))
                {
                    try
                    {
                        int smsactId = Convert.ToInt32(dt.Rows[0]["Id"]);
                        string CallingUrl = dt.Rows[0]["CallUrl"].ToString();
                        string key = dt.Rows[0]["Key"].ToString();
                        string tokenid = dt.Rows[0]["TokenId"].ToString();
                        int balance = Convert.ToInt32(dt.Rows[0]["Balance"]);
                        string login = dt.Rows[0]["Login"].ToString();
                        int isActive = Convert.ToInt32(dt.Rows[0]["IsActive"]);
                        if (balance > 0 && isActive == 1)
                        {

                            postValues.Add("From", CallDetails.mobileno);
                            postValues.Add("Url", "https://my.exotel.com/sensel1/exoml/start_voice/323529");
                            postValues.Add("CallerID", "9513886363 ");
                            //postValues.Add("StatusCallback", "http://54.164.125.228:8080/SenselRestService.svc/rest/GetStatusOfVoiceCall");
                            //postValues.Add("StatusCallbackContentType", "application/json");



                            foreach (KeyValuePair<string, string> postValue in postValues)
                            {
                                postString += postValue.Key + "=" + WebUtility.UrlEncode(postValue.Value) + "&";
                            }
                            postString = postString.TrimEnd('&');
                            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();


                            //string ExotelURL = "https://9d132d7735c0b057523375fbcdbcaf00d22abd7282e1aad0:b74603d4f89f09e649c2b5e2d31b0c2cb12896c1619218cb@api.exotel.com/v1/Accounts/sensel1/Calls/connect";
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            string callURL = CallingUrl; //"https://api.exotel.in/v1/Accounts/sensel1/Calls/connect";
                            HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(callURL);
                            try
                            {
                                //objRequest.Credentials = new NetworkCredential("9d132d7735c0b057523375fbcdbcaf00d22abd7282e1aad0", "b74603d4f89f09e649c2b5e2d31b0c2cb12896c1619218cb");
                                objRequest.Credentials = new NetworkCredential(key, tokenid);
                                objRequest.Method = "POST";
                                objRequest.ContentLength = postString.Length;
                                objRequest.ContentType = "application/x-www-form-urlencoded";

                                StreamWriter opWriter = null;
                                opWriter = new StreamWriter(objRequest.GetRequestStream());
                                opWriter.Write(postString);
                                opWriter.Close();
                            }
                            catch (Exception ex)
                            {
                                return (ex.ToString());
                            }
                            HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
                            //return Request.CreateResponse(objResponse);
                            // return new HttpResponseMessage(HttpStatusCode.);

                            using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                            {
                                postResponse = responseStream.ReadToEnd();
                                responseStream.Close();
                            }
                            decimal msglen = CallDetails.calltext.Length / 160.0m;
                            NoofSMS += Convert.ToInt32(Math.Ceiling(msglen));

                            //dbs.InsertSMSLog(mob[j], Message, Convert.ToInt32(Math.Ceiling(msglen)).ToString(), SMSAbout, "Outgoing", APIID.ToString(), responseString, smsactId, GlobalAccId, UserName);

                            if (!responseString.Contains("failed") && !responseString.Contains("No Valid mobile numbers found"))
                            {
                                balance = balance - NoofSMS;
                                dbx.UpdateCallActivationBalance(smsactId, balance);

                                //return (postResponse);
                                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
                                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                                //BEL refernce https://stackoverflow.com/questions/14783406/deserializing-nested-xml-into-c-sharp-objects
                                //To convert an XML node contained in string xml into a JSON string 
                                XmlSerializer serializer = new XmlSerializer(typeof(TwilioResponse));
                                MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(postResponse));
                                TwilioResponse resultingMessage = (TwilioResponse)serializer.Deserialize(memStream);
                                insertCallDetails responseDetails = new insertCallDetails();
                                foreach (var item in resultingMessage.CallList)
                                {
                                    responseDetails.sid = item.Sid;
                                    responseDetails.starttime = item.StartTime;
                                    responseDetails.datecreated = item.DateCreated;
                                    responseDetails.status = item.Status;
                                    responseDetails.duration = item.Duration;
                                    responseDetails.endtime = item.EndTime;
                                    responseDetails.price = item.Price;

                                }
                                responseDetails.vehicleid = CallDetails.vehicleid;
                                responseDetails.mobileno = CallDetails.mobileno;
                                responseDetails.calltext = CallDetails.calltext;
                                responseDetails.viotype = CallDetails.viotype;
                                responseDetails.haulier = CallDetails.haulier;
                                responseDetails.branchid = CallDetails.branchid;
                                string result = ObjBll.insertCallDetails(sessionid, responseDetails);


                            }

                        }
                        dbx.STL_RecordErrorMessage(postResponse);
                    }
                    catch (Exception ex)
                    {
                        dbx.STL_RecordErrorMessage(ex.ToString() + "-postString = " + postString);
                    }
                }
                else if ((CallDetails.accounttype) == "1")
                {
                    try
                    {
                        int smsactId = Convert.ToInt32(dt.Rows[0]["Id"]);
                        string CallingUrl = dt.Rows[0]["CallUrl"].ToString();
                        string key = dt.Rows[0]["Key"].ToString();
                        string tokenid = dt.Rows[0]["TokenId"].ToString();
                        int balance = Convert.ToInt32(dt.Rows[0]["Balance"]);
                        string login = dt.Rows[0]["Login"].ToString();
                        int isActive = Convert.ToInt32(dt.Rows[0]["IsActive"]);
                        if (balance > 0 && isActive == 1)
                        {

                            postValues.Add("From", CallDetails.mobileno);
                            postValues.Add("Url", "https://my.exotel.com/sensel1/exoml/start_voice/323529");
                            postValues.Add("CallerID", "9513886363 ");
                            postValues.Add("StatusCallback", "https://fleetsmart3.ui.sensel.in/SenselRestService.svc/rest/v3/GetStatusOfVoiceCall");
                            postValues.Add("StatusCallbackContentType", "application/json");

                            //String postString = "";

                            foreach (KeyValuePair<string, string> postValue in postValues)
                            {
                                postString += postValue.Key + "=" + WebUtility.UrlEncode(postValue.Value) + "&";
                            }
                            postString = postString.TrimEnd('&');
                            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();


                            //string ExotelURL = "https://9d132d7735c0b057523375fbcdbcaf00d22abd7282e1aad0:b74603d4f89f09e649c2b5e2d31b0c2cb12896c1619218cb@api.exotel.com/v1/Accounts/sensel1/Calls/connect";
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            string callURL = CallingUrl; //"https://api.exotel.in/v1/Accounts/sensel1/Calls/connect";
                            HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(callURL);
                            try
                            {
                                //objRequest.Credentials = new NetworkCredential("9d132d7735c0b057523375fbcdbcaf00d22abd7282e1aad0", "b74603d4f89f09e649c2b5e2d31b0c2cb12896c1619218cb");
                                objRequest.Credentials = new NetworkCredential(key, tokenid);
                                objRequest.Method = "POST";
                                objRequest.ContentLength = postString.Length;
                                objRequest.ContentType = "application/x-www-form-urlencoded";

                                StreamWriter opWriter = null;
                                opWriter = new StreamWriter(objRequest.GetRequestStream());
                                opWriter.Write(postString);
                                opWriter.Close();
                            }
                            catch (Exception ex)
                            {
                                return (ex.ToString());
                            }
                            HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
                            //return Request.CreateResponse(objResponse);
                            // return new HttpResponseMessage(HttpStatusCode.);

                            using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                            {
                                postResponse = responseStream.ReadToEnd();
                                responseStream.Close();
                            }
                            decimal msglen = CallDetails.calltext.Length / 160.0m;
                            NoofSMS += Convert.ToInt32(Math.Ceiling(msglen));

                            //dbs.InsertSMSLog(mob[j], Message, Convert.ToInt32(Math.Ceiling(msglen)).ToString(), SMSAbout, "Outgoing", APIID.ToString(), responseString, smsactId, GlobalAccId, UserName);

                            if (!responseString.Contains("failed") && !responseString.Contains("No Valid mobile numbers found"))
                            {
                                balance = balance - NoofSMS;
                                dbx.UpdateCallActivationBalance(smsactId, balance);

                                //return (postResponse);
                                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
                                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                                //BEL refernce https://stackoverflow.com/questions/14783406/deserializing-nested-xml-into-c-sharp-objects
                                //To convert an XML node contained in string xml into a JSON string 
                                XmlSerializer serializer = new XmlSerializer(typeof(TwilioResponse));
                                MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(postResponse));
                                TwilioResponse resultingMessage = (TwilioResponse)serializer.Deserialize(memStream);
                                insertCallDetails responseDetails = new insertCallDetails();
                                foreach (var item in resultingMessage.CallList)
                                {
                                    responseDetails.sid = item.Sid;
                                    responseDetails.starttime = item.StartTime;
                                    responseDetails.datecreated = item.DateCreated;
                                    responseDetails.status = item.Status;
                                    responseDetails.duration = item.Duration;
                                    responseDetails.endtime = item.EndTime;
                                    responseDetails.price = item.Price;

                                }
                                responseDetails.vehicleid = CallDetails.vehicleid;
                                responseDetails.mobileno = CallDetails.mobileno;
                                responseDetails.calltext = CallDetails.calltext;
                                responseDetails.viotype = CallDetails.viotype;
                                responseDetails.haulier = CallDetails.haulier;
                                responseDetails.branchid = CallDetails.branchid;
                                string result = ObjBll.insertCallDetails(sessionid, responseDetails);


                            }

                        }
                        dbx.STL_RecordErrorMessage(postResponse);
                    }
                    catch (Exception ex)
                    {
                        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                        string result = objBll.insertCallDetails(sessionid, CallDetails);
                        //dbx.STL_RecordErrorMessage(ex.ToString());
                        dbx.STL_RecordErrorMessage(ex.ToString() + "-postString = " + postString);
                    }
                }

            }
            return postResponse;

        }
        public String GetStatusOfVoiceCall(string CallSid, string DateUpdated, string Status, string RecordingUrl)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable MobileNo = objBll.GetMobileNoBySID(CallSid);
            string result = "";
            if (!string.IsNullOrEmpty(MobileNo.Rows[0]["MobileNo"].ToString()))
            {
                result = objBll.updatestatusofvoicecall(CallSid, DateUpdated, Status, RecordingUrl, MobileNo.Rows[0]["MobileNo"].ToString());
                if (result == "1")
                {
                    string domain = "https://asg.vehicle-tracking.co.in/SenselRestService.svc/rest/v3/";
                    string postData = "{\"callsid\":\"" + CallSid + "\",\"status\":\"" + Status + "\",\"mobileno\":\"" + MobileNo.Rows[0]["MobileNo"].ToString() + "\",\"updateddate\":\"" + DateUpdated + "\"}";
                    string cleaned = postData.Replace("\n", "").Replace("\r", " ");
                    result = objBll.MakeHttpRequest(domain + "SyncVoiceCallDetails", "POST", "application/json", cleaned);
                }
            }
            return result;
        }
        public String SyncVoiceCallDetails(string callsid, string status, string mobileno, string updateddate)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable dtSID = objBll.GetMobileNoBySID(callsid);
            string result = "";
            if (!string.IsNullOrEmpty(dtSID.Rows[0]["Sid"].ToString()))
            {
                result = objBll.updatestatusofvoicecall(callsid, updateddate, status, "", mobileno);
            }
            return result;
        }
        public List<TheftAlertDetails> GetTheftAlertRequestDetails(string phoneno, string truckId)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dtTheftAlertRequestDetails = new DataTable();
            dtTheftAlertRequestDetails = ObjBll.GetTheftAlertRequestDetails(phoneno, truckId);
            List<TheftAlertDetails> voicecalldetailslst = new List<TheftAlertDetails>();
            for (int i = 0; i < dtTheftAlertRequestDetails.Rows.Count; i++)
            {
                DataRow row = dtTheftAlertRequestDetails.Rows[i];
                var voicecalldetails = new TheftAlertDetails();
                {
                    voicecalldetails.Sid = row["Sid"].ToString();
                    voicecalldetails.MobileNo = row["MobileNo"].ToString();
                    voicecalldetails.VehicleId = row["VehicleId"].ToString();
                    voicecalldetails.Haulier = row["Haulier"].ToString();
                    voicecalldetails.VioType = row["VioType"].ToString();
                    voicecalldetails.DateCreated = row["DateCreated"].ToString();
                    voicecalldetails.CallText = row["CallText"].ToString();
                    voicecalldetails.StartTime = row["StartTime"].ToString();
                    voicecalldetails.EndTime = row["EndTime"].ToString();
                    voicecalldetails.Duration = row["Duration"].ToString();
                    voicecalldetails.Price = row["Price"].ToString();
                    voicecalldetails.Status = row["Status"].ToString();
                    voicecalldetails.BranchId = row["BranchId"].ToString();
                    voicecalldetails.VoiceCallStatus = row["VoiceCallStatus"].ToString();

                }
                ;
                voicecalldetailslst.Add(voicecalldetails);
            }
            return voicecalldetailslst;
        }
        public String InsertTheftAlertRequestDetails(TheftAlertDetailslst TheftAlertDetailslist)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string result = "";
            result = objbll.InsertVoiceCallDetailsIntoAsg(TheftAlertDetailslist);
            return result;
        }

        //Added by suvarna on 22/10/2020 to get plain text response for outboundcall
        public System.IO.Stream triggerCall(string CallSid, string From, string To, string DialWhomNumber)
        {

            // string result = "EVENT: UNSCHEDULED STOP DATE: 09/10/20 01:26 PM TT NUMBER: GJ12BT5253 HAULIER: ARPL LOCATION: National Highway 947, Zankhar - 361280, Jamnagar, Gujarat DURATION: >26 min DRIVER: 3571-RAJARAM RAY RAMGYAN RAY";
            string result = dbx.getCallTextBySid(CallSid);
            byte[] resultBytes = Encoding.UTF8.GetBytes(result);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain";
            return new MemoryStream(resultBytes);
        }
        //Added by Madhuri for inserting Language Preference for Vehicle vetting App-21-12-2020
        public Result insertLanguagePreference(string sessionid, string LanguagePreferred)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertLanguageSetting(sessionid, LanguagePreferred);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Language details submitted successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }
        public Result getLanguagePreference(string sessionid)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.getLanguageSetting(sessionid);
            if (result != null)
            {
                r.result = result;
            }
            return r;
        }
        //Added by Madhuri For sensel fleet smart app for SGL(Safety Green light)-15-01-2020
        public List<Vet_LoginConfig> GetSGLAppName(string imei)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dt = ObjBll.GetSGLAppName(imei);
            List<Vet_LoginConfig> Sgl_LoginConfigs = new List<Vet_LoginConfig>();
            if (dt != null && dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    Vet_LoginConfig vlc = new Vet_LoginConfig()
                    {
                        appId = dt.Rows[i]["AppId"].ToString(),
                        name = dt.Rows[i]["AppName"].ToString()
                    };
                    Sgl_LoginConfigs.Add(vlc);
                }
            }
            return Sgl_LoginConfigs;
        }
        public List<sglChecklist> GetSglCheckList(string appId, string imei, string chklistname)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            List<sglChecklist> res = new List<sglChecklist>();
            Object lang = ObjBll.GetLanguageByIMEI(imei);
            chklistname = chklistname + "-" + lang.ToString().ToLower();
            DataTable sglchklist = ObjBll.GetSglChecklist(appId, imei, chklistname);
            if (sglchklist.Rows.Count == 0)
            {
                chklistname = chklistname + "-" + "English-English";
                sglchklist = ObjBll.GetSglChecklist(appId, imei, chklistname);
            }
            if (sglchklist.Rows.Count > 0)
            {
                foreach (DataRow item in sglchklist.Rows)
                {
                    sglChecklist p = new sglChecklist();
                    string RuleId = "N/A";
                    string RuleName = "N/A";
                    string Color = "N/A";
                    string LanguagePrefred = "N/A";
                    RuleId = item["ruleid"].ToString();
                    RuleName = item["rulename"].ToString();
                    Color = item["color"].ToString();
                    LanguagePrefred = item["DriverLanguagePreference"].ToString();
                    p.ruleid = RuleId;
                    p.rulename = RuleName;
                    p.color = Color;
                    p.LanguagePrefered = LanguagePrefred;
                    res.Add(p);
                }
            }
            return res;
        }
        public Result insertSglCheckList(string imei, string ChecklistName, List<sgl_ChecklistValues> checklistValues)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertSglCheckList(imei, ChecklistName, checklistValues);
            if (Convert.ToInt64(result) > 0)
            {
                r.result = "Checklist submitted Successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }
        public List<sglChecklist> GetBialCheckList(string sessionid, string tripid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            List<sglChecklist> res = new List<sglChecklist>();
            DataTable bialchklist = ObjBll.GetBialCheckList(sessionid, tripid);
            if (bialchklist.Rows.Count > 0)
            {
                foreach (DataRow item in bialchklist.Rows)
                {
                    sglChecklist p = new sglChecklist();
                    string RuleId = "N/A";
                    string RuleName = "N/A";
                    RuleId = item["ChecklistId"].ToString();
                    RuleName = item["Rule"].ToString();
                    p.ruleid = RuleId;
                    p.rulename = RuleName;
                    res.Add(p);
                }
            }
            return res;
        }
        public Result insertbialchecklistdetails(string tripid, List<bial_ChecklistValues> checklistValues)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.insertbialchecklistdetails(tripid, checklistValues);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = "Checklist submitted successfully.";
                r.statuscode = "200";
            }
            else
            {
                r.result = "Failed,Please try again";
                r.statuscode = "500";
            }
            return r;
        }

        public string PostMobileGpsData(string sessionid, string source, string senttime, string rowscount, string data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                //objbll.STL_RecordErrorMessage(data);
                try
                {
                    //string accountid = objbll.GetAccountId(sessionid).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<MobileGPSDetails> objMobileGPSDetailsList = jsonSerializer.Deserialize<List<MobileGPSDetails>>(data);
                    for (int i = 0; i < objMobileGPSDetailsList.Count; i++)
                    {
                        objbll.InsertMobileGPSDetailsIntoPositionData(sessionid, source, senttime.ToString(), rowscount, objMobileGPSDetailsList[i]);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                return "Upload Failed";
            }
            finally
            {
                dbx.close();
            }
        }
        public string PostMobileAppTrackerData(string sessionid, string source, string senttime, string rowscount, string data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                //objbll.STL_RecordErrorMessage(data);
                try
                {
                    //string accountid = objbll.GetAccountId(sessionid).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<MobileAppDetails> objMobileAppTrackerList = jsonSerializer.Deserialize<List<MobileAppDetails>>(data);
                    for (int i = 0; i < objMobileAppTrackerList.Count; i++)
                    {
                        objbll.InsertMobileAppTrackerDetails(sessionid, source, senttime, rowscount, objMobileAppTrackerList[i]);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                return "Upload Failed";
            }
            finally
            {
                dbx.close();
            }
        }

        public Result PostMobileGpsDetails(string sessionid, string source, string senttime, string rowscount, List<MobileGPSDetails> gpslistValues)
        {

            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();

            string result = ObjBll.InsertMobileGPSDetailsIntoPositionDetails(sessionid, source, senttime, rowscount, gpslistValues);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = result + " GPS Data Submitted Successfully.";
                r.statuscode = "200";
            }
            else
            {
                //r.result = "Failed,Please try again";
                //r.statuscode = "500";

                r.result = result + " GPS Data Submitted Successfully.";
                r.statuscode = "200";


                //Record The Data
                try
                {
                    var jsonData = JsonConvert.SerializeObject(gpslistValues);
                    string postData = "{\"sessionid\":\"" + sessionid + "\",\"source\":\"" + source + "\",\"senttime\":\"" + senttime + "\",\"rowscount\":\"" + rowscount + "\",\"gpslistValues\":" + jsonData + "}";
                    ObjBll.STL_RecordErrorMessage("Smart Track GPS Data Status Code : " + r.statuscode + " , Result : " + r.result + ", Rows Updated : " + result + ", Data : " + postData);
                }
                catch (Exception ex)
                {
                    ObjBll.STL_RecordErrorMessage("Error Smart Track GPS Data " + ex.ToString());
                }
            }
            dbx.close();
            return r;
        }
        //
        // This method will get the lat/lon details of Branch (ASG) mobile phone and store it in branchmobilespositiondata table
        // Source parameter - will have the branch alias.
        //
        public Outcome PostBranchMobileGpsDetails(string sessionid, string source, string senttime, string rowscount, List<BranchMobileGPSDetails> gpslistValues)
        {

            Outcome o = new Outcome();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();

            string result = ObjBll.InsertBranchMobileGPSDetails(sessionid, source, senttime, rowscount, gpslistValues);
            if (Convert.ToInt16(result) > 0)
            {
                o.status = "success";
                o.gpsupdate = "yes";
            }
            else
            {
                o.status = "fail";
                o.gpsupdate = "yes";
            }

            //Record The Data
            try
            {
                var jsonData = JsonConvert.SerializeObject(gpslistValues);
                string postData = "{\"sessionid\":\"" + sessionid + "\",\"source\":\"" + source + "\",\"senttime\":\"" + senttime + "\",\"rowscount\":\"" + rowscount + "\",\"gpslistValues\":" + jsonData + "}";
                ObjBll.STL_RecordErrorMessage("Branch GPS Data Status Code : " + o.status + ", Data : " + postData);
            }
            catch (Exception ex)
            {
                ObjBll.STL_RecordErrorMessage("Smart Track GPS Data " + ex.ToString());
            }
            dbx.close();
            return o;
        }
        /*
        public string PostBranchMobileGpsDetails(string sessionid, string source, string senttime, string rowscount, List<BranchMobileGPSDetails>gpsListValues)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                //objbll.STL_RecordErrorMessage(data);
                try
                {
                    //string accountid = objbll.GetAccountId(sessionid).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<BranchMobileGPSDetails> objMobileGPSDetailsList = jsonSerializer.Deserialize<List<BranchMobileGPSDetails>>(gpsListValues);
                    for (int i = 0; i < objMobileGPSDetailsList.Count; i++)
                    {
                        objbll.InsertBranchMobileGPSDetails(sessionid, source, senttime.ToString(), rowscount, objMobileGPSDetailsList[i]);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                return "Upload Failed";
            }
        }*/

        public Result PostMobileAppTrackerDetails(string sessionid, string source, string senttime, string rowscount, List<MobileAppDetails> applistValues)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string result = ObjBll.InsertMobileAppTrackerDetails(sessionid, source, senttime, rowscount, applistValues);
            if (Convert.ToInt16(result) > 0)
            {
                r.result = result + " Device Data Submitted Successfully.";
                r.statuscode = "200";
            }
            else
            {
                //r.result = "Failed,Please try again";
                r.result = result + " Device Data Submitted Successfully.";
                r.statuscode = "200";

                //Record The Data
                try
                {
                    var jsonData = JsonConvert.SerializeObject(applistValues);
                    string postData = "{\"sessionid\":\"" + sessionid + "\",\"source\":\"" + source + "\",\"senttime\":\"" + senttime + "\",\"rowscount\":\"" + rowscount + "\",\"gpslistValues\":" + jsonData + "}";
                    ObjBll.STL_RecordErrorMessage("Smart Track Device Data Status Code : " + r.statuscode + " , Result : " + r.result + ", Rows Updated : " + result + ", Data : " + postData);
                }
                catch (Exception ex)
                {
                    ObjBll.STL_RecordErrorMessage("Smart Track Device Data" + ex.ToString());
                }
            }
            dbx.close();
            return r;
        }
        public AuthenticateMobileTrackingApp MobileAppTracker_AuthenticateDriver(string mobileno, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            AuthenticateMobileTrackingApp auth = new AuthenticateMobileTrackingApp();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            if (type.ToLower().Trim() == "otp_request")
            {
                auth.result = objbll.MobileTrackingApp_OTPRequest(mobileno);
            }
            else if (type.ToLower().Trim() == "otp_validate")
            {
                DataTable dt = objbll.MobileTrackingApp_OTPValidate(mobileno, otp);
                if (dt.Rows.Count > 0)
                {
                    auth.result = "Login Success";
                    auth.sessionid = dt.Rows[0]["sessionid"].ToString();
                    auth.domain1 = dt.Rows[0]["Domain1"].ToString();
                    auth.domain2 = dt.Rows[0]["Domain2"].ToString();
                    auth.gpsupdaterate = dt.Rows[0]["GPSAcqFrequency"].ToString();
                    auth.dataupdaterate = dt.Rows[0]["DataUpdateFrequency"].ToString();
                    auth.is24hourtracking = dt.Rows[0]["Is24HrTracking"].ToString();
                    auth.noofhours = dt.Rows[0]["NoOfHoursToTrack"].ToString();
                    auth.vehicle = dt.Rows[0]["VehicleNo"].ToString();
                    auth.tripid = dt.Rows[0]["TrackID"].ToString();
                    auth.username = dt.Rows[0]["DriverName"].ToString();
                    auth.usertype = "Driver";
                    auth.mobileno = dt.Rows[0]["MobileNo"].ToString();
                    auth.accountid = dt.Rows[0]["AccountID"].ToString();

                }
                else
                {
                    auth.result = "Invalid OTP";
                }
            }
            else
            {
                auth.result = "Invalid Request Type";
            }
            dbx.close();
            return auth;
        }

        //For mySensel.com
        public string PostMobileTrackerConfigData(MobileAppTrackerConfig apptrackerconfigdetails)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                //JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                // List<Metro_Invoices> objMetroInvoiceList = jsonSerializer.Deserialize<List<Metro_Invoices>>(data);
                //for (int i = 0; i < objMetroInvoiceList.Count; i++)
                //{
                //    objbll.Update_Metro_Invoices(objMetroInvoiceList[i], accountid);
                //}
                objbll.InsertMobileAppTrackerConfigDetails(apptrackerconfigdetails);
                return "Upload Successfull";
            }
            catch (System.ArgumentException ex)
            {
                return "Upload Failed";
            }
            catch (Exception ex)
            {
                return "Upload Failed";
            }
            finally
            {
                dbx.close();
            }
        }
        // Added by suvarna on 25/5/2021 for smart driver app to get dutytime details
        public Result PostWorkStartData(string IMEI, string startTime)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string vehicle = dbx.GetVehilceByIMEI(IMEI);
            string result = string.Empty;
            //BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            if (vehicle != "No Data")
            {
                string drivers = dbx.getdrivername(vehicle, startTime);
                string driverName = drivers.Split(',')[0].ToString();
                int driverId = Convert.ToInt32(drivers.Split(',')[1].ToString());
                result = dbx.insertWorkStartLog(vehicle, driverId, driverName, startTime);
                r.result = result;
            }
            else
                r.result = "Vehicle not assigned";//Modified by suvarna on 6/8/2021 to send popup message
            return r;
        }
        public Result PostWorkEndData(string IMEI, string endTime)
        {

            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                string vehicle = dbx.GetVehilceByIMEI(IMEI);
                string result = string.Empty;
                //BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                if (vehicle != "No Data")
                {
                    string drivers = dbx.getdrivername(vehicle, endTime);
                    string driverName = drivers.Split(',')[0].ToString();
                    int driverId = Convert.ToInt32(drivers.Split(',')[1].ToString());
                    DataTable worktable = dbx.GetWorkDoneData(vehicle);
                    string workStarted = worktable.Rows[0][4].ToString();
                    string workEnded = worktable.Rows[0][6].ToString();

                    if (!string.IsNullOrEmpty(workStarted) && string.IsNullOrEmpty(workEnded))
                    {
                        result = dbx.updateWorkEndLog(vehicle, workStarted, endTime);
                        dbx.updateVehicleSnapShot_dutyTime(vehicle, 0);
                        r.result = result;
                    }
                }
                else
                    r.result = "Vehicle not assigned";//Modified by suvarna on 6/8/2021 to send popup message
                return r;
            }
            catch (Exception ex) { return r; }
            finally
            {
                dbx.close();
            }
        }

        public DutyLimit GetWorkLimit(string IMEI)
        {
            //Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            DutyLimit drivers = new DutyLimit();
            string vehicle = dbx.GetVehilceByIMEI(IMEI);
            if (vehicle != "No Data")
            {

                int groupid = dbx.getGroupIdByVehicleid(vehicle);
                string sessionid = dbx.getSessionIdByGroupid(groupid);
                Object[] keyParams = dbx.getVehicleTripParams(sessionid);

                //string dutyTime = (((Object[])keyParams[0])[29]).ToString();
                String dutyTime = (((Object[])keyParams[0])[29]).ToString();
                int dt_threshold = 0;
                int dt_record = 0;
                int dt_alert = 0;
                int dt_alarm = 0;
                if (!string.IsNullOrEmpty(dutyTime))
                {
                    if (dutyTime.Contains(","))
                    {
                        dt_threshold = Convert.ToInt32(dutyTime.Split(',')[0]);
                        dt_record = Convert.ToInt32(dutyTime.Split(',')[1]);
                        dt_alert = Convert.ToInt32(dutyTime.Split(',')[2]);
                        dt_alarm = Convert.ToInt32(dutyTime.Split(',')[3]);
                    }
                    else
                    {
                        dt_threshold = Convert.ToInt32(dutyTime);
                    }
                }
                string languagePrefered = dbx.GetLanguageByIMEI(IMEI).ToString();


                if (!string.IsNullOrEmpty(languagePrefered) || !string.IsNullOrEmpty(dt_threshold.ToString()))
                {
                    //DataTable table = new DataTable();
                    //table.Columns.Add("DutyLimit");
                    //table.Columns.Add("PrefLangauge");
                    //DataRow dr = table.NewRow();
                    //dr[0] = dt_threshold;
                    //dr[1] = languagePrefered;
                    //table.Rows.Add(dr);

                    //if (table.Rows.Count > 0)
                    //{
                    //    for (int i = 0; i < table.Rows.Count; i++)
                    //    {
                    //        //var driver = new DutyLimit()
                    //        //{
                    //        //    dutylimit = Convert.ToInt32(table.Rows[i][0].ToString()),
                    //        //    preferedlanguage = table.Rows[i][1].ToString()
                    //        //};
                    //        drivers.dutylimit = Convert.ToInt32(table.Rows[i][0].ToString());
                    //        drivers.preferedlanguage = table.Rows[i][1].ToString();

                    //    }
                    //}
                    drivers.dutylimit = dt_threshold;
                    drivers.preferedlanguage = languagePrefered;
                }
            }
            dbx.close();
            return drivers;

        }
        public Result GetWorkStartTime(string IMEI)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string result = string.Empty;
            try
            {
                string WorkStartTime = dbx.GetWorkStartTime(IMEI);
                if (WorkStartTime == "Work Started")
                {
                    r.result = "200";
                }
            }
            catch (Exception ex)
            {
                r.result = "Work Time not started";
            }
            return r;
        }

        // Ended by suvarna on 25/5/2021 for smart driver app to get dutytime details

        // This API will be called after USER requests BT to be enabled for a device
        public Result PostBTMACDetails(string sessionid, string macid, string imei, string login, string unitId)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            String command = "(SET BLUETOOTH 0,SET BLUTHID " + macid + ",SET BLUIMDPAIR 0)";
            string result = ObjBll.InsertBluetoothMACDetails(sessionid, macid, imei, login);
            // Send serverrequest to the ASSET_SMART device
            ObjBll.addServerRequest(unitId, 5, command);
            r.result = result;
            return r;
        }

        // This API will be called after ASSET_SMART Ap where the user will indicate receipt of A device
        public Result DevReceiptAck(string login, string session, string assetname)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();

                int result = ObjBll.DevReceiptAck(login, session, assetname);

                r.result = (result > 0 ? "success" : "failed");
                return r;
            }
            catch
            {
                r.result = "failed";
                return (r);
            }
            finally
            {
                dbx.close();
            }
        }


        public Authenticate AssetSmartApp_Authenticate(string mobileno, string type, string otp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            string login = "";
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string accountid = "";
            try
            {
                if (type.ToLower().Trim() == "otp_request")
                {
                    auth.result = objbll.AssetSmartApp_OTPRequest(mobileno);
                }
                else if (type.ToLower().Trim() == "otp_validate")
                {
                    DataTable dt = objbll.AssetSmartApp_OTPValidate(mobileno, otp);
                    if (dt.Rows.Count > 0)
                    {
                        auth.result = "Login Success";
                        auth.userid = dt.Rows[0]["empid"].ToString();
                        auth.mobileno = mobileno = dt.Rows[0]["MobileNo"].ToString();
                        auth.sessionid = dt.Rows[0]["sessionid"].ToString();
                        auth.accountid = accountid = dt.Rows[0]["AccountId"].ToString();
                        auth.username = dt.Rows[0]["EmpName"].ToString();
                        auth.usertype = dt.Rows[0]["Type"].ToString();
                        auth.login = login = dt.Rows[0]["login"].ToString();
                        auth.vehsessionid = auth.sessionid;// objbll.getSessionIdFromLogin(login);
                        objbll.AssetSmartApp_UpdateLogin(mobileno, accountid, login);
                    }
                    else
                    {
                        auth.result = "Invalid OTP";
                    }
                }
                else
                {
                    auth.result = "Invalid Request Type";
                }
                return auth;
            }
            catch
            {
                auth.result = "Invalid Request Type";
                return auth;
            }
            finally
            {
                dbx.close();
            }
        }
        public Authenticate PassengerProApp_Authenticate(string mobileno, string type)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            Authenticate auth = new Authenticate();
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                if (type.ToLower().Trim() == "otp_request")
                {
                    auth.result = objbll.PassengerProApp_Authenticate(mobileno);
                }                
                else
                {
                    auth.result = "Invalid Request Type";
                }
                return auth;
            }
            catch
            {
                auth.result = "Invalid Request Type";
                return auth;
            }
            finally
            {
                dbx.close();
            }
        }

        // Added by suvarna on 13/7/2021 for asset smart to add test mode status
        public Result PostTestModeStatus(string IMEI, string login, string asset, string testtype)
        {
            Result r = new Result();
            try
            {
                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
                if (!string.IsNullOrEmpty(asset))
                {
                    dbx.insertPanicData(asset, DateTime.Now, testtype);
                    r.result = "Success";
                    //if (testtype == "TEST ON") // Per new logic this is not required any more. Refer PArser for state flow
                    //dbx.addServerRequest(asset, 5, "(SET FASTTOSLOW)");//Modified by suvarna on 6/8/2021 to send serverrequest for FASTTOSLOW
                }
                else
                    r.result = "Failed";
                return r;
            }
            catch (Exception ex)
            {
                dbx.close();
                return r;
            }
            finally
            {
                dbx.close();
            }
        }
        //Castrol Pull APi
        //Added by suvarna on 30/7/2021 to pull lastpositiondata along with HA and HB events for last 3 minutes
        public List<LastUpdtData> getVehicleDetails(string Token, string ClientId = "")
        {
            string access = AuthenticateAPIKey(Token, "12", "Json", ClientId);
            string veh = "";
            List<LastUpdtData> res = new List<LastUpdtData>();
            try
            {

                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string username = access.Split(',')[0];
                string SessionId = access.Split(',')[1];
                Object[] selectedValues = dbx.getLastPositions(SessionId, null);
                DataTable accconfig = objbll.GetAccountGroupConfig(SessionId);
                DateTime totime = DateTime.Now;
                DateTime fromtime = totime.AddMinutes(-3);

                for (int i = 0; i < selectedValues.Length; i++)
                {
                    veh = (((Object[])selectedValues[i])[(int)headersIndex.trucksIndex]).ToString();

                    LastUpdtData p = new LastUpdtData();
                    #region General Data
                    string latStr = (((Object[])selectedValues[i])[(int)headersIndex.latitudeIndex]).ToString();
                    string longStr = (((Object[])selectedValues[i])[(int)headersIndex.longitudeIndex]).ToString();
                    double speed = Convert.ToDouble(((Object[])selectedValues[i])[(int)headersIndex.speedIndex]);
                    DateTime timestamp = Convert.ToDateTime(((Object[])selectedValues[i])[(int)headersIndex.timesIndex]);

                    DataTable dtViol = objbll.GetViolationsByVehicleIDs("'" + veh + "'", fromtime.ToString("yyyy'-'MM'-'dd HH:mm:ss"), totime.ToString("yyyy'-'MM'-'dd HH:mm:ss"), "Harsh Braking");
                    DataTable dtViol1 = objbll.GetViolationsByVehicleIDs("'" + veh + "'", fromtime.ToString("yyyy'-'MM'-'dd HH:mm:ss"), totime.ToString("yyyy'-'MM'-'dd HH:mm:ss"), "High Acceleration");

                    string positionsTxt = string.Empty;
                    object[] PositiontxtData = dbx.getLatestPositiontxt(SessionId);
                    if (PositiontxtData != null)
                    {
                        int len = PositiontxtData.Length;
                        for (int k = 0; k < len; k++)
                        {
                            if ((((Object[])PositiontxtData[k])[0]).ToString() == veh)
                            {
                                try
                                {
                                    if (Convert.ToDateTime(timestamp) < Convert.ToDateTime((((Object[])PositiontxtData[k])[2]).ToString()).AddMinutes(5))
                                        positionsTxt = (((Object[])PositiontxtData[k])[1]).ToString();
                                    else
                                        positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                                }
                                catch { }
                                break;
                            }
                        }
                        if (positionsTxt == "")
                        {
                            positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                        }
                    }
                    else
                    {
                        positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                    }
                    #endregion
                    int uid = dbx.getUidFromConfigdata(veh);
                    string imei = dbx.getIMEIByUid(uid.ToString());
                    timestamp = timestamp.ToUniversalTime();
                    #region Form data
                    string[] dateandtime = timestamp.ToString("yyyy'-'MM'-'dd HH:mm:ss").Split(' ');
                    string date = dateandtime[0].ToString();
                    string time = dateandtime[1].ToString();
                    p.vehicleNo = veh.Replace(" ", "");
                    p.deviceNo = imei;
                    p.dateTime = date + "T" + time;
                    p.speed = speed.ToString();
                    p.latitude = latStr;
                    p.longitude = longStr;
                    p.currentLocation = positionsTxt;
                    List<HBEvent> hb = new List<HBEvent>();
                    for (int j = 0; j < dtViol.Rows.Count; j++)
                    {
                        HBEvent v = new HBEvent();
                        DateTime hbtime = Convert.ToDateTime(dateformat(dtViol.Rows[j]["timestamp"].ToString()));
                        hbtime = hbtime.ToUniversalTime();
                        dateandtime = hbtime.ToString("yyyy'-'MM'-'dd HH:mm:ss").Split(' ');
                        date = dateandtime[0].ToString();
                        time = dateandtime[1].ToString();
                        v.dateTime = date + "T" + time;
                        v.magnitude = "";
                        hb.Add(v);
                    }
                    p.harsh_braking = hb;
                    List<HAEvent> ha = new List<HAEvent>();
                    for (int k = 0; k < dtViol1.Rows.Count; k++)
                    {
                        HAEvent v1 = new HAEvent();
                        DateTime hatime = Convert.ToDateTime(dateformat(dtViol1.Rows[k]["timestamp"].ToString()));
                        hatime = hatime.ToUniversalTime();
                        dateandtime = hatime.ToString("yyyy'-'MM'-'dd HH:mm:ss").Split(' ');
                        date = dateandtime[0].ToString();
                        time = dateandtime[1].ToString();
                        v1.dateTime = date + "T" + time;
                        v1.magnitude = "";
                        ha.Add(v1);
                    }
                    p.rapid_acceleration = ha;
                    #endregion
                    res.Add(p);
                }
                return res;
            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", ex.ToString() + veh);
                dbx.close();
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }

        public Outcome getBranchAlias(string login, string imei)
        {
            Outcome o = new Outcome();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();

            DataTable branchData = ObjBll.getBranchDetailsFromIMEI(imei);
            try
            {
                if (branchData != null)
                    if (branchData.Rows.Count > 0)
                    {
                        o.status = branchData.Rows[0]["BranchName"].ToString();
                        o.gpsupdate = branchData.Rows[0]["getCoord"].ToString();
                    }
                return o;
            }
            catch
            {
                o.status = "fail";
                o.gpsupdate = "no";
                return o;
            }
            finally
            {
                dbx.close();
            }
        }
        // Added By Tusar on 30/08/2021
        // This API will be called by ASSET_SMART App to update IMEI number by MobileNo
        public Result AssetSmartApp_UpdateIMEIByMobileNo(string mobilenumber, string imei)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                ObjBll.AssetSmartApp_UpdateIMEIByMobileNo(mobilenumber, imei);
                r.result = "success";
                return r;
            }
            catch
            {
                r.result = "failed";
                return r;
            }
            finally
            {
                ObjBll.close();
            }
        }
        // Added by suvarna on 07/09/2021 to store taging details by QR code for ADANI
        public Result PostTagingData(string Vehicleid, string IMEI, string latlong)
        {
            string taginTime = dbx.formatDBDate(DateTime.Now);
            Result r = new Result();
            string res = string.Empty;
            string workStarted = string.Empty;
            string workEnded = string.Empty;
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string Assignvehicle = dbx.GetVehilceByIMEI(IMEI);
            try
            {
                DataTable worktable = new DataTable();
                DataTable worktable1 = new DataTable();
                if (Vehicleid.Contains("http://www.sensel.in/?VEH_ADANI_"))
                {
                    Vehicleid = dbx.GetVehicleidByQRCode(Vehicleid);
                    if (Vehicleid != "Server Error" && Vehicleid != "No vehicle" && Vehicleid != "Invalid QRCode")
                    {
                        if ((Vehicleid.ToLower().Replace(" ", "")) == (Assignvehicle.ToLower().Replace(" ", "")))
                        {
                            r.statuscode = "Assigned Vehicle";
                            string driverid = dbx.GetDriverIdByIMEI(IMEI);
                            string driverName = dbx.GetDriverNameByIMEI(IMEI);//Modified by suvarna on 9/9/2021
                            res = dbx.InsertDriverTaging(driverid, driverName, IMEI, latlong, Vehicleid, 1, Assignvehicle, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                            worktable = dbx.GetWorkDoneDataByDriver(driverid);//Modified by suvarna on 25/10/2021 to implement dutytime for ADANI
                            if (worktable.Rows.Count > 0)
                            {
                                workStarted = worktable.Rows[0][4].ToString();
                                workEnded = worktable.Rows[0][6].ToString();
                                if (!string.IsNullOrEmpty(workStarted) && string.IsNullOrEmpty(workEnded))
                                {
                                    dbx.updateWorkEndLogByDriver(driverid, workStarted, taginTime);
                                }
                            }
                            worktable1 = dbx.GetWorkDoneData(Vehicleid);//Modified by suvarna on 09/11/2021 to end dutytime by vehicleid for ADANI
                            if (worktable1.Rows.Count > 0)
                            {
                                workStarted = worktable1.Rows[0][4].ToString();
                                workEnded = worktable1.Rows[0][6].ToString();
                                if (!string.IsNullOrEmpty(workStarted) && string.IsNullOrEmpty(workEnded))
                                {
                                    dbx.updateWorkEndLog(Vehicleid, workStarted, taginTime);
                                }
                            }
                            dbx.insertWorkStartLog(Vehicleid, Convert.ToInt32(driverid), driverName, taginTime);
                            dbx.writeRFIDData(latlong.Split(',')[0].ToString(), latlong.Split(',')[1].ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm"), DateTime.Now.ToString("yyyy-MM-dd HH:mm"), IMEI, Vehicleid);
                        }
                        else
                        {
                            r.statuscode = "Vehicle not assigned";
                            string driverid = dbx.GetDriverIdByIMEI(IMEI);
                            string driverName = dbx.GetDriverNameByIMEI(IMEI);
                            res = dbx.InsertDriverTaging(driverid, driverName, IMEI, latlong, Vehicleid, 1, Assignvehicle, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                            worktable = dbx.GetWorkDoneDataByDriver(driverid);
                            if (worktable.Rows.Count > 0)
                            {
                                workStarted = worktable.Rows[0][4].ToString();
                                workEnded = worktable.Rows[0][6].ToString();
                                if (!string.IsNullOrEmpty(workStarted) && string.IsNullOrEmpty(workEnded))
                                {
                                    dbx.updateWorkEndLogByDriver(driverid, workStarted, taginTime);
                                }
                            }
                            worktable1 = dbx.GetWorkDoneData(Vehicleid);
                            if (worktable1.Rows.Count > 0)
                            {
                                workStarted = worktable1.Rows[0][4].ToString();
                                workEnded = worktable1.Rows[0][6].ToString();
                                if (!string.IsNullOrEmpty(workStarted) && string.IsNullOrEmpty(workEnded))
                                {
                                    dbx.updateWorkEndLog(Vehicleid, workStarted, taginTime);
                                }
                            }
                            dbx.insertWorkStartLog(Vehicleid, Convert.ToInt32(driverid), driverName, taginTime);
                            dbx.writeRFIDData(latlong.Split(',')[0].ToString(), latlong.Split(',')[1].ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm"), DateTime.Now.ToString("yyyy-MM-dd HH:mm"), IMEI, Vehicleid);
                        }
                        if (res.Length > 0)
                            r.result = "Success";
                    }
                    else
                    {
                        r.result = "Failed";
                        r.statuscode = "VehicleId not registered";
                    }
                }
                else
                {
                    r.result = "Failed";
                    r.statuscode = "VehicleId not registered";
                }
                return r;
            }
            catch (Exception ex)
            {
                dbx.close();
                return r;
            }
            finally
            {
                dbx.close();
            }
        }

        #region JourneyManagementPlan
        public JMPTrips GetJMPTripsDetails(string IMEI)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            JMPTrips jMPTrips = new JMPTrips();
            string vehicle = dbx.GetVehilceByIMEI(IMEI);

            DataTable dtDCTripDetails = dbx.GetDCTripDetailsByVehicleID(vehicle);
            if (vehicle != "No Data")
            {
                string driver = dbx.GetDriverNameByIMEI(IMEI);
                string languagePrefered = dbx.GetLanguageByIMEI(IMEI).ToString();
                if (dtDCTripDetails != null)
                {
                    if (dtDCTripDetails.Rows.Count > 0)
                    {
                        int groupid = dbx.getGroupIdByVehicleid(vehicle);
                        string sessionid = dbx.getSessionIdByGroupid(groupid);
                        Object[] keyParams = dbx.getVehicleTripParams(sessionid);

                        //For Continuous Driving
                        String contdrivingduration = (((Object[])keyParams[0])[10]).ToString();
                        int cd_threshold = 0;
                        int cd_record = 0;
                        int cd_alert = 0;
                        int cd_alarm = 0;
                        if (!string.IsNullOrEmpty(contdrivingduration))
                        {
                            if (contdrivingduration.Contains(","))
                            {
                                cd_threshold = Convert.ToInt32(contdrivingduration.Split(',')[0]);
                                cd_record = Convert.ToInt32(contdrivingduration.Split(',')[1]);
                                cd_alert = Convert.ToInt32(contdrivingduration.Split(',')[2]);
                                cd_alarm = Convert.ToInt32(contdrivingduration.Split(',')[3]);
                            }
                            else
                            {
                                cd_threshold = Convert.ToInt32(contdrivingduration);
                            }
                        }

                        //For Rest Time
                        String restduration = (((Object[])keyParams[0])[11]).ToString();
                        int resttime = 0;
                        if (!string.IsNullOrEmpty(restduration))
                        {
                            resttime = Convert.ToInt32(restduration);
                        }

                        //For Max Driving
                        String maxdrivingduration = (((Object[])keyParams[0])[2]).ToString();
                        int mx_threshold = 0;
                        int mx_record = 0;
                        int mx_alert = 0;
                        int mx_alarm = 0;
                        if (!string.IsNullOrEmpty(maxdrivingduration))
                        {
                            if (maxdrivingduration.Contains(","))
                            {
                                mx_threshold = Convert.ToInt32(maxdrivingduration.Split(',')[0]);
                                mx_record = Convert.ToInt32(maxdrivingduration.Split(',')[1]);
                                mx_alert = Convert.ToInt32(maxdrivingduration.Split(',')[2]);
                                mx_alarm = Convert.ToInt32(maxdrivingduration.Split(',')[3]);
                            }
                            else
                            {
                                mx_threshold = Convert.ToInt32(maxdrivingduration);
                            }
                        }

                        //Get Trip Type
                        string TripType = "";
                        DataTable dtJMPVehicleRouteSummary = dbx.GetJMPVehicleRouteSummaryByVehicle(vehicle);
                        if (dtJMPVehicleRouteSummary != null)
                        {
                            if (dtJMPVehicleRouteSummary.Rows.Count > 0)
                            {
                                TripType = dtJMPVehicleRouteSummary.Rows[0]["direction"].ToString();
                            }
                        }
                        //Get Trip Active Plan Details
                        jMPTrips.TripStart = "";
                        jMPTrips.IsStartTimeEditable = "Yes";
                        jMPTrips.IsTripEditable = "Yes";
                        jMPTrips.TripPlanStatus = "";

                        int countExistingTripPlans = dbx.CheckNoOfJMPPlansForTrip(dtDCTripDetails.Rows[0]["id"].ToString(), TripType.ToUpper());
                        if (countExistingTripPlans > 0)
                        {
                            DataTable dtTripActivePlanDetails = dbx.GetJMPTripActivePlanDetails(dtDCTripDetails.Rows[0]["id"].ToString(), TripType.ToUpper());
                            if (dtTripActivePlanDetails != null)
                            {
                                if (dtTripActivePlanDetails.Rows.Count > 0)
                                {
                                    jMPTrips.TripStart = dtTripActivePlanDetails.Rows[0]["StartTime"].ToString();
                                    jMPTrips.TripPlanStatus = dtTripActivePlanDetails.Rows[0]["Status"].ToString();
                                }
                            }
                        }
                        if (countExistingTripPlans >= 3)
                        {
                            jMPTrips.IsStartTimeEditable = "No";
                            jMPTrips.IsTripEditable = "No";
                        }



                        jMPTrips.TripId = dtDCTripDetails.Rows[0]["id"].ToString();
                        jMPTrips.Vehicle = vehicle;
                        jMPTrips.Driver = driver;
                        jMPTrips.Language = languagePrefered;
                        jMPTrips.ContinuousDrivingDuration = cd_threshold / 60;
                        jMPTrips.RestTimeDuration = (resttime / 60) - 10;
                        jMPTrips.MaxDrivingDuration = mx_threshold / 60;
                        jMPTrips.NightDrivingStart = (((Object[])keyParams[0])[3]).ToString();
                        jMPTrips.NightDrivingEnd = (((Object[])keyParams[0])[4]).ToString();
                        jMPTrips.StartLocation = dtDCTripDetails.Rows[0]["LoadingPlaceName"].ToString();
                        jMPTrips.EndtLocation = dtDCTripDetails.Rows[0]["Name"].ToString();
                        jMPTrips.TripType = TripType;
                        jMPTrips.RouteId = dtDCTripDetails.Rows[0]["routeid"].ToString();

                        if (TripType == "DOWN")
                        {
                            jMPTrips.StartLocation = dtDCTripDetails.Rows[0]["Name"].ToString();
                            jMPTrips.EndtLocation = dtDCTripDetails.Rows[0]["LoadingPlaceName"].ToString();
                        }
                    }
                    else
                    {
                        jMPTrips.TripId = "No";
                    }
                }
                else
                {
                    jMPTrips.TripId = "No";
                }
            }
            else
            {
                jMPTrips.TripId = "No";
            }
            dbx.close();
            return jMPTrips;
        }
        public JMPRoute GetJMPTripRouteDetails(string RouteId, string TripType, string tripid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            JMPRoute JMPRoute = new JMPRoute();

            DataTable dtRouteDetails = new DataTable();
            dtRouteDetails.Columns.Add("Order", typeof(int));
            dtRouteDetails.Columns[0].AutoIncrementSeed = 1;
            dtRouteDetails.Columns[0].AutoIncrement = true;
            dtRouteDetails.Columns.Add("Latitude", typeof(decimal));
            dtRouteDetails.Columns.Add("Longitude", typeof(decimal));
            dtRouteDetails.Columns.Add("Sequence", typeof(int));
            DataRow[] value = dbx.GetRouteSegmentsByRouteId(RouteId);
            string str = "";
            if (value.Length > 0)
            {
                str = value[0][0].ToString();
            }
            string[] segments = str.Split(',');

            for (int i = 0; i < segments.Length; i++)
            {
                DataTable dtSegmentRefRouteDetails = dbx.GetReferenceRouteDetailsBySegment(segments[i], TripType);
                dtRouteDetails.Merge(dtSegmentRefRouteDetails);
            }


            List<JMPRouteDetails> routeDetails = new List<JMPRouteDetails>();
            for (int k = 0; k < dtRouteDetails.Rows.Count; k++)
            {
                DataRow productRows = dtRouteDetails.Rows[k];
                var route = new JMPRouteDetails();
                {
                    route.latitude = productRows["Latitude"].ToString();
                    route.longitude = productRows["Longitude"].ToString();
                }
                ;
                routeDetails.Add(route);
            }
            JMPRoute.JMPRouteDetails = routeDetails;
            dbx.close();
            return JMPRoute;
        }
        public JMPParking GetJMPParkingDetails(string RouteId, string TripType, string tripid)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            JMPParking JMPParking = new JMPParking();

            DataTable dtRouteDetails = new DataTable();
            dtRouteDetails.Columns.Add("Order", typeof(int));
            dtRouteDetails.Columns[0].AutoIncrementSeed = 1;
            dtRouteDetails.Columns[0].AutoIncrement = true;
            dtRouteDetails.Columns.Add("Location");
            dtRouteDetails.Columns.Add("Latitude", typeof(decimal));
            dtRouteDetails.Columns.Add("Longitude", typeof(decimal));
            DataRow[] value = dbx.GetRouteSegmentsByRouteId(RouteId);
            string str = "";
            if (value.Length > 0)
            {
                str = value[0][0].ToString();
            }
            string[] segments = str.Split(',');

            for (int i = 0; i < segments.Length; i++)
            {
                DataTable dtSegmentRefRouteDetails = dbx.GetJMPParkingDetailsBySegment(segments[i].ToString().Replace(" ", "_") + "_UP"); // + TripType.ToUpper());
                dtRouteDetails.Merge(dtSegmentRefRouteDetails);
            }

            if (TripType.ToUpper() == "DOWN")
            {
                DataView dv = new DataView(dtRouteDetails);
                dv.Sort = "Order DESC";
                dtRouteDetails = dv.ToTable();
            }

            List<JMPParkingDetails> routeDetails = new List<JMPParkingDetails>();
            if (dtRouteDetails.Rows.Count > 0)
            {
                for (int k = 0; k < dtRouteDetails.Rows.Count; k++)
                {
                    DataRow productRows = dtRouteDetails.Rows[k];
                    var route = new JMPParkingDetails();
                    {
                        route.latitude = productRows["Latitude"].ToString();
                        route.longitude = productRows["Longitude"].ToString();
                        route.Location = productRows["Location"].ToString();
                    }
                    ;
                    routeDetails.Add(route);
                }
            }
            else
            {
                DataTable dtCustomerLocationDetails = dbx.GetJMPParkingDetailsByTripNo(tripid);
                if (dtCustomerLocationDetails.Rows.Count > 0)
                {
                    for (int k = 0; k < dtCustomerLocationDetails.Rows.Count; k++)
                    {
                        DataRow productRows = dtCustomerLocationDetails.Rows[k];
                        var route = new JMPParkingDetails();
                        {
                            route.latitude = productRows["Latitude"].ToString();
                            route.longitude = productRows["Longitude"].ToString();
                            route.Location = productRows["Location"].ToString();
                        }
                        ;
                        routeDetails.Add(route);
                    }
                }
            }

            JMPParking.JMPParkingDetails = routeDetails;
            dbx.close();
            return JMPParking;
        }
        public Result InsertJMPParkingLocationDetails(string tripid, string triptype, string tripstarttime, List<JMPParkingLocationDetails> addparkingspots)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            //Get the no of exisiting plans.
            int countExistingTripPlans = ObjBll.CheckNoOfJMPPlansForTrip(tripid, triptype);

            if (countExistingTripPlans <= 3)
            {
                //Inactivate the previous plans if exists.
                if (countExistingTripPlans > 0)
                    ObjBll.UpdateJMPTripPlanStatus(tripid, triptype);

                //Insert the plan details.
                string result = ObjBll.InsertJMPParkingLocationDetails(tripid, triptype, tripstarttime, addparkingspots);
                if (Convert.ToInt16(result) > 0)
                {
                    r.result = "Parking Details Submitted Successfully.";
                    r.statuscode = "200";
                }
                else
                {
                    r.result = "Failed,Please Try Again";
                    r.statuscode = "500";
                }
            }
            else
            {
                r.result = "Exceeded The Plan Limit";
                r.statuscode = "500";
            }
            return r;
        }
        public JMPTripActualDetails GetJMPTripActualDetails(string tripid, string triptype)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            JMPTripActualDetails TripActualDetails = new JMPTripActualDetails();
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            //Get no of plan done for the trip
            int countExistingTripPlans = objBll.CheckNoOfJMPPlansForTrip(tripid, triptype.ToUpper());
            string tripstarttime = "";
            string Planid = "0";
            if (countExistingTripPlans > 0)
            {
                DataTable dtTripActivePlanDetails = objBll.GetJMPTripActivePlanDetails(tripid, triptype.ToUpper());
                if (dtTripActivePlanDetails != null)
                {
                    if (dtTripActivePlanDetails.Rows.Count > 0)
                    {
                        tripstarttime = dtTripActivePlanDetails.Rows[0]["TripStartTime"].ToString();
                        Planid = dtTripActivePlanDetails.Rows[0]["PlanId"].ToString();
                        TripActualDetails.TripStartTime = dtTripActivePlanDetails.Rows[0]["StartTime"].ToString();
                    }
                }
                string vehicle = "";
                string lat = "";
                string lon = "";
                //Get Vehicle Number By TripId
                DataSet dt = objBll.getVehicleDCTrip(tripid);
                if (dt.Tables[0].Rows.Count > 0)
                {
                    vehicle = dt.Tables[0].Rows[0]["VehicleID"].ToString();
                }
                //Get Vehicle Current Location Details
                object values = objBll.getLastLatLong(vehicle.ToString());
                if (((object[])values).Length > 0)
                {
                    lat = ((object[])(((object[])(values))[0]))[0].ToString();
                    lon = ((object[])(((object[])(values))[0]))[1].ToString();
                }

                int groupid = dbx.getGroupIdByVehicleid(vehicle);
                string sessionid = dbx.getSessionIdByGroupid(groupid);
                Object[] keyParams = dbx.getVehicleTripParams(sessionid);

                //For Rest Time
                String restduration = (((Object[])keyParams[0])[11]).ToString();
                int resttime = 0;
                if (!string.IsNullOrEmpty(restduration))
                {
                    resttime = Convert.ToInt32(restduration);
                }
                //Convert to minutes
                resttime = (resttime / 60) - 10;

                DataTable dtRouteHaltDetails = new DataTable();
                dtRouteHaltDetails.Columns.Add("Order", typeof(int));
                dtRouteHaltDetails.Columns[0].AutoIncrementSeed = 1;
                dtRouteHaltDetails.Columns[0].AutoIncrement = true;
                dtRouteHaltDetails.Columns.Add("Vehicle", typeof(string));
                dtRouteHaltDetails.Columns.Add("Location", typeof(int));
                dtRouteHaltDetails.Columns.Add("Latitude", typeof(decimal));
                dtRouteHaltDetails.Columns.Add("Longitude", typeof(decimal));
                dtRouteHaltDetails.Columns.Add("Duration", typeof(int));
                dtRouteHaltDetails.Columns.Add("InTimeStamp", typeof(string));
                dtRouteHaltDetails.Columns.Add("OutTimeStamp", typeof(string));


                //Get Unscheduled Stop Details
                DataTable dtStoreVehiclesViolationHaltLocationDetails = objBll.GetVehicleHaltDetailsFromStoreVehicleViolations(vehicle, tripstarttime);
                DataTable dtVehicleInPlantHaltLocationDetails = objBll.GetVehicleHaltDetailsFromVehicleInPlant(vehicle, tripstarttime);
                if (dtStoreVehiclesViolationHaltLocationDetails != null)
                {
                    if (dtStoreVehiclesViolationHaltLocationDetails.Rows.Count > 0)
                    {
                        dtRouteHaltDetails.Merge(dtStoreVehiclesViolationHaltLocationDetails);
                    }
                }
                if (dtVehicleInPlantHaltLocationDetails != null)
                {
                    if (dtVehicleInPlantHaltLocationDetails.Rows.Count > 0)
                    {
                        dtRouteHaltDetails.Merge(dtVehicleInPlantHaltLocationDetails);
                    }
                }

                if (dtRouteHaltDetails != null)
                {
                    if (dtRouteHaltDetails.Rows.Count > 0)
                    {
                        List<JMPUnscheduledStopDetails> jmpunstopsDetails = new List<JMPUnscheduledStopDetails>();
                        for (int k = 0; k < dtRouteHaltDetails.Rows.Count; k++)
                        {
                            DataRow productRows = dtRouteHaltDetails.Rows[k];
                            var jmpunstops = new JMPUnscheduledStopDetails();
                            {
                                //jmpunstops.Latitude = productRows["Latitude"].ToString();
                                //jmpunstops.Longitude = productRows["Longitude"].ToString();
                                jmpunstops.Location = productRows["Location"].ToString();
                                jmpunstops.EntryATA = productRows["InTimeStamp"].ToString();
                                jmpunstops.ExitATA = productRows["OutTimeStamp"].ToString();
                                jmpunstops.ActRestDuration = productRows["Duration"].ToString();
                            }
                            ;
                            jmpunstopsDetails.Add(jmpunstops);
                        }
                        TripActualDetails.JMPUnscheduledStopDetails = jmpunstopsDetails;
                    }
                }


                //Get Scheduled Stop Details
                DataTable dtJMPTripActivePlanParkingDetails = objBll.GetJMPTripActivePlanParkingDetails(Planid);
                if (dtJMPTripActivePlanParkingDetails != null)
                {
                    if (dtJMPTripActivePlanParkingDetails.Rows.Count > 0)
                    {
                        List<JMPScheduledStopDetails> routeDetails = new List<JMPScheduledStopDetails>();
                        for (int k = 0; k < dtJMPTripActivePlanParkingDetails.Rows.Count; k++)
                        {
                            DataRow productRows = dtJMPTripActivePlanParkingDetails.Rows[k];
                            var route = new JMPScheduledStopDetails();
                            {
                                route.Latitude = productRows["Latitude"].ToString();
                                route.Longitude = productRows["Longitude"].ToString();
                                route.Location = productRows["Location"].ToString();
                                route.EntryETA = productRows["EntryETA"].ToString();
                                route.ExitETA = productRows["ExitETA"].ToString();
                                route.EntryATA = productRows["EntryATA"].ToString();
                                route.ExitATA = productRows["ExitATA"].ToString();
                                route.EstRestDuration = productRows["EstRestDuration"].ToString();
                                route.ActRestDuration = productRows["ActRestDuration"].ToString();
                                route.IsCompleted = productRows["IsCompleted"].ToString();
                            }
                            ;
                            routeDetails.Add(route);
                        }
                        TripActualDetails.JMPScheduledStopDetails = routeDetails;
                    }
                }

                //Update in Plan details table using actuals
                //Get the plan vs actual details from the table

                TripActualDetails.TripNo = tripid;
                TripActualDetails.TripType = triptype;
                TripActualDetails.NoOfExistingPlans = countExistingTripPlans.ToString();
                TripActualDetails.VehicleNo = vehicle;
                TripActualDetails.Latitude = lat;
                TripActualDetails.Longitude = lon;
                TripActualDetails.JMPUnscheduledStopDetails = null;
            }
            else
            {
                TripActualDetails.NoOfExistingPlans = countExistingTripPlans.ToString();
            }

            return TripActualDetails;
        }
        public JMPRoute GetJMPTripJRMRouteDetails(string RouteId, string TripType)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            JMPRoute JMPRoute = new JMPRoute();

            DataTable dtRouteDetails = new DataTable();
            dtRouteDetails.Columns.Add("Order", typeof(int));
            dtRouteDetails.Columns[0].AutoIncrementSeed = 1;
            dtRouteDetails.Columns[0].AutoIncrement = true;
            dtRouteDetails.Columns.Add("Latitude", typeof(decimal));
            dtRouteDetails.Columns.Add("Longitude", typeof(decimal));
            dtRouteDetails.Columns.Add("Color", typeof(string));
            dtRouteDetails.Columns.Add("Sequence", typeof(int));
            DataRow[] value = dbx.GetRouteSegmentsByRouteId(RouteId);
            string str = "";
            if (value.Length > 0)
            {
                str = value[0][0].ToString();
            }
            string[] segments = str.Split(',');



            if (TripType.ToLower() == "down")
            {
                for (int i = segments.Length - 1; i >= 0; i--)
                {
                    DataTable dtSegmentRefRouteDetails = dbx.GetReferenceRouteDetailsBySegment(segments[i], TripType);
                    dtRouteDetails.Merge(dtSegmentRefRouteDetails);
                }
            }
            else
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    DataTable dtSegmentRefRouteDetails = dbx.GetReferenceRouteDetailsBySegment(segments[i], TripType);
                    dtRouteDetails.Merge(dtSegmentRefRouteDetails);
                }
            }


            //Get From Way Point Ref
            DataTable dtWayPointRefDetails = new DataTable();
            dtWayPointRefDetails.Columns.Add("Order", typeof(int));
            dtWayPointRefDetails.Columns[0].AutoIncrementSeed = 1;
            dtWayPointRefDetails.Columns[0].AutoIncrement = true;
            dtWayPointRefDetails.Columns.Add("Location");
            dtWayPointRefDetails.Columns.Add("MinLatitude", typeof(decimal));
            dtWayPointRefDetails.Columns.Add("MinLongitude", typeof(decimal));
            dtWayPointRefDetails.Columns.Add("MaxLatitude", typeof(decimal));
            dtWayPointRefDetails.Columns.Add("MaxLongitude", typeof(decimal));
            dtWayPointRefDetails.Columns.Add("Description", typeof(string));

            if (TripType.ToLower() == "down")
            {
                for (int i = segments.Length - 1; i >= 0; i--)
                {
                    DataTable dtSegmentRefRouteDetails = dbx.GetJMPJRMDetailsBySegment(segments[i].ToString().Replace(" ", "_") + "_UP"); // + TripType.ToUpper());
                    dtWayPointRefDetails.Merge(dtSegmentRefRouteDetails);
                }
            }
            else
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    DataTable dtSegmentRefRouteDetails = dbx.GetJMPJRMDetailsBySegment(segments[i].ToString().Replace(" ", "_") + "_UP"); // + TripType.ToUpper());
                    dtWayPointRefDetails.Merge(dtSegmentRefRouteDetails);
                }
            }




            List<JMPRouteDetails> routeDetails = new List<JMPRouteDetails>();
            for (int k = 0; k < dtRouteDetails.Rows.Count; k++)
            {
                DataRow productRows = dtRouteDetails.Rows[k];
                var route = new JMPRouteDetails();
                {
                    route.latitude = productRows["Latitude"].ToString();
                    route.longitude = productRows["Longitude"].ToString();

                    double lat = Convert.ToDouble(productRows["Latitude"].ToString());
                    double lng = Convert.ToDouble(productRows["Longitude"].ToString());
                    double extraGeofence = 0.002;
                    string colorname = "Blue";
                    string colorcode = "#642EFE";
                    if (dtWayPointRefDetails.Rows.Count > 0)
                    {
                        for (int w = 0; w < dtWayPointRefDetails.Rows.Count; w++)
                        {
                            if (lat >= Convert.ToDouble(dtWayPointRefDetails.Rows[w]["MinLatitude"]) && lat <= Convert.ToDouble(dtWayPointRefDetails.Rows[w]["MaxLatitude"]) &&
                            lng >= Convert.ToDouble(dtWayPointRefDetails.Rows[w]["MinLongitude"]) && lng <= Convert.ToDouble(dtWayPointRefDetails.Rows[w]["MaxLongitude"]))
                            {
                                if (dtWayPointRefDetails.Rows[w]["Description"].ToString() == "ACCIDENT SPOT")
                                {
                                    colorname = "Red";
                                    colorcode = "#FF0000";
                                }
                                else if (dtWayPointRefDetails.Rows[w]["Description"].ToString() == "PARKING, REST AREA")
                                {
                                    colorname = "Green";
                                    colorcode = "#00FF00";
                                }
                                else if (dtWayPointRefDetails.Rows[w]["Description"].ToString() == "BLIND SPOT")
                                {
                                    colorname = "Yellow";
                                    colorcode = "#FFFF00";
                                }
                                else if (dtWayPointRefDetails.Rows[w]["Description"].ToString() == "PARKING")
                                {
                                    colorname = "Green";
                                    colorcode = "#00FF00";
                                }
                                else
                                {
                                    colorname = "Blue";
                                    colorcode = "#642EFE";
                                }
                                break;
                            }
                        }
                    }
                    route.colorname = colorname;
                    route.colorcode = colorcode;

                }
                ;
                routeDetails.Add(route);
            }
            JMPRoute.JMPRouteDetails = routeDetails;
            dbx.close();
            return JMPRoute;
        }
        #endregion


        public string PostMPGTripData(string key, string clientId, string tripdata)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "6", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                if (tripdata.Contains("TESTSENSELIN"))
                    return tripdata;
                objbll.STL_RecordErrorMessage(tripdata);
                try
                {
                    string SessionId = access.Split(',')[1];
                    string accountid = objbll.GetAccountId(SessionId).ToString();
                    string groupid = objbll.getGroupId(SessionId).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<MPG_TripDetails> objMPGTripList = jsonSerializer.Deserialize<List<MPG_TripDetails>>(tripdata);
                    for (int i = 0; i < objMPGTripList.Count; i++)
                    {
                        objbll.Insert_MPG_TripDetails(objMPGTripList[i], groupid, accountid);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfull";
            }
            catch (Exception ex)
            {
                return "Upload Failed";
            }
        }

        public Result GenerateTechnicalVettingReportFile(string vetId)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                bool flag = ObjBll.SaveVehicleVettingReport(vetId);
                if (flag)
                    r.result = "Success";
                else
                    r.result = "Failed";
            }
            catch (Exception ex)
            {
                r.result = "Exception----" + ex;
            }

            return r;
        }
        public Result GenerateTechnicalInvariantReportFile(string vetId)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                bool flag = ObjBll.SaveTechnicalInvariantReport(vetId);
                if (flag)
                    r.result = "Success";
                else
                    r.result = "Failed";
            }
            catch (Exception ex)
            {
                r.result = "Exception----" + ex;
            }

            return r;
        }
        public Result GenerateDIPReportFile(string vetId)
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                string filename = ObjBll.SaveVetReportAsPdf(vetId);
                if (filename == null || filename == "")
                    r.result = "Failed";
                else
                    r.result = "Success";

            }
            catch (Exception ex)
            {
                r.result = "Exception----" + ex;
            }

            return r;
        }

        public string PushVehicleTripDetailsOld(string key, string clientId, string data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "12", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                if (data.Contains("TESTSENSELIN"))
                    return data;
                objbll.STL_RecordErrorMessage(data);
                try
                {
                    data = data.Replace("}{", "},{");
                    string SessionId = access.Split(',')[1];
                    string accountid = objbll.GetAccountId(SessionId).ToString();
                    JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                    List<VehicleTripDetailsForAPI> objVehicleTripDetails = jsonSerializer.Deserialize<List<VehicleTripDetailsForAPI>>(data);
                    for (int i = 0; i < objVehicleTripDetails.Count; i++)
                    {
                        //objbll.Update_Metro_Invoices(objVehicleTripDetails[i], accountid);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    objbll.ExceptionLogging("PushVehicleTripDetails", "", clientId, key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    objbll.ExceptionLogging("PushVehicleTripDetails", "", clientId, key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfully";
            }
            catch (Exception ex)
            {
                objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data, "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                objbll.ExceptionLogging("PushVehicleTripDetails", "", clientId, key, data, sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                return "Upload Failed";
            }
        }

        public string PushVehicleTripDetails(string KEY, string CLIENTID, List<VehicleTripDetailsForAPI> DATA)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(KEY, "12", "json", CLIENTID);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                string SessionId = access.Split(',')[1];
                string accountid = objbll.GetAccountId(SessionId).ToString();
                string groupid = objbll.getGroupId(SessionId).ToString();

                objbll.getGroupId(SessionId).ToString();

                var jsonData = JsonConvert.SerializeObject(DATA);
                objbll.STL_RecordErrorMessage(jsonData);
                try
                {
                    for (int i = 0; i < DATA.Count; i++)
                    {
                        objbll.Insert_SHV_Trip_Details(DATA[i], SessionId);
                    }
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + DATA.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    objbll.ExceptionLogging("PushVehicleTripDetails", "", CLIENTID, KEY, DATA.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + DATA.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    objbll.ExceptionLogging("PushVehicleTripDetails", "", CLIENTID, KEY, DATA.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfully";
            }
            catch (Exception ex)
            {
                objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + DATA.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                objbll.ExceptionLogging("PushVehicleTripDetails", "", CLIENTID, KEY, DATA.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                return "Upload Failed";
            }
        }

        //Added  By Zoya on 20/11/2023 for Craft Silicon - Vault Device trip tracking
        //Added  By Zoya on 20/11/2023 for Craft Silicon - Vault Device trip tracking
        public AssetTripAPIResult PostAssetTripData(string key, string clientId, List<AssetTripDetailsEntity> tripdata)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            AssetTripAPIResult result = new AssetTripAPIResult();
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "6", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                string showpositionURL;
                string showrouteURL;
                var jsonData = JsonConvert.SerializeObject(tripdata);
                objbll.STL_RecordErrorMessage("CraftSiliconAPITest - " + jsonData);
                try
                {
                    string SessionId = access.Split(',')[1];
                    string accountid = objbll.GetAccountId(SessionId).ToString();
                    string groupid = objbll.getGroupId(SessionId).ToString();

                    List<AssetTripDetailsEntity> objAssetTripList = JsonConvert.DeserializeObject<List<AssetTripDetailsEntity>>(jsonData);
                    for (int i = 0; i < objAssetTripList.Count; i++)
                    {

                        string deviceId = objAssetTripList[i].deviceId;
                        string tripId = objAssetTripList[i].tripId;
                        string estTripStartDateTime = objAssetTripList[i].estTripStartDateTime;
                        string estTripEndDateTime = objAssetTripList[i].estTripEndDateTime;

                        objAssetTripList[i].showpositionURL = objbll.GetPositionURL(SessionId, deviceId);
                        objAssetTripList[i].showrouteURL = objbll.GetRouteURL(SessionId, deviceId, tripId, estTripStartDateTime, estTripEndDateTime);
                        objbll.Insert_Asset_TripDetails(objAssetTripList[i], groupid, accountid);
                    }
                    showpositionURL = objAssetTripList[0].showpositionURL;
                    showrouteURL = objAssetTripList[0].showrouteURL;

                    result.statuscode = "200";
                    result.result = "Ok";
                    result.showposition = showpositionURL;
                    result.showroute = showrouteURL;
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    result.statuscode = "500";
                    result.result = "Failed:Invalid JSON Data";
                    return result;
                }
                catch (Exception ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return result;
            }
            catch (Exception ex)
            {
                result.statuscode = "500";
                result.result = "Upload Failed";
                return result;
            }
        }

        public List<ChecklistGroup> GetChecklistByLanguage(ImeiRequest request)
        {
            string imei = request.imei;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            var groupedData = new Dictionary<string, ChecklistGroup>();
            DataTable dt = objbll.GetDriverChecklistByLanguage(imei);

            foreach (DataRow row in dt.Rows)
            {
                string code = row["code"].ToString().Trim();
                string icon = row["icon"].ToString().Trim();
                string title = row["title"].ToString().Trim();
                string itemId = row["items"].ToString().Trim();
                string description = row["description"].ToString().Trim();

                if (!groupedData.ContainsKey(code))
                {
                    groupedData[code] = new ChecklistGroup
                    {
                        code = code,
                        icon = icon,
                        title = title
                    };
                }

                groupedData[code].items.Add(new ChecklistItem
                {
                    id = itemId,
                    description = description
                });
            }

            return groupedData.Values.ToList();
        }
        //developed by zoya on 16/05/2025
        public string InsertDailyChecklistdata(ChecklistSubmission submission)
        {
            try
            {
                var objbll = new BusinessLogicLayer.BLL();
                DataTable dt = objbll.GetDriverdetailsByIMEI(submission.IMEI);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return "Driver not found.";
                }

                DataRow dr = dt.Rows[0];
                string driverId = dr["DriverId"].ToString().Trim();
                string driverName = dr["Name"].ToString().Trim();
                string vehicleId = dr["AssignedVehicleId"].ToString().Trim();
                string imei = dr["IMEI"].ToString().Trim();
                string Latitude = "";
                string Longitude = "";
                string accountId = dr["AccountId"].ToString().Trim();
                string Language = "English-English";
                DateTime submissionDateTime = DateTime.Now;
                string IsUnblocked = "001";
                string UnblockedRemarks = "Daily Checklist Filled Successfully";
                string UnblockedBy = "Smart Driver App";
                string Transporter = dr["Transporter"].ToString().Trim();
                string TransName = objbll.GetTransporter(Transporter);

                string submissionId = objbll.insertDailyChecklistHeader(driverId, vehicleId, driverName, submissionDateTime, imei, Latitude, Longitude, accountId);

                var checklistRows = new List<string>();
                var checklists = submission.Checklists.Select(c => new
                {
                    c.ChecklistItemID,
                    c.ItemValue,
                    c.Comment,
                    c.ImageName
                }).ToList();

                bool hasNo = checklists.Any(item => item.ItemValue.Equals("No", StringComparison.OrdinalIgnoreCase));

                foreach (var item in checklists)
                {
                    objbll.insertDailyChecklistDetail(submissionId, item.ChecklistItemID, item.ItemValue, item.Comment, item.ImageName);
                    objbll.insertDailyDriverChecklistStatus(driverId, vehicleId, submissionId, submissionDateTime);
                }

                // Background task for email + block/unblock
                Task.Run(() =>
                {
                    try
                    {
                        string baseImageUrl = "https://db-flatfile-backup.s3.us-east-1.amazonaws.com/fleetsmart3.sensel.in/App+Files/sensel/Sensel.in/fleetsmart3.ui.sensel.in/Uploads/placeinfo/";

                        foreach (var item in checklists)
                        {
                            DataTable dt1 = objbll.GetChecklistdescription(item.ChecklistItemID, Language);
                            string itemName = (dt1.Rows.Count > 0) ? dt1.Rows[0]["ItemDescription"].ToString() : item.ChecklistItemID;
                            string itemValue = item.ItemValue;
                            string comment = string.IsNullOrEmpty(item.Comment) ? "-" : item.Comment;

                            //string fallbackHtml = "<div style='display:flex; align-items:center; gap:5px;'><img src='https://cdn-icons-png.flaticon.com/512/1828/1828665.png' style='width:20px; height:20px;' alt='No Image'/><span>No Image</span></div>";
                            string fallbackHtml = "";

                            string imageLink = string.IsNullOrEmpty(item.ImageName)
                                ? fallbackHtml
                                : $@"<img src='{baseImageUrl}{item.ImageName}' 
             onerror=""this.onerror=null;this.outerHTML=`{fallbackHtml}`;""
             alt='' 
             style='width:100px; height:auto; display:block; border:1px solid #ccc;' />";


                            bool isNo = itemValue.Equals("No", StringComparison.OrdinalIgnoreCase);
                            string rowStyle = isNo ? "style='color:red; font-weight:bold;'" : "";

                            checklistRows.Add($@"<tr {rowStyle}>
                        <td>{itemName}</td>
                        <td>{itemValue}</td>
                        <td>{comment}</td>
                        <td>{imageLink}</td>
                    </tr>");

                            if (isNo) //If checklist is filled no it will block
                            {
                                //string reason = $"Daily Checklist filled as No - {itemName} - {submissionDateTime:dd MMM yy}";
                                string reason = $"Daily Checklist filled as No - {itemName}";

                                objbll.Insertstl_blockinfo(vehicleId, reason, "Smart Driver App Block", accountId, submissionDateTime);
                            }
                            else
                            { //If checklist is filled yes it will unblock 
                                //string reason = $"Daily Checklist filled as No - {itemName} - {submissionDateTime:dd MMM yy}";
                                string reason = $"Daily Checklist filled as No - {itemName}";

                                objbll.Updatestl_blockinfodata(IsUnblocked, UnblockedRemarks, UnblockedBy, submissionDateTime, vehicleId, accountId, reason);
                            }
                        }
                        //Developed by zoya for sending mail
                        //string EmailId = "zoya@sensel.in";
                        string EmailId = objbll.GetTransporteremail(Transporter);
                        string subject = $"Smart Driver Daily Checklist Filled For {vehicleId} By Driver - {driverName}";

                        string emailBody = $@"Dear Sir/Madam,<br/><br/> {vehicleId} Blocked in Daily Checklist,<br/><b>Driver Name:</b> {driverName}<br/>
                <b>Date Time:</b> {submissionDateTime:dd MMM yyyy hh:mm tt}<br/><b>Transporter:</b> {TransName}<br/><br/><b>Checklist Details:</b><br/><br/>
                <table border='1' cellpadding='5' cellspacing='0' style='border-collapse:collapse;'>
                    <thead>
                        <tr>
                            <th>Checklist Item</th>
                            <th>Item Value</th>
                            <th>Comment</th>
                            <th>Image</th>
                        </tr>
                    </thead>
                    <tbody>
                        {string.Join("", checklistRows)}
                    </tbody>
                </table><br/><br/>Best Regards,<br/>Sensel Telematics Pvt. Ltd.<br/><i>Note: Please do not reply to this mail as this is an auto-generated email.</i>";

                        if (!checklists.All(item => item.ItemValue.Equals("Yes", StringComparison.OrdinalIgnoreCase)))// Do not send mail If all checklist is Yes modified by ZOya
                        {
                            SendMail(EmailId, subject, emailBody, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Optional logging
                    }
                });

                return "200";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private static bool SendMail(string ToMail, string Subject, string emailbody, string AttachFile)
        {
            try
            {
                String server = "ONE";
                string FromMail = System.Web.Configuration.WebConfigurationManager.AppSettings["ReportingEmailId"];
                string Password = System.Web.Configuration.WebConfigurationManager.AppSettings["ReportingEmailPassword"];
                string serverStr = System.Web.Configuration.WebConfigurationManager.AppSettings["SERVER"];
                if (serverStr != null)
                    server = serverStr;
                String emailServer = System.Web.Configuration.WebConfigurationManager.AppSettings["emailServerAWS"];
                if (String.IsNullOrEmpty(emailServer))
                    emailServer = "localhost";
                PDFWriter.EmailSender es = new PDFWriter.EmailSender(emailServer);
                es.setupEmail(FromMail,
                                    ToMail,
                                    Subject,
                                    emailbody.ToString());
                es.setBodyHtml(true);
                if (!string.IsNullOrEmpty(AttachFile))
                    es.addAttachment(AttachFile);

                try
                {
                    es.send(FromMail, Password);

                    return true;

                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            catch { return false; }
        }

        public AssetTrip GetAssetTripData(string key, string clientId, string fromDate, string toDate, string DeviceId)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            AssetTripAPIResult result = new AssetTripAPIResult();
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "6", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();

            try
            {
                string SessionId = access.Split(',')[1];
                string accountid = objbll.GetAccountId(SessionId).ToString();
                string groupid = objbll.getGroupId(SessionId).ToString();

                List<ASSETSTAGE> stages = new List<ASSETSTAGE>();
                DataTable dt = objbll.GetAssetTripData(fromDate, toDate, groupid, accountid, DeviceId);

                foreach (DataRow row in dt.Rows)
                {
                    ASSETSTAGE stage = new ASSETSTAGE
                    {
                        EmployeeId = row["EmployeeId"].ToString(),
                        DeviceId = row["DeviceId"].ToString(),
                        TripId = row["TripId"].ToString(),
                        TripType = row["TripType"].ToString(),
                        TripStatus = row["TripStatus"].ToString(),
                        TripStartDateTimeEstimated = row["TripStartDateTimeEstimated"].ToString(),
                        TripEndDateTimeEstimated = row["TripEndDateTimeEstimated"].ToString(),
                        StartLocationLatitude = row["StartLocationLatitude"].ToString(),
                        StartLocationLongitude = row["StartLocationLongitude"].ToString(),
                        EndLocationLatitude = row["EndLocationLatitude"].ToString(),
                        EndLocationLongitude = row["EndLocationLongitude"].ToString()
                    };

                    stages.Add(stage);
                }

                AssetTrip assetTrip = new AssetTrip
                {
                    ASSET_TRIP_DATA = stages
                };

                return assetTrip;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED");
                throw;
            }
        }
        public string GetShowPositionsUrl(string VehicleId)
        {
            // Encrypt URL parameters
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            string SessionId = dbx.GetSessionIdByVehicleId(VehicleId);
            string username = objBll.getUserId(SessionId);
            string sHost = HttpContext.Current.Request.Url.Authority;
            sHost = sHost.Replace(":334", "");
            sHost = sHost.Replace("http://", "https://");
            string link = GenerateEncryptedShortenedUrl(username, SessionId, sHost, VehicleId);
            return link;
        }

        private string GenerateEncryptedShortenedUrl(string username, string SessionId, string sHost, string deviceid)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();

            string link = "";
            string BaseURL = "https://" + sHost;
            string dataUrl = $"sessionId={SessionId}&vehicleId={deviceid}";
            BusinessLogicLayer.StringCipher objCip = new BusinessLogicLayer.StringCipher();
            string encryptedData = objCip.Encrypt(dataUrl);
            string encodedEncryptedData = HttpUtility.UrlEncode(encryptedData);
            string fullUrl = "";
            if (username == "PEST")
            {
                fullUrl = $"{BaseURL}/PodarTrackme.aspx?e=1&q={encodedEncryptedData}";
            }
            if (username == "MSA")
            {
                fullUrl = $"{BaseURL}/ManipalSchool.aspx?e=1&q={encodedEncryptedData}";
            }
            else
            {
                fullUrl = $"{BaseURL}/TrackMe.aspx?e=1&q={encodedEncryptedData}";
                //string fullUrl = $"{BaseURL}/PodarTrackme.aspx?e=1&q={encodedEncryptedData}";
            }
            string shortenedUrl = ShortenUrl(fullUrl, username); // Your existing URL shortening method
            link = shortenedUrl;

            return link;
        }
        //Added By zoya for VehicleTrackingLink
        public string GetShowPositionsUrl1(string VehicleId, string fromDate = null, string toDate = null)
        {
            // Encrypt URL parameters
            //VehicleId = "MH04 KF 3487";
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                if (DateTime.TryParseExact(fromDate, "dd/MM/yyyy hh:mm:ss tt",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedFromDate))
                {
                    fromDate = parsedFromDate.ToString("yyyy-MM-dd HH:mm:ss"); // Convert to required format
                }
            }

            // Convert toDate if it has a value
            if (!string.IsNullOrWhiteSpace(toDate))
            {
                if (DateTime.TryParseExact(toDate, "dd/MM/yyyy hh:mm:ss tt",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedToDate))
                {
                    toDate = parsedToDate.ToString("yyyy-MM-dd HH:mm:ss"); // Convert to required format
                }
            }
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            string SessionId = dbx.GetSessionIdByVehicleId(VehicleId);
            string username = objBll.getUserId(SessionId);
            string sHost = HttpContext.Current.Request.Url.Authority;
            sHost = sHost.Replace(":334", "");
            sHost = sHost.Replace("http://", "https://");
            //string link = GenerateEncryptedShortenedUrl (username, SessionId, sHost, VehicleId);
            string link = GenerateEncryptedShortenedUrllink1(username, SessionId, sHost, VehicleId, fromDate, toDate);
            //string link = "https://TrackMe.VehicleId%MHKF3487%";
            return link;
        }
        private string GenerateEncryptedShortenedUrllink1(string username, string SessionId, string sHost, string deviceid, string fromDate, string toDate)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();

            string link = "";
            string BaseURL = "https://" + sHost;
            string dataUrl = $"sessionId={SessionId}&vehicleId={deviceid}&fromDate={fromDate}&toDate={toDate}";
            BusinessLogicLayer.StringCipher objCip = new BusinessLogicLayer.StringCipher();
            string encryptedData = objCip.Encrypt(dataUrl);
            string encodedEncryptedData = HttpUtility.UrlEncode(encryptedData);
            string fullUrl = "";
            fullUrl = $"{BaseURL}/DeviceTrackLink.aspx?e=1&q={encodedEncryptedData}";
            string shortenedUrl = ShortenUrl(fullUrl, username); // Your existing URL shortening method
            link = shortenedUrl;

            return link;
        }



        private string ShortenUrl(string longUrl, string username)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable table = objBll.GetCallActivationDetails(username, "txtly");
            string link = "";

            if (table.Rows.Count > 0)
            {
                string url = table.Rows[0]["CallUrl"].ToString();
                string requestUrl = $"{url}{HttpUtility.UrlEncode(longUrl)}";
                string result = objBll.MakeHttpRequest(requestUrl, "POST", "application/json");

                if (!string.IsNullOrEmpty(result))
                {
                    var response = JsonConvert.DeserializeObject<dynamic>(result);
                    link = response["txtly"].ToString();
                }
            }

            return link;
        }
        public Result PushSHVTripDetailsToZoho()
        {
            Result r = new Result();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            try
            {
                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                ObjBll.PushSHVTripDetailsToZoho("170812", "DTM");
                r.result = "Success";
            }
            catch (Exception ex)
            {
                r.result = "Failed";
            }
            return r;
        }

        public string GetHybrisMetroTokenForAPI()
        {
            JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                string res = ObjBll.MakeHttpRequest("https://api.sapdev.metro.co.in//authorizationserver/oauth/token?client_id=sensel_user&client_secret=sensel@123&grant_type=client_credentials", "POST", "application/json", "");
                MetroHybrisGetAccessToken objToken = jsonSerializer.Deserialize<MetroHybrisGetAccessToken>(res);
                string access = objToken.access_token;

                ObjBll.PushChangeDataToAPI(access, "OrderStatusUpdate", "2737");

                return "Success";
            }
            catch (Exception ex)
            {
                return "";
            }

        }

        public string MetroReturnRequest(string key, string clientid, MetroReturnRequest data)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "6", "json", clientid);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                string SessionId = access.Split(',')[1];
                string accountid = objbll.GetAccountId(SessionId).ToString();
                string groupid = objbll.getGroupId(SessionId).ToString();

                objbll.getGroupId(SessionId).ToString();

                var jsonData = JsonConvert.SerializeObject(data);
                objbll.STL_RecordErrorMessage(jsonData);
                try
                {
                    string taxinvoicenum = "";
                    //Get Invoice number by consignement 
                    DataTable dtInvoiceDetails = objbll.MetroGetInvoiceDetailsByConsignmentId(data.consignmentId.Trim());
                    if (dtInvoiceDetails != null)
                    {
                        if (dtInvoiceDetails.Rows.Count > 0)
                        {
                            taxinvoicenum = dtInvoiceDetails.Rows[0]["TaxInvoiceNum"].ToString();
                        }
                    }

                    if (!String.IsNullOrEmpty(taxinvoicenum))
                    {
                        // Update in metro invoice details - Return Id
                        // Insert into metro_invoice_product_return_log
                        // update metro_invoice_products

                        //Based on status insert or update or cancel
                        objbll.MetroReturnRequestUpdate(taxinvoicenum, data);
                    }
                    else
                    {

                    }

                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    // objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    // objbll.ExceptionLogging("PushVehicleTripDetails", "", clientid, key, data.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    // objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    //  objbll.ExceptionLogging("PushVehicleTripDetails", "", clientid, key, data.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfully";
            }
            catch (Exception ex)
            {
                // objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                // objbll.ExceptionLogging("PushVehicleTripDetails", "", clientid, key, data.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                return "Upload Failed";
            }
        }

        public string MetroPODPaymentUpdate(string key, string clientid, string consignmentid, string status, string timestamp)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "6", "json", clientid);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            try
            {
                string SessionId = access.Split(',')[1];
                string accountid = objbll.GetAccountId(SessionId).ToString();
                string groupid = objbll.getGroupId(SessionId).ToString();
                objbll.getGroupId(SessionId).ToString();

                objbll.STL_RecordErrorMessage("MetroPODPaymentUpdate : consignmentid-" + consignmentid + "-status-" + status + "-timestamp-" + timestamp);
                try
                {
                    objbll.MetroUpdateInvoiceForPODPaymentUpdate("", status, timestamp, consignmentid);
                }
                catch (System.ArgumentException ex)
                {
                    objbll.STL_RecordErrorMessage(ex.ToString());
                    // objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    // objbll.ExceptionLogging("PushVehicleTripDetails", "", clientid, key, data.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    return "Failed:Invalid JSON Data - " + ex.Message;
                }
                catch (Exception ex)
                {
                    // objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                    //  objbll.ExceptionLogging("PushVehicleTripDetails", "", clientid, key, data.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                    objbll.STL_RecordErrorMessage(ex.ToString());
                }
                return "Upload Successfully";
            }
            catch (Exception ex)
            {
                // objbll.SendFormatedMail(access.Split(',')[1], ex.Message.ToString() + " " + data.ToString(), "Post Push Vehicle Trip Details Failed", "PushVehicleTripDetails");
                // objbll.ExceptionLogging("PushVehicleTripDetails", "", clientid, key, data.ToString(), sHostName, sUserIP, ex.Message.ToString(), ex.GetType().Name.ToString(), exepurl, ex.StackTrace.ToString());
                return "Upload Failed";
            }
        }

        public long[] GetMMIDistanceMatrix()
        {
            try
            {
                Get45or451FromRegistry();
                PrintSecurityProtocalDiagnosis();
            }
            catch (Exception ex)
            {

            }


            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string res = "";
            try
            {
                //if (latlng.Length > 0)
                //{
                //    string EndPoint = "https://apis.mapmyindia.com/advancedmaps/v1/rvu8ga55okjz3u9qf76rsvgomzdmdd2h/distance_matrix/driving/";
                //    for (int i = 0; i < latlng.Length; i++)
                //    {
                //        if (i != 0)
                //            EndPoint += ";";
                //        EndPoint += latlng[i];
                //    }

                string EndPoint = "https://apis.mapmyindia.com/advancedmaps/v1/rvu8ga55okjz3u9qf76rsvgomzdmdd2h/distance_matrix/driving/12.9630406,77.7179527;10.2506821,85.7980593";
                string HTTPMethod = "GET";
                string ContentType = "application/json";
                res = MakeHttpRequest(EndPoint, HTTPMethod, ContentType);
                var data = JsonConvert.DeserializeObject<dynamic>(res);
                if (data["responseCode"].ToString() == "200")
                {
                    if (data["results"]["code"].ToString() == "Ok")
                    {
                        string[] distMat = data["results"]["distances"][0].ToString().Replace("\r\n", "").Replace("[", "").Replace("]", "").Replace(" ", "")
                            .Replace("null", "0").Split(',');
                        //objbll.insertMapApiLog("MMI", "Distance Matrix", distMat.Length.ToString());
                        long[] result = new long[distMat.Length];
                        for (int i = 0; i < distMat.Length; i++)
                        {
                            result[i] = Convert.ToInt64(Convert.ToDecimal(distMat[i]) / 1000);
                        }
                        return result;
                    }
                }
                objbll.STL_RecordErrorMessage("MMI_Distance_Matrix_" + data.ToString());
                //}
                return null;
            }
            catch (Exception ex)
            {
                objbll.STL_RecordErrorMessage("MMI_Distance_Matrix_" + ex.ToString() + "-->" + res);
                return null;
            }
        }

        public string GetMMIDistanceMatrixUsingSenselRestService(string[] latlng)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string res = "";
            try
            {
                if (latlng.Length > 0)
                {
                    string EndPoint = "http://apis.mapmyindia.com/advancedmaps/v1/rvu8ga55okjz3u9qf76rsvgomzdmdd2h/distance_matrix/driving/";
                    for (int i = 0; i < latlng.Length; i++)
                    {
                        if (i != 0)
                            EndPoint += ";";
                        EndPoint += latlng[i];
                    }
                    string HTTPMethod = "GET";
                    string ContentType = "application/json";
                    res = MakeHttpRequest(EndPoint, HTTPMethod, ContentType);
                    objbll.insertMapApiLog("MMI", "Distance Matrix-Azure", "1");

                    var data = JsonConvert.DeserializeObject<dynamic>(res);
                    //if (data["responseCode"].ToString() == "200")
                    //{
                    //    if (data["results"]["code"].ToString() == "Ok")
                    //    {
                    //        string[] distMat = data["results"]["distances"][0].ToString().Replace("\r\n", "").Replace("[", "").Replace("]", "").Replace(" ", "")
                    //            .Replace("null", "0").Split(',');
                    //        objbll.insertMapApiLog("MMI", "Distance Matrix", distMat.Length.ToString());
                    //        long[] result = new long[distMat.Length];
                    //        for (int i = 0; i < distMat.Length; i++)
                    //        {
                    //            result[i] = Convert.ToInt64(Convert.ToDecimal(distMat[i]) / 1000);
                    //        }
                    //        return result;
                    //    }
                    //}

                    objbll.STL_RecordErrorMessage("MMI_Distance_Matrix_Azure" + res.ToString());
                    return data;
                }
                return null;
            }
            catch (Exception ex)
            {
                objbll.STL_RecordErrorMessage("MMI_Distance_Matrix_Azure" + ex.ToString() + "-->" + res);
                return null;
            }
        }

        public string MakeHttpRequest(string EndPoint, string HttpMethod, string ContentType, string PostData = "", string Headers = "")
        {
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Enabled protocols for MakeHttpRequest:   " + ServicePointManager.SecurityProtocol + Environment.NewLine);
            var responseValue = string.Empty;
            try
            {
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(EndPoint);

                request.Method = HttpMethod;
                request.ContentLength = 0;
                request.ContentType = ContentType;
                System.Net.ServicePointManager.DefaultConnectionLimit = 100000;
                if (!string.IsNullOrEmpty(Headers))
                {
                    string[] heads = Headers.Split(new string[] { "||" }, StringSplitOptions.None);
                    foreach (string h in heads)
                    {
                        request.Headers.Add(h.Split(',')[0], h.Split(',')[1]);
                    }
                }
                if (!string.IsNullOrEmpty(PostData) && HttpMethod == "POST")
                {
                    var encoding = new UTF8Encoding();
                    var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(PostData);
                    request.ContentLength = bytes.Length;

                    using (var writeStream = request.GetRequestStream())
                    {
                        writeStream.Write(bytes, 0, bytes.Length);
                    }
                }
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                {
                    //if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    //{
                    //    var message = String.Format("Request failed. Received HTTP {0}", response.StatusCode);
                    //    return "";
                    //}
                    // grab the response
                    using (var responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            using (var reader = new StreamReader(responseStream))
                            {
                                responseValue = reader.ReadToEnd();
                            }
                        }
                    }

                    return responseValue;
                }
            }
            catch (Exception ex)
            {
                return "Error:" + responseValue + ex.ToString();
            }
        }

        public void PrintSecurityProtocalDiagnosis()
        {
            // print initial status
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Runtime: " + System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(int).Assembly.Location).ProductVersion + Environment.NewLine);
            Console.WriteLine("Runtime: " + System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(int).Assembly.Location).ProductVersion);
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Enabled protocols:   " + ServicePointManager.SecurityProtocol + Environment.NewLine);
            Console.WriteLine("Enabled protocols:   " + ServicePointManager.SecurityProtocol);
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Available protocols: " + Environment.NewLine);
            Console.WriteLine("Available protocols: ");
            Boolean platformSupportsTls12 = false;
            foreach (SecurityProtocolType protocol in Enum.GetValues(typeof(SecurityProtocolType)))
            {
                System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + protocol.GetHashCode() + Environment.NewLine);
                Console.WriteLine(protocol.GetHashCode());
                if (protocol.GetHashCode() == 3072)
                {
                    platformSupportsTls12 = true;
                }
            }

            Console.WriteLine("Is SSl3 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)48));
            Console.WriteLine("Is Tls1 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)192));
            Console.WriteLine("Is Tls11 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)768));
            Console.WriteLine("Is Tls12 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)3072));
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Is SSl3 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)48) + Environment.NewLine);
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Is Tls1 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)192) + Environment.NewLine);
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Is Tls11 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)768) + Environment.NewLine);
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Is Tls12 enabled: " + ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)3072) + Environment.NewLine);

            // enable Tls12, if possible
            if (!ServicePointManager.SecurityProtocol.HasFlag((SecurityProtocolType)3072))
            {
                if (platformSupportsTls12)
                {
                    System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Platform supports Tls12, but it is not enabled. Enabling it now." + Environment.NewLine);
                    Console.WriteLine("Platform supports Tls12, but it is not enabled. Enabling it now.");
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                }
                else
                {
                    System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Platform does not supports Tls12." + Environment.NewLine);
                    Console.WriteLine("Platform does not supports Tls12.");
                }
            }

            // disable ssl3
            if (ServicePointManager.SecurityProtocol.HasFlag(SecurityProtocolType.Ssl3))
            {
                System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Ssl3SSL3 is enabled. Disabling it now." + Environment.NewLine);
                Console.WriteLine("Ssl3SSL3 is enabled. Disabling it now.");
                // disable SSL3. Has no negative impact if SSL3 is already disabled. The enclosing "if" if just for illustration.
                System.Net.ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            }
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Enabled protocols:   " + ServicePointManager.SecurityProtocol + Environment.NewLine);
            Console.WriteLine("Enabled protocols:   " + ServicePointManager.SecurityProtocol);
            //Console.ReadKey();

            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", Environment.NewLine);

        }

        #region AIFS
        public Stream ProcessPromptRequest(AIQueryRequest prompt)
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string sessionId = prompt.sessionId;
                string promptText = prompt.prompt;

                int groupid = objbll.getGroupId(sessionId);
                int accountId = dbx.GetAccountIdByGroupId(groupid);
                string username = objbll.getUserId(sessionId);

                //generate query id will be combination of username and datetime ist now
                string queryId = $"{username}_{DateTime.UtcNow.AddMinutes(330).ToString("yyyyMMddHHmmss")}";

                promptText = promptText + " for input sessionId = " + sessionId;

                //Store the query and context info in DB or log for future processing
                dbx.SaveAIFSPromptLog(queryId, sessionId, username, groupid, accountId, promptText, "", "");

                //Call LLM or AI service to process the prompt and generate response
                string callGeminiResponse = CallGemini(promptText);

                bool hasError = HasGeminiError(callGeminiResponse);
                if (hasError)
                {
                    // Log raw response if error
                    dbx.InsertAIFSLLMLog(queryId, promptText, callGeminiResponse);
                    string errorHtml = $@"
            <html>
                <body style='font-family:Arial; padding:20px;'>
                    <h3 style='color:red;'>Server Error</h3>
                    <p>{"Error: LLM Service is Down! Please Try Again!"}</p>
                </body>
            </html>";

                    return ToStream(errorHtml);
                }
                else
                {
                    // Clean & extract valid JSON
                    string finalJson = ExtractRequiredJson(callGeminiResponse);

                    // Log cleaned JSON
                    dbx.InsertAIFSLLMLog(queryId, promptText, finalJson);

                    AIFSRequest queryObject = JsonConvert.DeserializeObject<AIFSRequest>(finalJson);

                    return ProcessAIFSRequest(queryObject);
                }
            }
            catch (Exception ex)
            {
                string errorHtml = $@"
            <html>
                <body style='font-family:Arial; padding:20px;'>
                    <h3 style='color:red;'>Server Error</h3>
                    <p>{"Error:" + ex.Message}</p>
                </body>
            </html>";

                return ToStream(errorHtml);
            }
        }
        private string CallGemini(string userText)
        {
            // 1. Updated Model ID to Gemini 2.5 Flash (Successor to 1.5 Flash)
            // Use "gemini-2.5-flash-lite" if you want even higher speed/lower cost.
            //string modelId = "gemini-2.5-flash";
            //string modelId = "gemini-1.5-flash-002";
            string modelId = "gemini-3-flash-preview";
            //string apiKey = "AIzaSyCpXGu24OPG70KXUOsHcSI0M2Ac5KCu_uw";
            string apiKey = "AIzaSyCp7Dvl804cz_4NnPScs0bCAjy_sRNT2zI";

            // 2. Correct AI Studio Endpoint for API Keys
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={apiKey}";
            //string endpoint = $"https://generativelanguage.googleapis.com/v1/models/{modelId}:generateContent?key={apiKey}";

            //string prompt = File.ReadAllText(@"C:\Users\Tusar\Downloads\MobileApp\Code\prompts_gemini.txt");

            string fileName = "prompts_gemini.txt";
            string filePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Uploads/placeinfo/" + fileName);

            // 2. Safely read the prompt
            string prompt = "";
            if (System.IO.File.Exists(filePath))
            {
                prompt = System.IO.File.ReadAllText(filePath);
                string currentDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd");
                prompt = prompt.Replace("{{CURRENT_DATE}}", currentDate);
            }
            else
            {
                return $"Error: Prompt file not found at {filePath}";
            }

            var requestBody = new
            {
                contents = new[]
                {
            new
            {
                role = "user",
                parts = new[]
                {
                    new { text = userText },
                    new { text = prompt }
                }
            }
        },
                // Optional: Force JSON output for your fleet reports
                generationConfig = new
                {
                    response_mime_type = "application/json"
                }
            };

            string json = JsonConvert.SerializeObject(requestBody);

            using (var client = new HttpClient())
            {
                // Header for reliability
                client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Use Async for better performance in Mobile/Web apps
                var response = client.PostAsync(endpoint, content).Result;

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = response.Content.ReadAsStringAsync().Result;
                    return $"Error {response.StatusCode}: {errorDetails}";
                }

                return response.Content.ReadAsStringAsync().Result;
            }
        }
        private bool HasGeminiError(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return true;

            string lower = response.ToLower();

            // Common LLM / API error indicators
            return lower.Contains("error")
                || lower.Contains("exception")
                || lower.Contains("invalid")
                || lower.Contains("failed")
                || lower.Contains("quota")
                || lower.Contains("rate limit")
                || lower.Contains("unauthorized")
                || lower.Contains("timeout");
        }
        private string ExtractRequiredJson(string geminiResponseJson)
        {
            if (string.IsNullOrWhiteSpace(geminiResponseJson))
                return null;

            try
            {
                // 1️⃣ Parse outer Gemini response
                JObject root = JObject.Parse(geminiResponseJson);

                // 2️⃣ Safely navigate candidates → content → parts
                JToken textToken =
                    root["candidates"]?
                        .FirstOrDefault()?["content"]?["parts"]?
                        .FirstOrDefault()?["text"];

                if (textToken == null)
                    return null;

                string innerText = textToken.ToString().Trim();

                // 3️⃣ Validate that extracted text is JSON
                if (!innerText.StartsWith("{") || !innerText.EndsWith("}"))
                    return null;

                // 4️⃣ Parse inner JSON (actual payload you want)
                JObject finalJson = JObject.Parse(innerText);

                // 5️⃣ Return clean JSON string
                return finalJson.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (JsonException)
            {
                // Invalid JSON → Gemini hallucination or format change
                return null;
            }
        }
        public Stream ProcessAIFSRequest(AIFSRequest request)
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            string html = "";
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string sessionId = request.userContext.sessionId;
                string usersvehicles = objbll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                string[] allLoginVehicles = usersvehicles.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] vehicleIds;
                string[] matchedVehicles;
                string fromDate = request.timeWindow.start;
                string toDate = request.timeWindow.end;

                if (request == null)
                {
                    string errorHtml = $@"
                    <html>
                        <body style='font-family:Arial; padding:20px;'>
                            <h3 style='color:red;'>Server Error: </h3>
                            <p><h3 style='color:red;'>Invalid Request</h3></p>
                        </body>
                    </html>";

                    return ToStream(errorHtml);
                }
                else
                {
                    if (request?.filters?.vehicles?.regoPartials != null
                        && request.filters.vehicles.regoPartials.Any())
                    {
                        // regoPartials may contain full or partial vehicle numbers
                        vehicleIds = request.filters.vehicles.regoPartials.ToArray();
                    }
                    else
                    {
                        // No vehicle filter applied
                        vehicleIds = null;
                    }

                    //if vehicleIds is null or blank get the vehicles by sessionid
                    if (vehicleIds != null && vehicleIds.Length > 0)
                    {
                        matchedVehicles = allLoginVehicles
                            .Where(actualVehicle =>
                            {
                                string actualNorm = NormalizeVehicle(actualVehicle);

                                return vehicleIds.Any(filter =>
                                {
                                    string filterNorm = NormalizeVehicle(filter);
                                    return !string.IsNullOrEmpty(filterNorm)
                                           && actualNorm.Contains(filterNorm);
                                });
                            })
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                    }
                    else
                    {
                        // No regoPartials → allow all vehicles
                        matchedVehicles = allLoginVehicles;
                    }
                }

                // Code added by Rashmitha for location filtering on vehicles based on lat , long
                if (request?.filters?.location?.lng != null
                    && request.filters.location.lat != null)
                {
                    decimal lat = request?.filters?.location?.lat ?? 0;
                    decimal lng = request?.filters?.location?.lng ?? 0;
                    //show lat ,lng in a file

                    string folder = HostingEnvironment.MapPath("~/App_Data");
                    if (string.IsNullOrEmpty(folder))
                        folder = AppDomain.CurrentDomain.BaseDirectory;
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    string file = Path.Combine(folder, "latlng_values.txt");
                    File.AppendAllText(file, $"lat={lat},lng={lng}{Environment.NewLine}");


                    double near = request?.filters?.location?.distanceKm ?? 0;
                    matchedVehicles = FilterVehiclesByLocation(matchedVehicles, lat, lng, near, fromDate, toDate);

                }

                var metrics = request?.filters?.metrics;
                string activeMetric = GetActiveMetric(request);
                var activeEvents = GetActiveEvents(request);
                if (request.reportType.ToLower() == "table")
                {
                    if (activeMetric == null && activeEvents.Count > 0)
                    {
                        string viotype = BuildViolationTypesForSql(activeEvents);
                        //objbll.GetViolationReport(fromDate, toDate, viotype, matchedVehicles, sessionId, "", "baseviolation");
                        ShowViolationReport showViolationReport = new ShowViolationReport();

                        //pass viotype as it is , dont replace ' with "" --Rashmitha
                        string violationReport = showViolationReport.writeTotalViolation(fromDate, toDate, viotype, string.Join(",", matchedVehicles), sessionId);
                        DataTable eventDataTable = ConvertViolationHtmlToDataTable(violationReport);
                        html = GenerateFullHtmlPageNew(eventDataTable, "Events Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                    }
                    else
                    {
                        switch (activeMetric)
                        {
                            case "speed":
                                int speedLimit = 50;
                                if (metrics.speed?.value != null)
                                {
                                    speedLimit = Convert.ToInt32(metrics.speed.value);

                                }
                                DataTable speedData = GetOverspeedReport(matchedVehicles, fromDate, toDate, speedLimit, 2);
                                html = GenerateFullHtmlPageNew(speedData, "Speed Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                                break;

                            case "distance":
                                double distance = 0;
                                if (metrics.distance?.value != null)
                                {
                                    distance = Convert.ToDouble(metrics.distance.value);

                                }
                                DataTable distanceData = GetDistanceReport(matchedVehicles, fromDate, toDate, distance);
                                html = GenerateFullHtmlPageNew(distanceData, "Distance Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                                break;

                            case "workHours":
                                DataTable workHoursData = GetWorkHrsReport(matchedVehicles, fromDate, toDate);
                                html = GenerateFullHtmlPageNew(workHoursData, "Work Hours Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                                break;

                            case "waypoint":
                                DataTable detailRouteData = GetDetailedRouteReport(matchedVehicles, fromDate, toDate);
                                html = GenerateFullHtmlPageNew(detailRouteData, "Detailed Route Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                                break;

                            default:
                                DataTable lastupdtData = GetLastPositionData(sessionId, matchedVehicles);
                                // If fromDate and toDate are null, use current date-time for both
                                if (fromDate == null)
                                    fromDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd HH:mm:ss");
                                if (toDate == null)
                                    toDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd HH:mm:ss");

                                html = GenerateFullHtmlPageNew(lastupdtData, "Live Tracking Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                                break;

                        }
                    }

                    if (activeMetric == null && activeEvents.Count > 0 && fromDate == null && toDate == null)
                    {
                        DataTable lastupdtData = GetLastPositionData(sessionId, matchedVehicles);
                        // If fromDate and toDate are null, use current date-time for both
                        if (fromDate == null)
                            fromDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd HH:mm:ss");
                        if (toDate == null)
                            toDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd HH:mm:ss");
                        html = GenerateFullHtmlPageNew(lastupdtData, "Live Tracking Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                    }
                }
                else if (request.reportType.ToLower() == "table and map")
                {
                    string errorHtml = $@"
                    <html>
                        <body style='font-family:Arial; padding:20px;'>
                            <h3 style='color:red;'>Error : </h3>
                            <p><h3 style='color:red;'> Report +  Map Feature Not Implemented Yet !</h3></p>
                        </body>
                    </html>";

                    return ToStream(errorHtml);
                }
                else if (request.reportType.ToLower() == "map")
                {
                    string sHost = HttpContext.Current.Request.Url.Authority;
                    string baseUrl = "https://" + sHost;
                    if (!string.IsNullOrEmpty(request.timeWindow.start))
                    {
                        fromDate = request.timeWindow.start;
                        toDate = request.timeWindow.end;
                        if (string.IsNullOrEmpty(toDate))
                        {
                             toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        string vehicleForRoute = matchedVehicles != null && matchedVehicles.Length > 0 ? matchedVehicles[0] : "";
                        string routeJson = GetRouteJson(vehicleForRoute, fromDate, toDate);
                        
                        bool isPlay = false;
                        bool showHalt = false;
                        bool showIdle = false;
                        bool showOverspeed = false;
                        string routeColor = "#0000FF";
                        int routeThickness = 3;
                        int playbackSpeed = 200; // Default Medium                        

                        if (request.query != null)
                        {
                            string qFull = (request.query.current ?? "").ToLower();

                            if (qFull.Contains("play")) isPlay = true;
                            if (qFull.Contains("halt")) showHalt = true;
                            if (qFull.Contains("idle") || qFull.Contains("idling")) showIdle = true;
                            if (qFull.Contains("overspeed") || qFull.Contains("alarm")) showOverspeed = true;

                            // Speed parsing
                            if (qFull.Contains("slow")) playbackSpeed = 500;
                            else if (qFull.Contains("fast")) playbackSpeed = 50;
                            else if (qFull.Contains("medium")) playbackSpeed = 200;

                            // Dynamic color parsing
                            // Supports color=red, color=#FF0000, color=rgb(0,0,0) or just "red" (fallback)
                            string detectedColor = "";
                            
                            // 1. Explicit Key-Value (color=...)
                            var colorMatch = Regex.Match(qFull, @"color=([a-z]+|#[0-9a-f]{3,6})", RegexOptions.IgnoreCase);
                            if (colorMatch.Success)
                            {
                                detectedColor = colorMatch.Groups[1].Value;
                            }
                            else
                            {
                                // 2. Fallback: Scan text for known colors if no explicit key
                                // Check for standard simple colors to support "route in yellow"
                                string[] commonColors = new[] { "red", "green", "blue", "yellow", "black", "white", "orange", "purple", "gray", "cyan", "magenta","pink" };
                                foreach (var c in commonColors)
                                {
                                    if (qFull.Contains(c) && !qFull.Contains("color=")) // Avoid double match if key exists but regex failed (rare)
                                    {
                                        detectedColor = c;
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(detectedColor))
                            {
                                routeColor = detectedColor;
                            }
                            
                            // Thickness parsing
                             // Extract thickness if needed, defaulting to 3 for now as simple containment is tricky without regex
                        }

                        MapDrawingRequest mapRequest = ConvertPositionJsonToMapRequest(routeJson, isPlay, showHalt, showIdle, showOverspeed, routeColor, routeThickness, playbackSpeed, baseUrl);
                        html = GenerateMapDrawingHtml(mapRequest);
                    }
                    else
                    {
                        string[] vehiclesForPosition = matchedVehicles;
                        string postionJson = GetPositionJson(vehiclesForPosition);
                        MapDrawingRequest mapRequest = ConvertPositionJsonToMapRequest(postionJson, false, false, false, false, "#0000FF", 3, 200, baseUrl);
                        html = GenerateMapDrawingHtml(mapRequest);
                    }
                }
                else if (request.reportType.ToLower() == "chart")
                {
                    // Chart: group by date and count distinct vehicles per event type (respecting request.filter.events)
                    if ((activeEvents == null || activeEvents.Count == 0) && activeMetric == null)
                    {
                        string errorHtml = $@"
        <html>
            <body style='font-family:Arial; padding:20px;'>
                <h3 style='color:red;'>Error : </h3>
                <p><h3 style='color:red;'>No events or metrics specified for chart.</h3></p>
            </body>
        </html>";

                        return ToStream(errorHtml);
                    }
                    if (activeMetric == null && activeEvents.Count > 0)
                    {
                        // Build viotype string used by legacy DB helper
                        string viotype = BuildViolationTypesForSql(activeEvents);

                        ShowViolationReport showViolationReport = new ShowViolationReport();
                        string violationHtml =
                            showViolationReport.writeTotalViolation(
                                fromDate,
                                toDate,
                                viotype,
                                string.Join(",", matchedVehicles),
                                sessionId
                            );

                        // Convert returned HTML -> flat table
                        DataTable eventTable = ConvertViolationHtmlToDataTable(violationHtml);


                        string groupBy = "day"; //From LLM User
                        // Build pivoted chart table (Date + one column per activeEvent) counting distinct vehicles
                        DataTable chartTable = BuildEventChartTable(eventTable, activeEvents, groupBy, fromDate, toDate);

                        if (chartTable == null || chartTable.Rows.Count == 0)
                        {
                            string errorHtml = $@"
        <html>
            <body style='font-family:Arial; padding:20px;'>
                <h3 style='color:red;'>Error : </h3>
                <p><h3 style='color:red;'>No chart data found for the supplied filters please ask for only one Event.</h3></p>
            </body>
        </html>";

                            return ToStream(errorHtml);
                        }

                        // Reuse existing renderer that expects DataTable with Date + columns matching activeEvents
                        html = GenerateEventChartHtml(chartTable, activeEvents, fromDate, toDate, groupBy);
                    }
                    else
                    {
                        switch (activeMetric)
                        {
                            case "speed":
                                int speedLimit = 50;
                                if (metrics.speed?.value != null)
                                {
                                    speedLimit = Convert.ToInt32(metrics.speed.value);

                                }
                                DataTable speedData = GetOverspeedReport(matchedVehicles, fromDate, toDate, speedLimit, 2);
                                var chatRequestForSpeed = new ChartRequest
                                {
                                    ChartType = "bar",
                                    Title = "Speed Chart Report",
                                    XField = "VehicleNo",
                                    YField = "MaxSpeed",
                                    GroupField = "",
                                    Height = 450,
                                    //Colors = new[] { "#1F77B4", "#FF7F0E", "#2CA02C", "#D62728" }
                                    Colors = new[] { "#1F77B4" }
                                };
                                html = GenerateChartHtmlNew(chatRequestForSpeed, speedData);
                                break;

                            case "distance":
                                double distance = 0;
                                if (metrics.distance?.value != null)
                                {
                                    distance = Convert.ToDouble(metrics.distance.value);

                                }
                                DataTable distanceData = GetDistanceReport(matchedVehicles, fromDate, toDate, distance);
                                var chatRequestForDistance = new ChartRequest
                                {
                                ChartType = "bar",
                                Title = "Distance Chart Report",
                                XField = "VehicleNo",
                                YField = "Distance",
                                GroupField = "",
                                Height = 450,
                                //Colors = new[] { "#1F77B4", "#FF7F0E", "#2CA02C", "#D62728" }
                                Colors = new[] { "#1F77B4" }
                                };
                                html = GenerateChartHtmlNew(chatRequestForDistance, distanceData);
                                  break;                       
                            default:
                                DataTable lastupdtData = GetLastPositionData(sessionId, matchedVehicles);
                                // If fromDate and toDate are null, use current date-time for both
                                if (fromDate == null)
                                    fromDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd HH:mm:ss");
                                if (toDate == null)
                                    toDate = DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd HH:mm:ss");

                                html = GenerateFullHtmlPageNew(lastupdtData, "Live Tracking Report", Convert.ToDateTime(fromDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"), Convert.ToDateTime(toDate).ToString("dd'/'MM'/'yyyy HH:mm:ss"));
                                break;
                        }
                    }
                }
                else
                {

                }
                return new MemoryStream(Encoding.UTF8.GetBytes(html)); // return HTML only
            }
            catch (Exception ex)
            {
                string errorHtml = $@"
            <html>
                <body style='font-family:Arial; padding:20px;'>
                    <h3 style='color:red;'>Server Error</h3>
                    <p>{"Error:" + ex.Message}</p>
                </body>
            </html>";

                return ToStream(errorHtml);
            }
        }
        private MapDrawingRequest ConvertPositionJsonToMapRequest(string positionJson, bool isPlay, bool showHalt, bool showIdle, bool showOverspeed, string routeColor, int routeThickness, int playbackSpeed, string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = "https://ui.vehicle-tracking.co.in"; // Fallback
            MapDrawingRequest mapRequest = new MapDrawingRequest();
            mapRequest.ClearBeforeDrawing = true;
            mapRequest.EnableZoom = true;
            mapRequest.EnablePan = true;
            mapRequest.Objects = new List<MapDrawingObject>();

            if (string.IsNullOrEmpty(positionJson)) return mapRequest;

            try
            {
                var token = JToken.Parse(positionJson);

                if (token is JArray positionList)
                {
                    foreach (var pos in positionList)
                    {
                        double lat = 0, lng = 0;
                        if (double.TryParse(Convert.ToString(pos["lat"]), out lat) && double.TryParse(Convert.ToString(pos["lng"]), out lng))
                        {
                            MapDrawingObject obj = new MapDrawingObject();
                            obj.ObjectType = "Icon";
                            obj.Id = Convert.ToString(pos["vehicleId"]);
                            obj.Position = new GeoPoint { Latitude = lat, Longitude = lng };
                            obj.IconUrl = baseUrl + "/images/greendot.png";

                            string remarks = Convert.ToString(pos["remarks"]) ?? "";

                            obj.InfoText = new Dictionary<string, string>();
                            obj.InfoText.Add("Vehicle No", Convert.ToString(pos["vehicleId"]));
                            obj.InfoText.Add("Speed", Convert.ToString(pos["speed"]) + " km/h");
                            obj.InfoText.Add("Location", Convert.ToString(pos["location"]));
                            obj.InfoText.Add("Date Time", Convert.ToString(pos["timestamp"]));

                            string info = Convert.ToString(pos["info"]);
                            if (!string.IsNullOrEmpty(info))
                            {
                                obj.InfoText.Add("Info", info);
                            }

                            obj.Remarks = remarks;
                            obj.IsVisible = true;
                            obj.IsClickable = true;
                            obj.ZIndex = 1;
                            if (remarks.Contains("Halted")) obj.IconUrl = baseUrl + "/images/red.jpg";
                            else if (remarks.Contains("Moving")) obj.IconUrl = baseUrl + "/images/run_top.gif";
                            else if (remarks.Contains("Idling")) obj.IconUrl = baseUrl + "/images/greendot.png";
                            else if (remarks.Contains("Overspeeding")) obj.IconUrl = baseUrl + "/images/yellow.png";

                            int iWidth = 15;
                            string wStr = Convert.ToString(pos["iconWidth"] ?? pos["IconWidth"]);
                            if (int.TryParse(wStr, out iWidth) && iWidth > 0)
                            {
                                obj.IconWidth = iWidth;
                            }
                            else
                            {
                                obj.IconWidth = 15;
                            }

                            int iHeight = 15;
                            string hStr = Convert.ToString(pos["iconHeight"] ?? pos["IconHeight"]);
                            if (int.TryParse(hStr, out iHeight) && iHeight > 0)
                            {
                                obj.IconHeight = iHeight;
                            }
                            else
                            {
                                obj.IconHeight = 15;
                            }

                            mapRequest.Objects.Add(obj);
                        }
                    }
                }
                else if (token is JObject routeObj)
                {
                    // Handle Route JSON
                    var routes = routeObj["route"] as JArray;
                    if (routes != null)
                    {
                        foreach (var route in routes)
                        {
                            var points = route["route_points"] as JArray;
                            if (points != null && points.Count > 1)
                            {
                                MapDrawingObject obj = new MapDrawingObject();
                                obj.ObjectType = "Route";
                                obj.Id = Convert.ToString(route["vehicle_number"]);
                                obj.Points = new List<GeoPoint>();

                                // Smart Zoom: Will calculate at end based on vehicle count
                                // Default styling
                                obj.LineColor = routeColor;
                                obj.LineThickness = routeThickness;

                                // Playback Animation
                                if (isPlay)
                                {
                                    if (obj.InfoText == null) obj.InfoText = new Dictionary<string, string>();
                                    if (!obj.InfoText.ContainsKey("Animation"))
                                    {
                                        obj.InfoText.Add("Animation", "true");
                                        obj.InfoText.Add("AnimationSpeed", playbackSpeed.ToString());
                                    }
                                }

                                foreach (var pt in points)
                                {
                                    double lat = 0, lng = 0;
                                    if (double.TryParse(Convert.ToString(pt["latitude"]), out lat) && double.TryParse(Convert.ToString(pt["longitude"]), out lng))
                                    {
                                        obj.Points.Add(new GeoPoint { Latitude = lat, Longitude = lng });
                                    }
                                }

                                if (obj.Points.Count > 0)
                                {
                                    // Add Start Marker (Green Flag)
                                    var startObj = new MapDrawingObject
                                    {
                                        ObjectType = "Icon",
                                        Id = obj.Id + " Start",
                                        Position = obj.Points.First(),
                                        IconUrl = baseUrl + "/images/start.gif",
                                        IconWidth = 15,
                                        IconHeight = 15,
                                        ZIndex = 100,
                                        InfoText = new Dictionary<string, string> { { "Type", "Start Point" }, { "Time", Convert.ToString(points.First()["timestamp"]) } }
                                    };
                                    mapRequest.Objects.Add(startObj);

                                    // Add End Marker (Red Flag)
                                    var endObj = new MapDrawingObject
                                    {
                                        ObjectType = "Icon",
                                        Id = obj.Id + " End",
                                        Position = obj.Points.Last(),
                                        IconUrl = baseUrl + "/images/red.jpg",
                                        IconWidth = 15,
                                        IconHeight = 15,
                                        ZIndex = 100,
                                        InfoText = new Dictionary<string, string> { { "Type", "End Point" }, { "Time", Convert.ToString(points.Last()["timestamp"]) } }
                                    };
                                    mapRequest.Objects.Add(endObj);

                                    mapRequest.Objects.Add(obj);
                                }
                            }
                        }

                        // Parse Halts if requested
                        if ((showHalt || showIdle) && routeObj["halts"] is JArray halts)
                        {
                            foreach (var halt in halts)
                            {
                                string type = Convert.ToString(halt["type"]); // "Halt" or "Idling"
                                bool isHalt = type.Equals("Halt", StringComparison.OrdinalIgnoreCase);
                                bool isIdle = type.Contains("Idling") || type.Equals("Idle", StringComparison.OrdinalIgnoreCase);

                                if ((showHalt && isHalt) || (showIdle && isIdle))
                                {
                                    double lat = 0, lng = 0;
                                    string haltText = Convert.ToString(halt["halt_text"])
                                            .Replace("<br>", " ")
                                            .Replace("<br/>", " ")
                                            .Replace("</br>", " ");
                                    if (double.TryParse(Convert.ToString(halt["latitude"]), out lat) && double.TryParse(Convert.ToString(halt["longitude"]), out lng))
                                    {
                                        var haltObj = new MapDrawingObject
                                        {
                                            ObjectType = "Icon",
                                            Id = type,
                                            Position = new GeoPoint { Latitude = lat, Longitude = lng },
                                            IconUrl = isHalt ? baseUrl + "/images/red.jpg" : baseUrl + "/images/greendot.png",
                                            IconWidth = 15,
                                            IconHeight = 15,
                                            ZIndex = 50,
                                            
                                        InfoText = new Dictionary<string, string>
                                            {
                                                { "Type", type },
                                                { "Vehicle", Convert.ToString(halt["vehicle_number"]) },
                                                { "Duration", haltText },
                                                { "Datetime", Convert.ToString(halt["timestamp"]) },
                                                { "Location", Convert.ToString(halt["location"]) }
                                            }
                                        };
                                        mapRequest.Objects.Add(haltObj);
                                    }
                                }
                            }
                        }

                        // Parse Overspeed/Alarms if requested
                        if (showOverspeed && routeObj["alarms"] is JArray alarms)
                        {
                            foreach (var alarm in alarms)
                            {
                                double lat = 0, lng = 0;
                                if (double.TryParse(Convert.ToString(alarm["latitude"]), out lat) && double.TryParse(Convert.ToString(alarm["longitude"]), out lng))
                                {
                                    var alarmObj = new MapDrawingObject
                                    {
                                        ObjectType = "Icon",
                                        Id = "Overspeed",
                                        Position = new GeoPoint { Latitude = lat, Longitude = lng },
                                        IconUrl = baseUrl + "/images/yellow.png",
                                        IconWidth = 15,
                                        IconHeight = 15,
                                        ZIndex = 60,
                                        InfoText = new Dictionary<string, string>
                                        {
                                            { "Type", "Overspeed" },
                                            { "Speed", Convert.ToString(alarm["speed"]) + " km/h" },
                                            { "Time", Convert.ToString(alarm["timestamp"]) }
                                        }
                                    };
                                    mapRequest.Objects.Add(alarmObj);
                                }
                            }
                        }

                        // Smart Zoom Logic
                        if (routes.Count == 1)
                        {
                            // Single vehicle -> 3km radius approx logic (Zoom 13)
                            mapRequest.DefaultZoomLevel = 13;
                        }
                        else
                        {
                            // Multiple vehicles -> Auto Fit
                            mapRequest.DefaultZoomLevel = 0;
                        }
                    }

                    // Auto-center map and Smart Zoom Logic
                    if (mapRequest.Objects.Count > 0)
                    {
                        // Center Map
                        if (mapRequest.Objects[0].ObjectType == "Icon")
                            mapRequest.Center = mapRequest.Objects[0].Position;
                        else if (mapRequest.Objects[0].ObjectType == "Route" && mapRequest.Objects[0].Points.Count > 0)
                            mapRequest.Center = mapRequest.Objects[0].Points[0];

                        // Smart Zoom
                        if (mapRequest.Objects[0].ObjectType == "Icon")
                        {
                            if (mapRequest.Objects.Count == 1)
                            {
                                mapRequest.DefaultZoomLevel = 13; // Approx 3km radius
                            }
                            else
                            {
                                mapRequest.DefaultZoomLevel = 0; // Auto-fit for multiple
                            }
                        }
                        // For Route, it is already set to 11 inside the loop if found
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Error converting position JSON: " + ex.Message);
            }

            return mapRequest;
        }
        public Stream DrawMapObjects(MapDrawingRequest drawingRequest)
        {
            string htmlResponse = "";
            try
            {
                htmlResponse = GenerateMapDrawingHtml(drawingRequest);
            }
            catch (Exception ex)
            {
                htmlResponse = $"<html><body><h1>Error</h1><p>{ex.Message}</p><p>{ex.StackTrace}</p></body></html>";
            }
            byte[] resultBytes = Encoding.UTF8.GetBytes(htmlResponse);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html; charset=utf-8";
            return new MemoryStream(resultBytes);
        }
        private string GenerateMapDrawingHtml(MapDrawingRequest request)
        {
            StringBuilder html = new StringBuilder();

            // Map configuration
            double centerLat = request.Center != null ? request.Center.Latitude : 12.96;
            double centerLng = request.Center != null ? request.Center.Longitude : 77.6;
            int zoom = request.DefaultZoomLevel > 0 ? request.DefaultZoomLevel : 6;
            bool autoFit = request.DefaultZoomLevel <= 0;

            string title = !string.IsNullOrEmpty(request.MapTitle) ? request.MapTitle : "Map";
            string zoomEnabled = (request.EnableZoom).ToString().ToLower();
            string panEnabled = (request.EnablePan).ToString().ToLower();

            string mapOptions = $@"
                center: [{centerLat}, {centerLng}],
                zoom: {zoom},
                zoomControl: {zoomEnabled},
                dragging: {panEnabled},
                scrollWheelZoom: {zoomEnabled},
                doubleClickZoom: {zoomEnabled},
                boxZoom: {zoomEnabled},
                touchZoom: {zoomEnabled},
                keyboard: {panEnabled},
                attributionControl: true
            ";

            html.Append($@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8' />
    <title>{title}</title>
    <meta name='viewport' content='width=device-width, initial-scale=1, shrink-to-fit=no'>

    <!-- Bootstrap 5 CSS -->
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet'>
    <!-- Google Fonts -->
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap' rel='stylesheet'>
    <!-- Font Awesome -->
    <link href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.2/css/all.min.css' rel='stylesheet'>

    <script src='https://apis.mapmyindia.com/advancedmaps/v1/9bzttjsyzyp9nt5zv64xhmkhulvjgow1/map_load?v=1.3'></script>

    <style>
        :root {{
            --primary-color: #0d6efd;
        }}
        body {{
            font-family: 'Inter', sans-serif;
            margin: 0;
            padding: 0;
            overflow: hidden;
            background-color: #f8f9fa;
        }}
        .map-container {{
            height: 100vh;
            width: 100%;
            position: relative;
        }}
        #map {{
            height: 100%;
            width: 100%;
        }}
        
        /* Premium Popup Styling */
        .leaflet-popup-content-wrapper {{
            padding: 0 !important;
            border-radius: 12px !important;
            overflow: hidden;
            box-shadow: 0 4px 15px rgba(0,0,0,0.15) !important;
        }}
        .leaflet-popup-content {{
            margin: 0 !important;
            width: 400px !important;
        }}
        .info-card {{
            border: none;
        }}
        .info-card-header {{
            background: linear-gradient(135deg, #0d6efd 0%, #0a58ca 100%);
            color: white;
            padding: 10px 15px;
            font-weight: 700;
            font-size: 14px;
        }}
        .info-card-body {{
            padding: 0;
        }}
        .info-table {{
            margin-bottom: 0;
            font-size: 12px;
        }}
        .info-table th {{
            background-color: #f8f9fa;
            color: #6c757d;
            font-weight: 600;
            width: 40%;
            padding: 8px 15px;
            border-bottom: 1px solid #dee2e6;
        }}
        .info-table td {{
            padding: 8px 15px;
            color: #212529;
            border-bottom: 1px solid #dee2e6;
        }}
        .remarks-section {{
            background-color: #fff3cd;
            padding: 8px 15px;
            font-size: 12px;
            border-top: 1px solid #ffeeba;
            color: #856404;
        }}

        /* Scrollbar for large tables */
        .info-scroll {{
            max-height: 150px;
            overflow-y: auto;
        }}

        @media (max-width: 768px) {{
            .leaflet-popup-content {{
                width: 320px !important;
            }}
        }}

        @media (max-width: 576px) {{
            .leaflet-popup-content {{
                width: 260px !important;
            }}
        }}
    </style>

    <script>
        var map;
        window.onload = function() {{
            var bounds = new L.LatLngBounds();
            var hasBounds = false;
            var autoFit = {autoFit.ToString().ToLower()};

            map = new MapmyIndia.Map('map', {{
                {mapOptions}
            }});

            var tileUrl = '{request.MapTileLayerUrl}';
            if (tileUrl && tileUrl.trim() !== '') {{
                L.tileLayer(tileUrl).addTo(map);
            }}

            // Pre-process objects for batching bounds
");

            if (request.Objects != null && request.Objects.Count > 0)
            {
                int index = 0;
                foreach (var obj in request.Objects.OrderBy(o => o.ZIndex))
                {
                    if (!obj.IsVisible) continue;

                    if (obj.ObjectType == "Icon" && obj.Position != null)
                    {
                        var lat = obj.Position.Latitude;
                        var lng = obj.Position.Longitude;
                        string iconUrl = !string.IsNullOrEmpty(obj.IconUrl) ? obj.IconUrl : "https://apis.mapmyindia.com/map_v3/1.png";
                        int w = obj.IconWidth > 0 ? obj.IconWidth : 30;
                        int h = obj.IconHeight > 0 ? obj.IconHeight : 40;
                        string clickable = obj.IsClickable.ToString().ToLower();

                        html.AppendFormat(@"
            var pos{0} = new L.LatLng({1}, {2});
            var icon{0} = L.icon({{
                iconUrl: '{3}',
                iconSize: [{4}, {5}],
                popupAnchor: [0, -{6}]
            }});

            var marker{0} = L.marker(pos{0}, {{
                icon: icon{0},
                zIndexOffset: {8},
                interactive: {7}
            }}).addTo(map);

            bounds.extend(pos{0});
            hasBounds = true;
", index, lat, lng, iconUrl, w, h, h / 2, clickable, obj.ZIndex * 100);

                        // Popup Generation (Premium Design)
                        if (obj.IsClickable)
                        {
                            StringBuilder popupHtml = new StringBuilder();
                            popupHtml.Append("<div class='info-card'>");
                            popupHtml.Append($"<div class='info-card-header'><i class='fas fa-truck-moving me-2'></i>{(string.IsNullOrEmpty(obj.Id) ? "Vehicle Details" : obj.Id)}</div>");
                            popupHtml.Append("<div class='info-card-body info-scroll'>");
                            popupHtml.Append("<table class='table info-table'>");

                            if (obj.InfoText != null)
                            {
                                foreach (var item in obj.InfoText)
                                {
                                    popupHtml.Append($"<tr><th>{System.Web.HttpUtility.HtmlEncode(item.Key)}</th><td>{System.Web.HttpUtility.HtmlEncode(item.Value)}</td></tr>");
                                }
                            }
                            popupHtml.Append("</table></div>");

                            if (!string.IsNullOrEmpty(obj.Remarks))
                            {
                                popupHtml.Append($"<div class='remarks-section'><strong>Remarks:</strong> {System.Web.HttpUtility.HtmlEncode(obj.Remarks)}</div>");
                            }
                            popupHtml.Append("</div>");

                            string safePopup = System.Web.HttpUtility.JavaScriptStringEncode(popupHtml.ToString());
                            html.AppendFormat(@"marker{0}.bindPopup(""{1}"");", index, safePopup);
                        }
                    }
                    else if (obj.ObjectType == "Route" && obj.Points != null && obj.Points.Count > 1)
                    {
                        html.Append($"            var pts{index} = [\n");
                        foreach (var pt in obj.Points)
                        {
                            html.Append($"                [{pt.Latitude}, {pt.Longitude}],\n");
                        }
                        html.Append("            ];\n");

                        string dashArray = obj.LineType == "dashed" ? "dashArray: '10, 5'," : "";
                        string clickable = obj.IsClickable.ToString().ToLower();

                        bool isAnimated = false;
                        if (obj.InfoText != null && obj.InfoText.ContainsKey("Animation") && obj.InfoText["Animation"] == "true")
                        {
                            isAnimated = true;
                        }

                        if (isAnimated)
                        {
                            html.AppendFormat(@"
            var poly{0} = L.polyline([], {{
                color: '{1}',
                weight: {2},
                {3}
                interactive: {4}
            }}).addTo(map);

            var path{0} = pts{0};
            var cursor{0} = 0;
            var speed{0} = {5}; // Dynamic speed

            function animateRoute{0}() {{
                if (cursor{0} < path{0}.length) {{
                    poly{0}.addLatLng(path{0}[cursor{0}]);
                    map.panTo(path{0}[cursor{0}]);
                    cursor{0}++;
                    setTimeout(animateRoute{0}, speed{0});
                }}
            }}
            
            // Start animation after small delay
            setTimeout(animateRoute{0}, 1000);

            // Bounds logic - extend for all points so map *could* fit them if autofit was true (though it likely isn't)
            pts{0}.forEach(function(p) {{ bounds.extend(p); }});
            hasBounds = true;
", index, obj.LineColor, obj.LineThickness, dashArray, clickable, (obj.InfoText.ContainsKey("AnimationSpeed") ? obj.InfoText["AnimationSpeed"] : "200"));
                        }
                        else
                        {
                            html.AppendFormat(@"
            var poly{0} = L.polyline(pts{0}, {{
                color: '{1}',
                weight: {2},
                {3}
                interactive: {4}
            }}).addTo(map);

            pts{0}.forEach(function(p) {{ bounds.extend(p); }});
            hasBounds = true;
", index, obj.LineColor, obj.LineThickness, dashArray, clickable);
                        }

                        if (obj.IsClickable && !string.IsNullOrEmpty(obj.Remarks))
                        {
                            string safeRemarks = System.Web.HttpUtility.JavaScriptStringEncode($"<div class='p-2'><strong>Route:</strong> {System.Web.HttpUtility.HtmlEncode(obj.Remarks)}</div>");
                            html.AppendFormat(@"poly{0}.bindPopup(""{1}"");", index, safeRemarks);
                        }
                    }
                    else if (obj.ObjectType == "Geofence")
                    {
                        string clickable = obj.IsClickable.ToString().ToLower();
                        string popupContent = System.Web.HttpUtility.JavaScriptStringEncode($"<div class='p-2'><strong>Geofence:</strong> {System.Web.HttpUtility.HtmlEncode(obj.GeofenceName ?? "Details")}</div>");

                        if (obj.GeofenceType == "Circle" && obj.Center != null)
                        {
                            html.AppendFormat(@"
            var circle{0} = L.circle([{1}, {2}], {{
                radius: {3},
                color: '{4}',
                fillColor: '{5}',
                fillOpacity: {6},
                interactive: {7}
            }}).addTo(map);
            bounds.extend([{1}, {2}]);
            hasBounds = true;
            circle{0}.bindPopup(""{8}"");
", index, obj.Center.Latitude, obj.Center.Longitude, obj.Radius, obj.BorderColor, obj.FillColor, obj.Opacity, clickable, popupContent);
                        }
                        else if (obj.GeofenceType == "Rectangle" && obj.Bounds != null)
                        {
                            html.AppendFormat(@"
            var rect{0} = L.rectangle([[{1}, {2}], [{3}, {4}]], {{
                color: '{5}',
                fillColor: '{6}',
                fillOpacity: {7},
                interactive: {8}
            }}).addTo(map);
            bounds.extend([{1}, {2}]);
            bounds.extend([{3}, {4}]);
            hasBounds = true;
            rect{0}.bindPopup(""{9}"");
", index, obj.Bounds.MinLatitude, obj.Bounds.MinLongitude, obj.Bounds.MaxLatitude, obj.Bounds.MaxLongitude, obj.BorderColor, obj.FillColor, obj.Opacity, clickable, popupContent);
                        }
                        else if (obj.GeofenceType == "Polygon" && obj.PolygonPoints != null && obj.PolygonPoints.Count > 0)
                        {
                            html.Append($"            var polyPts{index} = [\n");
                            foreach (var pt in obj.PolygonPoints)
                                html.Append($"                [{pt.Latitude}, {pt.Longitude}],\n");
                            html.Append("            ];\n");

                            html.AppendFormat(@"
            var polyFence{0} = L.polygon(polyPts{0}, {{
                color: '{1}',
                fillColor: '{2}',
                fillOpacity: {3},
                interactive: {4}
            }}).addTo(map);
            polyPts{0}.forEach(function(p) {{ bounds.extend(p); }});
            hasBounds = true;
            polyFence{0}.bindPopup(""{5}"");
", index, obj.BorderColor, obj.FillColor, obj.Opacity, clickable, popupContent);
                        }
                    }
                    index++;
                }
            }

            html.Append(@"
            if (autoFit && hasBounds && bounds.isValid()) {
                map.fitBounds(bounds, { padding: [50, 50] });
            }
        };
    </script>
</head>
<body>
    <div class='map-container'>
        <div id='map'></div>
    </div>

    <!-- Bootstrap 5 JS -->
    <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js'></script>
</body>
</html>");

            return html.ToString();
        }
        private static readonly Dictionary<string, string[]> EventToViolationMap =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
        { "overspeed", new[] { "OVERSPEEDING" } },

        { "harshBraking", new[] { "HARSH BRAKING" } },

        { "harshAcceleration", new[] { "HIGH ACCELERATION" } },

        { "nightDriving", new[] { "NIGHT DRIVING" } },

        { "routeDeviation", new[] { "ROUTE DEVIATION" } },

        { "continousDriving", new[] { "SHORT RUNTIME" } },

        { "maxDriving", new[] { "TOTAL RUNTIME" } }
        };
        private static string NormalizeVehicle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Replace(" ", "")
                .ToUpperInvariant();
        }
        private string GetActiveMetric(AIFSRequest request)
        {
            var metrics = request?.filters?.metrics;
            if (metrics == null) return null;

            if (metrics.speed?.value != null) return "speed";
            if (metrics?.distance != null) return "distance";
            if (metrics?.workHours != null) return "workHours";
            if (metrics?.waypoint != null) return "waypoint";

            return null;
        }
        private List<string> GetActiveEvents(AIFSRequest request)
        {
            var result = new List<string>();
            var events = request?.filters?.events;

            if (events == null)
                return result;

            if (events.panic != null) result.Add("panic");
            if (events.harshBraking != null) result.Add("harshBraking");
            if (events.harshAcceleration != null) result.Add("harshAcceleration");
            if (events.harshCornering != null) result.Add("harshCornering");
            if (events.halt != null) result.Add("halt");
            if (events.idling != null) result.Add("idling");
            if (events.overspeed != null) result.Add("overspeed");
            if (events.rfid != null) result.Add("rfid");
            if (events.geofenceEntry != null) result.Add("geofenceEntry");
            if (events.geofenceExit != null) result.Add("geofenceExit");

            return result;
        }
        private string BuildViolationTypesForSql(List<string> activeEvents)
        {
            if (activeEvents == null || activeEvents.Count == 0)
                return string.Empty;

            var violations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (activeEvents.Count() == 1)
            {
                foreach (var evt in activeEvents)
                {
                    if (EventToViolationMap.TryGetValue(evt, out var mappedViolations))
                    {
                        foreach (var v in mappedViolations)
                            violations.Add(v);
                    }
                }
                return string.Join(",", violations.Select(v => $"{v}"));
            }
            foreach (var evt in activeEvents)
            {
                if (EventToViolationMap.TryGetValue(evt, out var mappedViolations))
                {
                    foreach (var v in mappedViolations)
                        violations.Add(v);
                }
            }

            return string.Join(",", violations.Select(v => $"'{v}'"));
        }
        private string GenerateFullHtmlPage(DataTable dt, string reportName, string fromDateTime, string toDateTime)
        {
            if (dt == null || dt.Columns.Count == 0)
                return "<html><body><h3>No data available</h3></body></html>";

            var sbHead = new StringBuilder("<tr>");
            foreach (DataColumn c in dt.Columns)
                sbHead.Append($"<th class='tbl-header'>{c.ColumnName}</th>");
            sbHead.Append("</tr>");

            var sbBody = new StringBuilder();
            foreach (DataRow r in dt.Rows)
            {
                sbBody.Append("<tr>");
                foreach (DataColumn c in dt.Columns)
                    sbBody.Append($"<td>{r[c]}</td>");
                sbBody.Append("</tr>");
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>{reportName}</title>

<link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
<link href='https://cdn.datatables.net/1.13.5/css/dataTables.bootstrap5.min.css' rel='stylesheet'>
<link href='https://cdn.datatables.net/colreorder/1.6.2/css/colReorder.bootstrap5.min.css' rel='stylesheet'>

<style>
    .report-title {{
        color:#0B3C5D;
        font-weight:700;
        text-align:center;
        margin-bottom:4px;
    }}
    .report-subtitle {{
        color:#4F81BD;
        font-size:13px;
        text-align:center;
        margin-bottom:14px;
    }}
    .tbl-header {{
        background:#EAF2FB;
        color:#1F4E79;
        font-weight:600;
        border:1px solid #C5D9F1;
        white-space:nowrap;
    }}
</style>
</head>

<body>
<div class='container-fluid mt-3'>

    <h4 class='report-title'>{reportName}</h4>

    <div class='report-subtitle'>
        <b>From:</b> {fromDateTime:dd-MMM-yyyy HH:mm}
        &nbsp; | &nbsp;
        <b>To:</b> {toDateTime:dd-MMM-yyyy HH:mm}
    </div>

    <table id='dataTable' class='table table-striped table-bordered nowrap' style='width:100%'>
        <thead>{sbHead}</thead>
        <tbody>{sbBody}</tbody>
    </table>

</div>

<script src='https://code.jquery.com/jquery-3.6.0.min.js'></script>
<script src='https://cdn.datatables.net/1.13.5/js/jquery.dataTables.min.js'></script>
<script src='https://cdn.datatables.net/1.13.5/js/dataTables.bootstrap5.min.js'></script>
<script src='https://cdn.datatables.net/colreorder/1.6.2/js/dataTables.colReorder.min.js'></script>

<script>
$(function() {{
    $('#dataTable').DataTable({{
        responsive: true,
        scrollX: true,
        colReorder: true,
        pageLength: 25
    }});
}});
</script>

</body>
</html>";
        }
        private string GenerateFullHtmlPageNew(DataTable dt, string reportName, string fromDateTime, string toDateTime)
        {
            if (dt == null || dt.Columns.Count == 0)
                return "<html><body><h3>No data available</h3></body></html>";

            var sbHead = new StringBuilder("<tr>");
            foreach (DataColumn c in dt.Columns)
                sbHead.Append($"<th>{c.ColumnName}</th>");
            sbHead.Append("</tr>");

            var sbBody = new StringBuilder();
            foreach (DataRow r in dt.Rows)
            {
                sbBody.Append("<tr>");
                foreach (DataColumn c in dt.Columns)
                    sbBody.Append($"<td>{r[c]}</td>");
                sbBody.Append("</tr>");
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>{reportName}</title>

<link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
<link href='https://cdn.datatables.net/1.13.5/css/dataTables.bootstrap5.min.css' rel='stylesheet'>
<link href='https://cdn.datatables.net/colreorder/1.6.2/css/colReorder.bootstrap5.min.css' rel='stylesheet'>
<style>
    body {{
        background:#f8fbff;
        font-family:Segoe UI, Arial;
    }}

    /* Report Header */
    .panel-header {{
        background:#0b4f8a;
        color:#ffffff;
        font-size:22px;
        font-weight:600;
        text-align:center;
        padding:12px;
    }}

    /* From-To line */
    .panel-subheader {{
        background:#eaf2fb;
        color:#0b4f8a;
        font-size:13px;
        font-weight:600;
        text-align:center;
        padding:6px;
        border-bottom:1px solid #cfe2f3;
    }}

    /* Table Header */
    table.dataTable thead th {{
        background:#d9ecfb;
        color:#0b4f8a;
        font-weight:600;
        text-align:center;
        white-space:nowrap;
        border:1px solid #cfe2f3;
    }}

    /* Table Cells */
    table.dataTable tbody td {{
        white-space:nowrap;
        border-color:#e1e8f0;
    }}

    /* Row Hover */
    table.dataTable tbody tr:hover {{
        background:#eef6ff;
    }}

    /* Card polish */
    .card {{
        border:1px solid #cfe2f3;
        border-radius:6px;
    }}
</style>
</head>

<body>
<div class='container-fluid mt-3'>

    <div class='card shadow-sm'>

        <div class='panel-header'>
            {reportName}
        </div>

        <div class='panel-subheader'>
            From: {fromDateTime} &nbsp;&nbsp; | &nbsp;&nbsp;
            To: {toDateTime}
        </div>

        <div class='card-body pt-2'>
            <table id='dataTable' class='table table-bordered table-striped nowrap w-100'>
                <thead>{sbHead}</thead>
                <tbody>{sbBody}</tbody>
            </table>
        </div>

    </div>

</div>

<script src='https://code.jquery.com/jquery-3.6.0.min.js'></script>
<script src='https://cdn.datatables.net/1.13.5/js/jquery.dataTables.min.js'></script>
<script src='https://cdn.datatables.net/1.13.5/js/dataTables.bootstrap5.min.js'></script>
<script src='https://cdn.datatables.net/colreorder/1.6.2/js/dataTables.colReorder.min.js'></script>
<script>
$(function () {{
    $('#dataTable').DataTable({{
        scrollX: false,
        pageLength: 10,
        ordering: true,
        colReorder: true
    }});
}});
</script>

</body>
</html>";
        }


        private Stream ToStream(string content)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(byteArray);
        }

        public static string GenerateChartHtml(ChartRequest request, DataTable dt)
        {
            string jsonData = JsonConvert.SerializeObject(
                dt.AsEnumerable().Select(r =>
                    dt.Columns.Cast<DataColumn>()
                      .ToDictionary(c => c.ColumnName, c => r[c])
                )
            );

            var sb = new StringBuilder();

            sb.Append(@"
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset='utf-8' />

            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css'>
            <script src='https://d3js.org/d3.v7.min.js'></script>

            <style>
            .chart-container {
                width: 100%;
                overflow-x: auto;
            }
            </style>
            </head>
            <body>

            <div class='container-fluid'>
              <div class='row'>
                <div class='col-12'>
                  <h5 class='text-center mb-3'>" + request.Title + @"</h5>
                  <div id='" + request.ChartId + @"' class='chart-container'></div>
                </div>
              </div>
            </div>

            <script>
            const data = " + jsonData + @";
            const chartType = '" + request.ChartType + @"';
            const xField = '" + request.XField + @"';
            const yField = '" + request.YField + @"';
            const groupField = '" + request.GroupField + @"';

            renderChart();

            function renderChart() {
                if(chartType === 'line') drawLineChart();
                if(chartType === 'bar') drawBarChart();
                if(chartType === 'pie') drawPieChart();
            }
            ");

            sb.Append(@"
            function drawLineChart() {

                data.forEach(d => {
                    d[xField] = new Date(d[xField]);
                    d[yField] = +d[yField];
                });

                const grouped = groupField
                    ? d3.group(data, d => d[groupField])
                    : new Map([['All', data]]);

                const margin = {top: 30, right: 120, bottom: 40, left: 60};
                const width = document.getElementById('" + request.ChartId + @"').offsetWidth - margin.left - margin.right;
                const height = " + request.Height + @" - margin.top - margin.bottom;

                const svg = d3.select('#" + request.ChartId + @"')
                    .append('svg')
                    .attr('width', width + margin.left + margin.right)
                    .attr('height', height + margin.top + margin.bottom)
                    .append('g')
                    .attr('transform', `translate(${margin.left},${margin.top})`);

                const x = d3.scaleTime()
                    .domain(d3.extent(data, d => d[xField]))
                    .range([0, width]);

                const y = d3.scaleLinear()
                    .domain([0, d3.max(data, d => d[yField])])
                    .nice()
                    .range([height, 0]);

                svg.append('g')
                    .attr('transform', `translate(0,${height})`)
                    .call(d3.axisBottom(x));

                svg.append('g')
                    .call(d3.axisLeft(y));

                const color = d3.scaleOrdinal(d3.schemeTableau10);

                const line = d3.line()
                    .x(d => x(d[xField]))
                    .y(d => y(d[yField]));

                grouped.forEach((values, key) => {
                    svg.append('path')
                        .datum(values)
                        .attr('fill', 'none')
                        .attr('stroke', color(key))
                        .attr('stroke-width', 2)
                        .attr('d', line);
                });
            }
            ");

            sb.Append(@"
function drawBarChart() {

    const aggregated = d3.rollup(
        data,
        v => d3.sum(v, d => +d[yField]),
        d => d[groupField || xField]
    );

    const dataset = Array.from(aggregated, ([key, value]) => ({ key, value }));

    const margin = {top: 30, right: 20, bottom: 40, left: 60};
    const width = document.getElementById('" + request.ChartId + @"').offsetWidth - margin.left - margin.right;
    const height = " + request.Height + @" - margin.top - margin.bottom;

    const svg = d3.select('#" + request.ChartId + @"')
        .append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom)
        .append('g')
        .attr('transform', `translate(${margin.left},${margin.top})`);

    const x = d3.scaleBand()
        .domain(dataset.map(d => d.key))
        .range([0, width])
        .padding(0.2);

    const y = d3.scaleLinear()
        .domain([0, d3.max(dataset, d => d.value)])
        .nice()
        .range([height, 0]);

    svg.append('g')
        .attr('transform', `translate(0,${height})`)
        .call(d3.axisBottom(x));

    svg.append('g')
        .call(d3.axisLeft(y));

    svg.selectAll('rect')
        .data(dataset)
        .enter()
        .append('rect')
        .attr('x', d => x(d.key))
        .attr('y', d => y(d.value))
        .attr('width', x.bandwidth())
        .attr('height', d => height - y(d.value));
}
");

            sb.Append(@"
function drawPieChart() {

    const aggregated = d3.rollup(
        data,
        v => d3.sum(v, d => +d[yField]),
        d => d[groupField || xField]
    );

    const dataset = Array.from(aggregated, ([key, value]) => ({ key, value }));

    const width = 400;
    const height = 400;
    const radius = Math.min(width, height) / 2;

    const svg = d3.select('#" + request.ChartId + @"')
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .append('g')
        .attr('transform', `translate(${width / 2},${height / 2})`);

    const color = d3.scaleOrdinal(d3.schemeTableau10);

    const pie = d3.pie().value(d => d.value);
    const arc = d3.arc().innerRadius(0).outerRadius(radius);

    svg.selectAll('path')
        .data(pie(dataset))
        .enter()
        .append('path')
        .attr('d', arc)
        .attr('fill', d => color(d.data.key));
}
");


            sb.Append(@"
</script>
</body>
</html>
");

            return sb.ToString();
        }


        public static string GenerateChartHtmlNew(ChartRequest request, DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
                return "<html><body><h4>No data available</h4></body></html>";

            string jsonData = JsonConvert.SerializeObject(
                dt.AsEnumerable().Select(r =>
                    dt.Columns.Cast<DataColumn>()
                      .ToDictionary(c => c.ColumnName, c => r[c])
                )
            );

            string colorArray = request.Colors != null && request.Colors.Length > 0
                ? JsonConvert.SerializeObject(request.Colors)
                : "d3.schemeTableau10";

            var sb = new StringBuilder();

            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>

<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css'>
<script src='https://d3js.org/d3.v7.min.js'></script>

<style>
.chart-wrapper {{
    width: 100%;
    overflow-x: auto;
}}
svg {{
    font-family: Arial, sans-serif;
}}
.axis text {{
    font-size: 12px;
}}
.bar {{
    opacity: 0.85;
}}
.bar:hover {{
    opacity: 1;
}}
</style>
</head>

<body>
<div class='container-fluid mt-3'>
    <h5 class='text-center text-primary mb-3'>{request.Title}</h5>
    <div id='{request.ChartId}' class='chart-wrapper'></div>
</div>

<script>

const data = {jsonData};
const chartType = '{request.ChartType}';
const xField = '{request.XField}';
const yField = '{request.YField}';
const groupField = '{request.GroupField ?? ""}';
const colors = {colorArray};

render();

function render() {{
    if(chartType === 'line') drawLine();
    else if(chartType === 'pie') drawPie();
    else drawBar();
}}

function drawBar() {{

    const grouped = d3.rollup(
        data,
        v => d3.sum(v, d => +d[yField]),
        d => groupField ? d[groupField] : d[xField]
    );

    const dataset = Array.from(grouped, ([key, value]) => ({{ key, value }}));

    const margin = {{ top: 30, right: 20, bottom: 80, left: 60 }};
    const width = Math.max(dataset.length * 50, document.getElementById('{request.ChartId}').offsetWidth);
    const height = {request.Height};

    const svg = d3.select('#{request.ChartId}')
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .append('g')
        .attr('transform', `translate(${{margin.left}},${{margin.top}})`);

    const x = d3.scaleBand()
        .domain(dataset.map(d => d.key))
        .range([0, width - margin.left - margin.right])
        .padding(0.3);

    const y = d3.scaleLinear()
        .domain([0, d3.max(dataset, d => d.value)])
        .nice()
        .range([height - margin.top - margin.bottom, 0]);

    const color = d3.scaleOrdinal(colors);

    svg.append('g')
        .attr('transform', `translate(0,${{height - margin.top - margin.bottom}})`)
        .call(d3.axisBottom(x))
        .selectAll('text')
        .attr('transform', 'rotate(-40)')
        .style('text-anchor', 'end');

    svg.append('g').call(d3.axisLeft(y));

    svg.selectAll('.bar')
        .data(dataset)
        .enter()
        .append('rect')
        .attr('class', 'bar')
        .attr('x', d => x(d.key))
        .attr('y', d => y(d.value))
        .attr('width', x.bandwidth())
        .attr('height', d => (height - margin.top - margin.bottom) - y(d.value))
        .attr('fill', d => color(d.key));
}}

function drawLine() {{

    data.forEach(d => {{
        d[xField] = new Date(d[xField]);
        d[yField] = +d[yField];
    }});

    const groups = groupField
        ? d3.group(data, d => d[groupField])
        : new Map([['All', data]]);

    const margin = {{ top: 30, right: 120, bottom: 40, left: 60 }};
    const width = document.getElementById('{request.ChartId}').offsetWidth;
    const height = {request.Height};

    const svg = d3.select('#{request.ChartId}')
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .append('g')
        .attr('transform', `translate(${{margin.left}},${{margin.top}})`);

    const x = d3.scaleTime()
        .domain(d3.extent(data, d => d[xField]))
        .range([0, width - margin.left - margin.right]);

    const y = d3.scaleLinear()
        .domain([0, d3.max(data, d => d[yField])])
        .nice()
        .range([height - margin.top - margin.bottom, 0]);

    const color = d3.scaleOrdinal(colors);

    svg.append('g')
        .attr('transform', `translate(0,${{height - margin.top - margin.bottom}})`)
        .call(d3.axisBottom(x));

    svg.append('g').call(d3.axisLeft(y));

    const line = d3.line()
        .x(d => x(d[xField]))
        .y(d => y(d[yField]));

    groups.forEach((values, key) => {{
        svg.append('path')
            .datum(values)
            .attr('fill', 'none')
            .attr('stroke', color(key))
            .attr('stroke-width', 2)
            .attr('d', line);
    }});
}}

function drawPie() {{

    const grouped = d3.rollup(
        data,
        v => d3.sum(v, d => +d[yField]),
        d => groupField ? d[groupField] : d[xField]
    );

    const dataset = Array.from(grouped, ([key, value]) => ({{ key, value }}));

    const width = 400;
    const height = 400;
    const radius = Math.min(width, height) / 2;

    const svg = d3.select('#{request.ChartId}')
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .append('g')
        .attr('transform', `translate(${{radius}},${{radius}})`);

    const color = d3.scaleOrdinal(colors);

    const pie = d3.pie().value(d => d.value);
    const arc = d3.arc().innerRadius(0).outerRadius(radius);

    svg.selectAll('path')
        .data(pie(dataset))
        .enter()
        .append('path')
        .attr('d', arc)
        .attr('fill', d => color(d.data.key))
        .attr('stroke', '#fff')
        .style('stroke-width', '1px');
}}

</script>
</body>
</html>
");

            return sb.ToString();
        }

        private DataTable BuildEventChartTable(DataTable source, List<string> activeEvents, string groupBy, string fromDat, string toDat)
        {
            // ----------------------------
            // Parse date range
            // ----------------------------
            DateTime fromDate;
            DateTime toDate;

            if (!DateTime.TryParse(fromDat, out fromDate))
                throw new ArgumentException("Invalid fromDate");

            if (!DateTime.TryParse(toDat, out toDate))
                throw new ArgumentException("Invalid toDate");

            fromDate = fromDate.Date;
            toDate = toDate.Date;

            // ----------------------------
            // Result table
            // ----------------------------
            DataTable result = new DataTable();
            result.Columns.Add("Date", typeof(string));

            foreach (string evt in activeEvents)
                result.Columns.Add(evt, typeof(int));

            if (source == null || source.Rows.Count == 0)
                return result;

            // ----------------------------
            // Resolve columns
            // ----------------------------
            string vehicleCol =
                source.Columns.Contains("Vehicle") ? "Vehicle" :
                source.Columns.Contains("VehicleNo") ? "VehicleNo" :
                source.Columns.Contains("vehicle") ? "vehicle" :
                source.Columns.Contains("VehicleId") ? "VehicleId" : null;

            string dateCol =
                source.Columns.Contains("Date") ? "Date" :
                source.Columns.Contains("DateTime") ? "DateTime" :
                source.Columns.Contains("timestamp") ? "timestamp" : null;

            string timeCol =
                source.Columns.Contains("StartingTime") ? "StartingTime" :
                source.Columns.Contains("StartTime") ? "StartTime" : null;

            string eventCol =
                source.Columns.Contains("Event") ? "Event" :
                source.Columns.Contains("EventType") ? "EventType" :
                source.Columns.Contains("Type") ? "Type" :
                source.Columns.Contains("ViolationType") ? "ViolationType" : null;

            if (vehicleCol == null || dateCol == null || eventCol == null)
                return result;

            // ----------------------------
            // (groupKey, event) -> vehicles
            // ----------------------------
            Dictionary<Tuple<string, string>, HashSet<string>> counts =
                new Dictionary<Tuple<string, string>, HashSet<string>>();

            foreach (DataRow r in source.Rows)
            {
                DateTime datePart;
                if (!DateTime.TryParse(Convert.ToString(r[dateCol]), out datePart))
                    continue;

                DateTime fullDateTime = datePart.Date;

                // Hour grouping → add time
                if ((groupBy ?? "").ToLower() == "hour")
                {
                    if (timeCol == null) continue;

                    TimeSpan timePart;
                    if (!TimeSpan.TryParse(Convert.ToString(r[timeCol]), out timePart))
                        continue;

                    fullDateTime = fullDateTime.Add(timePart);
                }

                // Filter range
                if (fullDateTime < fromDate || fullDateTime > toDate.AddDays(1).AddTicks(-1))
                    continue;

                // ----------------------------
                // Match event
                // ----------------------------
                string rawEvent = Convert.ToString(r[eventCol]);
                if (string.IsNullOrWhiteSpace(rawEvent))
                    continue;

                string matchedEvent = null;

                foreach (string evt in activeEvents)
                {
                    string[] mapped;
                    if (EventToViolationMap.TryGetValue(evt, out mapped))
                    {
                        foreach (string m in mapped)
                        {
                            if (string.Equals(m, rawEvent, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedEvent = evt;
                                break;
                            }
                        }
                    }

                    if (matchedEvent != null)
                        break;

                    if (string.Equals(
                            evt.Replace(" ", ""),
                            rawEvent.Replace(" ", ""),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matchedEvent = evt;
                        break;
                    }
                }

                if (matchedEvent == null)
                    continue;

                string vehicle = Convert.ToString(r[vehicleCol]);
                if (string.IsNullOrWhiteSpace(vehicle))
                    continue;

                // ----------------------------
                // Grouping key
                // ----------------------------
                string key;

                switch ((groupBy ?? "day").ToLower())
                {
                    case "hour":
                        DateTime hourStart = new DateTime(
                            fullDateTime.Year,
                            fullDateTime.Month,
                            fullDateTime.Day,
                            fullDateTime.Hour,
                            0,
                            0
                        );
                        key = hourStart.ToString("yyyy-MM-dd HH:00");
                        break;

                    case "week":
                        DateTime weekStart = GetWeekStart(fullDateTime);
                        if (weekStart < fromDate)
                            weekStart = fromDate;

                        int weekNo = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                            weekStart,
                            CalendarWeekRule.FirstDay,
                            DayOfWeek.Monday
                        );

                        key = weekStart.ToString("yyyy") + "-W" + weekNo.ToString("D2");
                        break;

                    case "month":
                        key = fullDateTime.ToString("yyyy-MM");
                        break;

                    default:
                        key = fullDateTime.ToString("yyyy-MM-dd");
                        break;
                }

                Tuple<string, string> dictKey = new Tuple<string, string>(key, matchedEvent);

                HashSet<string> set;
                if (!counts.TryGetValue(dictKey, out set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    counts[dictKey] = set;
                }

                set.Add(vehicle.Trim());
            }

            // ----------------------------
            // Generate empty rows
            // ----------------------------
            if ((groupBy ?? "").ToLower() == "hour")
            {
                DateTime cursor = fromDate;
                while (cursor <= toDate.AddDays(1).AddTicks(-1))
                {
                    DataRow row = result.NewRow();
                    row["Date"] = cursor.ToString("yyyy-MM-dd HH:00");

                    foreach (string evt in activeEvents)
                        row[evt] = 0;

                    result.Rows.Add(row);
                    cursor = cursor.AddHours(1);
                }
            }
            else if ((groupBy ?? "").ToLower() == "week")
            {
                DateTime cursor = GetWeekStart(fromDate);

                while (cursor <= toDate)
                {
                    DateTime effectiveStart = cursor < fromDate ? fromDate : cursor;

                    int weekNo = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        effectiveStart,
                        CalendarWeekRule.FirstDay,
                        DayOfWeek.Monday
                    );

                    DataRow row = result.NewRow();
                    row["Date"] = effectiveStart.ToString("yyyy") + "-W" + weekNo.ToString("D2");

                    foreach (string evt in activeEvents)
                        row[evt] = 0;

                    result.Rows.Add(row);
                    cursor = cursor.AddDays(7);
                }
            }
            else if ((groupBy ?? "").ToLower() == "month")
            {
                DateTime m = new DateTime(fromDate.Year, fromDate.Month, 1);

                while (m <= toDate)
                {
                    DataRow row = result.NewRow();
                    row["Date"] = m.ToString("yyyy-MM");

                    foreach (string evt in activeEvents)
                        row[evt] = 0;

                    result.Rows.Add(row);
                    m = m.AddMonths(1);
                }
            }
            else
            {
                for (DateTime d = fromDate; d <= toDate; d = d.AddDays(1))
                {
                    DataRow row = result.NewRow();
                    row["Date"] = d.ToString("yyyy-MM-dd");

                    foreach (string evt in activeEvents)
                        row[evt] = 0;

                    result.Rows.Add(row);
                }
            }

            // ----------------------------
            // Fill counts
            // ----------------------------
            foreach (KeyValuePair<Tuple<string, string>, HashSet<string>> kv in counts)
            {
                string dateKey = kv.Key.Item1;
                string evt = kv.Key.Item2;

                DataRow[] rows = result.Select("Date = '" + dateKey + "'");
                if (rows.Length > 0)
                    rows[0][evt] = kv.Value.Count;
            }

            return result;
        }

        // Monday-based week start
        private static DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        private string GenerateEventChartHtml(DataTable chartData, List<string> activeEvents, string fromDate, string toDate, string groupBy)
        {
            if (chartData == null || chartData.Rows.Count == 0)
            {
                return "<html><body><h3>No chart data available</h3></body></html>";
            }

            // Convert DataTable → row-based JSON for D3
            var rows = new List<Dictionary<string, object>>();

            foreach (DataRow row in chartData.Rows)
            {
                var dict = new Dictionary<string, object>();
                dict["date"] = row["Date"].ToString();

                foreach (var evt in activeEvents)
                {
                    int val = 0;
                    if (chartData.Columns.Contains(evt))
                        int.TryParse(row[evt].ToString(), out val);

                    dict[evt] = val;
                }
                rows.Add(dict);
            }

            string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(rows);
            string jsonEvents = Newtonsoft.Json.JsonConvert.SerializeObject(activeEvents);

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='utf-8'/>");
            sb.AppendLine("<title>Event Chart</title>");
            sb.AppendLine("<script src='https://d3js.org/d3.v7.min.js'></script>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial; margin: 20px; }");
            sb.AppendLine(".tooltip { position:absolute; background:#000; color:#fff; padding:6px; border-radius:4px; font-size:12px; opacity:0; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine($"<h4 style='text-align:center;'>Event Chart ({string.Join(", ", activeEvents)})</h4>");
            sb.AppendLine($"<div style='text-align:center;color:#555;'>From: {fromDate} &nbsp; To: {toDate} &nbsp; Grouped By: {groupBy}</div>");

            sb.AppendLine("<div style='overflow-x:auto; width:100%;'>");
            sb.AppendLine("  <svg id='chart' height='450'></svg>");
            sb.AppendLine("</div>");



            sb.AppendLine("<div class='tooltip'></div>");

            sb.AppendLine("<script>");

            sb.AppendLine($"const data = {jsonData};");
            sb.AppendLine($"const events = {jsonEvents};");
            sb.AppendLine($"const groupBy = '{groupBy.ToLower()}';");



            sb.AppendLine(@"
const svg = d3.select('#chart');

const margin = { top: 40, right: 20, bottom: 80, left: 60 };
const height = +svg.attr('height') - margin.top - margin.bottom;

// ---- BAR CONFIG ----
const BAR_WIDTH = 50;
const BAR_GAP = 3;
const GROUP_GAP = 20;

// ---- CALCULATE WIDTH FROM DATA ----
const groupWidth =
    events.length * BAR_WIDTH +
    (events.length - 1) * BAR_GAP;

const requiredWidth =
    data.length * groupWidth +
    (data.length - 1) * GROUP_GAP;

const svgWidth = requiredWidth + margin.left + margin.right;

// 🔥 Resize SVG dynamically
svg.attr('width', svgWidth);

const width = svgWidth - margin.left - margin.right;

// ---- MAIN GROUP ----
const g = svg.append('g')
    .attr('transform', `translate(${margin.left},${margin.top})`);


const x0 = d3.scaleBand()
    .domain(data.map(d => d.date))
    .range([0, width])
    .paddingInner(0);

const x1 = d3.scaleBand()
    .domain(events)
    .range([0, groupWidth])
    .padding(0);


//const x1 = d3.scaleBand()
//    .domain(events)
//    .range([0, x0.bandwidth()])
//    .padding(0.05);






const y = d3.scaleLinear()
    .domain([0, d3.max(data, d => d3.max(events, e => d[e]))])
    .nice()
    .range([height, 0]);

const color = d3.scaleOrdinal()
    .domain(events)
    .range(d3.schemeTableau10);

// ---- X AXIS CONTROL (FONT SIZE / ROTATION / TICK DENSITY) ----
const tickStep =
    groupBy === 'hour' ? 2 :
    groupBy === 'day'  ? 1 :
    groupBy === 'week' ? 1 :
    1;

const xFontSize =
    groupBy === 'hour' ? 10 :
    groupBy === 'day'  ? 12 :
    groupBy === 'week' ? 13 :
    12;

const xRotation =
    groupBy === 'hour' ? -70 :
    groupBy === 'day'  ? -40 :
    groupBy === 'week' ? 0 :
    -40;

const xAxis = d3.axisBottom(x0)
    .tickValues(
        x0.domain().filter((d, i) => i % tickStep === 0)
    );

g.append('g')
    .attr('transform', `translate(0,${height})`)
    .call(xAxis)
    .selectAll('text')
    .style('font-size', xFontSize + 'px')
    .style('text-anchor', xRotation === 0 ? 'middle' : 'end')
    .attr('transform', `rotate(${xRotation})`);


g.append('g')
    .call(d3.axisLeft(y));

const tooltip = d3.select('.tooltip');

g.selectAll('g.layer')
    .data(data)
    .enter().append('g')
    .attr('class', 'layer')
    .attr('transform', d => `translate(${x0(d.date)},0)`)
    .selectAll('rect')
    .data(d => events.map(e => ({ event: e, value: d[e], date: d.date })))
    .enter().append('rect')
    .attr('x', d => x1(d.event))
.attr('width', BAR_WIDTH)
    .attr('y', d => y(d.value))
    .attr('height', d => height - y(d.value))
    .attr('fill', d => color(d.event))
    .on('mouseover', (event, d) => {
        tooltip.style('opacity', 1)
            .html(`<b>${d.event}</b><br>${d.date}: ${d.value}`)
            .style('left', event.pageX + 'px')
            .style('top', (event.pageY - 28) + 'px');
    })
    .on('mouseout', () => tooltip.style('opacity', 0));

// Legend
const legend = svg.append('g')
    .attr('transform', `translate(${margin.left},10)`);

events.forEach((e, i) => {
    const lg = legend.append('g')
        .attr('transform', `translate(${i * 120},0)`);

    lg.append('rect')
        .attr('width', 14)
        .attr('height', 14)
        .attr('fill', color(e));

    lg.append('text')
        .attr('x', 20)
        .attr('y', 12)
        .text(e)
        .style('font-size', '12px');
});
");

            sb.AppendLine("</script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }



        public DataTable GetDistanceReport(string[] vehicleIds, string fromDateStr, string toDateStr)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable dtDistanceReport = new DataTable();

            // Define columns
            dtDistanceReport.Columns.Add("VehicleNo", typeof(string));
            dtDistanceReport.Columns.Add("FromDateTime", typeof(string));
            dtDistanceReport.Columns.Add("ToDateTime", typeof(string));
            dtDistanceReport.Columns.Add("Distance", typeof(string));
            dtDistanceReport.Columns.Add("RunTime", typeof(string));

            // Safety check
            if (vehicleIds == null || vehicleIds.Length == 0)
                return dtDistanceReport;

            foreach (string vehicleId in vehicleIds)
            {
                try
                {
                    object[] dist = objBll.accumulateDistance(
                        vehicleId,
                        fromDateStr,
                        toDateStr,
                        null
                    );

                    string distance = (dist != null && dist.Length > 0)
                                        ? Convert.ToString(dist[0])
                                        : "0";

                    string runtime = (dist != null && dist.Length > 2)
                                        ? Convert.ToString(dist[2])
                                        : "0";

                    DataRow row = dtDistanceReport.NewRow();
                    row["VehicleNo"] = vehicleId;
                    row["FromDateTime"] = fromDateStr;
                    row["ToDateTime"] = toDateStr;
                    row["Distance"] = Math.Round(Convert.ToDouble(distance), 2);
                    row["RunTime"] = runtime;

                    dtDistanceReport.Rows.Add(row);
                }
                catch
                {
                    // Fail-safe row
                    DataRow row = dtDistanceReport.NewRow();
                    row["VehicleNo"] = vehicleId;
                    row["FromDateTime"] = fromDateStr;
                    row["ToDateTime"] = toDateStr;
                    row["Distance"] = "0:00";
                    row["RunTime"] = "00:00";

                    dtDistanceReport.Rows.Add(row);
                }
            }

            return dtDistanceReport;
        }
        public DataTable GetDistanceReport(string[] vehicleIds, string fromDateStr, string toDateStr, double minDistance)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable dtDistanceReport = new DataTable();

            // Define columns
            dtDistanceReport.Columns.Add("VehicleNo", typeof(string));
            dtDistanceReport.Columns.Add("FromDateTime", typeof(string));
            dtDistanceReport.Columns.Add("ToDateTime", typeof(string));
            dtDistanceReport.Columns.Add("Distance", typeof(double));
            dtDistanceReport.Columns.Add("RunTime", typeof(string));

            if (vehicleIds == null || vehicleIds.Length == 0)
                return dtDistanceReport;

            foreach (string vehicleId in vehicleIds)
            {
                try
                {
                    object[] dist = objBll.accumulateDistance(
                        vehicleId,
                        fromDateStr,
                        toDateStr,
                        null
                    );

                    double distance = (dist != null && dist.Length > 0 && dist[0] != null)
                                        ? Convert.ToDouble(dist[0])
                                        : 0;

                    string runtime = (dist != null && dist.Length > 2)
                                        ? Convert.ToString(dist[2])
                                        : "00:00";

                    // 🔹 APPLY THRESHOLD
                    if (distance <= minDistance)
                        continue;

                    DataRow row = dtDistanceReport.NewRow();
                    row["VehicleNo"] = vehicleId;
                    row["FromDateTime"] = fromDateStr;
                    row["ToDateTime"] = toDateStr;
                    row["Distance"] = Math.Round(distance, 2);
                    row["RunTime"] = runtime;

                    dtDistanceReport.Rows.Add(row);
                }
                catch
                {
                    // Optional: you can skip error rows OR log them
                    continue;
                }
            }

            return dtDistanceReport;
        }
        public DataTable GetDetailedRouteReport(string[] vehicleIds, string fromDateStr, string toDateStr)
        {
            // Final DataTable to return
            DataTable dt = new DataTable();
            dt.Columns.Add("Vehicle");
            dt.Columns.Add("Date");
            dt.Columns.Add("Time");
            dt.Columns.Add("Location");
            dt.Columns.Add("Distance");
            dt.Columns.Add("TotalDistance");
            dt.Columns.Add("AvgSpeed");

            foreach (string vehicleId in vehicleIds)
            {
                // Call your existing DB method
                Object[] rawRows = dbx.getRouteLatLong(vehicleId, fromDateStr, toDateStr);

                if (rawRows == null || rawRows.Length == 0)
                    continue;

                DateTime prevTime = DateTime.MinValue;
                string prevTimeFormatted = "";

                foreach (Object rowObj in rawRows)
                {
                    object[] row = (object[])rowObj;

                    // Mapping from your query:
                    // 0: id
                    // 1: DATE_FORMAT(timestamp)
                    // 2: Start
                    // 3: Stop
                    // 4: Lat
                    // 5: Long
                    // 6: Speed
                    // 7: UnixTimestamp
                    // 8: Fuel
                    // 9: LAC
                    // 10: CI

                    string dateTimeStr = row[1].ToString();  // formatted date
                    DateTime timestamp = Convert.ToDateTime(dateTimeStr);

                    string date = timestamp.ToString("dd/MM/yyyy");
                    string time = timestamp.ToString("hh:mm:ss tt");

                    // Build clickable location link like UI
                    //string location = $"https://maps.google.com/maps?q={row[4]},{row[5]}";

                    string lat = row[4]?.ToString();
                    string lng = row[5]?.ToString();
                    string locationName = "";
                    try
                    {
                        locationName = dbx.getPositionTxt(lat, lng, 1);
                    }
                    catch
                    {
                        locationName = "Unknown Location";
                    }

                    // Clickable link with location name
                    string location = $"<a href='https://maps.google.com/maps?q={lat},{lng}' target='_blank'>{locationName}</a>";

                    string distance = row[3]?.ToString() ?? "0";
                    string totalDistance = row[4]?.ToString() ?? "0";

                    // Calculate average speed
                    string avgSpeed = "";
                    string curTimeFormatted = timestamp.ToString("yyyy-MM-dd HH:mm:ss");

                    if (prevTime != DateTime.MinValue)
                    {
                        avgSpeed = CalculateAvgSpeed(distance, prevTimeFormatted, curTimeFormatted);
                    }

                    prevTime = timestamp;
                    prevTimeFormatted = curTimeFormatted;

                    // Add to final DataTable
                    dt.Rows.Add(
                        vehicleId,
                        date,
                        time,
                        location,
                        distance,
                        totalDistance,
                        avgSpeed
                    );
                }
            }

            return dt;
        }
        protected string CalculateAvgSpeed(string Distance, string preTimeStr, string curTimestr)
        {
            string avgSpdStr = "0.0";
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();

            try
            {
                if (!string.IsNullOrEmpty(Distance) &&
                    !string.IsNullOrEmpty(preTimeStr) &&
                    !string.IsNullOrEmpty(curTimestr))
                {
                    // Convert timestamp strings using your existing BLL converter
                    DateTime preTime = objBll.ConvertStringToDateTime(preTimeStr, "day");
                    DateTime curTime = objBll.ConvertStringToDateTime(curTimestr, "day");

                    // Travel time in seconds
                    double travelTimeSeconds = curTime.Subtract(preTime).TotalSeconds;

                    // Distance in meters (your old code assumed Distance is in km)
                    double travelDistMeters = Convert.ToDouble(Distance) * 1000;

                    if (travelTimeSeconds > 0.0)
                    {
                        // Formula: (meters/sec) × 3.6 = km/h
                        double avgSpeed = (travelDistMeters / travelTimeSeconds) * 3.6;

                        avgSpeed = Math.Round(avgSpeed, 2);
                        avgSpdStr = avgSpeed.ToString();
                    }
                }
            }
            catch
            {
                // swallow errors like your old UI code
            }

            return avgSpdStr;
        }
       
        //Code Added By Rashmitha 
        private string[] FilterVehiclesByLocation(string[] vehicleIds, decimal lat, decimal lng, double nearKm, string fromDate, string toDate)
        {
            if (vehicleIds == null || vehicleIds.Length == 0)
                return new string[0];

            List<string> filtered = new List<string>();
            foreach (var vehicleId in vehicleIds)
            {
                try
                {
                    // Get last known position for the vehicle in the given time window
                    Object[] positions = dbx.getLastLatLong(vehicleId);
                    if (positions != null && positions.Length > 0)
                    {
                        // Use the last position in the array
                        object[] last = (object[])positions[positions.Length - 1];
                        // 4: Lat, 5: Long (see getRouteLatLong mapping in your code)
                        if (decimal.TryParse(last[0]?.ToString(), out decimal vlat) &&
                            decimal.TryParse(last[1]?.ToString(), out decimal vlng))
                        {
                            //double dist = HaversineDistance((double)lat, (double)lng, (double)vlat, (double)vlng);
                            double dist = dbx.computeDistance((double)lat, (double)lng, (double)vlat, (double)vlng);
                            if (dist <= nearKm)
                                filtered.Add(vehicleId);
                        }
                    }
                }
                catch
                {
                    // Ignore errors for individual vehicles
                }
            }
            return filtered.ToArray();
        }
        public DataTable GetWorkHrsReport(string[] vehicleIds, string fromDateStr, string toDateStr)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("VehicleNo", typeof(string));
            //dt.Columns.Add("StartTime", typeof(string));      // HH:mm
            dt.Columns.Add("FromDateTime", typeof(string));
            dt.Columns.Add("ToDateTime", typeof(string));
            dt.Columns.Add("OnDuration(HH:MM:SS)", typeof(string));     // HH:mm
            dt.Columns.Add("OffDuration(HH:MM:SS)", typeof(string));   // HH:mm


            foreach (string vehicleId in vehicleIds)
            {
                long onSeconds = 0;
                long stopSeconds = 0;

                long currentBlockStart = -1;
                bool? currentStateOn = null; // true = ON, false = STOP

                long firstOnTs = -1;
                long lastTs = -1;

                object[] routeRows = dbx.getRouteLatLong(vehicleId, fromDateStr, toDateStr);

                if (routeRows != null)
                {
                    foreach (object r in routeRows)
                    {
                        object[] arr = r as object[];
                        if (arr == null) continue;

                        string stopBit = arr.Length > 2 ? Convert.ToString(arr[3]) : null;

                        long ts = -1;
                        if (arr.Length > 7 && long.TryParse(Convert.ToString(arr[7]), out ts)) { }
                        else if (arr.Length > 1 && DateTime.TryParse(Convert.ToString(arr[1]), out DateTime dtm))
                        {
                           ts = (long)(dtm.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                        }

                        if (ts <= 0) continue;

                        bool isOn = (stopBit == "0");

                        if (currentStateOn == null)
                        {
                            currentStateOn = isOn;
                            currentBlockStart = ts;

                            if (isOn && firstOnTs == -1)
                                firstOnTs = ts;
                        }
                        else if (currentStateOn != isOn)
                        {
                            long diff = ts - currentBlockStart;

                            if (currentStateOn == true)
                                onSeconds += diff;
                            else
                                stopSeconds += diff;

                            currentStateOn = isOn;
                            currentBlockStart = ts;

                            if (isOn && firstOnTs == -1)
                                firstOnTs = ts;
                        }

                        lastTs = ts;
                    }

                    // Close last block
                    if (currentBlockStart != -1 && lastTs > currentBlockStart)
                    {
                        long diff = lastTs - currentBlockStart;
                        if (currentStateOn == true)
                            onSeconds += diff;
                        else
                            stopSeconds += diff;
                    }
                }

                DataRow row = dt.NewRow();
                row["VehicleNo"] = vehicleId;
                //row["StartTime"] = firstOnTs > 0
                //    ? DateTimeOffset.FromUnixTimeSeconds(firstOnTs).ToString("HH:mm")
                //    : "";



                if (!string.IsNullOrWhiteSpace(fromDateStr) && DateTime.TryParse(fromDateStr, out DateTime fromDt))
                    row["FromDateTime"] = fromDt.ToString("dd-MM-yyyy HH:mm:ss");
                else
                    row["FromDateTime"] = fromDateStr ?? "";

                if (!string.IsNullOrWhiteSpace(toDateStr) && DateTime.TryParse(toDateStr, out DateTime toDt))
                    row["ToDateTime"] = toDt.ToString("dd-MM-yyyy HH:mm:ss");
                else
                    row["ToDateTime"] = toDateStr ?? "";

                //row["OnDuration(HH:MM)"] = TimeSpan.FromSeconds(onSeconds).ToString(@"hh\:mm");
                //row["OffDuration(HH:MM)"] = TimeSpan.FromSeconds(stopSeconds).ToString(@"hh\:mm");
                TimeSpan onTs = TimeSpan.FromSeconds(onSeconds);
                TimeSpan offTs = TimeSpan.FromSeconds(stopSeconds);

                row["OnDuration(HH:MM:SS)"] =
                    $"{(int)onTs.TotalHours:00}:{onTs.Minutes:00}:{onTs.Seconds:00}";

                row["OffDuration(HH:MM:SS)"] =
                    $"{(int)offTs.TotalHours:00}:{offTs.Minutes:00}:{offTs.Seconds:00}";

                dt.Rows.Add(row);
            }

            return dt;
        }
        public DataTable GetLastPositionData(string sessionId, string[] vehicleIds = null)
        {
            var dt = new DataTable();
            dt.Columns.AddRange(new[]
            {
        new DataColumn("VehicleNo"),
        new DataColumn("GPSDateTime"),
        new DataColumn("Latitude"),
        new DataColumn("Longitude"),
        new DataColumn("Speed"),
        new DataColumn("Location"),
        new DataColumn("Remarks"),
        //new DataColumn("Stop"),
        //new DataColumn("Direction"),
        //new DataColumn("Battery"),
        //new DataColumn("Fuel"),
        //new DataColumn("Geofence"),
        //new DataColumn("Transporter"),
        //new DataColumn("Odometer")
    });

            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string maxGprsDelayStr = System.Web.Configuration.WebConfigurationManager.AppSettings["MAXGPRSDELAY"] ?? "1200";
            string maxGpsDelayStr = System.Web.Configuration.WebConfigurationManager.AppSettings["MAXGPSDELAY"] ?? "240";
            double maxSpeed = 70.0; 
            DateTime endDate = DateTime.UtcNow.AddMinutes(330);
            Object[] values = dbx.getLastPositions(sessionId, null);

               HashSet<string> vehicleFilter = vehicleIds == null
     ? null
     : new HashSet<string>(
         vehicleIds.Select(v => v.Replace(" ", "").ToLower())
       );

            foreach (Object[] row in values)
            {
                string veh = row[(int)headersIndex.trucksIndex].ToString();

                if (vehicleFilter != null &&
                    !vehicleFilter.Contains(veh.Replace(" ", "").ToLower()))
                    continue;

                string lat = row[(int)headersIndex.latitudeIndex].ToString();
                string lng = row[(int)headersIndex.longitudeIndex].ToString();
                string ts = row[(int)headersIndex.timesIndex].ToString();
                string spd = row[(int)headersIndex.speedIndex].ToString();
                string stop = row[(int)headersIndex.StopIndex].ToString();

                string direction = row[8] != null
                    ? (Convert.ToInt32(row[8]) % 360).ToString()
                    : "0";

                string location = dbx.getPositionTxt(lat, lng, 3);
                // REMARKS CALCULATION (portal logic)
                string Remarks = "";
                DateTime GPS_last_update_date = DateTime.Parse(ts);
                DateTime timestamp_last_GPRS_update_date = GPS_last_update_date; // Approximation in absence of lastaccess table call

                if (Math.Abs((endDate - timestamp_last_GPRS_update_date).TotalSeconds) < Convert.ToInt32(maxGprsDelayStr))
                {
                    if (Math.Abs((endDate - GPS_last_update_date).TotalSeconds) > Convert.ToInt32(maxGpsDelayStr))
                    {
                        Remarks = "GPS Not Active";
                    }
                    else
                    {
                        if (Convert.ToInt16(stop) == 1) Remarks = "Vehicle Halted";
                        else if (Convert.ToDouble(spd) <= 3) Remarks = "Vehicle Idling";
                        else if (Convert.ToDouble(spd) > maxSpeed) Remarks = "Over Speeding";
                        else Remarks = "Vehicle Moving";
                    }
                }
                else
                {
                    Remarks = "Not Reachable";
                }
                DataRow dr = dt.NewRow();
                dr["VehicleNo"] = veh;
                dr["GPSDateTime"] = Convert.ToDateTime(ts).ToString("yyyy-MM-dd HH:mm:ss");
                dr["Latitude"] = lat;
                dr["Longitude"] = lng;
                dr["Speed"] = spd;
                dr["Location"] = location;
                dr["Remarks"] = Remarks;
                //dr["Stop"] = stop;
                //dr["Direction"] = direction;

                // Optional enrichments (safe + light)
                //dr["Battery"] = row.Length > 15 ? row[15]?.ToString() : "";
                //dr["Fuel"] = row.Length > 17 ? row[17]?.ToString() : "";
                //dr["Geofence"] = "";
                //dr["Transporter"] = "";
                //dr["Odometer"] = "";

                dt.Rows.Add(dr);
            }

            return dt;
        }   
        public DataTable GetOverspeedReport(string[] vehicleIds, string fromDateStr, string toDateStr, int speedLimit, int overspeedDurationMinutes)
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("VehicleNo", typeof(string));
            dt.Columns.Add("Circle", typeof(string));
            dt.Columns.Add("StartDate", typeof(string));
            dt.Columns.Add("StartTime", typeof(string));
            dt.Columns.Add("StartLocation", typeof(string));
            dt.Columns.Add("SpeedLimit", typeof(string));
            dt.Columns.Add("MaxSpeed", typeof(string));
            dt.Columns.Add("AvgSpeed", typeof(string));
            dt.Columns.Add("EndDate", typeof(string));
            dt.Columns.Add("EndTime", typeof(string));
            dt.Columns.Add("EndLocation", typeof(string));
            dt.Columns.Add("Distance", typeof(string));
            dt.Columns.Add("Remarks", typeof(string));
            ShowManyVehiclesOverspeedReport showManyVehiclesOverspeedReport = new ShowManyVehiclesOverspeedReport();
            if (vehicleIds == null || vehicleIds.Length == 0)
                return dt;

            foreach (string vehicleId in vehicleIds)
            {
                try
                {
                    Object[] values = dbx.getRouteLatLongByMaxSpeed(
                        vehicleId,
                        fromDateStr,
                        toDateStr,
                        speedLimit
                    );

                    if (values == null || values.Length == 0)
                        continue;

                    for (int i = 0; i < values.Length; i++)
                    {
                        string[] info = showManyVehiclesOverspeedReport.vehicleOverspeedInfo(i, values, overspeedDurationMinutes, speedLimit);

                        if (info == null)
                            continue;

                        DataRow row = dt.NewRow();
                        row["VehicleNo"] = vehicleId;
                        row["Circle"] = dbx.GetCircleNameByVehicleId(vehicleId);
                        row["StartDate"] = info[0];
                        row["StartTime"] = info[1];
                        row["StartLocation"] = info[2];
                        row["SpeedLimit"] = info[3];
                        row["MaxSpeed"] = info[4];
                        row["AvgSpeed"] = info[5];
                        row["EndDate"] = info[6];
                        row["EndTime"] = info[7];
                        row["EndLocation"] = info[8];
                        row["Distance"] = info[10];
                        row["Remarks"] = info[9];

                        dt.Rows.Add(row);
                    }
                }
                catch
                {
                    // Skip vehicle but continue others
                }
            }

            return dt;
        }
        public DataTable ConvertViolationHtmlToDataTable(string html)
        {
            DataTable dt = new DataTable();

            // Define columns
            dt.Columns.Add("Vehicle");
            dt.Columns.Add("Driver");
            dt.Columns.Add("Transporter");
            dt.Columns.Add("Date");
            dt.Columns.Add("Event");
            dt.Columns.Add("Type");
            dt.Columns.Add("SpeedOrAcceleration");
            dt.Columns.Add("Distance");
            dt.Columns.Add("Duration");
            dt.Columns.Add("TimeExceeded");
            dt.Columns.Add("NightDriving");
            dt.Columns.Add("RestTime");
            dt.Columns.Add("StartingTime");
            dt.Columns.Add("Location");

            if (string.IsNullOrEmpty(html))
                return dt;

            // Extract all rows
            MatchCollection rowMatches =
                Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline);

            // Skip header row (index 0)
            for (int i = 1; i < rowMatches.Count; i++)
            {
                string rowHtml = rowMatches[i].Groups[1].Value;

                // Extract columns
                MatchCollection cellMatches =
                    Regex.Matches(rowHtml, "<td.*?>(.*?)</td>", RegexOptions.Singleline);

                if (cellMatches.Count < 14)
                    continue;

                DataRow dr = dt.NewRow();

                dr["Vehicle"] = StripHtml(cellMatches[0].Groups[1].Value);
                dr["Driver"] = StripHtml(cellMatches[1].Groups[1].Value);
                dr["Transporter"] = StripHtml(cellMatches[2].Groups[1].Value);
                dr["Date"] = StripHtml(cellMatches[3].Groups[1].Value);
                dr["Event"] = StripHtml(cellMatches[4].Groups[1].Value);
                dr["Type"] = StripHtml(cellMatches[5].Groups[1].Value);
                dr["SpeedOrAcceleration"] = StripHtml(cellMatches[6].Groups[1].Value);
                dr["Distance"] = StripHtml(cellMatches[7].Groups[1].Value);
                dr["Duration"] = StripHtml(cellMatches[8].Groups[1].Value);
                dr["TimeExceeded"] = StripHtml(cellMatches[9].Groups[1].Value);
                dr["NightDriving"] = StripHtml(cellMatches[10].Groups[1].Value);
                dr["RestTime"] = StripHtml(cellMatches[11].Groups[1].Value);
                dr["StartingTime"] = StripHtml(cellMatches[12].Groups[1].Value);

                // Location: anchor text only
                dr["Location"] = StripHtml(cellMatches[13].Groups[1].Value);

                dt.Rows.Add(dr);
            }

            return dt;
        }
        private string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }
        private string GetPositionJson(string[] vehicleNumbers)
        {
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            List<object> vehiclePositions = new List<object>();

            // Setup configuration values (derived from PortalBll.cs snippet)
            string maxGprsDelayStr = System.Web.Configuration.WebConfigurationManager.AppSettings["MAXGPRSDELAY"] ?? "1200";
            string maxGpsDelayStr = System.Web.Configuration.WebConfigurationManager.AppSettings["MAXGPSDELAY"] ?? "240";
            double maxSpeed = 70.0; // Default, can be refined per vehicle if needed
            DateTime endDate = DateTime.UtcNow.AddMinutes(330); // Approximate IST conversion

            if (vehicleNumbers != null)
            {
                foreach (var v in vehicleNumbers)
                {
                    string VehicleId = v.Trim();
                    if (string.IsNullOrEmpty(VehicleId)) continue;

                    string sessionId = objbll.getSessionIdByVehicle(VehicleId);
                    int gid = dbx.getGroupId(sessionId);
                    Object[] selectedValues = dbx.getLastPositions(sessionId, null);

                    if (selectedValues != null)
                    {
                        string searchId = VehicleId.ToLower().Replace(" ", "");

                        for (int i = 0; i < selectedValues.Length; i++)
                        {
                            object[] row = (Object[])selectedValues[i];
                            string veh = row[(int)headersIndex.trucksIndex].ToString();

                            if (!string.IsNullOrEmpty(searchId) && searchId != "all")
                            {
                                if (!veh.ToLower().Replace(" ", "").Contains(searchId) && !searchId.Contains(veh.ToLower().Replace(" ", "")))
                                    continue;
                            }

                            string displayVehicleId = veh;
                            string latStr = row[(int)headersIndex.latitudeIndex].ToString();
                            string longStr = row[(int)headersIndex.longitudeIndex].ToString();
                            double speed = Convert.ToDouble(row[(int)headersIndex.speedIndex]);
                            string timestamp = row[(int)headersIndex.timesIndex].ToString();
                            // Attempt to get location from index 19 if available, fallback to BLL call
                            string location = row.Length > 19 && row[19] != null ? row[19].ToString() : objbll.getPositionTxt(latStr, longStr, 3, Convert.ToString(dbx.GetAccountIdByGroupId(gid)));
                            string info = row[(int)headersIndex.infoIndex].ToString();
                            //string iconType = row[(int)headersIndex.iconIndex].ToString();
                            //string iconDirection = row[(int)headersIndex.directionIndex].ToString();
                            //string loadType = row[(int)headersIndex.loadTypeIndex].ToString();
                            int StopBit = Convert.ToInt32(row[(int)headersIndex.StopIndex]);

                            // REMARKS CALCULATION (portal logic)
                            string Remarks = "";
                            DateTime GPS_last_update_date = DateTime.Parse(timestamp);
                            DateTime timestamp_last_GPRS_update_date = GPS_last_update_date; // Approximation in absence of lastaccess table call

                            if (Math.Abs((endDate - timestamp_last_GPRS_update_date).TotalSeconds) < Convert.ToInt32(maxGprsDelayStr))
                            {
                                if (Math.Abs((endDate - GPS_last_update_date).TotalSeconds) > Convert.ToInt32(maxGpsDelayStr))
                                {
                                    Remarks = "GPS Not Active";
                                }
                                else
                                {
                                    if (StopBit == 1) Remarks = "Vehicle Halted";
                                    else if (speed <= 3) Remarks = "Vehicle Idling";
                                    else if (speed > maxSpeed) Remarks = "Over Speeding";
                                    else Remarks = "Vehicle Moving";
                                }
                            }
                            else
                            {
                                Remarks = "Not Reachable";
                            }

                            vehiclePositions.Add(new
                            {
                                lat = latStr,
                                lng = longStr,
                                vehicleId = displayVehicleId,
                                speed = speed.ToString(),
                                timestamp = timestamp,
                                location = location,
                                remarks = Remarks,
                                info = info
                                //iconType = iconType,
                                //iconDirection = iconDirection,
                                //loadType = loadType
                            });

                            break;
                        }
                    }
                }
            }

            return JsonConvert.SerializeObject(vehiclePositions);
        }
        private string GeneratePositionHtml(string jsonPosition)
        {
            string html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Vehicle Positions</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <script src=""https://apis.mapmyindia.com/advancedmaps/v1/9bzttjsyzyp9nt5zv64xhmkhulvjgow1/map_load?v=1.3""></script>
    <style>
        html, body, #MapDiv { height: 100%; margin: 0; padding: 0; }
        .leaflet-popup-content-wrapper { padding: 0 !important; }
        .leaflet-popup-content { margin: 0 !important; min-width: auto !important; }
        .leaflet-popup-tip { width: 10px; height: 10px; }
        .VehInfo table { border-collapse: collapse; width: auto; max-width: 280px; font-family: Arial, sans-serif; font-size: 12px; }
        .VehInfo th, .VehInfo td { padding: 4px 6px; }
        .map-label-content { 
            position: relative; left: -50%; transform: translateX(-50%); 
            white-space: nowrap; font-size: 12px; font-weight: 700; 
            color: #000; text-shadow: 0 0 2px #fff; 
            background: transparent; border: none; padding: 0; 
        }
        .legend { padding: 5px; background: white; border: 1px solid #ccc; line-height: 18px; color: #333; }
        .legend ul { list-style: none; padding: 0; margin: 0; }
        .legend li { margin-bottom: 5px; font-size: 12px; }
        .legend img { width: 15px; vertical-align: middle; margin-right: 5px; }
    </style>
    <script>
      var map;
      var vehicles = [[JSON_DATA]];

      function getMarkerIcon(v) {
        var imgBase = 'file:///D:/Madhuri/Projects/Portal.New/images/';
        var markerImage = imgBase + 'marker.gif';
        var remarks = v.remarks || '';
        var iconType = v.iconType || 'Default';
        var loadType = v.loadType || '';

        if (iconType === 'Default') {
            if (remarks.includes('Tipper ON') || remarks.includes('Battery Low')) {
                markerImage = imgBase + 'pink.jpg';
            } else if (remarks.includes('Not Reachable') || remarks.includes('Not Reporting')) {
                markerImage = loadType.includes('UP') ? imgBase + 'ntRechble_load.gif' : imgBase + 'ntRechble.gif';
            } else if (remarks.includes('Vehicle Idling') || remarks.includes('Reporting')) {
                markerImage = loadType.includes('UP') ? imgBase + 'greendot_load.gif' : imgBase + 'greendot.png';
            } else if (remarks.includes('Vehicle ON') || remarks.includes('Vehicle Moving') || remarks.includes('Moving')) {
                markerImage = loadType.includes('UP') ? imgBase + 'run_top_load.png' : imgBase + 'run_top.gif';
            } else if (remarks.includes('Halted')) {
                markerImage = imgBase + 'red.jpg';
            } else if (remarks.includes('GPS Not Active')) {
                markerImage = loadType.includes('UP') ? imgBase + 'greydot_load.gif' : imgBase + 'greydot.gif';
            } else if (remarks.includes('Over Speeding')) {
                markerImage = loadType.includes('UP') ? imgBase + 'fast_top_load.png' : imgBase + 'fast_top.gif';
            }
        } else {
            if (remarks.includes('Tipper ON')) markerImage = imgBase + iconType + 'pink.png';
            else if (remarks.includes('Not Reachable')) markerImage = imgBase + iconType + 'black.png';
            else if (remarks.includes('Idling') || remarks.includes('ON') || remarks.includes('Moving')) markerImage = imgBase + iconType + 'green.png';
            else if (remarks.includes('GPS Not Active')) markerImage = imgBase + iconType + 'grey.png';
            else if (remarks.includes('Over Speeding')) markerImage = imgBase + iconType + 'yellow.png';
            else if (remarks.includes('Halted')) markerImage = imgBase + iconType + 'red.png';
            else markerImage = imgBase + 'greendot_load.gif';
        }
        return markerImage;
      }

      function getPopupContent(v) {
        var infoColor = 'red', secondColor = '#ffb399';
        var remarks = v.remarks || '';
        if (remarks.includes('Not Reachable') || remarks.includes('GPS Not Active')) {
            infoColor = 'grey'; secondColor = '#d3d3d3';
        } else if (remarks.includes('Moving') || remarks.includes('Reporting') || remarks.includes('Idling')) {
            infoColor = 'green'; secondColor = '#ccffcc';
        } else if (remarks.includes('Halted')) {
            infoColor = 'red'; secondColor = '#ffcccc';
        }
        
        var popupHtml = '<div class=""VehInfo"">' +
            '<table border=""1"">' +
            '<tr><th colspan=""2"" style=""background-color:' + infoColor + '; color:white; text-align:center;"">' + v.vehicleId + '</th></tr>' +
            '<tr><td><b>Date Time</b></td><td>' + v.timestamp + '</td></tr>' +
            '<tr><td><b>Speed</b></td><td>' + v.speed + ' kmph</td></tr>' +
            '<tr><td><b>Status</b></td><td>' + v.remarks + '</td></tr>' +
            '<tr><td><b>Location</b></td><td>' + v.location + '</td></tr>' +
            '</table></div>';
        return popupHtml;
      }

      window.onload = function() {
        console.log('Map loading initiated. Vehicles data:', vehicles);
        var map_div = document.getElementById('MapDiv');
        var bounds = new L.LatLngBounds();
        var imgBase = 'file:///D:/Madhuri/Projects/Portal.New/images/';
        
        // Initialize Map using ID string
        map = new MapmyIndia.Map('MapDiv', { zoomControl: true, hybrid: true });

        if (vehicles && vehicles.length > 0) {
            vehicles.forEach(function(v) {
                if (v.lat && v.lng) {
                    var pos = new L.LatLng(parseFloat(v.lat), parseFloat(v.lng));
                    var iconDirection = v.iconDirection || 0;
                    var markerIconPath = getMarkerIcon(v);
                    console.log('Adding marker for vehicle:', v.vehicleId, 'at:', v.lat, v.lng, 'Icon:', markerIconPath);
                    
                    var iconHtml = '<div style=""transform: rotate(' + iconDirection + 'deg);""><img src=""' + markerIconPath + '"" /></div>' +
                                   '<div class=""map-label-content"">' + v.vehicleId + '</div>';
                    
                    var customIcon = L.divIcon({
                        className: 'custom-vehicle-icon',
                        html: iconHtml,
                        iconSize: [32, 32],
                        iconAnchor: [16, 16]
                    });

                    var marker = new L.Marker(pos, { icon: customIcon }).addTo(map);
                    marker.bindPopup(getPopupContent(v));
                    bounds.extend(pos);
                }
            });

            if (!bounds.isValid()) {
                // If no valid points, set a default view
                map.setView([20.5937, 78.9629], 5);
            } else {
                map.fitBounds(bounds, { padding: [50, 50] });
                if (vehicles.length === 1) map.setZoom(15);
            }
        } else {
            // Default view for no vehicles
            map.setView([20.5937, 78.9629], 5);
        }

        var legend = L.control({ position: 'bottomleft' });
        legend.onAdd = function (map) {
            var div = L.DomUtil.create('div', 'info legend');
            div.innerHTML = '<ul>' +
                '<li><strong>Legend</strong></li>' +
                //'<li><img src=""' + imgBase + 'start.gif""/> Start Location</li>' +
                '<li><img src=""' + imgBase + 'run_top.gif""/> Vehicle Direction</li>' +
                '<li><img src=""' + imgBase + 'halt.gif""/> Halt Location</li>' +
                '<li><img src=""' + imgBase + 'greendot.png""/> Idling Location</li>' +
                '<li><img src=""' + imgBase + 'alarm.gif""/> Overspeed Location</li>' +
                '</ul>';
            return div;
        };
        legend.addTo(map);
      };
    </script>
</head>
<body>
    <div id=""MapDiv""></div>
</body>
</html>";
            return html.Replace("[[JSON_DATA]]", jsonPosition);
        }
        private string GetRouteJson(string VehicleNumber, string FromDate, string ToDate)
        {
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            string[] vehicleIds = !string.IsNullOrEmpty(VehicleNumber) ? VehicleNumber.Split(',') : new string[0];
            DateTime from = Convert.ToDateTime(FromDate);
            DateTime to = Convert.ToDateTime(ToDate);

            int range_limit = 168;
            if (vehicleIds.Length > 1)
                range_limit = 24;
            if ((to - from).TotalHours > range_limit)
            {
                throw new Exception("FAIL:DATE_RANGE_IS_MORE");
            }

            List<VehicleRoute> vehicleRoutes = new List<VehicleRoute>();
            List<GeofencePoint> geofencePoints = new List<GeofencePoint>();
            List<GeofencePointIrregular> geofencePointsIrregular = new List<GeofencePointIrregular>();
            List<HaltInfo> haltInfos = new List<HaltInfo>();
            List<AlarmInfo> alarmInfos = new List<AlarmInfo>();
            HashSet<string> addedGeofences = new HashSet<string>();

            double grandTotalDistance = 0;
            foreach (string vid in vehicleIds)
            {
                if (string.IsNullOrWhiteSpace(vid)) continue;

                object[] values = objbll.getRouteData(from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), vid, false);
                VehicleRoute vehicleRoute = new VehicleRoute { vehicle_number = vid, route_points = new List<RoutePoint>() };

                if (values != null)
                {
                    foreach (object val in values)
                    {
                        object[] row = (object[])val;
                        vehicleRoute.route_points.Add(new RoutePoint
                        {
                            latitude = row[(int)BusinessLogicLayer.BLL.headers.latitude].ToString(),
                            longitude = row[(int)BusinessLogicLayer.BLL.headers.longitude].ToString(),
                            timestamp = Convert.ToDateTime(row[(int)BusinessLogicLayer.BLL.headers.timestamp]).ToString("yyyy-MM-dd HH:mm:ss"),
                            speed = row[(int)BusinessLogicLayer.BLL.headers.speed].ToString(),
                            stop = row[(int)BusinessLogicLayer.BLL.headers.stop].ToString()
                        });
                    }
                }
                vehicleRoutes.Add(vehicleRoute);

                double totalDistance = 0;
                double distanceCorrection = 0;
                string sessionId = objbll.getSessionIdByVehicle(vid);

                if (!string.IsNullOrEmpty(sessionId))
                {
                    int accid = objbll.GetVehAccontId(vid);
                    DataTable dtconfig = objbll.GetAccountConfig(accid);
                    if (dtconfig != null && dtconfig.Rows.Count > 0)
                    {
                        distanceCorrection = Convert.ToDouble(dtconfig.Rows[0]["DistanceCorrection"].ToString());
                    }

                    int overspeedLimit = objbll.getOverspeedLimitBySession(sessionId);
                    if (overspeedLimit == 0) overspeedLimit = 50; // Default

                    if (values != null && values.Length > 0)
                    {
                        List<HaltInfo> localHalts = new List<HaltInfo>();
                        List<AlarmInfo> localAlarms = new List<AlarmInfo>();

                        objbll.showHalt2(0, values, true); // Initialize and reset BLL state for this vehicle

                        // BaseRoute logic initialization
                        DateTime stopTimestamp = DateTime.MinValue;
                        string tempSense = dbx.getTempSensorFlag(vid) ?? "";
                        double latitudeThresholdIdle = 0.00; // Match BaseRoute.cs line 251
                        double longitudeThresholdIdle = 0.00;

                        for (int i = 0; i < values.Length; i++)
                        {
                            object[] currRow = (object[])values[i];
                            DateTime startTimestamp = Convert.ToDateTime(currRow[(int)BusinessLogicLayer.BLL.headers.timestamp]);

                            // Track Distance
                            if (i > 0)
                            {
                                object[] prevRow = (object[])values[i - 1];
                                float stopLat = Convert.ToSingle(prevRow[(int)BusinessLogicLayer.BLL.headers.latitude]);
                                float startLat = Convert.ToSingle(currRow[(int)BusinessLogicLayer.BLL.headers.latitude]);
                                float stopLon = Convert.ToSingle(prevRow[(int)BusinessLogicLayer.BLL.headers.longitude]);
                                float startLon = Convert.ToSingle(currRow[(int)BusinessLogicLayer.BLL.headers.longitude]);

                                double deltaLastLat = Math.Abs(stopLat - startLat);
                                double deltaLastLon = Math.Abs(stopLon - startLon);

                                int curStopVal = Convert.ToInt32(currRow[(int)BusinessLogicLayer.BLL.headers.stop]);
                                int prevStopVal = Convert.ToInt32(prevRow[(int)BusinessLogicLayer.BLL.headers.stop]);

                                if (curStopVal == 0 || prevStopVal == 0 || tempSense.Contains("SLOW"))
                                {
                                    double speedVal = Convert.ToDouble(currRow[(int)BusinessLogicLayer.BLL.headers.speed]);
                                    float rpmVal = 0;
                                    if (currRow.Length > 8) float.TryParse(currRow[8].ToString(), out rpmVal);

                                    TimeSpan elapsedTime = startTimestamp.Subtract(stopTimestamp);
                                    bool isMlogData = (elapsedTime.TotalSeconds == 1 && speedVal > 2);

                                    // Distance segment skipped if stopTimestamp is MinValue (BaseRoute logic)
                                    if ((deltaLastLat > latitudeThresholdIdle * 4) || (deltaLastLon > longitudeThresholdIdle * 4) || tempSense.Contains("SLOW") || isMlogData || rpmVal >= 100.00)
                                    {
                                        double currentDist = objbll.computeDistance(stopLat, stopLon, startLat, startLon);

                                        if (!(currentDist >= 6 && elapsedTime.TotalMinutes < 1.0))
                                        {
                                            double distAddl = (distanceCorrection / 100) * currentDist;
                                            if ((elapsedTime.TotalMinutes > 0 && (currentDist / elapsedTime.TotalMinutes) >= 0.2 && (currentDist / elapsedTime.TotalMinutes) <= 4) || tempSense.Contains("SLOW") || rpmVal >= 100.00)
                                            {
                                                currentDist = (currentDist + distAddl) * 1.02;
                                                totalDistance += currentDist;
                                            }
                                            else if (elapsedTime.TotalSeconds < 60)
                                            {
                                                currentDist = (currentDist + distAddl) * 1.02;
                                                totalDistance += currentDist;
                                            }
                                            else if (currentDist < 1.1 && elapsedTime.TotalMinutes > 150)
                                            {
                                                currentDist = currentDist + distAddl; // No 1.02 multiplier for basement case
                                                totalDistance += currentDist;
                                            }
                                        }
                                    }
                                }
                                stopTimestamp = startTimestamp;
                            }

                            // Halts and Idling
                            object[] haltResult = objbll.showHalt2(i, values, true);
                            if (haltResult != null)
                            {
                                foreach (object h in haltResult)
                                {
                                    string[] hInfo = (string[])h;
                                    localHalts.Add(new HaltInfo
                                    {
                                        vehicle_number = vid,
                                        latitude = hInfo[7],
                                        longitude = hInfo[8],
                                        timestamp = hInfo[0] + " " + hInfo[1],
                                        location = hInfo[2],
                                        halt_text = hInfo[3],
                                        type = hInfo[3].StartsWith("Idling") ? "Idling" : "Halt"
                                    });
                                }
                            }

                            // Overspeed Alarms
                            double speedVal_os = Convert.ToDouble(currRow[(int)BusinessLogicLayer.BLL.headers.speed]);
                            if (speedVal_os > overspeedLimit)
                            {
                                string alarmLoc = "";
                                try
                                {
                                    alarmLoc = objbll.getPositionTxt(currRow[(int)BusinessLogicLayer.BLL.headers.latitude].ToString(), currRow[(int)BusinessLogicLayer.BLL.headers.longitude].ToString(), -1);
                                }
                                catch { }

                                localAlarms.Add(new AlarmInfo
                                {
                                    vehicle_number = vid,
                                    latitude = currRow[(int)BusinessLogicLayer.BLL.headers.latitude].ToString(),
                                    longitude = currRow[(int)BusinessLogicLayer.BLL.headers.longitude].ToString(),
                                    location = alarmLoc,
                                    timestamp = startTimestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                                    alarm_text = "Over Speed " + speedVal_os + " kmph"
                                });
                            }
                        }

                        // Now apply the TOTAL distance to all halts and alarms for this specific vehicle
                        string distLabel = "<br/> Distance:" + Math.Round(totalDistance, 1) + "km";
                        grandTotalDistance += totalDistance;
                        foreach (var h in localHalts)
                        {
                            h.halt_text += distLabel;
                            haltInfos.Add(h);
                        }
                        foreach (var a in localAlarms)
                        {
                            a.alarm_text += distLabel;
                            alarmInfos.Add(a);
                        }
                    }

                    // Fetch Geofences
                    //if (mapRequest.Geofence == "True")
                    //{
                    //    DataTable dtGeofence = objbll.GetAllAllowedRegions(vid, 0, true);
                    //    if (dtGeofence != null)
                    //    {
                    //        foreach (DataRow dr in dtGeofence.Rows)
                    //        {
                    //            string key = dr["regionName"].ToString() + dr["minLatitude"].ToString() + dr["minLongitude"].ToString();
                    //            if (!addedGeofences.Contains(key))
                    //            {
                    //                geofencePoints.Add(new GeofencePoint
                    //                {
                    //                    locstr = dr["regionName"].ToString().Replace("'", ""),
                    //                    minlat = dr["minLatitude"].ToString(),
                    //                    minlng = dr["minLongitude"].ToString(),
                    //                    maxlat = dr["maxLatitude"].ToString(),
                    //                    maxlng = dr["maxLongitude"].ToString()
                    //                });
                    //                addedGeofences.Add(key);
                    //            }
                    //        }
                    //    }

                    //    DataTable dtIrregular = objbll.GetAllGeofenceBySessionId(sessionId);
                    //    if (dtIrregular != null)
                    //    {
                    //        foreach (DataRow dr in dtIrregular.Rows)
                    //        {
                    //            if (dr["noofpoints"].ToString() == "9")
                    //            {
                    //                string key = dr["geofencename"].ToString();
                    //                if (!addedGeofences.Contains(key))
                    //                {
                    //                    geofencePointsIrregular.Add(new GeofencePointIrregular
                    //                    {
                    //                        locstr = dr["geofencename"].ToString().Replace("'", ""),
                    //                        lat1 = dr["lat1"].ToString(),
                    //                        lng1 = dr["lng1"].ToString(),
                    //                        lat2 = dr["lat2"].ToString(),
                    //                        lng2 = dr["lng2"].ToString(),
                    //                        lat3 = dr["lat3"].ToString(),
                    //                        lng3 = dr["lng3"].ToString(),
                    //                        lat4 = dr["lat4"].ToString(),
                    //                        lng4 = dr["lng4"].ToString(),
                    //                        lat5 = dr["lat5"].ToString(),
                    //                        lng5 = dr["lng5"].ToString(),
                    //                        lat6 = dr["lat6"].ToString(),
                    //                        lng6 = dr["lng6"].ToString(),
                    //                        lat7 = dr["lat7"].ToString(),
                    //                        lng7 = dr["lng7"].ToString(),
                    //                        lat8 = dr["lat8"].ToString(),
                    //                        lng8 = dr["lng8"].ToString(),
                    //                        lat9 = dr["lat9"].ToString(),
                    //                        lng9 = dr["lng9"].ToString()
                    //                    });
                    //                    addedGeofences.Add(key);
                    //                }
                    //            }
                    //        }
                    //    }
                    //}
                }
            }

            RouteResult result = new RouteResult
            {
                route = vehicleRoutes,
                geofencePoints = geofencePoints,
                geofencePointsIrregular = geofencePointsIrregular,
                halts = haltInfos,
                alarms = alarmInfos,
                totalDistance = Math.Round(grandTotalDistance, 1)
            };

            return JsonConvert.SerializeObject(result);
        }
        #endregion

        private void Get45or451FromRegistry()
        {
            string ver = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
            System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | TargetFrameworkName: " + ver + Environment.NewLine);
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\\"))
            {
                int releaseKey = Convert.ToInt32(ndpKey.GetValue("Release"));
                if (true)
                {
                    System.IO.File.AppendAllText(HostingEnvironment.ApplicationPhysicalPath + "/SecurityProtocalDiagnosisLog.txt", "DateTime: " + DateTime.Now.ToString() + " | Version: " + CheckFor45DotVersion(releaseKey) + Environment.NewLine);
                    Console.WriteLine("Version: " + CheckFor45DotVersion(releaseKey));
                }
            }
        }



        // Checking the version using >= will enable forward compatibility,  
        // however you should always compile your code on newer versions of 
        // the framework to ensure your app works the same. 
        private string CheckFor45DotVersion(int releaseKey)
        {
            if (releaseKey >= 528040)
            {
                return "4.8 or later";
            }
            if (releaseKey >= 461808)
            {
                return "4.7.2 or later";
            }
            if (releaseKey >= 461308)
            {
                return "4.7.1 or later";
            }
            if (releaseKey >= 460798)
            {
                return "4.7 or later";
            }
            if (releaseKey >= 394802)
            {
                return "4.6.2 or later";
            }
            if (releaseKey >= 394254)
            {
                return "4.6.1 or later";
            }
            if (releaseKey >= 393295)
            {
                return "4.6 or later";
            }
            if (releaseKey >= 393273)
            {
                return "4.6 RC or later";
            }
            if ((releaseKey >= 379893))
            {
                return "4.5.2 or later";
            }
            if ((releaseKey >= 378675))
            {
                return "4.5.1 or later";
            }
            if ((releaseKey >= 378389))
            {
                return "4.5 or later";
            }
            // This line should never execute. A non-null release key should mean 
            // that 4.5 or later is installed. 
            return "No 4.5 or later version detected";
        }


        public MetroDeliveryReturnRequest GetMetroDeliveryReturnRequest(string InvoiceNum, string status)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dtTripInvoices = ObjBll.MetroGetInvoiceDetailsByTaxInvoiceNum(InvoiceNum);
            for (int i = 0; i < dtTripInvoices.Rows.Count;)
            {
                DataRow rows = dtTripInvoices.Rows[i];
                var tripInvoice = new MetroDeliveryReturnRequest();
                {
                    //tripInvoice.CustomerID = rows["CustomerId"].ToString();
                    tripInvoice.storeId = Convert.ToInt32(InvoiceNum.Substring(0, 2).ToString());
                    //tripInvoice.storeId = Convert.ToInt32(rows["StoreNo"].ToString());
                    tripInvoice.consignmentId = rows["ConsignmentId"].ToString();
                    tripInvoice.deliverypointReturn = true;
                    tripInvoice.codAmount = rows["InvoiceAmountCollected"].ToString();

                    List<MetroReturnRequestEntryInput> tripInvoiceProducts = new List<MetroReturnRequestEntryInput>();
                    DataTable table2 = ObjBll.MetroTripSheetInvoiceProductData(rows["TaxInvoiceNum"].ToString());
                    for (int k = 0; k < table2.Rows.Count; k++)
                    {
                        DataRow productRows = table2.Rows[k];
                        var tripInvoiceProduct = new MetroReturnRequestEntryInput();
                        {
                            if (status == "Rejected")
                            {
                                tripInvoiceProduct.freebieText = "";
                                tripInvoiceProduct.itemId = productRows["ProductId"].ToString();
                                tripInvoiceProduct.quantity = Convert.ToInt32(productRows["TotalQuantity"].ToString());
                                tripInvoiceProduct.reasonNotes = "";
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(productRows["ReturnedQty"].ToString()))
                                {
                                    if (productRows["ReturnedQty"].ToString() != "0")
                                    {
                                        tripInvoiceProduct.freebieText = "";
                                        tripInvoiceProduct.itemId = productRows["ProductId"].ToString();
                                        tripInvoiceProduct.quantity = Convert.ToInt32(productRows["ReturnedQty"].ToString());
                                        tripInvoiceProduct.reasonNotes = "";
                                    }
                                }
                            }

                        }
                        ;

                        if (tripInvoiceProduct.quantity != 0)
                            tripInvoiceProducts.Add(tripInvoiceProduct);
                    }
                    tripInvoice.returnRequestEntryInputs = tripInvoiceProducts;
                }
                ;
                return tripInvoice;
            }
            return null;
        }

        public MetroPostDeliveryReturnDetails MetroPostDeliveryReturnDetails(string InvoiceNum)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataTable dtTripInvoices = ObjBll.MetroGetInvoiceDetailsByTaxInvoiceNum(InvoiceNum);
            for (int i = 0; i < dtTripInvoices.Rows.Count;)
            {
                DataRow rows = dtTripInvoices.Rows[i];
                var tripInvoice = new MetroPostDeliveryReturnDetails();
                {
                    ;
                    tripInvoice.consignmentId = rows["ConsignmentId"].ToString();
                    tripInvoice.returnId = rows["ReturnId"].ToString();
                    tripInvoice.date = Convert.ToDateTime(rows["Delivery_Status_Updated_On"].ToString()).ToString("dd'-'MM'-'yyyy hh:mm:ss tt");
                    tripInvoice.pickUpDetails = rows["DriverInfo"].ToString();

                    if (rows["Delivery_Status"].ToString() == "Return Picked Up" || rows["Delivery_Status"].ToString() == "Partially Picked Up")
                    {
                        tripInvoice.status = "RETURN_PICKED_UP";
                    }

                    List<ListOfProduct> tripInvoiceProducts = new List<ListOfProduct>();
                    DataTable table2 = ObjBll.MetroTripSheetInvoiceProductData(rows["TaxInvoiceNum"].ToString());
                    for (int k = 0; k < table2.Rows.Count; k++)
                    {
                        DataRow productRows = table2.Rows[k];
                        var tripInvoiceProduct = new ListOfProduct();
                        {
                            tripInvoiceProduct.productId = productRows["ProductId"].ToString();
                            if (rows["Delivery_Status"].ToString() == "Return Picked Up")
                            {
                                tripInvoiceProduct.pickedupQuantity = Convert.ToInt32(productRows["RequestedReturnQty"].ToString());
                            }
                            else if (rows["Delivery_Status"].ToString() == "Partially Picked Up")
                            {
                                tripInvoiceProduct.pickedupQuantity = Convert.ToInt32(productRows["ReturnedQty"].ToString());
                            }

                        }
                        ;
                        tripInvoiceProducts.Add(tripInvoiceProduct);
                    }
                    tripInvoice.listOfProducts = tripInvoiceProducts;
                }
                ;
                return tripInvoice;
            }
            return null;
        }
        public void ReceiveStreamDataFromFlespiFMC234(List<Flespi_FMC234Data> flespi_FMC234)
        {
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                var jsonSerializer = new JavaScriptSerializer();
                string jsonData = jsonSerializer.Serialize(flespi_FMC234);

                // Assuming only one object is in the list
                var message = flespi_FMC234.FirstOrDefault();
                if (message != null)
                {
                    string str_IMEI = message.Ident;
                    string str__Sim_IMEI = message.Ident;
                    double latitude = message.PositionLatitude;
                    double longitude = message.PositionLongitude;
                    bool ignitionstatus = message.EngineIgnitionStatus;
                    double speed = message.PositionSpeed;
                    double timestamp = message.Timestamp;
                    long unixSeconds = (long)timestamp;
                    int uid = 0;
                    string truckId = "";
                    int stop = ignitionstatus ? 0 : 1;
                    try
                    {
                        String[] uidTruckId;
                        if (str_IMEI == null)
                        {
                            uidTruckId = dbx.getTruckIdfromSimId(str__Sim_IMEI);
                        }
                        else
                        {
                            uidTruckId = dbx.getTruckIdfromIMEI(str_IMEI);
                        }
                        uid = Convert.ToInt32(uidTruckId[0]);
                        truckId = uidTruckId[1];
                    }
                    catch (Exception ex)
                    {
                        dbx.close(); // Consider logging exception here too
                    }
                    DateTime dt = ConvertUnixToIST(unixSeconds);
                    string ts = dbx.formatDBDate(dt);
                    //dbx.InsertFlespi234positiondata(truckId, speed, latitude, longitude, ts ,stop);

                    StringBuilder pduStr = new StringBuilder();

                    int altitude = 0;
                    int ginp1 = 0;
                    int ginp2 = 0;
                    int hvInp = 0;
                    byte temperature = 0;
                    byte inUse = 0;
                    byte accelerationInstances = 0;
                    byte decelerationInstances = 0;
                    bool weight_found = false;
                    int weight = 0;
                    ushort alarmData = 0;
                    ushort noGPSSignalDuration = 0;
                    int GSMsigstrength = 0;
                    byte GPRSDataKB = 0;
                    byte numSMS = 0;
                    byte numCallIns = 0;
                    byte numCallOuts = 0;
                    int timeZone = 5 * 60 * 60 + 30 * 60;
                    pduStr.Append("<pdu sender=\""
                       + uid //senderPhone
                       + "\" distance=\""
                       + ginp1
                       + "\" temperature=\""
                       + temperature
                       + "\" numIdle=\""
                       + hvInp
                       + "\" timeIdle=\""
                       + inUse
                       + "\" numAccel=\""
                       + accelerationInstances
                       + "\" numDecel=\""
                       + decelerationInstances
                       + "\" alarms=\"" + (weight_found ? string.Empty : "0x")
                       + (weight_found ? weight : alarmData)
                       + "\" noGPS=\""
                       + noGPSSignalDuration
                       + "\" curGSMSigStrength=\""
                       + GSMsigstrength          //curGSMSigStrength 
                       + "\" badSigCount=\""
                       + ginp2 //badSignalCount  Cement mixer drum rotation 
                       + "\" gprsDataKB=\""
                       + GPRSDataKB
                       + "\" numSMS=\""
                       + numSMS
                       + "\" numCallIns=\""
                       + numCallIns
                       + "\" numCallOuts=\""
                       + numCallOuts
                           + "\" altitude=\""
                       + altitude
                       + "\">\n");
                    pduStr.Append("    <alarms ");
                    pduStr.Append("/>\n");
                    pduStr.Append("<position date=\""
                                        + ts
                                        + "\" start=\""
                                        + 0
                                        //  + positiondata_StartA[j]   ignition data missing in the sample packet
                                        + "\" stop=\""
                                        + stop    //ignition data missing
                                        + "\" lat=\""
                                        + latitude
                                        + "\" long=\""
                                        + longitude
                                        + "\" speed=\""
                                        + speed
                                        + "\" fuel=\"");

                    pduStr.Append(0);                // + positiondata_fuelA[j]        fuel data missing in the sample packet

                    pduStr.Append("\" lac=\""
                            + "0"
                            + "\" ci=\""
                            + "0"
                            + "\"/>\n");

                    XmlDocument xmlPDUteltonika = new XmlDocument();
                    if (pduStr.Length != 0)
                    {
                        pduStr.Append("</pdu>\n");
                        //context.Response.Write("<BR> pduStr - " + pduStr.ToString());
                        xmlPDUteltonika.LoadXml(pduStr.ToString());
                        dbx.writeTrackerData(xmlPDUteltonika.FirstChild, truckId, "0");
                        dbx.writePositionTextFromParser(latitude, longitude, dt, truckId);
                        //Write to lastupdtdata
                        dbx.updateLastPositionData(truckId, dt, 0, stop, latitude, longitude, speed, 0, 0, 0, DateTime.UtcNow.AddSeconds(timeZone), 0, "80");

                    }
                }
                ObjBll.STL_RecordErrorMessage("Flespi - FMC234 :" + jsonData);
            }
            catch (Exception ex)
            {
                ObjBll.STL_RecordErrorMessage("Flespi - FMC234 Error: " + ex.Message);
            }
            WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
        }
        public void ReceiveStreamData(Stream inputStream)
        {
            // Read the HTTP POST request body
            using (StreamReader reader = new StreamReader(inputStream))
            {
                string json_string = reader.ReadToEnd();

                // Store the received JSON string in a file
                //File.WriteAllText("messages.json", json_string);
                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                //string dataAsString = Convert.ToBase64String(data);
                ObjBll.STL_RecordErrorMessage("FlespiTest - " + json_string);
            }

            // NOTE: it's important to send HTTP 200 OK status
            // to acknowledge successful messages delivery
            WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
        }
        public void ReceiveMessages(StreamData[] messages)
        {
            // Process the received messages
            foreach (var message in messages)
            {
                // Example: Log the device ident
                Console.WriteLine($"Received message from device: {message.ident}");
                BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
                //string dataAsString = Convert.ToBase64String(data);


                string str_IMEI = message.ident;
                double latitude = message.PositionLatitude;
                double longitude = message.PositionLongitude;
                bool ignitionstatus = message.EngineIgnitionStatus;
                double speed = message.Speed;
                long timestamp = message.timestamp;




                int uid = 0;
                string truckId = "";
                int stop = 0;
                if (ignitionstatus != false)
                    stop = 1;
                try
                {
                    String[] uidTruckId = dbx.getTruckIdfromIMEI(str_IMEI + "8");
                    uid = Convert.ToInt32(uidTruckId[0]);
                    truckId = uidTruckId[1];
                }
                catch (Exception ex)
                {
                    dbx.close();
                }

                DateTime dt = ConvertUnixToIST(timestamp);
                string ts = dbx.formatDBDate(dt);

                ObjBll.STL_RecordErrorMessage("FlespiTest - ident:" + message.ident + ", latitude: " + message.PositionLatitude + ", longitide: " + message.PositionLongitude + ", speed: " + message.Speed + ", ignitionstatus: " + message.EngineIgnitionStatus + ", uid: " + uid + ", truckid: " + truckId + ", TS: " + ts);

                StringBuilder pduStr = new StringBuilder();

                int altitude = 0;
                int ginp1 = 0;
                int ginp2 = 0;
                int hvInp = 0;
                byte temperature = 0;
                byte inUse = 0;
                byte accelerationInstances = 0;
                byte decelerationInstances = 0;
                bool weight_found = false;
                int weight = 0;
                ushort alarmData = 0;
                ushort noGPSSignalDuration = 0;
                int GSMsigstrength = 0;
                byte GPRSDataKB = 0;
                byte numSMS = 0;
                byte numCallIns = 0;
                byte numCallOuts = 0;

                pduStr.Append("<pdu sender=\""
                   + uid //senderPhone
                   + "\" distance=\""
                   + ginp1
                   + "\" temperature=\""
                   + temperature
                   + "\" numIdle=\""
                   + hvInp
                   + "\" timeIdle=\""
                   + inUse
                   + "\" numAccel=\""
                   + accelerationInstances
                   + "\" numDecel=\""
                   + decelerationInstances
                   + "\" alarms=\"" + (weight_found ? string.Empty : "0x")
                   + (weight_found ? weight : alarmData)
                   + "\" noGPS=\""
                   + noGPSSignalDuration
                   + "\" curGSMSigStrength=\""
                   + GSMsigstrength          //curGSMSigStrength 
                   + "\" badSigCount=\""
                   + ginp2 //badSignalCount  Cement mixer drum rotation 
                   + "\" gprsDataKB=\""
                   + GPRSDataKB
                   + "\" numSMS=\""
                   + numSMS
                   + "\" numCallIns=\""
                   + numCallIns
                   + "\" numCallOuts=\""
                   + numCallOuts
                       + "\" altitude=\""
                   + altitude
                   + "\">\n");
                pduStr.Append("    <alarms ");
                pduStr.Append("/>\n");



                pduStr.Append("<position date=\""
                                    + ts
                                    + "\" start=\""
                                    + 0
                                    //  + positiondata_StartA[j]   ignition data missing in the sample packet
                                    + "\" stop=\""
                                    + stop    //ignition data missing
                                    + "\" lat=\""
                                    + latitude
                                    + "\" long=\""
                                    + longitude
                                    + "\" speed=\""
                                    + speed
                                    + "\" fuel=\"");

                pduStr.Append(0);                // + positiondata_fuelA[j]        fuel data missing in the sample packet

                pduStr.Append("\" lac=\""
                        + "0"
                        + "\" ci=\""
                        + "0"
                        + "\"/>\n");

                XmlDocument xmlPDUteltonika = new XmlDocument();
                if (pduStr.Length != 0)
                {
                    pduStr.Append("</pdu>\n");
                    //context.Response.Write("<BR> pduStr - " + pduStr.ToString());
                    xmlPDUteltonika.LoadXml(pduStr.ToString());
                    dbx.writeTrackerData(xmlPDUteltonika.FirstChild, truckId, "0");
                    int timeZone = 5 * 60 * 60 + 30 * 60;

                    //Write to VehiclePostxt
                    dbx.writePositionTextFromParser(latitude, longitude, dt, truckId);
                    //Write to lastupdtdata
                    dbx.updateLastPositionData(truckId, dt, 0, stop, latitude, longitude, speed, 0, 0, 0, DateTime.UtcNow.AddSeconds(timeZone), 0, "80");
                }

            }
        }

        public void ReceiveDeviceData(List<S20DeviceData> deviceData)
        {
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            try
            {
                // Serialize the data to JSON format
                var jsonSerializer = new JavaScriptSerializer();
                string jsonData = jsonSerializer.Serialize(deviceData);

                ObjBll.STL_RecordErrorMessage("FlespiTest - S20  :" + jsonData);


            }
            catch (Exception ex)
            {
                ObjBll.STL_RecordErrorMessage("FlespiTest - S20  :" + ex.Message);
            }

            foreach (var message in deviceData)
            {
                //Store Data in the Database
                string str_IMEI = message.Ident;
                double latitude = message.PositionLatitude;
                double longitude = message.PositionLongitude;
                bool ignitionstatus = message.EngineIgnitionStatus;
                double speed = message.Speed;
                double timestamp = message.Timestamp;


                int uid = 0;
                string truckId = "";
                int stop = 0;
                if (ignitionstatus != false)
                    stop = 1;
                try
                {
                    String[] uidTruckId = dbx.getTruckIdfromIMEI(str_IMEI + "8");
                    uid = Convert.ToInt32(uidTruckId[0]);
                    truckId = uidTruckId[1];
                }
                catch (Exception ex)
                {
                    dbx.close();
                }

                DateTime dt = ConvertToIST(timestamp);
                string ts = dbx.formatDBDate(dt);

                StringBuilder pduStr = new StringBuilder();

                int altitude = 0;
                int ginp1 = 0;
                int ginp2 = 0;
                int hvInp = 0;
                byte temperature = 0;
                byte inUse = 0;
                byte accelerationInstances = 0;
                byte decelerationInstances = 0;
                bool weight_found = false;
                int weight = 0;
                ushort alarmData = 0;
                ushort noGPSSignalDuration = 0;
                int GSMsigstrength = 0;
                byte GPRSDataKB = 0;
                byte numSMS = 0;
                byte numCallIns = 0;
                byte numCallOuts = 0;

                pduStr.Append("<pdu sender=\""
                   + uid //senderPhone
                   + "\" distance=\""
                   + ginp1
                   + "\" temperature=\""
                   + temperature
                   + "\" numIdle=\""
                   + hvInp
                   + "\" timeIdle=\""
                   + inUse
                   + "\" numAccel=\""
                   + accelerationInstances
                   + "\" numDecel=\""
                   + decelerationInstances
                   + "\" alarms=\"" + (weight_found ? string.Empty : "0x")
                   + (weight_found ? weight : alarmData)
                   + "\" noGPS=\""
                   + noGPSSignalDuration
                   + "\" curGSMSigStrength=\""
                   + GSMsigstrength          //curGSMSigStrength 
                   + "\" badSigCount=\""
                   + ginp2 //badSignalCount  Cement mixer drum rotation 
                   + "\" gprsDataKB=\""
                   + GPRSDataKB
                   + "\" numSMS=\""
                   + numSMS
                   + "\" numCallIns=\""
                   + numCallIns
                   + "\" numCallOuts=\""
                   + numCallOuts
                       + "\" altitude=\""
                   + altitude
                   + "\">\n");
                pduStr.Append("    <alarms ");
                pduStr.Append("/>\n");



                pduStr.Append("<position date=\""
                                    + ts
                                    + "\" start=\""
                                    + 0
                                    //  + positiondata_StartA[j]   ignition data missing in the sample packet
                                    + "\" stop=\""
                                    + stop    //ignition data missing
                                    + "\" lat=\""
                                    + latitude
                                    + "\" long=\""
                                    + longitude
                                    + "\" speed=\""
                                    + speed
                                    + "\" fuel=\"");

                pduStr.Append(0);                // + positiondata_fuelA[j]        fuel data missing in the sample packet

                pduStr.Append("\" lac=\""
                        + "0"
                        + "\" ci=\""
                        + "0"
                        + "\"/>\n");

                XmlDocument xmlPDUteltonika = new XmlDocument();
                if (pduStr.Length != 0)
                {
                    pduStr.Append("</pdu>\n");
                    //context.Response.Write("<BR> pduStr - " + pduStr.ToString());
                    xmlPDUteltonika.LoadXml(pduStr.ToString());
                    dbx.writeTrackerData(xmlPDUteltonika.FirstChild, truckId, "0");
                    int timeZone = 5 * 60 * 60 + 30 * 60;

                    //Write to VehiclePostxt
                    dbx.writePositionTextFromParser(latitude, longitude, dt, truckId);
                    //Write to lastupdtdata
                    dbx.updateLastPositionData(truckId, dt, 0, stop, latitude, longitude, speed, 0, 0, 0, DateTime.UtcNow.AddSeconds(timeZone), 0, "80");

                }
            }

            WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
        }

        public DateTime ConvertToIST(double unixTimestamp)
        {
            // Convert Unix timestamp to UTC
            // DateTime utcDateTime = DateTimeOffset.FromUnixTimeSeconds((long)unixTimestamp).UtcDateTime;
            DateTime utcDateTime = DateTime.Now;
            // Convert UTC to IST (UTC+5:30)
            TimeZoneInfo istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime istDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, istTimeZone);

            return istDateTime;
        }
        public DateTime ConvertUnixToIST(long unixTimestamp)
        {
            // Unix Epoch time(1970 - 01 - 01 00:00:00 UTC)
            DateTime unixEpochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Add the Unix timestamp as seconds to Unix Epoch time
            DateTime dateTimeUtc = unixEpochTime.AddSeconds(unixTimestamp);

            // Get IST TimeZoneInfo
            TimeZoneInfo istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            // Convert UTC DateTime to IST
            DateTime dateTimeIst = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, istTimeZone);

            return dateTimeIst;
        }
        //Modified  for BIAL  08/07/2024 sahana
        public List<NotReachable> GetNotReachableVehicles(string Key, string clientId, string timeperiod, string format)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(Key, "31", format, clientId);
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string AccountName = objbll.GetAccountName(sessionId);
                int groupid = objbll.getGroupId(sessionId);
                if (groupid == 0)
                {
                    ErrorMessage customError = new ErrorMessage("200", "FAIL:USER NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                List<NotReachable> result = new List<NotReachable>();
                if (string.IsNullOrEmpty(timeperiod))
                {
                    ErrorMessage customError = new ErrorMessage("200", "FAIL:TIMEPERIOD_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                DateTime fromDate = DateTime.UtcNow.AddSeconds(objbll.GetTimeZoneDiff()).AddHours(-Convert.ToInt32(timeperiod));
                DataTable dt = objbll.GetLastPositionByBetweenTime(Convert.ToString(groupid), fromDate.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"), fromDate.ToString("yyyy-MM-dd HH:mm:ss"), "1", AccountName);
                //.AddYears(-10) for all not reachable data
                dt = CheckPosition(dt);

                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        NotReachable Datas = new NotReachable
                        {
                            vehicle = row["VehicleId"].ToString(),
                            uid = row["UID"].ToString(),
                            timestamp = row["timestamp"].ToString(),
                            location = row["positiontxt"].ToString(),
                            latitude = row["latitude"].ToString(),
                            longitude = row["longitude"].ToString(),
                            flag = "1"
                        };
                        result.Add(Datas);
                    }
                    return result;
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATA NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
            }
            catch (WebFaultException<ErrorMessage> ex)
            {
                // Rethrow WebFaultException with custom error message
                throw ex;
            }
            catch (Exception ex)
            {
                // Handle other exceptions with a generic error message
                ErrorMessage customError = new ErrorMessage("500", "FAIL:INTERNAL SERVER ERROR");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }//added for get vehicle exact location (not reachable vehicle)
        public DataTable CheckPosition(DataTable dt)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    if (string.IsNullOrEmpty(dr[3].ToString()))
                    {
                        if (!string.IsNullOrEmpty(dr[4].ToString()) && !string.IsNullOrEmpty(dr[5].ToString()))
                            dr[3] = objBll.getPositionTxt(dr[4].ToString(), dr[5].ToString(), 1);
                        dt.AcceptChanges();
                    }
                    else
                        dr[3] = dr[3].ToString();
                }
            }
            return dt;
        }

        //added for BIAL VIOLATION REPORT -SAHANA
        public ViolationData GetViolationReport(string Key, string clientId, string fromDate, string toDate, string VehicleId, string viotype, string format)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(Key, "32", format, clientId);
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string AccountName = objbll.GetAccountName(sessionId);
                string allviolations = "";
                int groupid = objbll.getGroupId(sessionId);
                if (groupid == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:USER NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (VehicleId == "All")
                {
                    VehicleId = "";
                }
                if (viotype == "All")
                {
                    allviolations = "'OVERSPEEDING','HARSH BRAKING','HIGH ACCELERATION','SHORT RUNTIME','TOTAL RUNTIME','NIGHT DRIVING','MAIN DISCONNECTED DRIVING','DUTY TIME'";

                }
                else
                {
                    allviolations = viotype;
                }
                List<VioData> Vioreports = new List<VioData>();
                DataTable data = objbll.GetAPIViolationReport(fromDate, toDate, allviolations, VehicleId, sessionId);
                if (data != null && data.Rows.Count > 0)
                {
                    foreach (DataRow row in data.Rows)
                    {
                        VioData vioreport = new VioData
                        {
                            vehicle = row["vehicleId"].ToString(),
                            uid = row["uid"].ToString(),
                            alertType = row["type"].ToString(),
                            alertDateTime = row["datetime"].ToString(),
                            location = row["Location"].ToString(),
                            latitude = row["latitude"].ToString(),
                            longitude = row["longitude"].ToString(),
                            alertThreshold = row["alertThreshold"].ToString(),
                        };

                        // Check the value of the 'type' column and add corresponding columns
                        string type = row["type"].ToString();
                        switch (type)
                        {
                            case "OVERSPEEDING":
                                vioreport.speed = row["remarks"].ToString();
                                break;
                            case "HARSH BRAKING":
                                vioreport.hbvalue = row["remarks"].ToString(); // Assuming you have a property named 'hbvalue' in VioData class
                                break;
                            case "HIGH ACCELERATION":
                                vioreport.havalue = row["remarks"].ToString(); // Assuming you have a property named 'havalue' in VioData class
                                break;
                            case "NIGHT DRIVING":
                                vioreport.duration = row["duration"].ToString() + " mins";
                                break;
                            case "SHORT RUNTIME":
                                vioreport.remarks = row["remarks"].ToString();
                                break;
                            default:
                                // Handle any other types here
                                break;
                        }

                        Vioreports.Add(vioreport);
                    }


                    ViolationData violationdata = new ViolationData
                    {
                        DATAELEMENTS = Vioreports
                    };
                    return violationdata;
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATA NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
            }
            catch (WebFaultException<ErrorMessage> ex)
            {
                // Rethrow WebFaultException with custom error message
                throw ex;
            }
            catch (Exception ex)
            {
                // Handle other exceptions with a generic error message
                ErrorMessage customError = new ErrorMessage("500", "FAIL:INTERNAL SERVER ERROR");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }

        // Addedfor BIAL distance 18-03-2024
        // Addedfor BIAL distance 18-03-2024
        public List<DistanceData> BIALDistanceReport(string Key, string clientId, string vehicleId, string uid, string timePeriod, string format)
        {
            DateTime to = DateTime.Now;
            DateTime from = to.AddHours(-Convert.ToInt32(timePeriod));
            string fromDate = Convert.ToString(from);
            string toDate = Convert.ToString(to);
            string access = AuthenticateAPIKey(Key, "33", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(vehicleId) && string.IsNullOrEmpty(uid))
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                else if (string.IsNullOrEmpty(timePeriod))
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:TIMEPERIOD_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    if (uid.ToLower() == "all")
                    {
                        vehicleId = usersvehicles;
                    }
                    else
                    {
                        string[] uids = uid.Split(',');
                        for (int i = 0; i < uids.Length; i++)
                        {
                            try
                            {
                                vehicleId += dbx.gettruckidfromUID(Convert.ToInt32(uids[i])) + ",";
                            }
                            catch { }
                        }
                    }
                }
                else if (vehicleId.ToLower() == "all")
                {
                    vehicleId = usersvehicles;
                }
                if ((to - from).Hours > 1)
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:TIMEPERIOD_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                List<DistanceData> dlist = new List<DistanceData>();
                string[] vehicleids = vehicleId.Split(',');
                foreach (string vehId in vehicleids)
                {
                    if (!string.IsNullOrEmpty(vehId))
                    {
                        string[] splitVeh = usersvehicles.Split(',');
                        if (splitVeh.Any(v => v.Replace(" ", "") == vehId.Replace(" ", "")))
                        {
                            try
                            {
                                DateTime currentFrom = from;
                                DateTime currentTo = currentFrom.AddMinutes(15); // Add 15 minutes initially

                                while (currentFrom < to) // Iterate until the end time
                                {
                                    // Fetch data for the current time interval
                                    object[] dist = objBll.accumulateDistance(vehId, currentFrom.ToString("yyyy-MM-dd HH:mm:ss"), currentTo.ToString("yyyy-MM-dd HH:mm:ss"), null);

                                    string lat = dist[3].ToString();
                                    if (!string.IsNullOrEmpty(lat))
                                    {
                                        // Create a DistanceData object and add it to the list
                                        DistanceData d = new DistanceData();
                                        d.uid = dbx.getUidFromConfigdata(vehId).ToString();
                                        d.vehicle = vehId;
                                        d.fromdate = currentFrom.ToString("yyyy-MM-dd HH:mm:ss");
                                        d.todate = currentTo.ToString("yyyy-MM-dd HH:mm:ss");
                                        d.distance = Math.Round(Convert.ToDecimal(dist[0]), 2).ToString();
                                        d.startlatlng = dist[3].ToString();
                                        d.endlatlng = dist[4].ToString();
                                        dlist.Add(d);
                                    }

                                    // Move to the next 15-minute interval
                                    currentFrom = currentTo;
                                    currentTo = currentTo.AddMinutes(15);

                                    // Check if currentTo has reached or exceeded the end time
                                    if (currentTo >= to)
                                    {
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log the exception or handle it as needed
                                Console.WriteLine($"An error occurred: {ex.Message}");
                            }

                        }
                    }
                }
                if (dlist.Count == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:NO_DATA_FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                return dlist;
            }
            finally
            {
                dbx.close();
            }
        }
        //modified for BIAL by sahana  22-07-2024
        public List<HaltData> BIALHaltReport(string Key, string clientId, string vehicleId, int maxHaltDuration, string type, string timePeriod, string format)
        {
            string access = AuthenticateAPIKey(Key, "35", format, clientId);
            try
            {
                DateTime to = DateTime.Now;
                DateTime from = to.AddHours(-Convert.ToInt32(timePeriod));
                string fromDate = Convert.ToString(from);
                string toDate = Convert.ToString(to);
                if (string.IsNullOrEmpty(timePeriod))
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:TIMEPERIOD_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                else if (string.IsNullOrEmpty(vehicleId))
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                if (type == "")
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:TYPE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                string Vehicles = "";
                if (vehicleId == "All")
                {
                    Vehicles = usersvehicles;
                }
                else
                {
                    Vehicles = vehicleId;
                }
                string[] vehicleIds = Vehicles.Split(',');
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:TIMEPERIOD_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                List<HaltData> hlist = new List<HaltData>();

                DataTable lHalt = writeBIALHaltReport(sessionId, vehicleIds, maxHaltDuration, fromDate, toDate, type);
                if (lHalt != null && lHalt.Rows.Count > 0)
                {
                    foreach (DataRow row in lHalt.Rows)
                    {
                        string vehicle = row["VehicleId"].ToString();
                        string uid = dbx.getUidFromConfigdata(vehicle).ToString();
                        HaltData data = new HaltData
                        {
                            vehicle = row["VehicleId"].ToString(),
                            uid = uid,
                            fromdate = row["FromDate"].ToString(),
                            todate = row["ToDate"].ToString(),
                            location = row["Location"].ToString(),
                            type = row["Type"].ToString(),
                            eventTime = row["EventTime"].ToString()
                        };

                        hlist.Add(data);
                    }

                    return hlist;
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "DATA NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
            }
            catch (WebFaultException<ErrorMessage> ex)
            {
                // Rethrow WebFaultException with custom error message
                throw ex;
            }
            finally
            {
                dbx.close();
            }
        }
        protected DataTable writeBIALHaltReport(string sessionId, string[] vehicleIds, int minHaltDuration, string fromDate, string toDate, string type)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("VehicleId", typeof(string));
            dataTable.Columns.Add("FromDate", typeof(string));
            dataTable.Columns.Add("Location", typeof(string));
            dataTable.Columns.Add("ToDate", typeof(string));
            dataTable.Columns.Add("Type", typeof(string));
            dataTable.Columns.Add("EventTime", typeof(string));

            foreach (string vehicleId in vehicleIds)
            {
                Object[] values = objBll.getRouteData(fromDate, toDate, vehicleId, false);

                for (int i = 0; i < values.Length; i++)
                {
                    object[] haltInfoList = objBll.showHalt2(i, values, true, minHaltDuration);

                    if (haltInfoList != null)
                    {
                        foreach (object[] haltInfo in haltInfoList)
                        {
                            // Assuming the structure of haltInfo is [FromDate, FromTime, Location, ToDate, ToTime, Remarks]
                            string fromDateTime = Convert.ToDateTime(haltInfo[0]).ToString("yyyy-MM-dd") + " " + Convert.ToDateTime(haltInfo[1]).ToString("HH:mm:ss");
                            string toDateTime = Convert.ToDateTime(haltInfo[5]).ToString("yyyy-MM-dd") + " " + Convert.ToDateTime(haltInfo[6]).ToString("HH:mm:ss");
                            string Remarks = haltInfo[3].ToString();
                            string[] splitRemark = Remarks.Split(':');
                            if (Remarks != "" && type == splitRemark[0])
                            {
                                double eventTimeInMinutes = ConvertToMinutes(splitRemark[1]);
                                if (eventTimeInMinutes >= 1)
                                {
                                    dataTable.Rows.Add(vehicleId, fromDateTime, haltInfo[2].ToString(), toDateTime, splitRemark[0], eventTimeInMinutes.ToString() + " mins");

                                }
                            }
                            else if (Remarks != "" && type == "All")
                            {
                                double eventTimeInMinutes = ConvertToMinutes(splitRemark[1]);
                                if (eventTimeInMinutes >= 1)
                                {
                                    dataTable.Rows.Add(vehicleId, fromDateTime, haltInfo[2].ToString(), toDateTime, splitRemark[0], eventTimeInMinutes.ToString() + " mins");
                                }
                            }
                        }
                    }
                }
            }

            return dataTable;
        }
        public static double ConvertToMinutes(string timeString)
        {
            double totalMinutes = 0;
            string[] timeParts = timeString.Split(',');

            foreach (var part in timeParts)
            {
                if (part.Contains("hr"))
                {
                    totalMinutes += double.Parse(part.Replace("hr", "").Trim()) * 60;
                }
                else if (part.Contains("min"))
                {
                    totalMinutes += double.Parse(part.Replace("min", "").Trim());
                }
                else if (part.Contains("sec"))
                {
                    totalMinutes += double.Parse(part.Replace("sec", "").Trim()) / 60;
                }
            }

            return Math.Ceiling(totalMinutes);
        }

        //written by sahana  22-03-2024
        public List<HaltData> HaltReport(string Key, string clientId, string vehicleId, string fromDate, string toDate, int maxHaltDuration, string format)
        {
            string access = AuthenticateAPIKey(Key, "35", format, clientId);
            try
            {
                if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                if (string.IsNullOrEmpty(vehicleId))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:VEHICLE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string usersvehicles = objBll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                string Vehicles = "";
                if (vehicleId == "All")
                {
                    Vehicles = usersvehicles;
                }
                else
                {
                    Vehicles = vehicleId;
                }
                string[] vehicleIds = Vehicles.Split(',');
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                try
                {
                    from = Convert.ToDateTime(fromDate);
                    to = Convert.ToDateTime(toDate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }

                if ((to - from).TotalDays > 15)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<HaltData> hlist = new List<HaltData>();

                DataTable lHalt = writeHaltReport(sessionId, vehicleIds, maxHaltDuration, fromDate, toDate);
                if (lHalt != null && lHalt.Rows.Count > 0)
                {
                    foreach (DataRow row in lHalt.Rows)
                    {
                        string Vehicleid = row["VehicleId"].ToString();
                        string uid = dbx.getUidFromConfigdata(Vehicleid).ToString();
                        HaltData Datas = new HaltData
                        {
                            vehicle = row["VehicleId"].ToString(),
                            uid = uid,
                            fromdate = row["FromDate"].ToString(),
                            location = row["Location"].ToString(),
                            todate = row["ToDate"].ToString(),
                            remarks = row["Remarks"].ToString()
                        };

                        hlist.Add(Datas);
                    }
                    return hlist;
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATA NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
            }
            catch (WebFaultException<ErrorMessage> ex)
            {
                // Rethrow WebFaultException with custom error message
                throw ex;
            }
            finally
            {
                dbx.close();
            }
        }
        protected DataTable writeHaltReport(string sessionId, string[] vehicleIds, int minHaltDuration, string fromDate, string toDate)
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("VehicleId", typeof(string));
            dataTable.Columns.Add("FromDate", typeof(string));
            dataTable.Columns.Add("Location", typeof(string));
            dataTable.Columns.Add("ToDate", typeof(string));
            dataTable.Columns.Add("Remarks", typeof(string));

            foreach (string vehicleId in vehicleIds)
            {
                Object[] values = objBll.getRouteData(fromDate, toDate, vehicleId, false);

                for (int i = 0; i < values.Length; i++)
                {
                    object[] haltInfoList = objBll.showHalt2(i, values, true, minHaltDuration);

                    if (haltInfoList != null)
                    {
                        foreach (object[] haltInfo in haltInfoList)
                        {
                            // Assuming the structure of haltInfo is [FromDate, FromTime, Location, ToDate, ToTime, Remarks]
                            string fromDateTime = Convert.ToDateTime(haltInfo[0]).ToString("yyyy-MM-dd") + " " + Convert.ToDateTime(haltInfo[1]).ToString("HH:mm:ss");
                            string toDateTime = Convert.ToDateTime(haltInfo[5]).ToString("yyyy-MM-dd") + " " + Convert.ToDateTime(haltInfo[6]).ToString("HH:mm:ss");
                            string Remarks = haltInfo[3].ToString();
                            if (Remarks != "")
                            {
                                dataTable.Rows.Add(vehicleId, fromDateTime, haltInfo[2].ToString(), toDateTime, haltInfo[3].ToString());
                            }
                        }
                    }
                }
            }

            return dataTable;
        }

        //Added for BIAL 27-03-2024 sahana
        public List<GeofenceData> GetGeofenceAlert(string Key, string vehicleId, string clientId, string fromDate, string toDate, string format)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(Key, "35", format, clientId);
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string AccountName = objbll.GetAccountName(sessionId);
                string usersvehicles = objbll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                int groupid = objbll.getGroupId(sessionId);
                if (groupid == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:USER NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                List<GeofenceData> result = new List<GeofenceData>();
                string vehicles = "";
                if (vehicleId == "All")
                {
                    vehicles = objbll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");
                }
                else
                {
                    vehicles = vehicleId;
                }
                DataTable dt = objbll.GetVehicleInPlant(vehicles, fromDate, toDate, "0,1,2,22");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string Vehicleid = row["vehicleid"].ToString();
                        string Uid = dbx.getUidFromConfigdata(Vehicleid).ToString();
                        GeofenceData Datas = new GeofenceData
                        {
                            vehicle = row["vehicleid"].ToString(),
                            uid = Uid,
                            location = row["locationstr"].ToString(),
                            intime = row["intimestamp"].ToString(),
                            outtime = row["outtimestamp"].ToString()
                        };
                        result.Add(Datas);
                    }
                    return result;
                }
                else
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATA NOT FOUND");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
            }
            catch (WebFaultException<ErrorMessage> ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                ErrorMessage customError = new ErrorMessage("500", "FAIL:INTERNAL SERVER ERROR");
                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.InternalServerError);
            }
            finally
            {
                dbx.close();
            }
        }
        public Stream GetMapRulesData(MapRulesEntity map)
        {
            string VehicleId = "";
            var jsonResponse = "";
            object finalResponse = null;
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
            List<PositionData1> res = new List<PositionData1>();
            List<Response> responses = new List<Response>();
            List<ShowRouteResponse> responseList = new List<ShowRouteResponse>();

            if (map?.map_requests?.Any(r => r.request_type == "ShowPosition") == true)
            {
                // Check if map_requests and vehicle_data are not null
                if (map?.map_requests != null)
                {
                    foreach (var mapRequest in map.map_requests)
                    {
                        if (mapRequest.vehicle_data != null)
                        {
                            foreach (var VehicleList in mapRequest.vehicle_data)
                            {
                                try
                                {
                                    VehicleId = VehicleList;

                                    string SessionId = objbll.getSessionIdByVehicle(VehicleId);
                                    Object[] selectedValues = dbx.getLastPositions(SessionId, null);
                                    DateTime now = DateTime.UtcNow.AddSeconds(dbx.GetTimeZoneDiff(SessionId));
                                    DataTable accconfig = objbll.GetAccountGroupConfig(SessionId);
                                    int gid = dbx.getGroupId(SessionId);
                                    string BatteryStatusinLand = "0";
                                    string ShowAuxInputinLanding = "0";
                                    String maxGpsDelayStr = "";
                                    String maxGprsDelayStr = "";
                                    int maxSpeedDisplayTime = 300;
                                    double maxSpeed = 70.0;
                                    string InfoColor = "black";
                                    string Marker = "black.png";
                                    if (gid != 0)
                                        maxSpeed = Convert.ToDouble(dbx.getMaxSpeed(gid));
                                    string Datetime = null;
                                    int timeZone = objbll.GetTimeZoneDiff(SessionId);
                                    DateTime endDate = DateTime.UtcNow;
                                    endDate = endDate.AddSeconds(timeZone);//for converting in to IST
                                    if (Datetime != null)
                                        endDate = Convert.ToDateTime(Datetime);
                                    if (accconfig.Rows.Count > 0)
                                    {
                                        BatteryStatusinLand = Convert.ToString(accconfig.Rows[0]["ShowBatteryStatusinLand"]);
                                        ShowAuxInputinLanding = Convert.ToString(accconfig.Rows[0]["ShowAuxInputinLanding"]);
                                    }

                                    if (!string.IsNullOrEmpty(VehicleId))
                                    {
                                        VehicleId = VehicleId.ToLower().Replace(" ", "");
                                    }
                                    DataTable dtNrInterval = dbx.getNRconfigurationByAccount(SessionId);
                                    if (dtNrInterval != null && dtNrInterval.Rows.Count > 0)
                                    {
                                        if (!string.IsNullOrEmpty(dtNrInterval.Rows[0]["NRInterval"].ToString()))
                                        {
                                            int NRInhrs = Convert.ToInt32(dtNrInterval.Rows[0]["NRInterval"]);
                                            int NRintervalInSeconds = NRInhrs * 3600;
                                            maxGprsDelayStr = NRintervalInSeconds.ToString();
                                            maxGpsDelayStr = maxGprsDelayStr;
                                        }

                                    }
                                    for (int i = 0; i < selectedValues.Length; i++)
                                    {
                                        string veh = (((Object[])selectedValues[i])[(int)headersIndex.trucksIndex]).ToString();
                                        if (!string.IsNullOrEmpty(VehicleId) && VehicleId != "all")
                                        {
                                            if (!VehicleId.Contains(veh.ToLower().Replace(" ", "")))
                                                continue;
                                        }

                                        PositionData1 p = new PositionData1();
                                        string latStr = (((Object[])selectedValues[i])[(int)headersIndex.latitudeIndex]).ToString();
                                        string longStr = (((Object[])selectedValues[i])[(int)headersIndex.longitudeIndex]).ToString();
                                        double speed = Convert.ToDouble(((Object[])selectedValues[i])[(int)headersIndex.speedIndex]);
                                        string timestamp = (((Object[])selectedValues[i])[(int)headersIndex.timesIndex]).ToString();
                                        string vehinfo = (((Object[])selectedValues[i])[(int)headersIndex.infoIndex]).ToString();
                                        string stop = (((Object[])selectedValues[i])[(int)headersIndex.StopIndex]).ToString();
                                        string TempSense = (((Object[])selectedValues[i])[11]).ToString();
                                        string OnOffTypeVal = (((Object[])selectedValues[i])[10]).ToString();
                                        string hvinp = "0";
                                        if ((((Object[])selectedValues[i])[13]) != null)
                                            hvinp = (((Object[])selectedValues[i])[13]).ToString();

                                        string gvinp = "0";
                                        if ((((Object[])selectedValues[i])[14]) != null)
                                            gvinp = (((Object[])selectedValues[i])[14]).ToString();

                                        string direction = "0";
                                        if ((((Object[])selectedValues[i])[8]) != null)
                                        {
                                            direction = (Convert.ToInt32((((Object[])selectedValues[i])[8]).ToString()) % 360).ToString();
                                        }

                                        string positionsTxt = string.Empty;
                                        object[] PositiontxtData = dbx.getLatestPositiontxt(SessionId);
                                        if (PositiontxtData != null)
                                        {
                                            int len = PositiontxtData.Length;
                                            for (int k = 0; k < len; k++)
                                            {
                                                if ((((Object[])PositiontxtData[k])[0]).ToString() == veh)
                                                {
                                                    try
                                                    {
                                                        if (Convert.ToDateTime(timestamp) < Convert.ToDateTime((((Object[])PositiontxtData[k])[2]).ToString()).AddMinutes(5))
                                                            positionsTxt = (((Object[])PositiontxtData[k])[1]).ToString();
                                                        else
                                                            positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                                                    }
                                                    catch { }
                                                    break;
                                                }
                                            }
                                            if (positionsTxt == "")
                                            {
                                                positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                                            }
                                        }
                                        else
                                        {
                                            positionsTxt = dbx.getPositionTxt(latStr, longStr, 3);
                                        }

                                        int uid = dbx.getUidFromConfigdata(veh);

                                        String Remarks = null;
                                        // Get last GPS access time we can get it from the positiondata which is acesssed by getLastPositions() in xmldb
                                        String timestamp_last_GPS_update = (((Object[])selectedValues[i])[(int)headersIndex.timesIndex]).ToString();
                                        DateTime GPS_last_update_date = DateTime.Parse(timestamp_last_GPS_update);

                                        int StopBit = int.Parse((((Object[])selectedValues[i])[(int)headersIndex.StopIndex]).ToString());
                                        Object[] GPRSvalues = dbx.getDisconnectedVehicles(SessionId, 0);
                                        DateTime timestamp_last_GPRS_update_date = GPS_last_update_date;
                                        if (Datetime != null)
                                        {
                                            timestamp_last_GPRS_update_date = GPS_last_update_date;
                                        }
                                        else
                                        {
                                            for (int j = 0; j < GPRSvalues.Length; j++)
                                            {
                                                if ((((Object[])selectedValues[i])[(int)headersIndex.trucksIndex]).ToString().Equals((((Object[])GPRSvalues[j])[0]).ToString()))
                                                {
                                                    // GPRS last access time
                                                    timestamp_last_GPRS_update_date = DateTime.Parse((((Object[])GPRSvalues[j])[2]).ToString());
                                                    break;
                                                }
                                            }
                                        }
                                        if ((endDate - timestamp_last_GPRS_update_date).TotalSeconds < Convert.ToInt32(maxGprsDelayStr))//>>>>>>>>>>>>>>>>>>>>For testing TotalMinutes to TotalDays
                                        {
                                            if (Math.Abs((endDate - GPS_last_update_date).TotalSeconds) > Convert.ToInt32(maxGpsDelayStr))//>>>>>>>>>>>>>>>>>>>>For testing TotalMinutes to TotalDays
                                            {
                                                speed = 0; ((Object[])selectedValues[i])[(int)headersIndex.speedIndex] = 0;
                                                ((Object[])selectedValues[i])[(int)headersIndex.timesIndex] = timestamp_last_GPRS_update_date.ToString("dd MMM yy,hh:mm:ss tt");
                                                Remarks = Remarks + "GPS Not Active,";
                                            }
                                            else
                                            {
                                                if ((endDate - timestamp_last_GPRS_update_date).TotalSeconds > maxSpeedDisplayTime || Math.Abs((endDate - GPS_last_update_date).TotalSeconds) > Convert.ToInt32(maxGprsDelayStr))//Added on 13-7-2014 in the request that the speed should be zero if it is not rechable for more then 20 Mins
                                                { speed = 0; ((Object[])selectedValues[i])[(int)headersIndex.speedIndex] = 0; }
                                                if (StopBit == 1)
                                                {
                                                    if (TempSense == "SLOW" && (OnOffTypeVal == "MACHINE" || OnOffTypeVal == "ENGINE"))
                                                        Remarks = Remarks + "Vehicle OFF,";//Vehicle OFF //Asp PerBEML-PSR Req 
                                                    else
                                                        Remarks = Remarks + "Vehicle Halted,";//Vehicle Halted
                                                    speed = 0;
                                                    ((Object[])selectedValues[i])[(int)headersIndex.speedIndex] = 0;
                                                }
                                                else if (speed <= 3)
                                                {
                                                    if (TempSense == "SLOW" && (OnOffTypeVal == "MACHINE" || OnOffTypeVal == "ENGINE"))
                                                        Remarks = Remarks + "Vehicle ON,";//Vehicle ON //Asp PerBEML-PSR Req 
                                                    else
                                                        Remarks = Remarks + "Vehicle Idling,";//Vehicle Idling
                                                    speed = 0; ((Object[])selectedValues[i])[(int)headersIndex.speedIndex] = 0;
                                                }
                                                else
                                                {
                                                    if (TempSense == "SLOW" && (OnOffTypeVal == "MACHINE" || OnOffTypeVal == "ENGINE") && speed > 3)
                                                        Remarks = Remarks + "VM,";
                                                    else if (TempSense == "SLOW" && (OnOffTypeVal == "MACHINE" || OnOffTypeVal == "ENGINE"))
                                                        Remarks = Remarks + "Vehicle ON,";//Vehicle ON //Asp PerBEML-PSR Req                                         
                                                    else if (speed > maxSpeed)
                                                        Remarks = Remarks + "Over Speeding,";//Over Speeding
                                                    else
                                                        Remarks = Remarks + "Vehicle Moving,";//Vehicle Moving
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if ((endDate - timestamp_last_GPRS_update_date).TotalSeconds > maxSpeedDisplayTime)//Added on 13-7-2014 in the request that the speed should be zero if it is not rechable for more then 20 Mins
                                            { speed = 0; ((Object[])selectedValues[i])[(int)headersIndex.speedIndex] = 0; }
                                            Remarks = Remarks + "Not Reachable,";//Not Reachable
                                            ((Object[])selectedValues[i])[(int)headersIndex.speedIndex] = 0;

                                        }
                                        if (Remarks.Contains("Not Reachable") || Remarks.Contains("GPS Not Active") || Remarks.Contains("Not Reporting"))
                                        {
                                            InfoColor = "black";
                                            Marker = "black.png";
                                        }
                                        if (Remarks.Contains("Vehicle Idling") || Remarks.Contains("Vehicle ON") || Remarks.Contains("Vehicle Moving") || Remarks.Contains("Reporting"))
                                        {
                                            InfoColor = "green";
                                            Marker = "green.png";
                                        }
                                        if (Remarks.Contains("Vehicle Halted"))
                                        {
                                            InfoColor = "red";
                                            Marker = "red.png";
                                        }
                                        if (Remarks.Contains("Over Speeding"))
                                        {
                                            InfoColor = "gold";
                                            Marker = "yellow.png";
                                        }

                                        p.vehicle = veh;
                                        p.uid = uid.ToString();
                                        p.vehicleinfo = vehinfo;
                                        p.gpsupdatedtime = Convert.ToDateTime(timestamp).ToString("yyyy'-'MM'-'dd HH:mm:ss");
                                        p.latitude = latStr;
                                        p.longitude = longStr;
                                        p.location = positionsTxt;
                                        p.speed = speed.ToString();
                                        p.stop = stop;
                                        p.hvinp = hvinp;
                                        p.gvinp = gvinp;
                                        p.direction = direction;
                                        p.Remarks = Remarks;
                                        p.InfoColor = InfoColor;
                                        p.Marker = Marker;

                                        res.Add(p);
                                    }
                                    // Aggregate all position data under a single "ShowPosition" request
                                    if (res.Count > 0)
                                    {
                                        if (responses.Any(r => r.request_type == "ShowPosition"))
                                        {
                                            // If ShowPosition already exists, just add the new data to it
                                            var existingResponse = responses.First(r => r.request_type == "ShowPosition");
                                            existingResponse.position_data.AddRange(res.Select(r => new PositionDataResponse
                                            {
                                                vehicle_number = r.vehicle,
                                                latitude = r.latitude,
                                                longitude = r.longitude,
                                                location = r.location,
                                                speed = r.speed,
                                                timestamp = r.gpsupdatedtime,
                                                vehicleinfo = r.vehicleinfo,
                                                Remarks = r.Remarks,
                                                InfoColor = r.InfoColor,
                                                Marker = Marker
                                            }));
                                        }
                                        else
                                        {
                                            // If ShowPosition doesn't exist, create a new one
                                            Response positionResponse = new Response
                                            {
                                                request_type = "ShowPosition",
                                                position_data = res.Select(r => new PositionDataResponse
                                                {
                                                    vehicle_number = r.vehicle,
                                                    latitude = r.latitude,
                                                    longitude = r.longitude,
                                                    location = r.location,
                                                    speed = r.speed,
                                                    timestamp = r.gpsupdatedtime,
                                                    vehicleinfo = r.vehicleinfo,
                                                    Remarks = r.Remarks,
                                                    InfoColor = r.InfoColor,
                                                    Marker = Marker
                                                }).ToList(),
                                                map_data = new List<MapData>
                                            {
                                                new MapData() // This will use the hardcoded values from MapData class
                                            }
                                            };

                                            responses.Add(positionResponse);
                                        }
                                        res.Clear();
                                    }

                                }
                                catch (Exception ex)
                                {
                                    // Handle exceptions here
                                    Console.WriteLine($"Error: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                finalResponse = new
                {
                    responses = responses.Select(r => new
                    {
                        request_type = r.request_type,
                        position_data = r.position_data.Select(p => new
                        {
                            p.vehicle_number,
                            p.latitude,
                            p.longitude,
                            p.location,
                            p.speed,
                            p.timestamp,
                            p.vehicleinfo,
                            p.Remarks,
                            p.InfoColor,
                            p.Marker
                        }).ToList(),
                        map_data = new List<object>
                        {
                        new
                        {
                            centerlatlong = "12.96,77.6",
                            DefualtZoomLevel = "6.00"
                        }
                        }
                    }).ToList()
                };

                jsonResponse = JsonConvert.SerializeObject(finalResponse ?? new { responses = new List<object>() });
                WebOperationContext.Current.OutgoingResponse.ContentType = "application/json";

                return new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse));
            }
            if (map?.map_requests?.Any(r => r.request_type == "ShowRoute") == true)
            {

                foreach (var mapRequest in map.map_requests.Where(r => r.request_type == "ShowRoute"))
                {
                    if (mapRequest.route_data != null && mapRequest.route_data.Count == 3)
                    {
                        try
                        {
                            VehicleId = mapRequest.route_data[0]; // Vehicle ID
                                                                  //DateTime from = DateTime.ParseExact(mapRequest.route_data[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                                                  //DateTime to = DateTime.ParseExact(mapRequest.route_data[2], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                            string sessionId = objbll.getSessionIdByVehicle(VehicleId);
                            //string usersvehicles = objbll.GetVehicles(sessionId).Replace("'", "").Replace(",Select", "");

                            DateTime from = new DateTime();
                            DateTime to = new DateTime();
                            try
                            {
                                from = Convert.ToDateTime(mapRequest.route_data[1]);
                                to = Convert.ToDateTime(mapRequest.route_data[2]);
                            }
                            catch
                            {
                                ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                            }
                            if (VehicleId.Contains(","))
                                VehicleId = VehicleId.TrimEnd(',');

                            int range_limit = 168;
                            if (VehicleId.Contains(","))
                                range_limit = 24;
                            if ((to - from).TotalHours > range_limit)
                            {
                                ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                                throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                            }
                            List<VehicleRoute> vehicleRoutes = new List<VehicleRoute>();
                            string[] vehicleIds = VehicleId.Split(',');
                            foreach (string vid in vehicleIds)
                            {
                                object[] values = objbll.getRouteData(from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), vid, false);
                                VehicleRoute vehicleRoute = new VehicleRoute { vehicle_number = vid, route_points = new List<RoutePoint>() };

                                foreach (object val in values)
                                {
                                    object[] row = (object[])val;
                                    vehicleRoute.route_points.Add(new RoutePoint
                                    {
                                        latitude = row[(int)BusinessLogicLayer.BLL.headers.latitude].ToString(),
                                        longitude = row[(int)BusinessLogicLayer.BLL.headers.longitude].ToString(),
                                        timestamp = Convert.ToDateTime(row[(int)BusinessLogicLayer.BLL.headers.timestamp]).ToString("yyyy-MM-dd HH:mm:ss"),
                                        speed = row[(int)BusinessLogicLayer.BLL.headers.speed].ToString(),
                                        stop = row[(int)BusinessLogicLayer.BLL.headers.stop].ToString()
                                    });
                                }

                                vehicleRoutes.Add(vehicleRoute);
                            }

                            if (vehicleRoutes.Count > 0)
                            {
                                responseList.Add(new ShowRouteResponse
                                {
                                    request_type = "ShowRoute",
                                    route_data = vehicleRoutes,
                                    map_data = new List<MapData> { new MapData() }
                                });
                            }
                            // Construct response

                            finalResponse = new { responses = responseList };
                            jsonResponse = JsonConvert.SerializeObject(finalResponse);
                            WebOperationContext.Current.OutgoingResponse.ContentType = "application/json";

                            return new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse));

                        }
                        catch (Exception ex)
                        {
                            // Handle exceptions here
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                }
            }

            jsonResponse = JsonConvert.SerializeObject(finalResponse ?? new { responses = new List<object>() });
            WebOperationContext.Current.OutgoingResponse.ContentType = "application/json";

            return new MemoryStream(Encoding.UTF8.GetBytes(jsonResponse));

        }

        //ATGL CAB API Integration 23052025 
        public CabTripResponse PushCABTripData(string key, string clientId, List<CabTripDetailsRequest> tripdata)
        {
            CabTripResponse response = new CabTripResponse();
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "40", "json", clientId);
            BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();


            try
            {
                string SessionId = "";// access.Split(',')[1];
                var jsonData = JsonConvert.SerializeObject(tripdata);
                objbll.STL_RecordErrorMessage(jsonData);

                foreach (var trip in tripdata)
                {
                    string tripId = trip.trip_id;
                    string trigger = trip.trigger_point;//Trip start or Trip End status
                    string loginVehicleNo = objbll.GetCorrectVehicleNumber(trip.vehicle_number);

                    if (!String.IsNullOrEmpty(loginVehicleNo))
                    {
                        string dist = "0";
                        string runtime = "";

                        string tripStartTime = string.Empty;
                        string tripEndTime = string.Empty;
                        try
                        {
                            if (!String.IsNullOrEmpty(trip.trip_start_timestamp) && trip.trip_start_timestamp != "null")
                                try
                                {
                                    tripStartTime = objbll.formatDBDate(DateTime.ParseExact(trip.trip_start_timestamp, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));

                                }
                                catch
                                {
                                    tripStartTime = objbll.formatDBDate(Convert.ToDateTime(trip.trip_start_timestamp));
                                }
                        }
                        catch (Exception ex)
                        {
                            response.statuscode = "500";
                            response.status = "Failed: Invalid Date & Time";
                            return response;
                        }
                        try
                        {
                            if (!String.IsNullOrEmpty(trip.trip_end_timestamp) && trip.trip_end_timestamp != "null")
                                try
                                {
                                    tripEndTime = objbll.formatDBDate(DateTime.ParseExact(trip.trip_end_timestamp, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));

                                }
                                catch
                                {
                                    tripEndTime = objbll.formatDBDate(Convert.ToDateTime(trip.trip_end_timestamp));
                                }
                        }
                        catch (Exception ex)
                        {
                            response.statuscode = "500";
                            response.status = "Failed: Invalid Date & Time";
                            return response;
                        }

                        //Update the formatted Data
                        trip.vehicle_number = loginVehicleNo;
                        trip.trip_start_timestamp = tripStartTime;
                        trip.trip_end_timestamp = tripEndTime;
                        if (trigger == "START_TRIP")
                        {
                            //bool tripExists = true;//objbll.IsTripExists(tripId, trigger);
                            String Trackinglink = GetShowPositionsUrl(loginVehicleNo);
                            trip.gps_tracking_url = Trackinglink;
                            int res = objbll.Insert_atgl_cab_trip_details(trip, SessionId);

                            //response.TrackingLink = "demourl/kyzser";
                            response.TrackingLink = Trackinglink;
                            response.TripDistance = "0";
                        }
                        else if (trigger == "END_TRIP")
                        {
                            try
                            {
                                bool tripExists = objbll.IsTripExists(tripId, "START_TRIP");
                                try
                                {
                                    if (tripExists)
                                    {
                                        tripStartTime = objbll.GetCabTripStartTS(tripId);
                                    }
                                }
                                catch
                                {
                                    response.statuscode = "404";
                                    response.status = "Failed: Trip Not Found";
                                    return response;
                                }

                                object[] distobj = objbll.accumulateDistance(loginVehicleNo, tripStartTime, tripEndTime, null);
                                if (distobj.Length > 0)
                                {
                                    dist = distobj[0].ToString();
                                    runtime = distobj[2].ToString();
                                }
                            }
                            catch
                            {
                                dist = "00";
                            }
                            response.TrackingLink = "";
                            response.TripDistance = dist;
                            trip.trip_distance_km = Convert.ToDecimal(dist);
                            int res = objbll.Insert_atgl_cab_trip_details(trip, SessionId);
                        }
                        else
                        {
                            response.statuscode = "500";
                            response.status = "Failed: Invalid Trigger";
                            return response;
                        }
                        response.statuscode = "200";
                        response.status = "Success";
                    }
                    else
                    {
                        response.statuscode = "500";
                        response.status = "Failed: Invalid Vehicle Number";
                    }
                }
                return response;
            }
            catch (System.ArgumentException ex)
            {
                objbll.STL_RecordErrorMessage(ex.ToString());
                response.statuscode = "500";
                response.status = "Failed: Invalid JSON Data";
                return response;
            }
            catch (Exception ex)
            {
                objbll.STL_RecordErrorMessage(ex.ToString());
                response.statuscode = "500";
                response.status = "Exception: " + ex.Message;
                return response;
            }
        }
        public List<CabTripData> GetCABCompletedTripData(string Key, string clientId, string fromdate, string todate, string format)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(Key, "41", format, clientId);
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string AccountName = objbll.GetAccountName(sessionId);
                int groupid = objbll.getGroupId(sessionId);
                if (groupid == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "User Not Found.");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                if (string.IsNullOrEmpty(fromdate) || string.IsNullOrEmpty(todate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                try
                {
                    from = Convert.ToDateTime(fromdate);
                    to = Convert.ToDateTime(todate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                if ((to - from).TotalHours > 24)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<CabTripData> result = new List<CabTripData>();
                try
                {
                    DataTable dt = objbll.GetCabTripDeatils(Convert.ToDateTime(fromdate), Convert.ToDateTime(todate));
                    //.AddYears(-10) for all not reachable data
                    dt = CheckPosition(dt);

                    if (dt != null && dt.Rows.Count > 0)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            DateTime tripstart = Convert.ToDateTime(row["trip_start_timestamp"]);
                            DateTime tripend = Convert.ToDateTime(row["trip_end_timestamp"]);

                            CabTripData Datas = new CabTripData
                            {
                                trip_id = row["trip_id"].ToString(),
                                vehicle_number = row["vehicle_number"].ToString(),
                                trip_start_timestamp = tripstart.ToString("yyyy-MM-dd HH:mm:ss"),
                                trip_end_timestamp = tripend.ToString("yyyy-MM-dd HH:mm:ss"),
                                trip_distance_km = row["trip_distance_km"].ToString()
                            };
                            result.Add(Datas);
                        }
                        return result;
                    }
                    else
                    {
                        ErrorMessage customError = new ErrorMessage("404", "FAIL:DATA NOT FOUND");
                        throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                    }
                }
                catch (WebFaultException<ErrorMessage> ex)
                {
                    // Rethrow WebFaultException with custom error message
                    throw ex;
                }
            }
            finally
            {
                dbx.close();
            }
        }

        public GeofenceTripReport GetGeofenceTripDetails(string key, string clientid, string fromdate, string todate, string format)
        {
            WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;
            string exepurl = HttpContext.Current.Request.Url.ToString();
            string access = AuthenticateAPIKey(key, "42", format, clientid);
            try
            {
                BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
                string accountid = access.Split(',')[3].ToString();
                string sessionId = access.Split(',')[1].ToString();
                string AccountName = objbll.GetAccountName(sessionId);
                int groupid = objbll.getGroupId(sessionId);
                if (groupid == 0)
                {
                    ErrorMessage customError = new ErrorMessage("404", "User Not Found.");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.OK);
                }
                DateTime from = new DateTime();
                DateTime to = new DateTime();
                if (string.IsNullOrEmpty(fromdate) || string.IsNullOrEmpty(todate))
                {
                    ErrorMessage customError = new ErrorMessage("404", "FAIL:DATE_RANGE_PARAM_MISSING");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.NotFound);
                }
                try
                {
                    from = Convert.ToDateTime(fromdate);
                    to = Convert.ToDateTime(todate);
                }
                catch
                {
                    ErrorMessage customError = new ErrorMessage("400", "FAIL:DATE_RANGE_IS_INVALID_FORMAT");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.BadRequest);
                }
                if ((to - from).TotalHours > 24)
                {
                    ErrorMessage customError = new ErrorMessage("412", "FAIL:DATE_RANGE_IS_MORE_THAN_24HR");
                    throw new WebFaultException<ErrorMessage>(customError, HttpStatusCode.PreconditionFailed);
                }
                List<GeofenceTripReport> result = new List<GeofenceTripReport>();
                try
                {
                    // Create sample data
                    var report = new GeofenceTripReport
                    {
                        report_from_date = "2025-11-01 00:00:00",
                        report_to_date = "2025-11-01 23:59:59",
                        vehicle_trips = new List<VehicleTrip>
                {
                    new VehicleTrip
                    {
                        vehicle_no = "GJ 06 BV 1234",
                        trip_no = 1,
                        trip_start = new TripStart
                        {
                            location = "IOCL Chudasama Automobile - 823, Barwala and Ranpur Talukas",
                            location_code = "Loc12345",
                            in_time = "2025-10-31 19:43:00",
                            out_time = "2025-11-01 07:57:00"
                        },
                        trip_end = new TripEnd
                        {
                            location = "HPCL NG Gadhiya, Barwala and Ranpur Talukas",
                            location_code = "Loc98765",
                            in_time = "2025-11-01 09:17:00",
                            out_time = "2025-11-01 12:37:00"
                        },
                        distance_km = 50.83,
                        route_traveled = "Route Link"
                    },
                    new VehicleTrip
                    {
                        vehicle_no = "GJ 06 BV 5678",
                        trip_no = 1,
                        trip_start = new TripStart
                        {
                            location = "HPCL NG Gadhiya, Barwala",
                            location_code = "Loc54321",
                            in_time = "2025-11-01 14:00:00",
                            out_time = "2025-11-01 15:15:00"
                        },
                        trip_end = new TripEnd
                        {
                            location = "IOCL Chudasama Automobile",
                            location_code = "Loc67890",
                            in_time = "2025-11-01 16:00:00",
                            out_time = "2025-11-01 17:10:00"
                        },
                        distance_km = 48.25,
                        route_traveled = "Route Link 2"
                    }
                     }
                    };

                    // Serialize to JSON
                    return report;
                }
                catch (WebFaultException<ErrorMessage> ex)
                {
                    // Rethrow WebFaultException with custom error message
                    throw ex;
                }
            }
            finally
            {
                dbx.close();
            }
        }
    }
}