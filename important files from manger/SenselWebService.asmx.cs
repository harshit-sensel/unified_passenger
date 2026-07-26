using PDFWriter;
using Portal.New;
using Portal.UserControls;
using Sensel.XmlDB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using static BusinessLogicLayer.BLL;
//using System.Net.Http.Formatting;
//namespace Portal
//{
/// <summary>
/// Summary description for SenselWebService
/// </summary>
[WebService(Namespace = "http://tempuri.org/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
[System.ComponentModel.ToolboxItem(false)]
// To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
[System.Web.Script.Services.ScriptService]
public class SenselWebService : System.Web.Services.WebService
{
    BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
    string sHost = BusinessLogicLayer.BLL.getHostName();
    string sUserIp = BusinessLogicLayer.BLL.GetUser_IP();
    protected int timeZone = 5 * 60 * 60 + 30 * 60;
    [WebMethod(Description = "Keeps your current session alive", EnableSession = true)]
    public void PingSession()
    {
    }

    [WebMethod]
    public string LoadHeaderDashBoard(string SId)
    {
        using (Page page = new Page())
        {
            HtmlForm form = new HtmlForm();

            UserControl userControl = (UserControl)page.LoadControl("~/UserControls/ucHeaderDashBoard.ascx");
            (userControl.FindControl("lblSessionId") as Label).Text = SId;
            form.Controls.Add(userControl);


            using (StringWriter writer = new StringWriter())
            {
                page.Controls.Add(form);
                HttpContext.Current.Server.Execute(page, writer, false);
                return writer.ToString();
            }
        }
    }
    [WebMethod]
    public string GetDashBoardValue(string SId, string SpeedLimit, string DashboardAccess)
    {
        try
        {
            string rRValue = objBll.GetDashBoardValue(SId, SpeedLimit, DashboardAccess);
            return rRValue;
        }
        catch
        {
            return string.Empty;
        }
    }
    [WebMethod]
    public void AddupdateSpeedLimit(string SId, string sSpeedLimit)
    {
        objBll.AddUpdateSpeedLimit(sSpeedLimit, SId);
    }

    [WebMethod]
    [ScriptMethod(UseHttpGet = false, XmlSerializeString = true, ResponseFormat = ResponseFormat.Xml)]
    public XmlDocument GetVehiclesBySessionId()
    {
        string SessionId = string.Empty;
        string strWhere = string.Empty;
        if (HttpContext.Current.Request.Form["SessionId"] != null)
        {
            SessionId = HttpContext.Current.Request.Form["SessionId"];
        }
        else
            return null;

        FlexiGridModel fm = Common_FlexiGrid.FlexiGridOption(HttpContext.Current, Activator.CreateInstance(typeof(VehicleModel)));

        if (HttpContext.Current.Request.Form["VehicleNo"] != null)
        {
            fm.whereCondition = HttpContext.Current.Request.Form["VehicleNo"];
        }

        int start = ((fm.page - 1) * fm.rp);


        int iTotal = 0;

        List<VehicleModel> lst = new List<VehicleModel>();
        lst = GetVehiclesInfo(SessionId, fm.whereCondition, fm.sortExp, start, fm.rp, ref iTotal).ToList();



        XDocument xmlDoc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("rows", new XElement("page", fm.page.ToString()), new XElement("total", iTotal.ToString()),
                    lst.Select(row => new XElement("row", new XElement("cell", "<input type='checkbox' class='chck' name='chk' value= '" + row.VehicleID + "' />"),
                                                          new XElement("cell", row.VehicleID),
                                                          new XElement("cell", row.VehicleInfo),
                                                          new XElement("cell", row.DateTime.Split(',')[0]),
                                                          new XElement("cell", row.DateTime.Split(',')[1]),
                                                          new XElement("cell", row.Location),
                                                          new XElement("cell", row.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : row.Speed),
                                                          new XElement("cell", GetStatus(row.remarks)),
                                                          new XElement("cell", row.remarks)
                                                    )
                                )
                             )
        );

        XmlDocument newDoc = new XmlDocument();
        newDoc.LoadXml(xmlDoc.ToString());


        return newDoc;
    }

    //For Fleet-Smart App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetVehicles(string SessionId = "18b41205e845f4f604cbf76d539c0f746256e288")
    {
        string accountName = objBll.GetAccountName(SessionId);
        string login = objBll.getUserId(SessionId);
        string timestamp = objBll.GetMobileAppLastRequest(login, sUserIp, "GetVehicles");
        Object[] account = objBll.getAccountInfo(SessionId);
        String acntGrpAtt = "";
        string remarksforalerts = "";
        if (account != null)
            if (account.Length > 0)
            {
                acntGrpAtt = account[3].ToString();//Convert.ToString(((Object[])account[0])[3]);
            }
        string domainName = HttpContext.Current.Request.Url.Host;
        //Added by Madhuri for ASG code optimisations - 19-03-2025
        //if (acntGrpAtt == "AT" && (domainName.Contains("test") || domainName.Contains("demo.fleetsmart.in")))
        if (acntGrpAtt == "AT" && (domainName.Contains("test") || domainName.Contains("asg")))
        {
            if (!string.IsNullOrEmpty(timestamp))
            {
                DateTime lastreq = Convert.ToDateTime(timestamp);
                DateTime now = DateTime.Now;
                if ((now - lastreq).TotalSeconds < 30)
                    return "[]";
            }
            DataTable vehiclesdt = objBll.GetAsgVehiclesInfo(SessionId);//To get the assets detials those are present in LastUpdtdata Table

            // Convert DataTable to JSON
            JavaScriptSerializer js = new JavaScriptSerializer();
            var memoryStream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(List<Vehicle>));
            serializer.WriteObject(memoryStream, ConvertDataTableToList(vehiclesdt, SessionId));//To convert the datatable into serializable format

            string jsonData = Encoding.UTF8.GetString(memoryStream.ToArray());
            //logWriter.WriteToLog("ASGLanding.aspx.cs:getData() end:" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff"), true);
            return jsonData;
        }
        else
        {
            if (!string.IsNullOrEmpty(timestamp))
            {
                DateTime lastreq = Convert.ToDateTime(timestamp);
                DateTime now = DateTime.Now;
                if ((now - lastreq).TotalSeconds < 30)
                    return "[]";
            }
            DataTable accconfig = objBll.GetAccountGroupConfig(SessionId);
            string BatteryStatusinLand = "0";
            DataTable marketActiveVehicles = null;
            if (accconfig.Rows.Count > 0)
            {
                BatteryStatusinLand = Convert.ToString(accconfig.Rows[0]["ShowBatteryStatusinLand"]);
                string ShowMarketVeh = Convert.ToString(accconfig.Rows[0]["ShowMarketVehOnLand"]);

                if (ShowMarketVeh == "1")
                    marketActiveVehicles = objBll.getActiveMarketVehicles("", "-1", Convert.ToInt16(Convert.ToString(accconfig.Rows[0]["GroupId"])), "1");
            }

            string VehicleId = "";
            List<Vehicle> lst = new List<Vehicle>();
            lst = GetVehiclesInfo(SessionId).ToList();
            var Vehicles = new List<Vehicle>();
            string UpdatingVehicles = "";

            if (acntGrpAtt == "AT")
            {
                lst = lst.OrderBy(item => item.remarks, new RemarksComparer()).ToList();
            }
            foreach (var item in lst)
            {
                bool flag = false;
                if (marketActiveVehicles == null)
                    flag = true;
                else
                {
                    DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item.VehicleID + "'");
                    if (dr.Length > 0)
                        flag = true;
                }
                if (flag)
                {
                    string Datetime = null;
                    DateTime endDate = DateTime.UtcNow;
                    timeZone = objBll.GetTimeZoneDiff(SessionId);
                    endDate = endDate.AddSeconds(timeZone);//for converting in to IST
                    if (Datetime != null)
                        endDate = Convert.ToDateTime(Datetime);
                    int Batpercentage = 0;
                    if (item.Battery != null && BatteryStatusinLand == "1")
                    {
                        int BatVal = Convert.ToInt32(item.Battery);//For low battery devices
                        string uid = objBll.GetUIdByVehicleId(item.VehicleID);
                        if (!string.IsNullOrEmpty(uid))
                            if ((endDate - Convert.ToDateTime(item.DateTime)).TotalDays >= 15)
                            {
                                Batpercentage = 0;
                            }
                            else
                            {
                                //Batpercentage = objBll.GetBatteryPercentageByVoltage(BatVal, Convert.ToInt32(uid));
                                Batpercentage = Convert.ToInt16(item.Battery);
                            }
                    }
                    UpdatingVehicles += item.VehicleID.Trim() + ",";
                    if (acntGrpAtt == "AT")
                    {
                        //Added by Madhuri for making not reporting interval configurable-15122023 by default Not reporting interval is 72 hrs
                        DataTable dtNrInterval = objBll.getNRconfigurationByAccount(SessionId);
                        int NRInterval = 72;
                        if (dtNrInterval.Rows.Count > 0)
                        {
                            if (!string.IsNullOrEmpty(dtNrInterval.Rows[0]["NRInterval"].ToString()))
                            {
                                NRInterval = Convert.ToInt32(dtNrInterval.Rows[0]["NRInterval"]);
                            }
                        }
                        //Ended by Madhuri for making not reporting interval configurable-15122023 by default Not reporting interval is 72 hrs

                        object[] theftInfo = objBll.gettheftalertdataforAsset(item.VehicleID.Trim());
                        if ((((((Object[])theftInfo[0])[0]).ToString()).Trim()).Equals(item.VehicleID.ToString().Trim()))
                        {
                            //    if (((((Object[])theftInfo[0])[1]).ToString()).Equals("1"))
                            if (((((Object[])theftInfo[0])[1]).ToString()).Equals("1") || ((((Object[])theftInfo[0])[1]).ToString()).Equals("2"))
                            {
                                //remarksforalerts = "Not Reporting-Theft Alert";
                                if (item.remarks.Contains("Not Reporting"))
                                {
                                    //if ((endDate - Convert.ToDateTime(item.DateTime)).TotalSeconds < 259200)
                                    if ((endDate - Convert.ToDateTime(item.DateTime)).TotalHours < NRInterval)
                                    {
                                        remarksforalerts = "Not Reporting-Theft Alert";
                                    }
                                    else { remarksforalerts = "Not Reporting"; }
                                }
                                else { remarksforalerts = "Theft Alert"; }
                            }
                            else
                                remarksforalerts = item.remarks;
                        }
                        var v = new Vehicle()
                        {
                            Select = "",
                            VehicleID = item.VehicleID,
                            VehicleInfo = item.VehicleInfo,
                            DateTime = item.DateTime.Split(',')[0],
                            Time = item.DateTime.Split(',')[1],
                            Location = item.Location,
                            Speed = item.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : item.Speed,
                            Status = GetStatus(remarksforalerts),
                            remarks = remarksforalerts,
                            LAt = item.LAt,
                            longi = item.longi,
                            direction = item.direction,
                            IconType = item.IconType,
                            Battery = Batpercentage.ToString()
                        };

                        if (string.IsNullOrEmpty(VehicleId) || VehicleId.Contains(item.VehicleID))
                            Vehicles.Add(v);

                    }
                    else if (accountName.ToLower().Contains("shell"))
                    {

                        var v = new Vehicle()
                        {
                            Select = "",
                            VehicleID = item.VehicleID,
                            VehicleInfo = item.VehicleInfo,
                            DateTime = item.DateTime.Split(',')[0],
                            Time = item.DateTime.Split(',')[1],
                            Location = item.Location,
                            Speed = item.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : item.Speed,
                            Status = GetStatus(item.remarks),
                            remarks = item.remarks,
                            LAt = item.LAt,
                            longi = item.longi,
                            direction = item.direction,
                            IconType = item.IconType,
                            Battery = Batpercentage.ToString(),
                            DriverInfo = objBll.GetDriverInfoByVehicleLanding(item.VehicleID)
                        };
                        if (string.IsNullOrEmpty(VehicleId) || VehicleId.Contains(item.VehicleID))
                            Vehicles.Add(v);
                    }
                    else
                    {
                        var v = new Vehicle()
                        {
                            Select = "",
                            VehicleID = item.VehicleID,
                            //VehicleInfo = item.VehicleInfo,
                            VehicleInfo = string.IsNullOrEmpty(item.VehicleInfo) ? GetCategoryByVehicle(item.VehicleID, objBll) : item.VehicleInfo,
                            DateTime = item.DateTime.Split(',')[0],
                            Time = item.DateTime.Split(',')[1],
                            Location = item.Location,
                            Speed = item.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : item.Speed,
                            Status = GetStatus(item.remarks),
                            remarks = item.remarks,
                            LAt = item.LAt,
                            longi = item.longi,
                            direction = item.direction,
                            IconType = item.IconType,
                            Battery = Batpercentage.ToString()
                        };
                        if (string.IsNullOrEmpty(VehicleId) || VehicleId.Contains(item.VehicleID))
                            Vehicles.Add(v);
                    }
                }
            }

            //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
            DataTable lstAll = objBll.GetVehiclesBySessionId(SessionId, "").Tables[0];
            //Added By Madhuri for ASG code optimization - 30042024
            // Get distinct VehicleIds from list data
            var distinctListData = lst.Select(obj => obj.VehicleID);

            // Get distinct VehicleIds from the DataTable
            var distinctDataTable = lstAll.AsEnumerable()
                .Select(row => row.Field<string>("VehicleID")) // Ensure column name matches the property name in Vehicle class
                .Distinct();

            // Find the VehicleIds that are in DataTable but not in list data
            var notInListData = distinctDataTable.Except(distinctListData.Select(id => id));
            if (lstAll.Rows.Count > 0)
            {
                //foreach (DataRow item in lstAll.Rows)
                foreach (var vehicleId in notInListData)
                {
                    bool flag = false;
                    if (marketActiveVehicles == null)
                        flag = true;
                    else
                    {
                        //DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item["VehicleID"].ToString() + "'");
                        DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + vehicleId.ToString() + "'");
                        if (dr.Length > 0)
                            flag = true;
                    }
                    if (flag)
                    {
                        //if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                        if (!UpdatingVehicles.Contains(vehicleId.ToString().Trim() + ","))
                        {
                            if (accountName.ToLower().Contains("shell"))
                            {
                                var v = new Vehicle()
                                {
                                    Select = "",
                                    //VehicleID = item["VehicleID"].ToString(),
                                    //VehicleInfo = item["VehicleInfo"].ToString(),
                                    VehicleID = vehicleId.ToString(),
                                    VehicleInfo = vehicleId.ToString(),
                                    DateTime = "",
                                    Time = "",
                                    Location = "",
                                    Speed = "0",
                                    Status = "NR",
                                    remarks = "NR",
                                    HaltDuration = "",
                                    direction = "0",
                                    IconType = "Default",
                                    Battery = "0",
                                    //DriverInfo = objBll.GetDriverInfoByVehicleLanding(item["VehicleID"].ToString())
                                    DriverInfo = objBll.GetDriverInfoByVehicleLanding(vehicleId.ToString())
                                };
                                Vehicles.Add(v);
                            }
                            else
                            {
                                var v = new Vehicle()
                                {
                                    Select = "",
                                    //VehicleID = item["VehicleID"].ToString(),
                                    //VehicleInfo = item["VehicleInfo"].ToString(),
                                    VehicleID = vehicleId.ToString(),
                                    VehicleInfo = vehicleId.ToString(),
                                    DateTime = "",
                                    Time = "",
                                    Location = "",
                                    Speed = "0",
                                    Status = "NR",
                                    remarks = "NR",
                                    HaltDuration = "",
                                    direction = "0",
                                    IconType = "Default",
                                    Battery = "0"
                                };
                                Vehicles.Add(v);
                            }


                        }
                    }
                }
            }

            var jss = new JavaScriptSerializer();
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehicles", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
            rlog.Start();
            return jss.Serialize(Vehicles);
        }
    }
    private static string GetCategoryByVehicle(string vehicleid, BusinessLogicLayer.BLL objBll)
    {
        string res = objBll.GetCategoryByVehicle(vehicleid);
        return (string.IsNullOrEmpty(res)) ? "" : "(" + res + ")";
    }
private static object ConvertDataTableToList(DataTable dt, string SessionId)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        var vehicles = new List<Vehicle>();
        // Extract existing vehicle IDs from dt
        var dtVehicleIds = new HashSet<string>(dt.AsEnumerable().Select(row => row["truckId"].ToString().Trim()));

        foreach (DataRow row in dt.Rows)
        {
            var vehicle = new Vehicle()
            {
                Select = "",
                VehicleID = row["truckId"].ToString(),
                VehicleInfo = row["VehicleInfo"].ToString(),
                DateTime = row["UpdateDate1"].ToString(),
                //Time = row["UpdateTime"].ToString(),
                Time = " " + row["UpdateTime"].ToString(),
                Location = row["positiontxt"].ToString(),
                Speed = row["speed"].ToString(),
                Status = "",
                remarks = row["Remarks"].ToString(),
                LAt = row["latitude"].ToString(),
                longi = row["longitude"].ToString(),
                direction = row["direction"].ToString(),
                Battery = row["BV"].ToString(),
                IconType = row["IconType"].ToString(),
                //IconSet = IconSetFlag,
                Bearer = row["fuellevel"].ToString(),
                SignalLevel = row["gvinp1"].ToString(),
                BranchId = row["BranchID"].ToString()
            };

            vehicles.Add(vehicle);
        }
        DataTable lstAll = objBll.GetVehiclesBySessionId(SessionId, "").Tables[0];
        var lstAllVehicleIds = new HashSet<string>(lstAll.AsEnumerable().Select(row => row.Field<string>("VehicleID").Trim()));
        // Get distinct VehicleIds from list data
        var distinctListData = vehicles.Select(obj => obj.VehicleID);

        // Find vehicles that are in lstAll but not in dt
        var notInDt = lstAllVehicleIds.Except(dtVehicleIds);//To get Assets those are not present in LastUpdtdata table but present in vehiclesgroupsmap table

        if (lstAll.Rows.Count > 0)
        {
            foreach (var vehicleId in notInDt)
            {
                var v = new Vehicle()
                {
                    Select = "",
                    VehicleID = vehicleId.ToString(),
                    VehicleInfo = vehicleId.ToString(),
                    DateTime = "",
                    Time = "",
                    Location = "",
                    Speed = "0",
                    Status = "NR",
                    remarks = "NR",
                    HaltDuration = "",
                    direction = "0",
                    IconType = "Default",
                    Battery = "0"
                };
                vehicles.Add(v);
            }
        }

        return vehicles;
    }
    class RemarksComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            // Define the desired order of the remarks
            List<string> desiredOrder = new List<string> { "Priority Theft Alert", "Theft Alert", "Not Reporting,", "Reporting," };

            // Get the index of x and y in the desired order list
            int indexX = desiredOrder.IndexOf(x);
            int indexY = desiredOrder.IndexOf(y);

            // Compare the indexes and return the result
            return indexX.CompareTo(indexY);
        }
    }
    //For Fleet-Smart App//
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetParticularVehicles(string SessionId, string VehicleId)
    {
        List<Vehicle> lst = new List<Vehicle>();
        lst = GetVehiclesInfo(SessionId).ToList();
        var Vehicles = new List<Vehicle>();
        DataTable accconfig = objBll.GetAccountGroupConfig(SessionId);
        string BatteryStatusinLand = "0";
        if (accconfig.Rows.Count > 0)
        {
            BatteryStatusinLand = Convert.ToString(accconfig.Rows[0]["ShowBatteryStatusinLand"]);
        }
        string UpdatingVehicles = "";
        foreach (var item in lst)
        {
            int Batpercentage = 0;
            if (item.Battery != null && BatteryStatusinLand == "1")
            {
                int BatVal = Convert.ToInt32(item.Battery);
                //Batpercentage = objBll.GetBatteryPercentageByVoltage(BatVal, Convert.ToInt32(objBll.GetUIdByVehicleId(item.VehicleID)));
                Batpercentage = Convert.ToInt16(item.Battery);
            }
            UpdatingVehicles += item.VehicleID.Trim() + ",";
            var v = new Vehicle()
            {
                Select = "",
                VehicleID = item.VehicleID,
                VehicleInfo = item.VehicleInfo,
                DateTime = item.DateTime.Split(',')[0],
                Time = item.DateTime.Split(',')[1],
                Location = item.Location,
                Speed = item.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : item.Speed,
                Status = GetStatus(item.remarks),
                remarks = item.remarks,
                LAt = item.LAt,
                longi = item.longi,
                direction = item.direction,
                IconType = item.IconType,
                Battery = Batpercentage.ToString()
            };
            if (string.IsNullOrEmpty(VehicleId) || VehicleId.Replace(" ", "").Contains(item.VehicleID.Replace(" ", "")))
                Vehicles.Add(v);
        }

        //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
        DataTable lstAll = objBll.GetVehiclesBySessionId(SessionId, "").Tables[0];
        if (lstAll.Rows.Count > 0)
        {
            foreach (DataRow item in lstAll.Rows)
            {
                if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                {
                    var v = new Vehicle()
                    {
                        Select = "",
                        VehicleID = item["VehicleID"].ToString(),
                        VehicleInfo = item["VehicleInfo"].ToString(),
                        DateTime = "",
                        Time = "",
                        Location = "",
                        Speed = "0",
                        Status = "NR",
                        remarks = "NR",
                        HaltDuration = "",
                        direction = "0",
                        IconType = "Default",
                        Battery = "0"
                    };
                    if (string.IsNullOrEmpty(VehicleId) || VehicleId.Replace(" ", "").Contains(v.VehicleID.Replace(" ", "")))
                        Vehicles.Add(v);
                }
            }
        }

        var jss = new JavaScriptSerializer();
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetParticularVehicles", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
        rlog.Start();
        return jss.Serialize(Vehicles);
    }

    //For Fleet-Smart App//
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetVehicleLastPosition(string SessionId, string vehicle)
    {
        string login = objBll.getUserId(SessionId);
        string timestamp = objBll.GetMobileAppLastRequest(login, sUserIp, "GetVehiclesLastPosition");
        string dummy = "";
        if (!string.IsNullOrEmpty(timestamp))
        {
            DateTime lastreq = Convert.ToDateTime(timestamp);
            DateTime now = DateTime.Now;
            if ((now - lastreq).TotalSeconds < 30)
                return "[]";
        }
        DataTable accconfig = objBll.GetAccountGroupConfig(SessionId);
        string BatteryStatusinLand = "0";
        if (accconfig.Rows.Count > 0)
        {
            BatteryStatusinLand = Convert.ToString(accconfig.Rows[0]["ShowBatteryStatusinLand"]);
        }
        List<Vehicle> lst = new List<Vehicle>();
        // Too many methods with the same name so adding one more parameter of no consequence
        lst = GetVehiclesInfo(SessionId, vehicle, dummy).ToList();
        var Vehicles = new List<Vehicle>();
        string UpdatingVehicles = "";
        foreach (var item in lst)
        {
            int Batpercentage = 0;
            if (item.Battery != null && BatteryStatusinLand == "1")
            {
                int BatVal = Convert.ToInt32(item.Battery);
                //Batpercentage = objBll.GetBatteryPercentageByVoltage(BatVal, Convert.ToInt32(objBll.GetUIdByVehicleId(item.VehicleID)));
                Batpercentage = Convert.ToInt16(item.Battery);
            }
            UpdatingVehicles += item.VehicleID.Trim() + ",";
            if (vehicle.Contains(item.VehicleID))
            {
                var v = new Vehicle()
                {
                    Select = "<input type='checkbox' class='chck' name='chk' value= '" + item.VehicleID + "' />",
                    VehicleID = item.VehicleID,
                    VehicleInfo = item.VehicleInfo,
                    DateTime = item.DateTime.Split(',')[0],
                    Time = item.DateTime.Split(',')[1],
                    Location = item.Location,
                    Speed = item.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : item.Speed,
                    Status = GetStatus(item.remarks),
                    remarks = item.remarks,
                    LAt = item.LAt,
                    longi = item.longi,
                    direction = item.direction,
                    IconType = item.IconType,
                    Battery = Batpercentage.ToString()
                };
                Vehicles.Add(v);
            }
        }

        //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
        DataTable lstAll = objBll.GetVehiclesBySessionId(SessionId, "").Tables[0];
        if (lstAll.Rows.Count > 0)
        {
            foreach (DataRow item in lstAll.Rows)
            {
                if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                {
                    var v = new Vehicle()
                    {
                        Select = "",
                        VehicleID = item["VehicleID"].ToString(),
                        VehicleInfo = item["VehicleInfo"].ToString(),
                        DateTime = "",
                        Time = "",
                        Location = "",
                        Speed = "0",
                        Status = "NR",
                        remarks = "NR",
                        HaltDuration = "",
                        direction = "0",
                        IconType = "Default",
                        Battery = "0"
                    };
                    Vehicles.Add(v);
                }
            }
        }

        var jss = new JavaScriptSerializer();
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclesLastPosition", "Fleet-Smart", "", login, SessionId, sHost, sUserIp));
        rlog.Start();
        return jss.Serialize(Vehicles);
    }

    //For Metro Driver App//
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void UpdateMetroCustomerAddressByURL(string CustomerID, string Latitude, string Longitude)
    {
        List<Vehicle> lst = new List<Vehicle>();
        Services ser = new Services();
        SenselRestService s = new SenselRestService();
        //string access = s.AuthenticateAPIKey(Key, "1", "json", clientId);
        var jss = new JavaScriptSerializer();
        try
        {
            objBll.UpdateMetroCustomerAddress(CustomerID, Latitude, Longitude);
        }
        catch (Exception e)
        {
        }
    }

    //For Fleet-Smart App//
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetVehiclePositionByToken(string Key, string clientId, string VehicleId)
    {
        List<Vehicle> lst = new List<Vehicle>();
        Services ser = new Services();
        SenselRestService s = new SenselRestService();
        string access = s.AuthenticateAPIKey(Key, "1", "json", clientId);
        var jss = new JavaScriptSerializer();
        var Vehicles = new List<Vehicle>();
        try
        {
            string username = access.Split(',')[0];
            string SessionId = access.Split(',')[1];
            lst = GetVehiclesInfo(SessionId).ToList();
            string UpdatingVehicles = "";
            foreach (var item in lst)
            {
                UpdatingVehicles += item.VehicleID.Trim() + ",";
                var v = new Vehicle()
                {
                    Select = "",
                    VehicleID = item.VehicleID,
                    VehicleInfo = item.VehicleInfo,
                    DateTime = item.DateTime.Split(',')[0],
                    Time = item.DateTime.Split(',')[1],
                    Location = item.Location,
                    Speed = item.remarks.ToString().ToUpper().Contains("VEHICLE HALTED") ? "0" : item.Speed,
                    Status = GetStatus(item.remarks),
                    remarks = item.remarks,
                    LAt = item.LAt,
                    longi = item.longi,
                    direction = item.direction,
                    IconType = item.IconType
                };
                if (string.IsNullOrEmpty(VehicleId) || VehicleId.Contains(item.VehicleID))
                    Vehicles.Add(v);
            }
            //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
            DataTable lstAll = objBll.GetVehiclesBySessionId(SessionId, "").Tables[0];
            if (lstAll.Rows.Count > 0)
            {
                foreach (DataRow item in lstAll.Rows)
                {
                    if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                    {
                        var v = new Vehicle()
                        {
                            Select = "",
                            VehicleID = item["VehicleID"].ToString(),
                            VehicleInfo = item["VehicleInfo"].ToString(),
                            DateTime = "",
                            Time = "",
                            Location = "",
                            Speed = "0",
                            Status = "NR",
                            remarks = "NR",
                            HaltDuration = "",
                            direction = "0",
                            IconType = "Default"
                        };
                        Vehicles.Add(v);
                    }
                }
            }

            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclePositionByToken", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
            rlog.Start();
        }
        catch { }
        return jss.Serialize(Vehicles);
    }

    /// <summary>
    /// Created by vamsi krishna oon 2020-10-20
    /// </summary>
    /// <param name="psngrID"></param>
    /// <param name="VehicleId"></param>
    /// <returns>selected vehicle position details for tracking</returns>
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetVehiclePositionForPsngrApp(string psngrID, string VehicleId)
    {
        List<Vehicle> lst = new List<Vehicle>();
        var jss = new JavaScriptSerializer();
        try
        {
            string SessionId = objBll.getSessionByPsngrID(psngrID);
            lst = GetVehiclesInfo(SessionId).ToList();
            lst = lst.FindAll(x => x.VehicleID.Replace(" ", "") == VehicleId.Replace(" ", ""));
            lst.ForEach(x => x.remarks = objBll.GetVehicleStatusByCode(x.remarks));
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclePositionForPsngrApp", "Passenegr App", "", "", SessionId, sHost, sUserIp));
            rlog.Start();
        }
        catch { }
        return jss.Serialize(lst);
    }

    //For Fleet-Smart App//
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetListOfVehicles(string SessionId)
    {
        List<VehicleList> lst = new List<VehicleList>();
        DataTable dt = objBll.GetVehiclesBySessionId(SessionId).Tables[0];
        if (dt.Rows.Count > 0)
        {
            foreach (DataRow item in dt.Rows)
            {
                var v = new VehicleList()
                {
                    vehicleid = item["VehicleID"].ToString(),
                    vehicleinfo = item["VehicleInfo"].ToString(),
                };
                lst.Add(v);
            }
        }
        var jss = new JavaScriptSerializer();
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetListOfVehicles", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
        rlog.Start();
        return jss.Serialize(lst);
    }

    //For Anti Theft App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string StopFastUpdate(string SessionId, string VehicleId)
    {
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        string vehicleinfo = objbll.GetVehicleInfoinVehicles(VehicleId);
        string SendTo = System.Text.RegularExpressions.Regex.Match(vehicleinfo, @"\d{12}").Value;
        if (string.IsNullOrWhiteSpace(SendTo))
            SendTo = System.Text.RegularExpressions.Regex.Match(vehicleinfo, @"\d{11}").Value;
        if (string.IsNullOrWhiteSpace(SendTo))
            SendTo = System.Text.RegularExpressions.Regex.Match(vehicleinfo, @"\d{10}").Value;

        string command = "SENSEL SET FASTTOSLOW";//Modified by suvarna on 1/2/2021 to remove mobileno and also send serverrequest
        objbll.addServerRequest(VehicleId, 5, "(SET FASTTOSLOW)");
        if (objbll.SendSMSwithLog(SendTo, command, "STOPFASTUPDATE", "0", "SENSEL"))
        {
            objbll.insertPanicData(VehicleId, DateTime.Now, "SENT_STOPFAST_CMD");
            return "Successfully Clear Theft command sent";
        }
        else
        {
            objbll.insertPanicData(VehicleId, DateTime.Now, "SENT_STOPFAST_SMSFAILED");
            return "Failed,Please try again";
        }

    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void RecordErrorLog(string error)
    {
        Thread rlog = new Thread(() => objBll.STL_RecordErrorMessage(error));
        rlog.Start();
    }

    //For Fleet-Smart App//
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertCustomerSupportComplaint(string userName, string ComponetType, string Summary, string Description, string EmailId)
    {
        string Host = BusinessLogicLayer.BLL.getHostName();
        XmlDb.AdminService.Service obj = new XmlDb.AdminService.Service();
        string sessionid = objBll.getSessionIdFromLogin(userName);
        string GlobalAccId = objBll.GetGlobalAccountIdBySession(sessionid).ToString();
        string AccountName = objBll.GetAccountName(sessionid).ToString();
        int i = obj.InsertCustomerSupportComplaint(userName, ComponetType, Summary.Replace("'", "").Replace("<", "").Replace(">", ""), Description.Replace("'", "").Replace("<", "").Replace(">", ""), Host, EmailId.Replace("'", "").Replace("<", "").Replace(">", ""), GlobalAccId, AccountName);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertCustomerSupportComplaint", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));
        rlog.Start();
        return i.ToString();
    }

    [WebMethod]
    [ScriptMethod(UseHttpGet = false, XmlSerializeString = true, ResponseFormat = ResponseFormat.Xml)]
    public XmlDocument GetVehiclesForReport()
    {
        string SessionId = string.Empty;
        if (HttpContext.Current.Request.Form["SessionId"] != null)
        {
            SessionId = HttpContext.Current.Request.Form["SessionId"];
        }
        else
            return null;

        FlexiGridModel fm = Common_FlexiGrid.FlexiGridOption(HttpContext.Current, Activator.CreateInstance(typeof(VehicleModel)));

        if (HttpContext.Current.Request.Form["VehicleNo"] != null)
        {
            fm.whereCondition = HttpContext.Current.Request.Form["VehicleNo"];
        }

        int start = ((fm.page - 1) * fm.rp);


        int iTotal = 0;

        List<VehicleModel> lst = new List<VehicleModel>();
        lst = GetVehicles(SessionId, fm.whereCondition, fm.sortExp, start, fm.rp, ref iTotal).ToList();

        XDocument xmlDoc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("rows", new XElement("page", fm.page.ToString()), new XElement("total", iTotal.ToString()),
                    lst.Select(row => new XElement("row", new XElement("cell", "<input type='checkbox' class='chck' name='chk' value= '" + row.VehicleID + "' />"),
                                                    new XElement("cell", row.VehicleID),
                                                    new XElement("cell", row.VehicleInfo)
                                                    )
                                )
                             )
        );

        XmlDocument newDoc = new XmlDocument();
        newDoc.LoadXml(xmlDoc.ToString());
        return newDoc;
    }

    public IQueryable<VehicleModel> GetVehicles(string SessionId, string whereClause, string sortExp, int startRowIndex, int numberOfRows, ref int iTotal)
    {
        DataSet ds = objBll.GetVehiclesBySessionId(SessionId, whereClause);
        var Vehicles = new List<VehicleModel>(ds.Tables[0].Rows.Count);
        foreach (DataRow row in ds.Tables[0].Rows)
        {
            var values = row.ItemArray;
            var v = new VehicleModel()
            {
                VehicleID = (string)values[0],
                VehicleInfo = (string)values[4],
            };
            Vehicles.Add(v);
        }
        iTotal = Vehicles.Count();
        return Vehicles.AsQueryable().Skip(startRowIndex).Take(numberOfRows);
    }



    /// <summary>
    /// Format
    /// maxLat#minLat#maxLong#minLong#latSpan#longSpan#extraZoom#zoom#fuel#Dial#PANIC SWITCH PRESENT(optional)#acOn/OffAlertPresent(OPTIONAL)#vehicleid#DateTime#LAt#long#Speed#id#vehicleInfo
    /// #Location#direction#idlingInstance(if enabled AC 0/1 or DUTY 0/1 OR NC)
    /// The vehicle information repeats for all selected vehicles
    /// </summary>
    /// <param name="SessionId"></param>
    /// <param name="whereClause"></param>
    /// <param name="sortExp"></param>
    /// <param name="startRowIndex"></param>
    /// <param name="numberOfRows"></param>
    /// <param name="iTotal"></param>
    /// <returns></returns>
    public IQueryable<VehicleModel> GetVehiclesInfo(string SessionId, string whereClause, string sortExp, int startRowIndex, int numberOfRows, ref int iTotal)
    {

        String posStr = objBll.GetVehiclesInfo(SessionId);
        String[] posStrSplit = posStr.Split('#');
        var Vehicles = new List<VehicleModel>();
        for (int i = 12; i < posStrSplit.Length - 1;)
        {
            var v = new VehicleModel()
            {
                VehicleID = (string)posStrSplit[i++],
                DateTime = (string)posStrSplit[i++],
                LAt = (string)posStrSplit[i++],
                longi = (string)posStrSplit[i++],
                Speed = (string)posStrSplit[i++],
                id = (string)posStrSplit[i++],
                VehicleInfo = (string)posStrSplit[i++],
                Location = (string)posStrSplit[i++],
                direction = (string)posStrSplit[i++],
                idlingInstance = (string)posStrSplit[i++],
                remarks = (string)posStrSplit[i++],
                IconType = (string)posStrSplit[i++],
                Battery = (string)posStrSplit[i++],
                HaltDuration = (string)posStrSplit[i++]
                //Remarks="", 
            };
            Vehicles.Add(v);
        }


        var data = Vehicles.AsQueryable();

        if (!string.IsNullOrEmpty(whereClause))
        {
            data = data.Where(c => c.VehicleID.Contains(whereClause) || c.VehicleInfo.Contains(whereClause));
        }

        iTotal = data.Count();

        return data.Skip(startRowIndex).Take(numberOfRows);


        //return Vehicles.AsQueryable().Skip(startRowIndex).Take(numberOfRows);
    }

    public IQueryable<VehicleModel> GetVehiclesInfo(string SessionId, string whereClause)
    {

        String posStr = objBll.GetVehiclesInfo(SessionId);
        String[] posStrSplit = posStr.Split('#');
        var Vehicles = new List<VehicleModel>();
        for (int i = 12; i < posStrSplit.Length - 1;)
        {
            var v = new VehicleModel()
            {
                VehicleID = (string)posStrSplit[i++],
                DateTime = (string)posStrSplit[i++],
                LAt = (string)posStrSplit[i++],
                longi = (string)posStrSplit[i++],
                Speed = (string)posStrSplit[i++],
                id = (string)posStrSplit[i++],
                VehicleInfo = (string)posStrSplit[i++],
                Location = (string)posStrSplit[i++],
                direction = (string)posStrSplit[i++],
                idlingInstance = (string)posStrSplit[i++],
                remarks = (string)posStrSplit[i++],
                IconType = (string)posStrSplit[i++],
                Battery = (string)posStrSplit[i++],
                HaltDuration = (string)posStrSplit[i++]
            };
            Vehicles.Add(v);
        }


        var data = Vehicles.AsQueryable();

        if (!string.IsNullOrEmpty(whereClause))
        {
            data = data.Where(c => c.VehicleID.Contains(whereClause) || c.VehicleInfo.Contains(whereClause));
        }

        return data;



    }


    /// <summary>
    /// Format
    /// maxLat#minLat#maxLong#minLong#latSpan#longSpan#extraZoom#zoom#fuel#Dial#PANIC SWITCH PRESENT(optional)#acOn/OffAlertPresent(OPTIONAL)#vehicleid#DateTime#LAt#long#Speed#id#vehicleInfo
    /// #Location#direction#idlingInstance(if enabled AC 0/1 or DUTY 0/1 OR NC)
    /// The vehicle information repeats for all selected vehicles
    /// </summary>
    /// <param name="SessionId"></param>
    /// <param name="vehicle"></param>
    /// <param name="dummy"></param> this is of no consequence as had to introduce another parameter so that it does not conflict with other ,emethod of same name
    /// <returns></returns>
    /// 
    public IQueryable<Vehicle> GetVehiclesInfo(string SessionId, string vehicle = "", string dummy = "")
    {
        String vtype = objBll.getUIDType(SessionId);
        String posStr = objBll.GetVehiclesInfo(SessionId, vehicle);
        String[] posStrSplit = posStr.Split('#');
        var Vehicles = new List<Vehicle>();
        if (vtype == "Asset")
        {
            for (int i = 12; i < posStrSplit.Length - 1;)
            {
                var v = new Vehicle()
                {
                    VehicleID = (string)posStrSplit[i++],
                    DateTime = (string)posStrSplit[i++],
                    LAt = (string)posStrSplit[i++],
                    longi = (string)posStrSplit[i++],
                    Speed = (string)posStrSplit[i++],
                    id = (string)posStrSplit[i++],
                    VehicleInfo = (string)posStrSplit[i++],
                    Location = (string)posStrSplit[i++],
                    direction = (string)posStrSplit[i++],
                    idlingInstance = (string)posStrSplit[i++],
                    remarks = (string)posStrSplit[i++],
                    IconType = (string)posStrSplit[i++],
                    Battery = (string)posStrSplit[i++],
                    HaltDuration = (string)posStrSplit[i++],
                    Bearer = (string)posStrSplit[i++],
                    SignalLevel = (string)posStrSplit[i++]
                };
                Vehicles.Add(v);
            }
        }
        else
        {
            for (int i = 12; i < posStrSplit.Length - 1;)
            {
                var v = new Vehicle()
                {
                    VehicleID = (string)posStrSplit[i++],
                    DateTime = (string)posStrSplit[i++],
                    LAt = (string)posStrSplit[i++],
                    longi = (string)posStrSplit[i++],
                    Speed = (string)posStrSplit[i++],
                    id = (string)posStrSplit[i++],
                    VehicleInfo = (string)posStrSplit[i++],
                    Location = (string)posStrSplit[i++],
                    direction = (string)posStrSplit[i++],
                    idlingInstance = (string)posStrSplit[i++],
                    remarks = (string)posStrSplit[i++],
                    IconType = (string)posStrSplit[i++],
                    Battery = (string)posStrSplit[i++],
                    HaltDuration = (string)posStrSplit[i++],
                    //Bearer = (string)posStrSplit[i++],
                    //SignalLevel = (string)posStrSplit[i++]
                    FuelLevel = (string)posStrSplit[i++]
                };
                Vehicles.Add(v);
            }
        }
        return Vehicles.AsQueryable();

    }

    public string GetStatus(string remark)
    {
        if (remark.Contains("Vehicle Halted"))
            return "<img src=\"images/redstop.png\" height=\"36\" width=\"36\" align=\"center\"/>";
        else if (remark.Contains("Not Reachable"))
            return "<img src=\"images/black.png\" height=\"36\" width=\"36\" align=\"center\"/>";
        else if (remark.Contains("GPS Not Active"))
            return "<img src=\"images/grey.png\" height=\"36\" width=\"36\" align=\"center\"/>";
        else if (remark.Contains("Vehicle Moving"))
            return "<img src=\"images/greengif.gif\" height=\"36\" width=\"36\" align=\"center\"/>";
        else if (remark.Contains("panic") || remark.Contains("Panic"))
            return "<img src=\"images/red.gif\" height=\"36\" width=\"36\" align=\"center\"/>";
        if (remark.Contains("Over Speeding"))
            return "<img src=\"images/yellow.png\" height=\"36\" width=\"36\" align=\"center\"/>";
        else return null;

    }



    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string getPALHData(string sPALHVal, string SessionId)
    {
        try
        {
            char[] delimiterChars = { '@' };
            string[] sVehicleID = sPALHVal.Split(delimiterChars);
            string sUserName = objBll.getUserId(SessionId);
            DateTime dt = Convert.ToDateTime(sVehicleID[1].ToString().Replace("/", "-"));
            objBll.disablePanic(sUserName, sVehicleID[0], dt.ToString("yyyy-MM-dd HH:mm:ss"));
            return "Panic disable successfully";
        }
        catch
        {
            return "Panic not disabled";
        }
    }
    //Added By Madhuri for Muthoot demo 23-08-2024
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string DisableTheftAlert(string sPALHVal, string SessionId,string Remarks)
    {
        try
        {
            char[] delimiterChars = { '@' };
            string[] sVehicleID = sPALHVal.Split(delimiterChars);
            string sUserName = objBll.getUserId(SessionId);
            DateTime dt = Convert.ToDateTime(sVehicleID[1].ToString().Replace("/", "-"));
            objBll.disableTheftAlert(sUserName, sVehicleID[0], dt.ToString("yyyy-MM-dd HH:mm:ss"), Remarks);
            return "Theft Alert cleared successfully";
        }
        catch (Exception ex)
        {
            return "Theft Alert not cleared successfully";
        }
    }
    //Get source and destinations for driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRouteDestinations()
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        DataTable[] tables = objBll.GetSourceDestination("totalgas");
        string str = "";
        var sriali = new JavaScriptSerializer();
        if (tables.Length > 0)
        {
            for (int i = 0; i < tables[1].Rows.Count; i++)
            {
                if (i == 0)
                    str = tables[1].Rows[i]["Destination"].ToString();
                else
                    str = str + "," + tables[1].Rows[i]["Destination"].ToString();
            }
        }
        string[] segments = str.Split(',');
        return sriali.Serialize(segments);

    }
    //Get source and destinations for driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRouteSourceDestinationsByIMEI(string IMEI)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        string accountid = objBll.GetDriverAccountByIMEI(IMEI);
        DataTable[] tables = objBll.GetSourceDestination("", accountid);

        List<Dictionary<string, object>> ParentRow = new List<Dictionary<string, object>>();

        var sriali = new JavaScriptSerializer();
        if (tables.Length > 0)
        {
            for (int i = 0; i < tables[0].Rows.Count; i++)
            {
                Dictionary<string, object> childRow = new Dictionary<string, object>();
                childRow.Add("Source", tables[0].Rows[i]["Source"].ToString());
                ParentRow.Add(childRow);
            }
            for (int i = 0; i < tables[1].Rows.Count; i++)
            {
                Dictionary<string, object> childRow = new Dictionary<string, object>();
                childRow.Add("Destination", tables[1].Rows[i]["Destination"].ToString());
                ParentRow.Add(childRow);
            }
        }
        return sriali.Serialize(ParentRow);

    }


    //Get source and destinations for driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRouteSourceDestinations(string login)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        DataTable[] tables = objBll.GetSourceDestination(login);

        List<Dictionary<string, object>> ParentRow = new List<Dictionary<string, object>>();

        var sriali = new JavaScriptSerializer();
        if (tables.Length > 0)
        {
            for (int i = 0; i < tables[0].Rows.Count; i++)
            {
                Dictionary<string, object> childRow = new Dictionary<string, object>();
                childRow.Add("Source", tables[0].Rows[i]["Source"].ToString());
                ParentRow.Add(childRow);
            }
            for (int i = 0; i < tables[1].Rows.Count; i++)
            {
                Dictionary<string, object> childRow = new Dictionary<string, object>();
                childRow.Add("Destination", tables[1].Rows[i]["Destination"].ToString());
                ParentRow.Add(childRow);
            }
        }
        return sriali.Serialize(ParentRow);

    }

    //for driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRouteSegments(string source, string destination, string SessionId)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        DataRow[] value = objBll.GetRouteSegments(source, destination, SessionId);
        string str = "";
        var sriali = new JavaScriptSerializer();
        if (value.Length > 0)
        {
            str = value[0][0].ToString();
            sriali.MaxJsonLength = Int32.MaxValue;
        }
        string[] segments = str.Split(',');
        return sriali.Serialize(segments);

    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetReffRouteData(string ReffRoute, string SessionId)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();

        List<ReffRoue> ReffRouteName = new List<ReffRoue>();
        List<ReffRoueLatLng> ReffRoutelatlng = new List<ReffRoueLatLng>();

        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        if (ReffRoute != string.Empty)
        {

            DataRow[] value = ObjBll.ReffRoute(SessionId, ReffRoute);
            for (int i = 0; i < value.Length; i++)
            {
                var reffroutelatlng = new ReffRoueLatLng()
                {
                    Lat = value[i][0].ToString(),
                    Lng = value[i][1].ToString()

                };
                ReffRoutelatlng.Add(reffroutelatlng);

            }
            return sriali.Serialize(ReffRoutelatlng);
        }
        else
        {
            DataRow[] value = ObjBll.ReffRoute(SessionId);
            for (int i = 0; i < value.Length; i++)
            {
                var reffroutename = new ReffRoue()
                {
                    ReffRouteName = value[i][0].ToString(),

                };
                ReffRouteName.Add(reffroutename);

            }
            return sriali.Serialize(ReffRouteName);
        }

    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetPlacesData(string CurLat, string CurLong, string type)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        List<Place> places = new List<Place>();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataRow[] value = ObjBll.GetPlaces(CurLat, CurLong, type);
        for (int i = 0; i < value.Length; i++)
        {
            var curplace = new Place()
            {
                Name = value[i][1].ToString(),
                Address = value[i][3].ToString(),
                Phone = value[i][4].ToString(),
                Latitude = value[i][5].ToString(),
                Longitude = value[i][6].ToString()

            };
            places.Add(curplace);

        }
        return sriali.Serialize(places);


    }
    //for finding destination
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetSourceDestination(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        List<Place> places = new List<Place>();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        string value = ObjBll.GetsourceDestination(IMEI);
        return sriali.Serialize(value);


    }
    //for driverapp
    [WebMethod]
    public string GetAllPlacesDataByImei(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        List<Place> places = new List<Place>();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataRow[] value = ObjBll.GetjrmSpots(IMEI, "map");
        for (int i = 0; i < value.Length; i++)
        {
            var curplace = new Place()
            {
                Latitude = value[i][0].ToString(),
                Longitude = value[i][1].ToString(),
                Name = value[i][2].ToString(),
                Address = value[i][3].ToString(),
                Phone = value[i][4].ToString()
            };
            places.Add(curplace);
        }
        return sriali.Serialize(places);
    }
    //for getting jrmSpots in driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetjrmSpots(string IMEI)
    {
        try
        {
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            DataRow[] value = ObjBll.GetjrmSpots(IMEI, "dashboard");
            if (value.Length > 0)
            {
                string str = "";
                for (int i = 0; i < value.Length; i++)
                {
                    str += value[i][0].ToString() + "#" + value[i][1].ToString() + "#"
                        + value[i][2].ToString() + "#" + value[i][3].ToString() + "#"
                        + value[i][4].ToString() + "|";
                }
                return str;
            }
            else
                return "No jrmSpots";
        }
        catch
        {
            return "No jrmSpots";
        }
    }
    //for inserting mobilegps data from driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string insertMobileGpsData(string IMEI, string data)
    {
        try
        {
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            int value = ObjBll.insertMobileGpsData(IMEI, data);
            if (value > 0)
                return "Data Inserted successfully";
            else
                return "Failed to insert";
        }
        catch
        {
            return "Failed to insert";
        }
    }
    //Log for driver app for audio playing
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string insertlogAudioData(String imei, String logData)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable dt = objBll.getDriverDataByIMEI(imei);
        if (dt.Rows.Count > 0)
        {
            try
            {
                int value = ObjBll.insertlogAudioData(dt.Rows[0]["AssignedVehicleId"].ToString(), logData);
                if (value > 0)
                    return "Data Inserted successfully";
                else
                    return "Failed to insert";
            }
            catch
            {
                return "Failed to insert";
            }
        }
        else
            return "No Vehicle Assigned";
    }
    //for emergency vehicles
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetEmergencyVehicles()
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        List<EmergencyVehicle> vehicles = new List<EmergencyVehicle>();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        // DataRow[] value = ObjBll.Getemergencyvehicle();
        return sriali.Serialize(vehicles);
    }
    //for emergency vehicles
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetEmergencyVehiclesNew(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        List<EmergencyVehicle> vehicles = new List<EmergencyVehicle>();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        string accountid = objBll.GetDriverAccountByIMEI(IMEI);
        DataRow[] value = ObjBll.Getemergencyvehicle(accountid);
        for (int i = 0; i < value.Length; i++)
        {
            var vehicle = new EmergencyVehicle()
            {
                ID = value[i][0].ToString(),
                Latitude = value[i][1].ToString(),
                Longitude = value[i][2].ToString(),
                time = value[i][3].ToString()
            };
            vehicles.Add(vehicle);
        }
        return sriali.Serialize(vehicles);
    }
    //for storing NFC data
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string NFCData(string IMEI, string NFC, string lat, string lng, string tagid)
    {
        bool result;
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        int rows = objBll.InsertNFCData(IMEI, NFC, lat, lng, tagid);
        if (rows > 0)
            result = true;
        else
            result = false;
        return sriali.Serialize(result);
    }

    //for storing Position data
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string PositionData(string IMEI, string lat, string lng)
    {
        bool result;
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        int rows = objBll.InsertMobilePositionData(IMEI, lat, lng);
        if (rows > 0)
            result = true;
        else
            result = false;
        return sriali.Serialize(result);
    }

    //for checking notifications
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string CheckNotification(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.GetNotifications(IMEI).Tables[0];
        if (table.Rows.Count > 0)
        {
            List<Notification> notifications = new List<Notification>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var notification = new Notification()
                {
                    Driverid = table.Rows[i][0].ToString(),
                    Subject = table.Rows[i][1].ToString(),
                    Info = table.Rows[i][2].ToString(),
                    Date = table.Rows[i][3].ToString(),
                    count = table.Rows[0][4].ToString(),
                    Isnotified = table.Rows[i][5].ToString(),
                    Priority = table.Rows[i][6].ToString()
                };
                notifications.Add(notification);
            }
            return sriali.Serialize(notifications);
        }
        else
        {
            return "0";
        }
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string CheckDriverAppNotification(string MobileNo)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.GetDriverAppNotifications(MobileNo).Tables[0];
        if (table.Rows.Count > 0)
        {
            List<Notification> notifications = new List<Notification>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var notification = new Notification()
                {
                    Driverid = table.Rows[i][0].ToString(),
                    Subject = table.Rows[i][1].ToString(),
                    Info = table.Rows[i][2].ToString(),
                    Date = table.Rows[i][3].ToString(),
                    count = table.Rows[0][4].ToString(),
                    Isnotified = table.Rows[i][5].ToString(),
                    Priority = table.Rows[i][6].ToString()
                };
                notifications.Add(notification);
            }
            return sriali.Serialize(notifications);
        }
        else
        {
            return "0";
        }
    }

    //for driver information
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDriverinfo()
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        object[] table = objBll.GetDriversData();

        if (table.Length > 0)
        {
            List<Driver> drivers = new List<Driver>();
            for (int i = 0; i < table.Length; i++)
            {

                object[] row = (object[])table[i];
                var driver = new Driver()
                {

                    Driverid = row[0].ToString(),
                    Drivername = row[1].ToString(),
                    mobile = row[2].ToString(),
                    Imei = row[5].ToString(),
                    Licenceid = row[3].ToString().Replace('/', '|'),
                };
                drivers.Add(driver);
            }
            return sriali.Serialize(drivers);
        }
        else
        {
            return "0";
        }
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void UpdateNotifiedDate(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        objBll.UpdateNotifiedDate(IMEI);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void UpdateNotifiedDateDriverApp(string MobileNo)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        objBll.UpdateNotifiedDateDriverApp(MobileNo);
    }

    //for driver points
    [WebMethod]
    public string GetDriverpoints(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        string res = objBll.Driverpoints(IMEI);
        return res.ToString();

    }

    //for transportorwise driver points
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetTransportorpoints(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.TransportorPoints(IMEI);
        List<points> drivers = new List<points>();
        if (table.Rows.Count > 0)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var driver = new points()
                {
                    name = table.Rows[i][0].ToString(),
                    point = table.Rows[i][1].ToString()
                };
                drivers.Add(driver);
            }
        }
        else
        {
            var driver = new points()
            {
                name = "No Data",
                point = "0"
            };
            drivers.Add(driver);
        }
        return sriali.Serialize(drivers);
    }
    //for best driver points
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetBestDriverByIMEI(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        string accountId = objBll.GetDriverAccountByIMEI(IMEI);
        DataTable table = objBll.BestDriver(accountId);
        if (table.Rows.Count > 0 && accountId != "0")
        {
            var driver = new points()
            {
                name = table.Rows[0][0].ToString(),
                point = table.Rows[0][1].ToString(),
                t_name = table.Rows[0][2].ToString()
            };
            return sriali.Serialize(driver);
        }
        else
        {
            var driver = new points()
            {
                name = "No Data",
                point = "0",
                t_name = "No Data"
            };
            return sriali.Serialize(driver);
        }
    }
    //for best driver points
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetBestDriver()
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.BestDriver();
        if (table.Rows.Count > 0)
        {
            var driver = new points()
            {
                name = table.Rows[0][0].ToString(),
                point = table.Rows[0][1].ToString(),
                t_name = table.Rows[0][2].ToString()
            };
            return sriali.Serialize(driver);
        }
        else
        {
            var driver = new points()
            {
                name = "No Data",
                point = "0",
                t_name = "No Data"
            };
            return sriali.Serialize(driver);
        }
    }
    //keep useractivity of driverapp
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void Keepuseractivitylog(string imei, string page, string lat, string lng)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        objBll.keeplog(imei, page, lat, lng);
    }
    //keep useractivity of PassengerPro App - 11-05-2026
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void KeepPassengerProuseractivitylog(string passengerId, string vehicleId, string page, string lat, string lng, string AppVersion)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        objBll.keepPassengerApplog(passengerId, vehicleId, page, lat, lng, AppVersion);
    }
    //for transportor app
    //check login
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetLoginDetails(string username, string password)
    {
        string res = "false";
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable dt = objBll.GetLoginDetails(username, password);
        if (dt.Rows.Count > 0)
        {
            if (string.IsNullOrEmpty(dt.Rows[0][2].ToString()))
                res = dt.Rows[0][0].ToString() + "@#" + sHost + "@#" + sHost;
            else
                res = dt.Rows[0][0].ToString() + "@#" + dt.Rows[0][1].ToString() + "@#" + dt.Rows[0][2].ToString();
        }
        Thread rlog = new Thread(() => ObjBll.WebServiceRequestlog("GetLoginDetails", "Fleet-Smart", "", username, "", sHost, sUserIp));
        rlog.Start();
        return res;
    }

    //for transportor app
    //check login capture IMEI
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetLoginDetailsIMEI(string username, string password, string IMEI, string latlng)
    {
        string res = "false";
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable dt = objBll.GetLoginDetails(username, password);
        if (dt.Rows.Count > 0)
        {
            if (string.IsNullOrEmpty(dt.Rows[0][2].ToString()))
                res = dt.Rows[0][0].ToString() + "@#" + sHost + "@#" + sHost;
            else
                res = dt.Rows[0][0].ToString() + "@#" + dt.Rows[0][1].ToString() + "@#" + dt.Rows[0][2].ToString();
        }
        Thread rlog = new Thread(() => ObjBll.WebServiceRequestlogIMEI("GetLoginDetails", "Fleet-Smart", IMEI + ";" + latlng, username, "", sHost, sUserIp));
        rlog.Start();
        return res;
    }

    //Get menus by loginid for mobile app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetMenusByUSer(string username)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable dt = objBll.GetMenusByUser(username);
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;

        List<MobileMenu> menus = new List<MobileMenu>();
        for (int i = 0; i < dt.Rows.Count; i++)
        {

            DataRow row = dt.Rows[i];
            var menu = new MobileMenu()
            {
                Menukey = row[1].ToString(),
                MenuValue = row[2].ToString()
            };
            menus.Add(menu);
        }
        Thread rlog = new Thread(() => ObjBll.WebServiceRequestlog("GetMenusByUSer", "Fleet-Smart", "", username, "", sHost, sUserIp));
        rlog.Start();
        return sriali.Serialize(menus);
    }

    //check session
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetSessionid(string username)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string sessionid = objBll.getSessionIdFromLogin(username);
        return sessionid;
    }

    //check session
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetMapType(string username)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.getMapTypeFromLogin(username);
    }

    //get Seesionid From IMEI. Usef by SMART_DRIVER
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetSessionIDByIMEI(string IMEI)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        Imei result = new Imei();
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        //return objBll.getSessionIdFromIMEI(IMEI);
        DataTable table = objBll.getSessionIdFromIMEI(IMEI);
        if (table != null)
        {
            if (table.Rows.Count > 0)
            {
                result.session = table.Rows[0]["sessionid"].ToString();
                result.account = table.Rows[0]["Name"].ToString();
            }
            else
            {
                result.session = "Error";
                result.account = "Error";
            }
        }
        else
        {
            result.session = "Error";
            result.account = "Error";
        }

        return js.Serialize(result);
    }

    //for checking transporter notifications
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string CheckTransporterNotification(string login)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.GetTransporterNotifications(login).Tables[0];
        if (table.Rows.Count > 0)
        {
            List<Notification> notifications = new List<Notification>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var notification = new Notification()
                {
                    Driverid = table.Rows[i][0].ToString(),
                    Subject = table.Rows[i][1].ToString(),
                    Info = table.Rows[i][2].ToString(),
                    Date = table.Rows[i][3].ToString(),
                    count = table.Rows[0][4].ToString(),
                    Isnotified = table.Rows[i][5].ToString(),
                    Priority = table.Rows[i][5].ToString()
                };
                notifications.Add(notification);
            }
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("CheckTransporterNotification", "Fleet-Smart", "", login, "", sHost, sUserIp));
            rlog.Start();
            return sriali.Serialize(notifications);
        }
        else
        {
            return "0";
        }
    }

    //transporter app notification
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void UpdateTransporterNotifiedDate(string login)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        objBll.UpdateTransporterNotifiedDate(login);
    }

    //for checking transporter notifications
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetCCM_Notification(string login)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.GetCCM_Notifications(login).Tables[0];
        if (table.Rows.Count > 0)
        {
            List<Notification> notifications = new List<Notification>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var notification = new Notification()
                {
                    Driverid = table.Rows[i][0].ToString(),
                    Subject = table.Rows[i][1].ToString(),
                    Info = table.Rows[i][2].ToString(),
                    Date = table.Rows[i][3].ToString(),
                    count = table.Rows[0][4].ToString(),
                    Isnotified = table.Rows[i][5].ToString(),
                    Priority = table.Rows[i][5].ToString()
                };
                notifications.Add(notification);
            }
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetCCM_Notifications", "Smart_Track CCM", "", login, "", sHost, sUserIp));
            rlog.Start();
            return sriali.Serialize(notifications);
        }
        else
        {
            return "0";
        }
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void UpdateCCM_NotifiedDate(string login)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        objBll.UpdateCCM_NotifiedDate(login);
    }

    //Customer details
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetCustomersData()
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        object[] table = objBll.GetCustomersData();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;


        List<Customer> customers = new List<Customer>();
        for (int i = 0; i < table.Length; i++)
        {

            object[] row = (object[])table[i];
            var customer = new Customer()
            {

                CustomerName = row[0].ToString(),
                Customercode = row[1].ToString(),
            };
            customers.Add(customer);
        }
        return sriali.Serialize(customers);

    }
    [WebMethod]
    public void InsertUnloadQty(string IMEI, string CustomerCode, string grossweight, string tareweight)
    {

        string VehicleId = objBll.GetVehilceByIMEI(IMEI);
        objBll.AddAppUnloadQtyDetails(IMEI, CustomerCode, VehicleId, grossweight, tareweight);
        decimal UnloadedQty = (Convert.ToDecimal(grossweight) - Convert.ToDecimal(tareweight));
        DataTable plant = objBll.getPlantGeoLocation(CustomerCode);
        if (plant.Rows.Count > 0)
        {
            objBll.InsertUnloadQuantity(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), "", CustomerCode);
            Thread t = new Thread(() => { CustomerloadedDCs.sendMailForUnload(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), "", "Driver"); });
            t.Start();
            Thread unchk = new Thread(() => { CustomerDCAssign.UpdateDeliveryConfirm("0"); });
            unchk.Start();
        }
    }
    [WebMethod]
    public string InsertUnloadQtyNew(string IMEI, string CustomerCode, string grossweight, string tareweight, string photo)
    {
        string result = null;
        if (string.IsNullOrEmpty(IMEI))
            return "Reported Successfully";
        string VehicleId = objBll.GetVehilceByIMEI(IMEI);
        if (!string.IsNullOrEmpty(VehicleId))
        {
            objBll.AddAppUnloadQtyDetails(IMEI, CustomerCode, VehicleId, grossweight, tareweight);
            decimal UnloadedQty = (Convert.ToDecimal(grossweight) - Convert.ToDecimal(tareweight));
            DataTable plant = objBll.getPlantGeoLocation(CustomerCode);
            if (plant.Rows.Count > 0)
            {
                result = "Reported Successfully";
                objBll.InsertUnloadQuantity(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), photo, CustomerCode);
                Thread t = new Thread(() => { CustomerloadedDCs.sendMailForUnload(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), photo, "Driver"); });
                t.Start();
                Thread unchk = new Thread(() => { CustomerDCAssign.UpdateDeliveryConfirm("0"); });
                unchk.Start();
            }
            else
            {
                plant = objBll.GetCustomerData("", CustomerCode).Tables[0];
                if (plant.Rows.Count > 0)
                {
                    result = "Reported Successfully";
                    objBll.InsertUnloadQuantity(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["Name"].ToString(), UnloadedQty.ToString(), "", photo, CustomerCode);
                }
                else
                    result = "Wrong customer";
            }
        }
        else
        {
            result = "Vehicle No Not Assgined to this Mobile,Please contact TotalGaz";
        }
        return result;
    }

    [WebMethod]
    public string UnloadCustomerDC(string CustomerCode, string VehicleId, string grossweight, string tareweight)
    {
        try
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            objBll.AddAppUnloadQtyDetails("Web", CustomerCode, VehicleId, grossweight, tareweight);
            decimal UnloadedQty = (Convert.ToDecimal(grossweight) - Convert.ToDecimal(tareweight));
            DataTable plant = objBll.getPlantGeoLocation(CustomerCode);
            if (plant.Rows.Count > 0)
            {
                objBll.InsertUnloadQuantity(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), "", CustomerCode);
                Thread t = new Thread(() => { CustomerloadedDCs.sendMailForUnload(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), "", "Customer"); });
                t.Start();
                Thread unchk = new Thread(() => { CustomerDCAssign.UpdateDeliveryConfirm("0"); });
                unchk.Start();
            }
            return "Reported Successfully";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
    [WebMethod]
    public string UnloadCustomerDCSTK(string CustomerCode, string VehicleId, string grossweight, string tareweight, string Plant)
    {
        try
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            objBll.AddAppUnloadQtyDetails("Web", CustomerCode, VehicleId, grossweight, tareweight);
            decimal UnloadedQty = (Convert.ToDecimal(grossweight) - Convert.ToDecimal(tareweight));
            DataTable plant = objBll.getPlantGeoLocation(CustomerCode);
            if (plant.Rows.Count > 0)
            {
                objBll.InsertUnloadQuantity(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), "", CustomerCode, Plant);
                Thread t = new Thread(() => { CustomerloadedDCs.sendMailForUnload(VehicleId, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), plant.Rows[0]["locstr"].ToString(), UnloadedQty.ToString(), plant.Rows[0]["unloademailid"].ToString(), "", "Customer"); });
                t.Start();
                Thread unchk = new Thread(() => { CustomerDCAssign.UpdateDeliveryConfirm("0"); });
                unchk.Start();
            }
            return "Reported Successfully";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    [WebMethod]
    public string addDriverNotifications(string Vehicle, string Subject, string Detail)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        int id = objBll.AddNotifications(Vehicle, Subject, Detail);
        return id.ToString();
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetvehiclesBysession(string SessionId)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetVehiclesBySessionId(SessionId).Tables[0];
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;


        List<VehicleModel> vehicles = new List<VehicleModel>();
        for (int i = 0; i < table.Rows.Count; i++)
        {

            DataRow row = table.Rows[i];
            var vehicle = new VehicleModel()
            {

                VehicleID = row[0].ToString(),
            };
            vehicles.Add(vehicle);
        }
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetvehiclesBysession", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
        rlog.Start();
        return sriali.Serialize(vehicles);
    }

    //Running hours for Smart_Driver
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRunningTime(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string vehicle = objBll.GetVehilceByIMEI(IMEI);
        DataRow row = objBll.GetRunningTime(vehicle, IMEI);
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        if (row != null && row[1] != null)
        {
            return row[0].ToString() + "#" + Convert.ToDateTime(row[1].ToString()).ToString("dd-MM-yyyy hh:mm:ss tt") + "#" + vehicle;
        }
        else
        {
            return "No updates";
        }
    }

    //Running hours for Smart_Driver
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRunAndContinuousTime(string IMEI)
    {
        try
        {
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string vehicle = objBll.GetVehilceByIMEI(IMEI);
            DataRow row = objBll.GetRunAndContinuousTime(vehicle, IMEI);
            var sriali = new JavaScriptSerializer();
            sriali.MaxJsonLength = Int32.MaxValue;
            if (row != null && row[1] != null)
            {
                return row[0].ToString() + "#" + Convert.ToDateTime(row[1].ToString()).ToString("dd-MM-yyyy hh:mm:ss tt") + "#" + row[2].ToString() + "#" + row[3].ToString() + "#" + vehicle;
            }
            else
            {
                return "No updates";
            }
        }
        catch
        {
            return "No updates";
        }
    }

    //jrm limitations for Smart_Driver
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetJrmLimitations(string IMEI)
    {
        try
        {
            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            string vehicle = objBll.GetVehilceByIMEI(IMEI);
            DataTable dt = objBll.GetJrmLimitations(vehicle, IMEI);
            if (dt != null && dt.Rows.Count > 0)
            {
                string str = "";
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (String.IsNullOrEmpty(dt.Rows[0][i].ToString()))
                        return "No updates";
                    else
                        str += dt.Rows[0][i].ToString();
                    if (i != dt.Columns.Count - 1)
                        str += "#";
                }
                return str;
            }
            else
            {
                return "No updates";
            }
        }
        catch
        {
            return "10h 0min#08:00:00 PM#05:00:00 AM#2h 0min#0h 20min#4h 30min#0h 45min#60#2592000#600#259200000#30#10#0#Sensel12345";
        }
    }

    //Prefered Language of driver for Smart_Driver
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetLanguage(string IMEI)
    {
        try
        {

            BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
            return objBll.GetLanguageByIMEI(IMEI).ToString();
        }
        catch
        {
            return "English-English";
        }
    }
    //Driver Checklist
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public int InsertChecklist(string IMEI, bool EngineOil, bool CoolantLevel, bool WaterSeperator, bool EDCWarningLight, bool VaccumIndicator, bool TyrePressure, bool WheelNuts, bool TyreWear, bool Gauges, bool Wiper, bool CabinLights, bool WaterLevel, bool Battery, bool GPS, string photos)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        //insert driver activity
        objBll.keeplog(IMEI, "Vehicle Checklist", "0.000000", "0.000000");
        return ObjBll.InsertChecklist(IMEI, EngineOil, CoolantLevel, WaterSeperator, EDCWarningLight, VaccumIndicator, TyrePressure, WheelNuts, TyreWear, Gauges, Wiper, CabinLights, WaterLevel, Battery, GPS, photos);
    }
    //Driver Checklist
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public int InsertChecklistNew(string IMEI, bool EngineOil, bool CoolantLevel, bool WaterSeperator, bool EDCWarningLight, bool VaccumIndicator, bool TyrePressure, bool WheelNuts, bool TyreWear, bool Gauges, bool Wiper, bool CabinLights, bool WaterLevel, bool Battery, string photos)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        //insert driver activity
        //objBll.keeplog(IMEI, "Vehicle Checklist", "0.000000", "0.000000");
        return ObjBll.InsertChecklist(IMEI, EngineOil, CoolantLevel, WaterSeperator, EDCWarningLight, VaccumIndicator, TyrePressure, WheelNuts, TyreWear, Gauges, Wiper, CabinLights, WaterLevel, Battery, true, photos);
    }

    /*[WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDriverChecklistByLanguage(string imei)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable dt = ObjBll.GetDriverChecklistByLanguage(imei);

        var grouped = dt.AsEnumerable()
            .GroupBy(row => new
            {
                Code = row.Field<string>("code"),
                Icon = row.Field<string>("icon"),
                Title = row.Field<string>("title")
            })
            .Select(g => new
            {
                code = g.Key.Code,
                icon = g.Key.Icon,
                title = g.Key.Title,
                items = g.Select(item => new
                {
                    id = item.Field<string>("items"),
                    description = item.Field<string>("description")
                }).ToList()
            }).ToList();

        return new JavaScriptSerializer().Serialize(grouped);
    }*/


    //For violationreport in fleet_Smart
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetViolationReport(string SessionId)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.Getviolation(SessionId);
        if (table.Rows.Count > 0)
        {
            List<ViolationReport> violations = new List<ViolationReport>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var vehicle = new ViolationReport()
                {
                    vehicleid = table.Rows[i][0].ToString(),
                    nightdriving = table.Rows[i][1].ToString(),
                    overspeed = table.Rows[i][2].ToString(),
                    harsebrake = table.Rows[i][3].ToString(),
                    runtime = table.Rows[i][4].ToString()
                };
                violations.Add(vehicle);
            }
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("ViolationReport", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
            rlog.Start();
            return sriali.Serialize(violations);

        }
        else
        {
            return "0";
        }
    }

    //For ConsolidatedReport in fleet_Smart
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetConsolidatedReport(string SessionId)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable table = objBll.GetConsolidatedReport(SessionId);

        if (table.Rows.Count > 0)
        {
            List<ConsolidatedReport> vehicles = new List<ConsolidatedReport>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var vehicle = new ConsolidatedReport()
                {
                    vehicleid = table.Rows[i][0].ToString(),
                    yesterday = table.Rows[i][1].ToString(),
                    thismonth = table.Rows[i][2].ToString(),
                    thisyear = table.Rows[i][3].ToString()
                };
                vehicles.Add(vehicle);

            }
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("ConsolidatedReport", "Fleet-Smart", "", "", SessionId, sHost, sUserIp));
            rlog.Start();
            return sriali.Serialize(vehicles);
        }
        else
        {
            return "0";
        }
    }

    //For Distance Report in fleet smart app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDistanceReport(string vehicleId, string fromdate, string todate)
    {
        DateTime fromDate = Convert.ToDateTime(fromdate);
        DateTime toDate = Convert.ToDateTime(todate);
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        int count = 0;
        double totalDistance = 0;
        DateTime startDate = fromDate;
        Object lastLatLong = null;
        List<DistanceReport> trips = new List<DistanceReport>();
        while (toDate.CompareTo(fromDate) > 0 && count++ < 31)
        {
            DistanceReport trip = new DistanceReport();
            String tripStartStr = ObjBll.formatDBDate(fromDate);
            DateTime tripEnd = fromDate.AddDays(1);
            // from date can have any start time. Days in between should end at midnight
            tripEnd = tripEnd.AddSeconds(-1 * tripEnd.Second - tripEnd.Minute * 60 - tripEnd.Hour * 60 * 60);
            // last date should end on toDate.
            if (tripEnd.CompareTo(toDate) > 0)
            {
                tripEnd = toDate;
            }
            String tripEndStr = ObjBll.formatDBDate(tripEnd);
            double tripDistance = 0;
            Object[] ret = ObjBll.accumulateDistance(vehicleId, tripStartStr, tripEndStr, lastLatLong);
            tripDistance = (double)ret[0];
            lastLatLong = ret[1];
            totalDistance += tripDistance;

            trip.vehicleid = vehicleId;
            trip.tripdistance = Math.Round(tripDistance, 2);
            trip.totaldistance = Math.Round(totalDistance, 2);
            trip.tripdate = fromDate.ToString("dd-MM-yyyy");
            trips.Add(trip);

            fromDate = tripEnd;

        }
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("ConsolidatedReport", "Fleet-Smart", "", "", objBll.getSessionIdByVehicle(vehicleId), sHost, sUserIp));
        rlog.Start();
        return sriali.Serialize(trips);

    }

    [WebMethod]
    public string addTransporterNotifications(string IMEI, string message)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        XmlDBAccess dbx = new XmlDBAccess();
        string vehicle = objBll.GetVehilceByIMEI(IMEI);
        if (!string.IsNullOrEmpty(vehicle))
        {
            if (message.Contains("Panic button pressed"))
                dbx.insertPanicData(vehicle, DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff()), "panic");//insert into panic table
            int id = 0;
            try
            {
                string[] driver = dbx.getdrivername(vehicle, DateTime.Now.ToString("yyyy-MM-dd")).Split(',');
                string login = dbx.GetTransporterloginByVehicleId(vehicle);
                if (message.Contains("Panic button pressed"))
                {
                    id = objBll.AddTransporterNotifications(login, "Panic pressed(" + vehicle + ")", message, 1);
                }
                else
                {
                    id = objBll.AddTransporterNotifications(login, driver[0] + "(" + vehicle + ")", message, 1);
                }
            }
            catch
            {

            }
            return id.ToString();
        }
        else
        {
            return "0";
        }
    }
    [WebMethod]
    public string addDriverPanicNotifications(string MobileNo,string vehicle, string message)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        XmlDBAccess dbx = new XmlDBAccess();
        if (!string.IsNullOrEmpty(vehicle))
        {
            if (message.Contains("Panic button pressed"))
                dbx.insertPanicData(vehicle, DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff()), "panic");//insert into panic table
            int id = 0;
            try
            {
                DataTable driverData = dbx.GetDriverDataForDriverApp(MobileNo);
                string login = dbx.GetTransporterloginByVehicleId(vehicle);
                if (message.Contains("Panic button pressed"))
                {
                    id = objBll.AddTransporterNotifications(login, "Panic pressed(" + vehicle + ")", message, 1);
                    string Driver_Name = "";
                    string Driver_MobileNo = "";
                    if (driverData.Rows.Count > 0)
                    {
                        Driver_Name = driverData.Rows[0]["Name"].ToString();
                        Driver_MobileNo = driverData.Rows[0]["MobileNo"].ToString();
                        try
                        {
                            DataTable dtMail = objBll.GetMailIdsConfig(Convert.ToInt32(driverData.Rows[0]["AccountId"].ToString()), "Driver_APP_PANIC", "");
                            if (dtMail.Rows.Count > 0)
                            {
                                string body = "Dear Sir/Madam,<br/><br/>Following Driver pressed panic button through Driver App.<br/>";
                                body += "<br/>Driver Name:" + Driver_Name + " ";
                                body += "<br/>Driver MobileNo:" + Driver_MobileNo;
                                body += "<br/>Vehicle Id:" + vehicle;
                                body += "<br/><br/>Best Regards,<br/>Sensel Telematics ";
                                Thread mail = new Thread(() => SendMail(vehicle + "-Driver pressed Panic Button ", body, "", "", "", dtMail));
                                mail.Start();
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    //id = objBll.AddTransporterNotifications(login, driverData.Rows[0]["Name"].ToString() + "(" + vehicle + ")", message, 1);
                    string updatedMessage = message;

                    // Check if message contains <img developed by zoya on 11/04/2026 to save s3 path.
                    if (!string.IsNullOrEmpty(message) && message.Contains("<img"))
                    {
                        updatedMessage = System.Text.RegularExpressions.Regex.Replace(
                            message,
                            @"src=['""]https?:\/\/[^\/]+\/Uploads\/placeinfo\/",
                            "src='https://db-flatfile-backup.s3.us-east-1.amazonaws.com/fleetsmart3.sensel.in/App+Files/sensel/Sensel.in/fleetsmart3.ui.sensel.in/Uploads/"
                        );
                    }

                    id = objBll.AddTransporterNotifications(login,driverData.Rows[0]["Name"].ToString() + "(" + vehicle + ")",updatedMessage,1);
                }
            }
            catch
            {

            }
            return id.ToString();
        }
        else
        {
            return "0";
        }
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string DriverAppPassengerOTPValidate(string otp, string Vehicle)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("DriverAppPassengerOTPValidate", "Vehicle" + Vehicle, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.DriverAppPassengerOTPValidate(otp, Vehicle);
        if (dt == null || dt.Rows.Count == 0)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in dt.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in dt.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRoadApi(string pposition, string cposition)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.GetRoadApi(pposition, cposition);
    }
    //[WebMethod]
    //[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    //public Object[] GetMissedPoints(string missedpointsstarttime,string vehList)
    //{
    //    BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
    //    DateTime Todate = DateTime.Now;
    //    DateTime StartTime = DateTime.Parse(missedpointsstarttime);
    //    string fromDateStr = ObjBll.formatDBDate(StartTime);
    //    DateTime EndTime = DateTime.Parse(Todate.ToString());
    //    string toDateStr = ObjBll.formatDBDate(EndTime);
    //    return objBll.getRouteLatLong(vehList, fromDateStr, toDateStr);
    //}

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetRoadApiMMI(string pposition, string cposition)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.GetRoadApiMMI(pposition, cposition);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string ReportAccident(string vehicleid, string datetime, string username)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        AccidentReport acc = new AccidentReport();
        //Thread t = new Thread(() => { acc.GenerateAccidentReport(vehicleid, datetime, username,"1"); });
        //t.Start();
        acc.GenerateAccidentReport(vehicleid, datetime, username, "", "1", true, true);
        return "true";
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetTotalVehicles()
    {
        string vehicles = null;
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = objBll.GetTotalVehicles();
        for (int i = 0; i < table.Rows.Count; i++)
            if (i == 0)
                vehicles = table.Rows[i][0].ToString();
            else
                vehicles = vehicles + "#" + table.Rows[i][0].ToString();
        return vehicles;
    }

    //for blocking user
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetLoginConfigData(string username)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string sessionid = objBll.getSessionIdFromLogin(username);

        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetLoginConfigData", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));

        string globalid = ObjBll.GetGlobalAccountIdBySession(sessionid);
        DataTable table = null;
        if (!string.IsNullOrEmpty(globalid))
            table = objBll.GetBlockingDetails(username, globalid);

        AccountConfig account = new AccountConfig();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        //Get other config data like log,landing refresh,dashboard refresh,map type,map view and aux input from accountgroups
        DataTable dt = objBll.GetAccountGroupConfig(sessionid);
        if (dt == null)
            return null;
        if (dt.Rows.Count > 0)
        {
            account.logo = null;
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
        }
        else
        {
            account.logo = null;
            account.map_refreshrate = null;
            account.map_type = null;
            account.map_view = null;
            account.dasboard_refreshrate = null;
            account.landing_refreshrate = null;
            account.aux_input = null;
            account.map_zoom_level = 0;
            account.showbatteryinland = 0;
            account.reportDateRange1 = null;
            account.reportDateRange2 = null;
        }
        if (table == null)
        {
            account.blocked_reason = null;
            account.blocked_date = null;
        }
        else if (table.Rows.Count < 1)
        {
            account.blocked_reason = null;
            account.blocked_date = null;
        }
        else
        {
            account.blocked_reason = table.Rows[0]["Reason"].ToString();
            account.blocked_date = table.Rows[0]["Blockeddate"].ToString();
        }
        rlog.Start();
        return sriali.Serialize(account);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetWIFIConfigdata(string username)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string sessionid = objBll.getSessionIdFromLogin(username);

        DataTable table = null;
        WIFIConfig wifi = new WIFIConfig();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable dt = objBll.GetWIFIConfig(sessionid);
        if (dt == null)
            return null;
        if (dt.Rows.Count > 0)
        {
            wifi.WIFIFlag = dt.Rows[0]["WIFIflag"].ToString();
            //wifi.videoUrl = "https://youtu.be/wZmsj8TzYSU";
            wifi.videoUrl = "https://youtu.be/hKDvbh4_L38";
        }
        
        return sriali.Serialize(wifi);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertWIFIConfigdata(string wifiusername, string wifipassword, string branchname,string connectionstatus)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = objBll.InsertWIFIConfigdata(wifiusername, wifipassword, branchname, connectionstatus);
        if(res != "0")
        { return "Success"; }
        else { return "Please try again"; }
        
    }
    /// <summary>
    /// Get Speed of vehicle to stop using mobile while driving
    /// </summary>
    /// <param name="IMEI"></param>
    /// <returns>speed of vehicle</returns>
    [WebMethod]
    public string GetVehicleSpeedByIMEI(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataTable dt = objBll.GetVehicleSpeedByIMEI(IMEI);
        if (dt.Rows.Count > 0)
        {
            string latest = "1";
            DateTime timestamp = DateTime.Parse(dt.Rows[0]["timestamp"].ToString());
            DateTime now = DateTime.UtcNow.AddSeconds(ObjBll.GetTimeZoneDiff());
            if ((now - timestamp).TotalMinutes > 10)
                latest = "0";
            return dt.Rows[0]["stop"].ToString() + "," + dt.Rows[0]["speed"].ToString() + "," + dt.Rows[0]["timestamp"].ToString() + "," + latest;
        }
        else
            return "1,0,2016-10-21 18:00:01,0";
    }
    //Get vehicles count 
    [WebMethod]
    public string GetVehiclesNumbers(string sessionid)
    {
        String vtype = objBll.getUIDType(sessionid);
        string domainName = HttpContext.Current.Request.Url.Host;
        if (vtype == "vehicle" || vtype == "Rail")
        {

            List<Vehicle> lst = new List<Vehicle>();
            lst = GetVehiclesInfo(sessionid).ToList();
            int total_vehicles = 0;
            int moving_vehicles = 0;
            int halted_vehicles = 0;
            int idling_vehicles = 0;
            int notreachable_vehicles = 0;
            string UpdatingVehicles = "";

            DataTable accconfig = objBll.GetAccountGroupConfig(sessionid);
            string ShowMarketVeh = Convert.ToString(accconfig.Rows[0]["ShowMarketVehOnLand"]);
            DataTable marketActiveVehicles = null;
            if (ShowMarketVeh == "1")
                marketActiveVehicles = objBll.getActiveMarketVehicles("", "-1", Convert.ToInt16(Convert.ToString(accconfig.Rows[0]["GroupId"])), "1");

            foreach (var item in lst)
            {
                string remarks = item.remarks;
                bool flag = false;
                if (marketActiveVehicles == null)
                    flag = true;
                else
                {
                    DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item.VehicleID + "'");
                    if (dr.Length > 0)
                        flag = true;
                }
                if (flag)
                {
                    UpdatingVehicles += item.VehicleID.Trim() + ",";
                    if (remarks.Contains("VM") || remarks.Contains("OS"))
                    {
                        moving_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks.Contains("VH"))
                    {
                        halted_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks.Contains("VI"))
                    {
                        idling_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks.Contains("NR") || remarks.Contains("GNA"))
                    {
                        notreachable_vehicles++;
                        total_vehicles++;
                    }
                }
            }

            //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
            DataTable lstAll = objBll.GetVehiclesBySessionId(sessionid, "").Tables[0];
            if (lstAll.Rows.Count > 0)
            {
                foreach (DataRow item in lstAll.Rows)
                {
                    bool flag = false;
                    if (marketActiveVehicles == null)
                        flag = true;
                    else
                    {
                        DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item["VehicleID"].ToString() + "'");
                        if (dr.Length > 0)
                            flag = true;
                    }
                    if (flag)
                    {
                        if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                        {
                            notreachable_vehicles++;
                            total_vehicles++;
                        }
                    }
                }
            }


            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclesNumbers", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));
            rlog.Start();
            return total_vehicles.ToString() + "$" + moving_vehicles.ToString() + "$" + idling_vehicles.ToString() + "$" + halted_vehicles.ToString() + "$" + notreachable_vehicles.ToString();
        }
        //else if (vtype == "Asset")
        else if (vtype == "Asset" && (domainName.Contains("test") || domainName.Contains("localhost")))
            {
            int total_vehicles = 0;
            int Reporting_vehicles = 0;
            int NotReporting_vehicles = 0;
            string UpdatingVehicles = "";
            string vehicleId = "";
            int theftalert = 0;
            int lowbat = 0;
            int Batpercentage = 0;
            List<Vehicle> lst = new List<Vehicle>();
            //lst = GetVehiclesInfo(sessionid).ToList();
            DataTable vehiclesdt = objBll.GetAsgVehiclesInfo(sessionid);
            DataTable accconfig = objBll.GetAccountGroupConfig(sessionid);
            int accountid = objBll.GetAccountId(sessionid);
            String lowbattery = objBll.GetlowBatteryRatio(accountid);
            foreach (DataRow item in vehiclesdt.Rows)
            {
                vehicleId = item["truckId"].ToString().Trim();
                Batpercentage = Convert.ToInt32(item["BV"].ToString().Trim());
                string remarks = item["Remarks"].ToString();
                bool flag = true;

                if (flag)
                {
                    UpdatingVehicles += vehicleId.Trim() + ",";
                    if (remarks == "Reporting")
                    {
                        Reporting_vehicles++;
                        total_vehicles++;
                        if (Batpercentage < Convert.ToInt32(lowbattery))
                        {
                            lowbat++;
                        }
                    }
                    else if (remarks == "Not Reporting")
                    {
                        NotReporting_vehicles++;
                        total_vehicles++;
                        if (Batpercentage < Convert.ToInt32(lowbattery))
                        {
                            lowbat++;
                        }
                    }
                    else if (remarks == "Priority Theft Alert" || remarks == "Theft Alert")
                    {
                        theftalert++;
                        total_vehicles++;
                        if (Batpercentage < Convert.ToInt32(lowbattery))
                        {
                            lowbat++;
                        }
                    }
                }
            }
            DataTable lstAll = objBll.GetVehiclesBySessionId(sessionid, "").Tables[0];

            // Extract truck IDs from vehiclesdt
            var dtVehicleIds = new HashSet<string>(
                vehiclesdt.AsEnumerable()
                          .Select(row => row["truckId"].ToString().Trim())
            );

            // Extract Vehicle IDs from lstAll
            var lstAllVehicleIds = new HashSet<string>(
                lstAll.AsEnumerable()
                      .Select(row => row["VehicleID"].ToString().Trim())
            );

            // Find vehicles present in lstAll but NOT in vehiclesdt
            var notReportingVehicles = lstAllVehicleIds.Except(dtVehicleIds);

            // Count them as Not Reporting
            foreach (string NotMatchingVehicles in notReportingVehicles)
            {
                NotReporting_vehicles++;
                total_vehicles++;
            }
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclesNumbers", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));
            rlog.Start();
            //return lst.Count.ToString() + "$" + Reporting_vehicles.ToString() + "$" + lowbat.ToString() + "$" + theftalert.ToString() + "$" + NotReporting_vehicles.ToString();
            return total_vehicles.ToString() + "$" + Reporting_vehicles.ToString() + "$" + lowbat.ToString() + "$" + theftalert.ToString() + "$" + NotReporting_vehicles.ToString();

        }
        else if (vtype == "Asset")
        {
            int total_vehicles = 0;
            int Reporting_vehicles = 0;
            int NotReporting_vehicles = 0;
            string UpdatingVehicles = "";
            List<Vehicle> lst = new List<Vehicle>();
            lst = GetVehiclesInfo(sessionid).ToList();
            DataTable accconfig = objBll.GetAccountGroupConfig(sessionid);
            string ShowMarketVeh = Convert.ToString(accconfig.Rows[0]["ShowMarketVehOnLand"]);
            DataTable marketActiveVehicles = null;
            if (ShowMarketVeh == "1")
                marketActiveVehicles = objBll.getActiveMarketVehicles("", "-1", Convert.ToInt16(Convert.ToString(accconfig.Rows[0]["GroupId"])), "1");
            foreach (var item in lst)
            {
                string remarks = item.remarks;
                bool flag = false;
                if (marketActiveVehicles == null)
                    flag = true;
                else
                {
                    DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item.VehicleID + "'");
                    if (dr.Length > 0)
                        flag = true;
                }
                if (flag)
                {
                    UpdatingVehicles += item.VehicleID.Trim() + ",";
                    if (remarks == "Reporting,")
                    {
                        Reporting_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks == "Not Reporting,")
                    {
                        NotReporting_vehicles++;
                        total_vehicles++;
                    }
                }
            }

            int t = 0;
            int theftalert = 0;
            int lowbat = 0;
            String vehicleId = String.Empty;
            string activeVehls = "";
            object[] vehicles = objBll.getVehicles(sessionid);
            for (t = 0; t < vehicles.Length; t++)
            {
                vehicleId = (String)((Object[])vehicles[t])[0];
                if (!string.IsNullOrEmpty(activeVehls))
                {
                    if (!activeVehls.Contains(vehicleId))
                        continue;
                }
                object[] theftInfo = objBll.gettheftalertdataforAsset(vehicleId);
                for (int j = 0; j < theftInfo.Length; j++)
                {
                    if ((((((Object[])theftInfo[j])[0]).ToString()).Trim()).Equals(vehicleId.Trim()))
                    {
                        //if (((((Object[])theftInfo[j])[1]).ToString()).Equals("1"))
                        if (((((Object[])theftInfo[j])[1]).ToString()).Equals("1") || ((((Object[])theftInfo[j])[1]).ToString()).Equals("2"))
                        {
                            theftalert++;
                        }
                        int BatVal = Convert.ToInt16(((((Object[])theftInfo[j])[3]).ToString()));
                        int accountid = objBll.GetAccountId(sessionid);
                        String lowbattery = objBll.GetlowBatteryRatio(accountid);
                        string Datetime = null;
                        DateTime endDate = DateTime.UtcNow;
                        timeZone = objBll.GetTimeZoneDiff(sessionid);
                        endDate = endDate.AddSeconds(timeZone);//for converting in to IST
                        if (Datetime != null)
                            endDate = Convert.ToDateTime(Datetime);
                        int Batpercentage = 0;
                        if ((endDate - Convert.ToDateTime(((((Object[])theftInfo[j])[2]).ToString()))).TotalDays >= 15)
                        {
                            Batpercentage = 0;
                        }
                        else
                        {
                            //Batpercentage = objBll.getbatterystatus(BatVal);
                            Batpercentage = BatVal;
                        }

                        if (!String.IsNullOrEmpty(lowbattery))
                        {
                            if (Batpercentage < Convert.ToInt16(lowbattery))
                            {
                                lowbat++;
                            }
                        }
                    }
                }
            }
            //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
            DataTable lstAll = objBll.GetVehiclesBySessionId(sessionid, "").Tables[0];
            if (lstAll.Rows.Count > 0)
            {
                foreach (DataRow item in lstAll.Rows)
                {
                    bool flag = false;
                    if (marketActiveVehicles == null)
                        flag = true;
                    else
                    {
                        DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item["VehicleID"].ToString() + "'");
                        if (dr.Length > 0)
                            flag = true;
                    }
                    if (flag)
                    {
                        if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                        {
                            NotReporting_vehicles++;
                            total_vehicles++;
                        }
                    }
                }
            }
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclesNumbers", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));
            rlog.Start();
            //return lst.Count.ToString() + "$" + Reporting_vehicles.ToString() + "$" + lowbat.ToString() + "$" + theftalert.ToString() + "$" + NotReporting_vehicles.ToString();
            return total_vehicles.ToString() + "$" + Reporting_vehicles.ToString() + "$" + lowbat.ToString() + "$" + theftalert.ToString() + "$" + NotReporting_vehicles.ToString();

        }
        else
        {
            List<Vehicle> lst = new List<Vehicle>();
            lst = GetVehiclesInfo(sessionid).ToList();
            int total_vehicles = 0;
            int moving_vehicles = 0;
            int halted_vehicles = 0;
            int idling_vehicles = 0;
            int notreachable_vehicles = 0;
            string UpdatingVehicles = "";

            DataTable accconfig = objBll.GetAccountGroupConfig(sessionid);
            string ShowMarketVeh = Convert.ToString(accconfig.Rows[0]["ShowMarketVehOnLand"]);
            DataTable marketActiveVehicles = null;
            if (ShowMarketVeh == "1")
                marketActiveVehicles = objBll.getActiveMarketVehicles("", "-1", Convert.ToInt16(Convert.ToString(accconfig.Rows[0]["GroupId"])), "1");

            foreach (var item in lst)
            {
                string remarks = item.remarks;
                bool flag = false;
                if (marketActiveVehicles == null)
                    flag = true;
                else
                {
                    DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item.VehicleID + "'");
                    if (dr.Length > 0)
                        flag = true;
                }
                if (flag)
                {
                    UpdatingVehicles += item.VehicleID.Trim() + ",";
                    if (remarks.Contains("VM") || remarks.Contains("OS"))
                    {
                        moving_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks.Contains("VH"))
                    {
                        halted_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks.Contains("VI"))
                    {
                        idling_vehicles++;
                        total_vehicles++;
                    }
                    else if (remarks.Contains("NR") || remarks.Contains("GNA"))
                    {
                        notreachable_vehicles++;
                        total_vehicles++;
                    }
                }
            }

            //Check and Displaying All Vehicles in Landing(If No Data in PositionData)
            DataTable lstAll = objBll.GetVehiclesBySessionId(sessionid, "").Tables[0];
            if (lstAll.Rows.Count > 0)
            {
                foreach (DataRow item in lstAll.Rows)
                {
                    bool flag = false;
                    if (marketActiveVehicles == null)
                        flag = true;
                    else
                    {
                        DataRow[] dr = marketActiveVehicles.Select("VehicleId='" + item["VehicleID"].ToString() + "'");
                        if (dr.Length > 0)
                            flag = true;
                    }
                    if (flag)
                    {
                        if (!UpdatingVehicles.Contains(item["VehicleID"].ToString().Trim() + ","))
                        {
                            notreachable_vehicles++;
                            total_vehicles++;
                        }
                    }
                }
            }

            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclesNumbers", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));
            rlog.Start();
            return total_vehicles.ToString() + "$" + moving_vehicles.ToString() + "$" + idling_vehicles.ToString() + "$" + halted_vehicles.ToString() + "$" + notreachable_vehicles.ToString();
        }
    }

    /// <summary>
    /// Get transporter data by account id 
    /// </summary>
    /// <param name="accountid"></param>
    /// <returns>transporters data</returns>
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetTransporterData(string sessionid)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = objBll.GetContractorTripDetails(sessionid).Tables[0];
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;


        List<Transporter> transporters = new List<Transporter>();
        for (int i = 0; i < table.Rows.Count; i++)
        {

            DataRow row = table.Rows[i];
            var transporter = new Transporter()
            {
                Transporterid = row[0].ToString(),
                TransporterName = row[1].ToString(),
                Login = row[2].ToString(),
                ShortName = row[3].ToString(),
            };
            transporters.Add(transporter);
        }
        return sriali.Serialize(transporters);

    }

    [WebMethod]
    public int InsertTransporterNotification(string transporters, string subject, string detail, Int16 priority = 0)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.AddTransporterNotifications(transporters, subject, detail, priority);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetVehicleAndDriverinfo(string sessionid)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = objBll.GetVehicleAndDriverinfo(sessionid);
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        List<VehicleDriver> vehicles = new List<VehicleDriver>();
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var vehicle = new VehicleDriver()
            {
                vehicleid = table.Rows[i][0].ToString(),
                vehicleinfo = table.Rows[i][1].ToString(),
                drivername = table.Rows[i][2].ToString(),
                contact_No = table.Rows[i][3].ToString() + " " + table.Rows[i][4].ToString()
            };
            vehicles.Add(vehicle);

        }

        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehicleAndDriverinfo", "Fleet-Smart", "", "", sessionid, sHost, sUserIp));
        rlog.Start();
        return sriali.Serialize(vehicles);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertDriverFeedback(string IMEI, string latlng, string customercode, string cooperative, string attitude, string cleanness, string PPE, string unload_procedure, string comments)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.InsertDriverFeedback(IMEI, latlng, customercode, cooperative, attitude, cleanness, PPE, unload_procedure, comments);

    }

    //Driver self appraisal
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public int InsertDriverSelfAppraisal(string IMEI, int checklist, int resttaken, int noviolation)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.InsertDriverAppraisal(IMEI, checklist, resttaken, noviolation);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string DeviatioReport(string IMEI, string place, string placetype, string minlat, string minlng, string photo)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string mailids = string.Empty;
        string vehicle = objBll.GetVehilceByIMEI(IMEI);
        string drivername = "No Name";
        int accountid = 9;
        if (vehicle != "No Data")
        {
            string sessionid = objBll.getSessionIdByVehicle(vehicle);
            int groupid = objBll.getGroupId(sessionid);
            accountid = objBll.GetAccountId(sessionid);
            XmlDBAccess dbx = new XmlDBAccess();
            mailids = dbx.GetMailIds(groupid.ToString());
            string[] driver = dbx.getdrivername(vehicle, DateTime.Now.ToString("yyyy-MM-dd")).Split(',');
            drivername = driver[0];
        }
        if (placetype.Contains("JRM"))
        {
            objBll.insertPlantgeolocaion_total("", place, (Convert.ToDouble(minlat) - 0.0005).ToString(), (Convert.ToDouble(minlng) - 0.0005).ToString(), (Convert.ToDouble(minlat) + 0.0005).ToString(), (Convert.ToDouble(minlng) + 0.0005).ToString(), "20", 0, null, null, 0);
            DataTable routenames = objBll.GetRouteNames(Convert.ToDouble(minlat) - 0.0005, Convert.ToDouble(minlat) + 0.0005, Convert.ToDouble(minlng) - 0.0005, Convert.ToDouble(minlng) + 0.0005, accountid);
            if (routenames != null)
            {
                int speed = placetype.Contains("25") ? 25 : 35;

                foreach (DataRow row in routenames.Rows)
                {
                    string route = row["routename"].ToString().Replace(" ", "_");
                    objBll.insertwaypointsrefData(Convert.ToDouble(minlat) - 0.0005, Convert.ToDouble(minlat) + 0.0005, Convert.ToDouble(minlng) - 0.0005, Convert.ToDouble(minlng) + 0.0005, route, place, speed, accountid);
                }
            }
        }
        string resp = objBll.insertDeviatioReport(IMEI, place, placetype, minlat, minlng, photo);
        Thread t = new Thread(() => { DeviationReportTotal.sendMailForUnload(IMEI, objBll.formatDBDate(DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff())), drivername, vehicle, place, placetype, photo, mailids, sHost); });
        t.Start();
        return resp;
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string ReportPlaceByUser(string IMEI, string place, string placetype, string minlat, string minlng, string photo, string user, string appname)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.insertDeviatioReport(IMEI, place, placetype, minlat, minlng, photo, user, appname);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string TagingReport(string IMEI, string latlng, string tagInOrOut, string Photopath)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return objBll.insertTagingReport(IMEI, latlng, tagInOrOut, Photopath);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertDriverChecklistPhotos(string MobileNo, string DriverPhotoName, string VehiclePhotoName, string Timestamp)
    {        
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetDriverDataForDriverApp(MobileNo);
        if (table != null && table.Rows.Count > 0)
        {
            string result = objBll.InsertDriverChecklistPhotos(table.Rows[0]["DriverId"].ToString(), DriverPhotoName, VehiclePhotoName, Timestamp);
            return result;
        }
        else
        {
            return "Please try again..";
        }
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDeviationByVehicle(string vehicleid, string fromdate, string todate)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        DataTable table = objBll.GetDeviationByVehicle(vehicleid, fromdate, todate);
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        List<storeviolation> vehicles = new List<storeviolation>();
        if (table != null)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var vehicle = new storeviolation()
                {
                    vehicleid = table.Rows[i][0].ToString(),
                    type = table.Rows[i][1].ToString(),
                    datetime = table.Rows[i][2].ToString(),
                    remarks = table.Rows[i][3].ToString(),
                    latitude = table.Rows[i][4].ToString(),
                    longitude = table.Rows[i][5].ToString()
                };
                vehicles.Add(vehicle);

            }
            return sriali.Serialize(vehicles);
        }
        else
            return null;

    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetCustomerList(string customercode)
    {
        BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
        DataTable table = objBll.GetCustomerListWithStocklevel(customercode);
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        List<Customer> customers = new List<Customer>();
        for (int i = 0; i < table.Rows.Count; i++)
        {

            var customer = new Customer()
            {

                Customercode = table.Rows[i]["CustomerCode"].ToString(),
                CustomerName = table.Rows[i]["Name"].ToString(),
                StockLevel = table.Rows[i]["StockLevel"].ToString(),
                StockLevelTime = table.Rows[i]["timestamp"].ToString(),
                Next3daysExpected = table.Rows[i]["Next3DaysExpected"].ToString(),
                DCNo = table.Rows[i]["DCNo"].ToString(),
                Quantity = table.Rows[i]["Quantity"].ToString(),
                LastDispatched = table.Rows[i]["DateTime"].ToString(),
                LastDelivered = table.Rows[i]["DeliveredOn"].ToString(),
                LoadOnTransit = objBll.GetCustomerLoadOnTransit(table.Rows[i]["customerId"].ToString())

            };
            customers.Add(customer);
        }
        return sriali.Serialize(customers);

    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public int UpdateCustomerCurrentStocklevel(string customercode, string quantity, string date, string next3daysexpected, string addedby)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        int res = objBll.UpdateCustomerCurrentStocklevel(customercode, quantity, date, next3daysexpected, addedby);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("UpdateCustomerCurrentStocklevel", "Smart_Track CCM", "", "", "", sHost, sUserIp));
        rlog.Start();
        return res;
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string UpdateCustomerCurrentStocklevelNew(string customercode, string quantity, string date, string next3daysexpected, string addedby)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = objBll.UpdateCustomerCurrentStocklevelNew(customercode, quantity, date, next3daysexpected, addedby);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("UpdateCustomerCurrentStocklevelNew", "Smart_Track CCM", "", "", "", sHost, sUserIp));
        rlog.Start();
        return res;
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string DriverCounseleingSmartFleet(string vehicleid, string topic, string details, string counseledby, string attachment, string license_number)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string userid = counseledby.Split('[')[1].Replace("]", "");
        return ObjBll.insertDriverCounselingSmartFleet(vehicleid, topic, details, counseledby, attachment, license_number, userid);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string UpdateCustomerLogisticRequest(string customercode, string quantity, string date, string requestedby)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DateTime DispatchDate = Convert.ToDateTime(date);
        if (DispatchDate.Date <= DateTime.Now.Date)
        {
            return "You Can't Request Load for past Date,Please select valid date";
        }
        else
        {
            int RTKM = objBll.GetCustomerSupplyPlantRTKM(customercode);
            if (RTKM > 0)
            {
                string plant = objBll.getDefaultPlantName(customercode);
                //reverse ETA
                if (plant.ToLower().Contains("bangalore"))
                    DispatchDate = Convert.ToDateTime(objBll.findReverseETA(DispatchDate, RTKM / 2, 35, 36000, "05:00", "05:00", 16200, 2400, 32400, 0).Split(',')[0]);
                else
                    DispatchDate = Convert.ToDateTime(objBll.findReverseETA(DispatchDate, RTKM / 2, 35, 36000, "05:00", "20:00", 16200, 2400, 32400, 0).Split(',')[0]);
                //reqdate = reqdate.AddHours(-TransitTime);
                if (DispatchDate.Date <= DateTime.Now.Date)
                {
                    objBll.UpdateCustomerLogisticRequest(customercode, quantity, date, requestedby, "1", DispatchDate).ToString();
                    return "Supply cannot be executed due to paucity of Transit time";
                }
            }
            else
            {
                DispatchDate = DispatchDate.AddDays(-1);
            }
        }
        return objBll.UpdateCustomerLogisticRequest(customercode, quantity, date, requestedby, "0", DispatchDate).ToString();
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string UpdateCustomerLogisticRequestNew(string customercode, string quantity, string date, string requestedby)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DateTime DispatchDate = Convert.ToDateTime(date);
        if (DispatchDate.Date <= DateTime.Now.Date)
        {
            return "You Can't Request Load for past Date,Please select valid date";
        }
        else
        {
            int RTKM = objBll.GetCustomerSupplyPlantRTKM(customercode);
            if (RTKM > 0)
            {
                string plant = objBll.getDefaultPlantName(customercode);
                //reverse ETA
                if (plant.ToLower().Contains("bangalore"))
                    DispatchDate = Convert.ToDateTime(objBll.findReverseETA(DispatchDate, RTKM / 2, 35, 36000, "05:00", "05:00", 16200, 2400, 32400, 0).Split(',')[0]);
                else
                    DispatchDate = Convert.ToDateTime(objBll.findReverseETA(DispatchDate, RTKM / 2, 35, 36000, "05:00", "20:00", 16200, 2400, 32400, 0).Split(',')[0]);
                //reqdate = reqdate.AddHours(-TransitTime);
                if (DispatchDate.Date <= DateTime.Now.Date)
                {
                    objBll.UpdateCustomerLogisticRequestNew(customercode, quantity, date, requestedby, "1", DispatchDate).ToString();
                    return "Supply cannot be executed due to paucity of Transit time";
                }
            }
            else
            {
                DispatchDate = DispatchDate.AddDays(-1);
            }
        }
        return objBll.UpdateCustomerLogisticRequestNew(customercode, quantity, date, requestedby, "0", DispatchDate).ToString();
    }

    [WebMethod]
    public string GetAllPlacesData()
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        List<Place> places = new List<Place>();
        var sriali = new JavaScriptSerializer();
        sriali.MaxJsonLength = Int32.MaxValue;
        DataRow[] value = ObjBll.GetAllPlaces("6");
        for (int i = 0; i < value.Length; i++)
        {
            var curplace = new Place()
            {
                Name = value[i][1].ToString(),
                Address = value[i][3].ToString(),
                Phone = value[i][4].ToString(),
                Latitude = value[i][5].ToString(),
                Longitude = value[i][6].ToString()

            };
            places.Add(curplace);

        }
        value = ObjBll.GetAllPlaces("");
        for (int i = 0; i < value.Length; i++)
        {
            var curplace = new Place()
            {
                Name = value[i][1].ToString(),
                Address = value[i][3].ToString(),
                Phone = value[i][4].ToString(),
                Latitude = value[i][5].ToString(),
                Longitude = value[i][6].ToString()

            };
            places.Add(curplace);

        }
        return sriali.Serialize(places);
    }
    //to record driver feedback in night zone
    [WebMethod]
    public int InsertJrmResponse(string IMEI, string cur_latlng, string response, string jrmType, string Latlng, string isNightZone)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.InsertJrmResponse(IMEI, cur_latlng, response, jrmType, Latlng, isNightZone);
    }
    //show all violations on map
    [WebMethod]
    public string GetAllViolationByIMEI(string fromDate, string toDate, string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetAllViolationByVehicle(fromDate, toDate, IMEI);
        if (table == null)
            return "No Vehicle";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }
    //self appraisal record
    [WebMethod]
    public int InsertDriverSelfAppraisalNew(string IMEI, int checklist, int restTaken, int overspeed, int jrmOverspeed, int nightDriving, int continuesDriving, int maxDriving)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.InsertDriverAppraisalnew(IMEI, checklist, restTaken, overspeed, jrmOverspeed, nightDriving, continuesDriving, maxDriving);
    }
    [WebMethod]
    public int InsertDriverSelfAppraisalNew1(string IMEI, int checklist, int restTaken, int overspeed, int jrmOverspeed, int nightDriving, int continuesDriving, int maxDriving,int AuthorizedRoute)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.InsertDriverAppraisalnew1(IMEI, checklist, restTaken, overspeed, jrmOverspeed, nightDriving, continuesDriving, maxDriving, AuthorizedRoute);
    }
    [WebMethod]
    public int InsertViolationReasons(string IMEI, string violationType, string latLng, string reason)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.InsertViolationReasons(IMEI, violationType, latLng, reason);
    }
    //get locked apps
    [WebMethod]
    public string GetLockedApps(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetLockedApps(IMEI);
        if (table == null)
            return "No Vehicle";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }


    [WebMethod]
    public string GetVehicleidByQRCode(string qrcode)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.GetVehicleidByQRCode(qrcode);
    }

    [WebMethod]
    public string GetNokiaDriverData(string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetNokiaDriverData(IMEI);
        if (table == null)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns) 
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }
    [WebMethod]
    public string GetDriverDataForDriverApp(string MobileNo)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetDriverDataForDriverApp(MobileNo);
        if (table == null)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    [WebMethod]
    public string InsertDriverTaging(string driverId, string drivername, string IMEI, string latlng, string vehicleId, int tag, string Photopath="", string TagTime = "", string source = "")
    {
        string result= objBll.InsertDriverTaging(driverId, drivername, IMEI, latlng, vehicleId, tag, Photopath, TagTime = "", source = "");
        if (result == "1")
        {
            return "Tag-In done successfully";
        }
        else
        {
            return "Please try again";
        }
    }

    [WebMethod]
    public string GetNokiaDriverDataByMobileNo(string Mobile)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable table = ObjBll.GetNokiaDriverDataByMobileNo(Mobile);
        if (table == null)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    [WebMethod]
    public string EditVehicleidByQRCodes(string vehicleid, string qrcode, string mobile, string IMEI)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.EditVehicleidByQRCode(vehicleid, qrcode, mobile, IMEI);
    }

    [WebMethod]
    public string EditVehicleidByQRCode(string vehicleid, string qrcode)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.EditVehicleidByQRCode(vehicleid, qrcode);
    }

    [WebMethod]
    public int EditDriverData(string Name, string IMEI, string LicenseNo, string DOB, string mobileNo, string vehicleid, string photo)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        return ObjBll.EditDriverData(Name, IMEI, LicenseNo, DOB, mobileNo, vehicleid, photo);
    }

    [WebMethod]
    public string GetNearestVehicles(string IMEI, string latitude, string longitude, double radius)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        double lat = Convert.ToDouble(latitude);
        double lng = Convert.ToDouble(longitude);
        DataTable dt = objBll.getDriverDataByIMEI(IMEI);
        if (dt.Rows.Count > 0)
        {
            string account = dt.Rows[0]["AccountId"].ToString();
            int accountid = Convert.ToInt32(account);
            DataTable table = ObjBll.GetNearestVehiclesByAccount(lat, lng, accountid, radius);
            if (table == null)
                return "No Data";
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in table.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);

        }
        else
            return "No IMEI";
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDropDownForApp(string appName, string key)
    {
        DataTable table = objBll.GetDropDownForApp(appName, key);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetDropDownForApp", "psngr_" + appName + "_" + key, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (table == null)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetPsngrInfoWithValidation(string mobileNo, string flag)
    {
        DataTable table = objBll.GetPsngrInfoWithValidation(mobileNo, flag);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetPsngrInfoWithValidation", "psngr_" + mobileNo + "_" + flag, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (flag.Contains("OTP"))
        {
            if (table.Rows.Count > 0)
            {
                string[] otp = flag.Split('-');
                string str = "One Time Password for Passenger APP is " + otp[1];
                if (objBll.SendSMSwithLog(mobileNo, str, "Passenger App OTP", table.Rows[0]["globalaccountid"].ToString(), table.Rows[0]["login"].ToString()))
                    return "SMS Send Successfully";
                else
                    return "Failed to send SMS";
            }
            else
                return "Login not found to send SMS";
        }
        else
        {
            if (table == null)
                return "No Data";
            else if (table.Rows[0][1].ToString() == "No Data Logout")
                return "No Data Logout";
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in table.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);
        }
    }

    #region Feed Smart
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartMobileNoValidate(string mobileNo)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartMobileNoValidate", "psngr_" + mobileNo, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        return objbll.FeedSmartApp_OTPRequest(mobileNo);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartMobileNoOTPValidate(string mobileNo, string otp)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartMobileNoOTPValidate", "psngr_" + mobileNo, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.FeedSmartApp_OTPValidate(mobileNo, otp);
        if (dt == null || dt.Rows.Count == 0)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in dt.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in dt.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartPacketsInfo(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartPacketsInfo", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.FeedSmartApp_PacketsInfo(sessionid);
        if (dt == null || dt.Rows.Count == 0)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in dt.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in dt.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertFeedSmartPacketsInfo(string sessionid, string morningPacks, string eveningPacks)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartPacketsInfo", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        return objbll.InsertFeedSmartPacketsInfo(sessionid, morningPacks, eveningPacks).ToString();
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertFeedSmartFoodProviderEntry(string sessionid, string morningPacks, string eveningPacks, string locationid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertFeedSmartFoodProviderEntry", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        return objbll.InsertFeedSmartFoodProviderEntry(sessionid, morningPacks, eveningPacks, locationid);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertFeedSmartFoodDeliverEntry(string sessionid, string tripid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertFeedSmartFoodDeliverEntry", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        return objbll.InsertFeedSmartFoodDeliverEntry(sessionid, tripid);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertFeedSmartProfileInfo(string name, string mobileno, string location, string latlng, string address)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertFeedSmartProfileInfo", "psngr_" + mobileno, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        return objbll.InsertFeedSmartProfileInfo(name, mobileno, location, latlng, address);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartFoodProviderData(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartFoodProviderData", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.GetFeedSmartFoodProviderData(sessionid);
        return ConvertDatatableToSerialize(dt);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartFoodDeliverData(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartFoodDeliverData", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.GetFeedSmartFoodDeliverData(sessionid);
        return ConvertDatatableToSerialize(dt);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartFoodRqdLocData(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartFoodRqdLocData", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.GetFeedSmartFoodRqdLocData();
        return ConvertDatatableToSerialize(dt);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartFoodDeliveryPending(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartFoodDeliveryPending", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.GetFeedSmartFoodDeliveryPending();
        return ConvertDatatableToSerialize(dt);
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetFeedSmartAdminTrips(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetFeedSmartAdminTrips", "psngr_" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        DataTable dt = objbll.GetFeedSmartAdminTrips(sessionid);
        return ConvertDatatableToSerialize(dt);
    }
    #endregion

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetAppCurrentVersion(string packageName)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetAppCurrentVersion", packageName, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL(System.Web.Configuration.WebConfigurationManager.AppSettings["adminPanelDBConnectionString"]);
        DataTable dt = objbll.GetAppCurrentVersion(packageName);
        return ConvertDatatableToSerialize(dt);
    }

    public string ConvertDatatableToSerialize(DataTable dt)
    {
        if (dt == null || dt.Rows.Count == 0)
            return "No Data";
        else
        {
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in dt.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);
        }
    }


    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetPsngrInfoWithValidationNew(string mobileNo, string flag, string appName)
    {
        DataTable table = objBll.GetPsngrInfoWithValidation(mobileNo, flag, appName);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetPsngrInfoWithValidationNew", "psngr_" + mobileNo + "_" + flag + "_" + appName, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (flag.Contains("OTP"))
        {
            if (table.Rows.Count > 0)
            {
                string[] otp = flag.Split('-');
                string str = "One Time Password for Passenger APP is " + otp[1];
                if (objBll.SendSMSwithLog(mobileNo, str, "Passenger App OTP", table.Rows[0]["globalaccountid"].ToString(), table.Rows[0]["login"].ToString()))
                    return "SMS Send Successfully";
                else
                    return "Failed to send SMS";
            }
            else
                return "Login not found to send SMS";
        }
        else
        {
            if (table == null)
                return "No Data";
            else if (table.Rows.Count > 0 && table.Columns.Count > 1 && table.Rows[0][1].ToString() == "No Data Logout")
                return "No Data Logout";
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in table.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);
        }
    }
    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string getDriverInfoWithValidation(string mobileNo, string flag)
    {
        DataTable table = objBll.getDriverInfoWithValidation(mobileNo);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("getDriverInfoWithValidation", "Driver_" + mobileNo + "_" + flag  , "", "", "", sHost, sUserIp));
        rlog.Start();
        if (flag.Contains("OTP"))
        {
            if (table.Rows.Count > 0)
            {
                string[] otp = flag.Split('-');
                string str = "One Time Password for Driver APP is " + otp[1];
                if (objBll.SendSMSwithLog(mobileNo, str, "Driver App OTP", "3117", "nokia"))                
                    return "SMS Send Successfully";                    
                else
                    return "Failed to send SMS";
            }
            else
                return "Login not found to send SMS";
        }
        else
        {
            if (table == null)
                return "No Data";
            else if (table.Rows.Count > 0 && table.Columns.Count > 1 && table.Rows[0][1].ToString() == "No Data Logout")
                return "No Data Logout";
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in table.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);
        }
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetPsngrTowerLocations(string mobileno, string zone, string enteredkey)
    {
        DataTable table = objBll.GetPsngrTowerLocations(mobileno, zone, enteredkey);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetPsngrTowerLocations", "psngr_" + mobileno + "_" + zone + "_" + enteredkey, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (table != null && table.Rows.Count > 0)
        {
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in table.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);
        }
        else
            return "No Data";
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string CheckPsngrTowerLocation(string mobileno, string towername)
    {
        string str = objBll.CheckPsngrTowerLocation(mobileno, towername);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("CheckPsngrTowerLocation", "psngr_" + mobileno + "_" + towername, "", "", "", sHost, sUserIp));
        rlog.Start();
        return str;
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetPsngrInfoWithValidationWithImei(string imei, string flag, string appName)
    {
        DataTable table = objBll.GetPsngrInfoWithValidationWithImei(imei, flag, appName);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetPsngrInfoWithValidationWithImei", "psngr_" + imei + "_" + flag + "_" + appName, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (table == null)
            return "No Data";
        else if (table.Rows[0][1].ToString() == "No Data Logout")
            return "No Data Logout";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetPsngrNotifications(string psngrId)
    {
        DataTable table = objBll.GetPsngrNotifications(psngrId);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetPsngrNotifications", "psngr_" + psngrId, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (table == null)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public void PsngrNotificationNotified(string psngrId)
    {
        objBll.PsngrNotificationNotified(psngrId);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("PsngrNotificationNotified", "psngr_" + psngrId, "", "", "", sHost, sUserIp));
        rlog.Start();
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertPsngrChecklist(string psngrId, string vehicleId, string type, string rules, string wfmid, string ptw,
        string driverId, string IMEI, string Lat, string Lng, string Manual, string DriverDetails, string OMR, string gpscheckid, string gpsReason)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = ObjBll.InsertPsngrChecklist(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, IMEI, Lat, Lng, Manual, DriverDetails, OMR, gpscheckid, gpsReason);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertPsngrChecklist", "psngr_" + psngrId + "_" + type, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res.Contains("PsngrMessage-"))
            return res;
        else if (res == "0")
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertPsngrChecklistNew1(string psngrId, string vehicleId, string type, string rules, string wfmid, string ptw,
        string driverId, string IMEI, string Lat, string Lng, string Manual, string DriverDetails, string OMR, string gpscheckid, string gpsReason, string driverImage)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = ObjBll.InsertPsngrChecklist(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, IMEI, Lat, Lng, Manual, DriverDetails, OMR, gpscheckid, gpsReason, driverImage);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertPsngrChecklistNew1", "psngr_" + psngrId + "_" + type, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res.Contains("PsngrMessage-"))
            return res;
        else if (res == "0")
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertPsngrChecklistNew2(string psngrId, string vehicleId, string type, string rules, string wfmid, string ptw, string driverId, string IMEI,
        string Lat, string Lng, string Manual, string DriverDetails, string OMR, string gpscheckid, string gpsReason, string driverImage, string towername)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = ObjBll.InsertPsngrChecklist(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, IMEI, Lat, Lng, Manual, DriverDetails, OMR, gpscheckid, gpsReason, driverImage, towername);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertPsngrChecklistNew2", "psngr_" + psngrId + "_" + type + "_" + towername, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res.Contains("PsngrMessage-"))
            return res;
        else if (res == "0")
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertPsngrChecklistNew3(string psngrId, string vehicleId, string type, string rules, string wfmid, string ptw, string driverId, string IMEI,
        string Lat, string Lng, string Manual, string DriverDetails, string OMR, string gpscheckid, string gpsReason, string driverImage, string towername, string vehiclephoto, string Tagin_OdometerPhoto, string Tagout_OdometerPhoto)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = ObjBll.InsertPsngrChecklist(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, IMEI, Lat, Lng, Manual, DriverDetails, OMR, gpscheckid, gpsReason, driverImage, towername, vehiclephoto, Tagin_OdometerPhoto, Tagout_OdometerPhoto);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertPsngrChecklistNew2", "psngr_" + psngrId + "_" + type + "_" + towername, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res.Contains("PsngrMessage-"))
            return res;
        else if (res == "0")
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertPsngrChecklistNew(string psngrId, string vehicleId, string type, string rules, string wfmid, string ptw,
        string driverId, string IMEI, string Lat, string Lng, string Manual, string DriverDetails, string OMR)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = ObjBll.InsertPsngrChecklist(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, IMEI, Lat, Lng, Manual, DriverDetails, OMR);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertPsngrChecklistNew", "psngr_" + psngrId + "_" + type, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res.Contains("PsngrMessage-"))
            return res;
        else if (res == "0")
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string InsertPanicAlertFromApp(string Id, string vehicleId, string type)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        DataTable dt = objBll.GetPassangersInfo("", "", "", Id);
        string psngr_Name = "";
        string psngr_Region = "";
        string psngr_MobileNo = "";
        string phone = "";
        String[] sms = null;
        if (dt.Rows.Count > 0)
        {
            psngr_Name = dt.Rows[0]["PsngrName"].ToString();
            psngr_Region = dt.Rows[0]["RegionName"].ToString();
            psngr_MobileNo = dt.Rows[0]["MobileNo"].ToString();
            int Accountid = Convert.ToInt32(dt.Rows[0]["AccountId"].ToString());
            try
            {
                DataTable dtMail = objBll.GetMailIdsConfig(Accountid, "PSNGR_APP_PANIC", "");
                if (dtMail.Rows.Count > 0)
                {
                    string body = "Dear Sir/Madam,<br/><br/>Following passenger pressed panic button through Passenger App.<br/><br/>";
                    body += "<br/>Passenger Name:" + psngr_Name + " " + psngr_Region;
                    body += "<br/>Passenger MobileNo:" + psngr_MobileNo;
                    body += "<br/>Vehicle Id:" + vehicleId;
                    body += "<br/><br/>Best Regards,<br/>Sensel Telematics ";
                    Thread mail = new Thread(() => SendMail(vehicleId + "-Passenger pressed Panic Button " + psngr_Region, body, "", "", "", dtMail));
                    mail.Start();
                    try
                    {
                        if (Accountid == 5632)
                        {
                            string GlobalAccId = objBll.GetGlobalAccountId(Accountid.ToString());
                            string login = objBll.getUserId(objBll.getSessionIdByVehicle(vehicleId));
                            Object[] smsEmail = objBll.getEmailSMS(vehicleId);
                            phone = (((Object[])smsEmail[0])[1]).ToString();  // SMS phone
                            if (!String.IsNullOrEmpty(phone))
                            {
                                if (phone.Contains('|'))
                                    sms = phone.Split('|');
                                else
                                    sms = phone.Split(',');
                            }
                            string message = "A passenger travelling in " + vehicleId + " pressed the panic button. Name: " + psngr_Name + " Mobile No: " + psngr_MobileNo + "  ";
                            objBll.SendSMSwithLog(phone, message, "Amazon-PanicAlert", GlobalAccId, login);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        int res = ObjBll.InsertPanicAlertFromPassengerApp(Id, vehicleId, type, psngr_Name, psngr_Region, psngr_MobileNo);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("InsertPanicAlertFromApp", "psngr_" + Id + "_" + type, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res == 0)
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string UpdatePsngrHomeLocation(string psngrId, string lat, string lng)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        int res = ObjBll.UpdatePsngrHomeLocation(psngrId, lat, lng);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("UpdatePsngrHomeLocation", "psngr_" + psngrId + "_" + lat + "_" + lng, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res == 0)
            return "Failed to insert";
        else
            return "Inserted Successfully";
    }

    public void SendMail(string subject, string emailBody, string EmailId, string EmailId_cc, string EmailId_bcc, DataTable MailIdConfig)
    {
        if (MailIdConfig != null)
        {
            if (MailIdConfig.Rows.Count > 0)
            {
                EmailId = MailIdConfig.Rows[0]["EmailIds"].ToString();
                EmailId_cc = MailIdConfig.Rows[0]["EmailIds_cc"].ToString();
                EmailId_bcc = MailIdConfig.Rows[0]["EmailIds_bcc"].ToString();
            }
        }
        String emailServer = System.Web.Configuration.WebConfigurationManager.AppSettings["emailServer"];
        string FromMail = System.Web.Configuration.WebConfigurationManager.AppSettings["ReportingEmailId"];
        string Password = System.Web.Configuration.WebConfigurationManager.AppSettings["ReportingEmailPassword"];
        if (String.IsNullOrEmpty(emailServer))
            emailServer = "localhost";
        EmailSender es = new EmailSender(emailServer);
        es = new EmailSender(emailServer);
        es.setupEmail(FromMail, EmailId, subject, emailBody.ToString(), EmailId_cc, EmailId_bcc);
        es.setBodyHtml(true);
        try
        {
            es.send(FromMail, Password);
        }
        catch { }
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetMobVehGpsCheck(string vehicleId, string source, string sourceId, string timeThreshold, string distThreshold, string lat, string lng)
    {
        string res = "";
        DateTime now = DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff());
        string vehLat = "0.0";
        string vehLng = "0.0";
        string vehLastUpdt = "2000-01-01";
        try
        {
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetMobVehGpsCheck", sourceId + "_" + vehicleId + "_" + timeThreshold + "_" + distThreshold + "_" + lat + "_" + lng, "", "", "", sHost, sUserIp));
            rlog.Start();
            DataTable table = objBll.GetPsngrAssgndVehLastAccess(vehicleId);
            if (table == null || table.Rows.Count == 0)
                res = "Block-No update of vehicle is available within the system";
            else
            {
                vehLat = table.Rows[0]["latitude"].ToString();
                vehLng = table.Rows[0]["longitude"].ToString();
                vehLastUpdt = table.Rows[0]["timestamp"].ToString();
                if ((now - Convert.ToDateTime(vehLastUpdt)).TotalMinutes <= Convert.ToInt32(timeThreshold))
                {
                    if (objBll.computeDistance(Convert.ToDouble(lat), Convert.ToDouble(lng), Convert.ToDouble(vehLat), Convert.ToDouble(vehLng)) * 1000 <= Convert.ToInt32(distThreshold))
                    {
                        res = "Allow-Conditions Satisfied";
                    }
                    else
                    {
                        res = "Block-Vehicle is not in the range of " + distThreshold + " meters";
                    }
                }
                else
                {
                    if (source == "Passenger")
                        res = "Allow-No update of vehicle is available in last " + timeThreshold + " minutes";
                    else
                        res = "Block-No update of vehicle is available in last " + timeThreshold + " minutes";
                }
            }
        }
        catch
        {
            res = "Block-Service issue contact admin Or Try Again";
        }
        objBll.insertMobVehGps(source, sourceId, objBll.formatDBDate(now), lat, lng, vehicleId, vehLat, vehLng, objBll.formatDBDate(Convert.ToDateTime(vehLastUpdt)), res.Split('-')[0], res.Split('-')[1]);
        return res;
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string VehicleMobileGPSCheck(string vehicleId, string source, string sourceId, string lat, string lng)
    {
        int timeThreshold = 30;
        int distThreshold = 300;
        string res = "";
        DateTime now = DateTime.UtcNow.AddSeconds(objBll.GetTimeZoneDiff());
        string vehLat = "0.0";
        string vehLng = "0.0";
        string vehLastUpdt = "2000-01-01";
        int id = 0;
        try
        {
            Thread rlog = new Thread(() => objBll.WebServiceRequestlog("VehicleMobileGPSCheck", sourceId + "_" + vehicleId + "_" + timeThreshold + "_" + distThreshold + "_" + lat + "_" + lng, "", "", "", sHost, sUserIp));
            rlog.Start();
            DataTable dt = objBll.GetPsngrInfoById(sourceId);
            if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["Designation"].ToString() == "Engineer")
            {
                DataTable table = objBll.GetPsngrAssgndVehLastAccess(vehicleId);
                if (table == null || table.Rows.Count == 0)
                    res = "Allow@&@No update of vehicle is available within the system";
                else
                {
                    vehLat = table.Rows[0]["latitude"].ToString();
                    vehLng = table.Rows[0]["longitude"].ToString();
                    vehLastUpdt = table.Rows[0]["timestamp"].ToString();
                    if ((now - Convert.ToDateTime(vehLastUpdt)).TotalMinutes <= timeThreshold)
                    {
                        if (objBll.computeDistance(Convert.ToDouble(lat), Convert.ToDouble(lng), Convert.ToDouble(vehLat), Convert.ToDouble(vehLng)) * 1000 <= distThreshold)
                        {
                            res = "Allow@&@Conditions Satisfied";
                        }
                        else
                        {
                            res = "Block@&@Vehicle is not in the range";
                        }
                    }
                    else
                    {
                        res = "Block@&@No update of vehicle is available in last " + timeThreshold + " minutes";
                    }
                }
                string[] resStr = res.Split(new string[] { "@&@" }, StringSplitOptions.None);
                id = objBll.insertMobVehGps(source, sourceId, now.ToString(), lat, lng, vehicleId, vehLat, vehLng, vehLastUpdt, resStr[0], resStr[1]);
            }
            else
                res = "Allow@&@No Need of GPS Check";
        }
        catch (Exception e)
        {
            res = "Block@&@Service issue contact admin Or Try Again";
        }
        return res + "@&@" + id;
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetVehiclesByAccountId(string accountId)
    {
        DataTable table = objBll.GetVehiclesByAccountId(accountId);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetVehiclesByAccountId", accountId, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (table == null)
            return "No Data";
        JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
        Dictionary<string, object> childRow;
        foreach (DataRow row in table.Rows)
        {
            childRow = new Dictionary<string, object>();
            foreach (DataColumn col in table.Columns)
            {
                childRow.Add(col.ColumnName, row[col]);
            }
            parentRow.Add(childRow);
        }
        return jsSerializer.Serialize(parentRow);
    }

    //For Passenger App
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string UpdtPsngrAssgndVeh(string psngrId, string vehicleId)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        int res = ObjBll.UpdtPsngrAssgndVeh(psngrId, vehicleId);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("UpdtPsngrAssgndVeh", "psngr_" + psngrId + "_" + vehicleId, "", "", "", sHost, sUserIp));
        rlog.Start();
        if (res == 0)
            return "Failed to insert";
        else
            return "Updated Successfully";
    }

    //For detail route report
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDetailRouteReport(string vehicleId, string fromDate, string toDate, int timegap)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        XmlDBAccess dbx = new XmlDBAccess();
        if (vehicleId != null)
        {
            var sriali = new JavaScriptSerializer();
            Object[] values = dbx.getRouteLatLong(vehicleId, fromDate, toDate);
            if (values.Length > 0)
            {
                DataTable dtaccConfig = dbx.GetAccountConfig(0, vehicleId);
                List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();

                Dictionary<string, object> childRow;
                DateTime prv_Time = Convert.ToDateTime(((Object[])values[0])[1]);
                BaseRoute route = new BaseRoute();
                route.dbx = new XmlDBAccess();
                route.positionTimeGapSecs = timegap;
                for (int i = 0; i < values.Length; i++)
                {

                    String[] positionInfo = route.showPosition(i, values, vehicleId, dtaccConfig);
                    if (positionInfo != null)
                    {
                        prv_Time = Convert.ToDateTime(((Object[])values[i])[1]);
                        childRow = new Dictionary<string, object>();
                        DateTime Time = Convert.ToDateTime(positionInfo[0] + " " + positionInfo[1]);
                        childRow.Add("VehicleId", vehicleId);
                        childRow.Add("DateTime", Time.ToString("dd'/'MM'/'yyyy") + " " + Time.ToString("HH:mm:ss"));
                        childRow.Add("Location", positionInfo[2]);
                        childRow.Add("Distance", positionInfo[3]);
                        childRow.Add("Total Distance", positionInfo[4]);

                        parentRow.Add(childRow);

                    }

                }
                sriali.MaxJsonLength = Int32.MaxValue;
                return sriali.Serialize(parentRow);
            }
            return "vehicle not updated between these dates";
        }
        else
            return "VehicleId missing";
    }
    //Get already covered route for smart driver app
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetCoveredRouteData(string IMEI, string SessionId)
    {

        XmlDBAccess dbx = new XmlDBAccess();

        string vehicle = dbx.GetVehilceByIMEI(IMEI);
        //get last dc information
        if (vehicle != "No Data")
        {
            DataTable dt = dbx.GetTripDetailsByVehicle(vehicle);
            string starttime = dbx.formatDBDate(DateTime.Now);
            var sriali = new JavaScriptSerializer();

            if (dt != null)
                if (dt.Rows.Count > 0)
                {
                    starttime = dbx.formatDBDate(Convert.ToDateTime(dt.Rows[0]["DateTime"].ToString()));
                    Object[] values = dbx.getRouteLatLong(vehicle, starttime, dbx.formatDBDate(DateTime.Now));
                    if (values.Length > 0)
                    {
                        List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
                        Dictionary<string, object> childRow;
                        DateTime prv_Time = Convert.ToDateTime(((Object[])values[0])[1]);

                        for (int i = 1; i < values.Length; i++)
                        {
                            if (((Object[])values[i - 1])[4].ToString() != ((Object[])values[i])[4].ToString())
                            {
                                childRow = new Dictionary<string, object>();
                                childRow.Add("DateTime", Convert.ToDateTime(((Object[])values[i])[1].ToString()));
                                childRow.Add("Latitude", ((Object[])values[i])[4]);
                                childRow.Add("Longitude", ((Object[])values[i])[5]);
                                parentRow.Add(childRow);
                            }
                        }
                        sriali.MaxJsonLength = Int32.MaxValue;
                        return sriali.Serialize(parentRow);
                    }
                }
        }
        else
            return "No vehicle allocated";
        return "No trip found";
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetUnloadVehiclesForCust(string CustomerCode)
    {
        try
        {
            BusinessLogicLayer.BLL objBll = new BusinessLogicLayer.BLL();
            //Thread rlog = new Thread(() => objBll.WebServiceRequestlog("GetUnloadVehiclesForCust", "", CustomerCode, "", "", sHost, sUserIp));
            //rlog.Start();
            DataTable table = objBll.GetUnloadVehiclesForCust(CustomerCode);
            if (table == null || table.Rows.Count == 0)
                return "No Data";
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            List<Dictionary<string, object>> parentRow = new List<Dictionary<string, object>>();
            Dictionary<string, object> childRow;
            foreach (DataRow row in table.Rows)
            {
                childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            return jsSerializer.Serialize(parentRow);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    //Added by suvarna on 4/2/2020 TO store ASSET SMART REQUEST LOG 
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string insertUpdtRateRequestLog(string sessionId, string id, string requestType, int requestedValue, string source)
    {
        BusinessLogicLayer.BLL ObjBll = new BusinessLogicLayer.BLL();
        string res = objBll.insertUpdtRateRequestLog(sessionId, id, requestType, requestedValue, source);
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("insertUpdtRateRequestLog", "Asset Smart", "", "", "", sHost, sUserIp));
        rlog.Start();
        return res;
    }
    //Added by suvarna on 4/2/2020 TO Get ASSET SMART Config Data 
    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string getAssetStatus(string sessionid)
    {
        Thread rlog = new Thread(() => objBll.WebServiceRequestlog("getAssetStatus", "Asset Smart" + sessionid, "", "", "", sHost, sUserIp));
        rlog.Start();
        BusinessLogicLayer.BLL objbll = new BusinessLogicLayer.BLL();
        //DataTable details = objBll.GetVehiclesBySessionId(sessionid).Tables[0];
        List<AssetUpdtType> AssetList = objBll.GetAssetUpdtTypeList(sessionid);//Modified by suvarna on 21/12/2020
        var jss = new JavaScriptSerializer();
        return jss.Serialize(AssetList);
    }
    //For Distance Report in fleet smart app
    public Object[] GetLatLngArrayBwnTime(Object[] values, DateTime start, DateTime end)
    {
        ArrayList list = new ArrayList();
        for (int i = 0; i < values.Length; i++)
        {
            DateTime time = DateTime.Parse((((Object[])values[i])[2]).ToString());
            if (time >= start)
            {
                if (time <= end)
                {
                    list.Add(((Object[])values[i]));
                }
                else
                {
                    break;
                }
            }
        }
        return list.ToArray();
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string GetDrivingHoursReport(string vehicleId, string fromdate, string todate)
    {
        string str = "";
        string strTemp = "";
        List<Driverhrsdetails> Driverdetails = new List<Driverhrsdetails>();
        var sriali = new JavaScriptSerializer();
        StringBuilder sb = new StringBuilder();
        string vehicleid = vehicleId.Split(',')[0];
        DataTable data = objBll.GetDriverTagInOutReport("", Convert.ToDateTime(fromdate), Convert.ToDateTime(todate), vehicleid);
        object[] values = objBll.getLatLong(vehicleid, Convert.ToDateTime(fromdate).ToString("yyyy-MM-dd HH:mm:ss"), Convert.ToDateTime(todate).ToString("yyyy-MM-dd HH:mm:ss"));
        List<string> trips = objBll.GetOnOffTripsTime(vehicleid, values, Convert.ToDateTime(fromdate), Convert.ToDateTime(todate));
        
        foreach (string trip in trips)
        {
            string TripId = "0";
            string TripOn = "";
            string[] temp = trip.Split(',');
            if (temp.Length > 2)
            {
                TripId = temp[2];
            }
            if (temp.Length > 3)
            {
                TripOn = temp[3];
            }
            int runtime = Convert.ToInt32(temp[6]);
            double distance = Math.Round(Convert.ToDouble(temp[5]), 1);
            DateTime TripActualStart = Convert.ToDateTime(temp[3]);
            DateTime TripActualEnd = Convert.ToDateTime(temp[4]);
            if (distance >= 0)
            {
                object[] vals = GetLatLngArrayBwnTime(values, Convert.ToDateTime(temp[0]), Convert.ToDateTime(temp[1]));
                str = writeDistanceRport(vehicleid, Convert.ToDateTime(temp[0]), Convert.ToDateTime(temp[1]), vals, temp[7].Replace("|", ","), temp[8].Replace("|", ","), distance, runtime, TripActualStart, TripActualEnd);
                strTemp = strTemp + str;
            }
        }
        return strTemp;
    }
    public string writeDistanceRport(string vehicleId, DateTime from, DateTime to, object[] values, string start_latlng, string end_latlng, double distance, int runtime, DateTime TripActualStart, DateTime TripActualEnd)
    {
        try
        {
            List<Driverhrsdetails> Driverdetails = new List<Driverhrsdetails>();
            var sriali = new JavaScriptSerializer();
            object[] ret = objBll.accumulateDistance(vehicleId, from.ToString("yyyy-MM-dd HH:mm:ss"), to.ToString("yyyy-MM-dd HH:mm:ss"), null, values);
            string startpositionTxt = "", topositionTxt = "";
            int accountid = objBll.getAccountIdByVehicleId(vehicleId);
            if (start_latlng.ToString() != "")
                startpositionTxt = objBll.getPositionTxt(ret[3].ToString().Split(',')[0], ret[3].ToString().Split(',')[1], -1, accountid.ToString());//Modified by suvarna on 27/7/2021 as googlemaplink was pointing to correct location but location names are different bcz start_latlng/end_latlng values are different from googlemaplink latlongs. so to match both latlongs did this modification
            if (end_latlng.ToString() != "")
                topositionTxt = objBll.getPositionTxt(ret[4].ToString().Split(',')[0], ret[4].ToString().Split(',')[1], -1, accountid.ToString());
            if (startpositionTxt != "" || topositionTxt != "")
            {
                from = TripActualStart;
                to = TripActualEnd;
                TimeSpan ts = new TimeSpan(0, 0, runtime);
                ts = TripActualEnd.Subtract(TripActualStart);
                if (distance >= 0)
                {
                    string driver = objBll.getdrivername(vehicleId, from.ToString("yyyy-MM-dd HH:mm:ss"));
                    string str = "{Driver:\"" + driver.Split(',')[0]
                          + "\",Vehicleid:\"" + vehicleId
                          + "\",FromDate:\"" + from.ToString("dd'/'MM'/'yyyy hh:mm:ss tt")
                          + "\",FromLocation:\"" + startpositionTxt
                          + "\",ToDate:\"" + to.ToString("dd'/'MM'/'yyyy hh:mm:ss tt")
                          + "\",ToLocation:\"" + topositionTxt
                          + "\",RunTime:\"" + ts
                          + "\",Distance:\"" + Math.Round(distance, 1)
                          + "\"},";
                    return str;
                }
            }
            return "";
        }
        catch { return ""; }
    }
    public class AccountConfig
    {
        public string logo { get; set; }
        public string map_refreshrate { get; set; }
        public string dasboard_refreshrate { get; set; }
        public string landing_refreshrate { get; set; }
        public string map_type { get; set; }
        public string map_view { get; set; }
        public string aux_input { get; set; }
        public string blocked_reason { get; set; }
        public string blocked_date { get; set; }
        public int map_zoom_level { get; set; }
        public int showbatteryinland { get; set; }
        public string reportDateRange1 { get; set; }
        public string reportDateRange2 { get; set; }
    }
    public class WIFIConfig
    {
        public string WIFIFlag { get; set; }
        public string videoUrl { get; set; }
    }


    public class VehicleModel
    {
        public string Select { get; set; }
        public string VehicleID { get; set; }
        public string DateTime { get; set; }
        public string Time { get; set; }
        public string LAt { get; set; }
        public string longi { get; set; }
        public string Speed { get; set; }
        public string id { get; set; }
        public string VehicleInfo { get; set; }
        public string Location { get; set; }
        public string direction { get; set; }
        public string idlingInstance { get; set; }
        public string remarks { get; set; }
        public string Status { get; set; }
        public string HaltDuration { get; set; }
        public string DistfrmHome { get; set; }
        public string DirtoHome { get; set; }
        public string IconType { get; set; }
        public string AuxInput { get; set; }
        public string DriverInfo { get; set; }
        public string TripState { get; set; }
        public string Capacity { get; set; }
        public string Battery { get; set; }
    }

    public class ReffRoue
    {
        public string ReffRouteName { get; set; }
    }
    public class ReffRoueLatLng
    {
        public string Lat { get; set; }
        public string Lng { get; set; }
    }
    public class Place
    {
        public string Name { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }

    }
    public class points
    {
        public string name { get; set; }
        public string point { get; set; }
        public string t_name { get; set; }
    }
    public class EmergencyVehicle
    {
        public string ID { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string time { get; set; }

    }

    public class Notification
    {
        public string Driverid { get; set; }
        public string Subject { get; set; }
        public string Info { get; set; }
        public string Date { get; set; }
        public string count { get; set; }
        public string Isnotified { get; set; }
        public string Priority { get; set; }
    }

    class Driver
    {
        public string Driverid { get; set; }
        public string Drivername { get; set; }
        public string mobile { get; set; }
        public string Imei { get; set; }
        public string Licenceid { get; set; }
    }
    class Customer
    {
        public string Customercode { get; set; }
        public string CustomerName { get; set; }
        public string StockLevel { get; set; }
        public string StockLevelTime { get; set; }
        public string Next3daysExpected { get; set; }
        public string DCNo { get; set; }
        public string Quantity { get; set; }
        public string LastDispatched { get; set; }
        public string LastDelivered { get; set; }
        public double LoadOnTransit { get; set; }
    }
    class Transporter
    {
        public string Transporterid { get; set; }
        public string TransporterName { get; set; }
        public string Login { get; set; }
        public string ShortName { get; set; }
    }
    class MobileMenu
    {
        public string Menukey { get; set; }
        public string MenuValue { get; set; }
    }

    class ViolationReport
    {
        public string vehicleid { get; set; }
        public string nightdriving { get; set; }
        public string overspeed { get; set; }
        public string harsebrake { get; set; }
        public string runtime { get; set; }
    }

    class ConsolidatedReport
    {
        public string vehicleid { get; set; }
        public string yesterday { get; set; }
        public string thismonth { get; set; }
        public string thisyear { get; set; }
    }

    class VehicleDriver
    {
        public string vehicleid { get; set; }
        public string vehicleinfo { get; set; }
        public string drivername { get; set; }
        public string contact_No { get; set; }
    }
    class VehicleList
    {
        public string vehicleid { get; set; }
        public string vehicleinfo { get; set; }
    }
    class DistanceReport
    {
        public string vehicleid { get; set; }
        public double tripdistance { get; set; }
        public double totaldistance { get; set; }
        public string tripdate { get; set; }

    }

    class storeviolation
    {
        public string vehicleid { get; set; }
        public string type { get; set; }
        public string datetime { get; set; }
        public string remarks { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }

    }
    
    public class Imei
    {
        public string session { get; set; }
        public string account { get; set; }
    }
    public class Driverhrsdetails
    {
        public string Driver { get; set; }
        public string Vehicleid { get; set; }
        public string FromDate { get; set; }
        public string FromLocation { get; set; }
        public string ToDate { get; set; }
        public string ToLocation { get; set; }
        public TimeSpan RunTime { get; set; }
        public Double Distance { get; set; }
    }
}
