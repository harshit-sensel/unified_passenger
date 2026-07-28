package com.sensel.passengerpro;

import android.util.Log;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * Modern REST Network Client replacing KSOAP2 for Unified Passenger Application.
 */
public class WebServices {

    private static final String TAG = "WebServices_REST";

    // Helper method to make HTTP REST requests
    private String makeHttpRequest(String endpoint, String method, String jsonBody) {
        HttpURLConnection conn = null;
        try {
            URL url = new URL(UrlConfig.REST_BASE_URL + endpoint);
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod(method);
            conn.setRequestProperty("Content-Type", "application/json");
            conn.setRequestProperty("Accept", "application/json");
            conn.setConnectTimeout(10000);
            conn.setReadTimeout(10000);

            if (jsonBody != null && (method.equals("POST") || method.equals("PUT"))) {
                conn.setDoOutput(true);
                try (OutputStream os = conn.getOutputStream()) {
                    byte[] input = jsonBody.getBytes("utf-8");
                    os.write(input, 0, input.length);
                }
            }

            int responseCode = conn.getResponseCode();
            if (responseCode == HttpURLConnection.HTTP_OK) {
                try (BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), "utf-8"))) {
                    StringBuilder response = new StringBuilder();
                    String responseLine;
                    while ((responseLine = br.readLine()) != null) {
                        response.append(responseLine.trim());
                    }
                    return response.toString();
                }
            } else {
                Log.e(TAG, "HTTP Error Code: " + responseCode);
                return "No Data";
            }
        } catch (Exception e) {
            Log.e(TAG, "Error in HTTP request to " + endpoint, e);
            return "No Data";
        } finally {
            if (conn != null) {
                conn.disconnect();
            }
        }
    }

    // 1. Phone Login Validation
    public String GetPsngrInfoWithValidation(String mobileno, String flag) {
        try {
            JSONObject json = new JSONObject();
            json.put("MobileNo", mobileno);
            json.put("Flag", flag != null ? flag : "Validate");
            json.put("AppName", "com.sensel.passengerapp");
            return makeHttpRequest("auth/validate-phone", "POST", json.toString());
        } catch (Exception e) {
            return "No Data";
        }
    }

    // Overload for 3 parameters
    public String GetPsngrInfoWithValidation(String mobileno, String flag, String appName) {
        return GetPsngrInfoWithValidation(mobileno, flag);
    }

    // Dynamic Menu Fetching API
    public String GetMenusByUser(String mobileno) {
        try {
            JSONObject json = new JSONObject();
            json.put("MobileNo", mobileno != null ? mobileno : "");
            json.put("Username", mobileno != null ? mobileno : "");
            return makeHttpRequest("auth/get-menus", "POST", json.toString());
        } catch (Exception e) {
            Log.e(TAG, "Error in GetMenusByUser", e);
            return "[]";
        }
    }


    // 2. IMEI Silent Auto-Login Validation
    public String GetPsngrInfoWithValidationWithImei(String imei, String flag) {
        try {
            JSONObject json = new JSONObject();
            json.put("Imei", imei);
            json.put("Flag", flag != null ? flag : "Validate");
            json.put("AppName", "com.sensel.passengerapp");
            return makeHttpRequest("auth/validate-imei", "POST", json.toString());
        } catch (Exception e) {
            return "No Data";
        }
    }

    // Overload for 3 parameters
    public String GetPsngrInfoWithValidationWithImei(String imei, String flag, String appName) {
        return GetPsngrInfoWithValidationWithImei(imei, flag);
    }

    // 3. Vehicle Telemetry Position
    public String GetVehiclePositionForPsngrApp(String psngrID, String vehicleId) {
        return makeHttpRequest("vehicle/position?psngrID=" + psngrID + "&vehicleId=" + vehicleId, "GET", null);
    }

    // 4. Checklist Insert
    public String InsertPsngrChecklistNew3(String psngrId, String vehicleId, String type, String rules, String wfmid,
                                           String ptw, String driverId, String imei, String lat, String lng, String manual,
                                           String driverDetails, String omr, String gpscheckid, String gpsReason,
                                           String driverImage, String towerName, String vehiclephoto,
                                           String taginOdometerPhoto, String tagoutOdometerPhoto) {
        try {
            JSONObject json = new JSONObject();
            json.put("PsngrId", psngrId);
            json.put("VehicleId", vehicleId);
            json.put("Type", type);
            json.put("Rules", rules);
            json.put("DriverId", driverId);
            json.put("Imei", imei);
            json.put("Lat", lat);
            json.put("Lng", lng);
            json.put("DriverDetails", driverDetails);
            json.put("Omr", omr);
            json.put("DriverImage", driverImage);
            json.put("TowerName", towerName);
            json.put("Vehiclephoto", vehiclephoto);
            json.put("TaginOdometerPhoto", taginOdometerPhoto);
            json.put("TagoutOdometerPhoto", tagoutOdometerPhoto);
            return makeHttpRequest("checklist/insert", "POST", json.toString());
        } catch (Exception e) {
            return "Failed to insert";
        }
    }

    // Legacy method overloads for InsertPsngrChecklist
    public String InsertPsngrChecklist(String psngrId, String vehicleId, String type, String rules, String wfmid,
                                        String ptw, String driverId, String imei, String lat, String lng, String manual,
                                        String driverDetails, String omr, String gpscheckid, String gpsReason,
                                        String driverImage, String towerName, String vehiclephoto,
                                        String taginOdometerPhoto, String tagoutOdometerPhoto) {
        return InsertPsngrChecklistNew3(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, imei, lat, lng, manual,
                driverDetails, omr, gpscheckid, gpsReason, driverImage, towerName, vehiclephoto, taginOdometerPhoto, tagoutOdometerPhoto);
    }

    public String InsertPsngrChecklistNew2(String psngrId, String vehicleId, String type, String rules, String wfmid,
                                         String ptw, String driverId, String imei, String lat, String lng, String manual,
                                         String driverDetails, String omr, String gpscheckid, String gpsReason,
                                         String driverImage, String towerName, String vehiclephoto,
                                         String taginOdometerPhoto, String tagoutOdometerPhoto) {
        return InsertPsngrChecklistNew3(psngrId, vehicleId, type, rules, wfmid, ptw, driverId, imei, lat, lng, manual,
                driverDetails, omr, gpscheckid, gpsReason, driverImage, towerName, vehiclephoto, taginOdometerPhoto, tagoutOdometerPhoto);
    }

    // 5. SOS Panic Alert
    public String InsertPanicAlertFromApp(String id, String vehicleId, String type) {
        try {
            JSONObject json = new JSONObject();
            json.put("Id", id);
            json.put("VehicleId", vehicleId);
            json.put("Type", type);
            return makeHttpRequest("alerts/panic", "POST", json.toString());
        } catch (Exception e) {
            return "Failed";
        }
    }

    // 6. Home Location Update
    public String UpdatePsngrHomeLocation(String psngrId, String lat, String lng) {
        try {
            JSONObject json = new JSONObject();
            json.put("PsngrId", psngrId);
            json.put("Lat", lat);
            json.put("Lng", lng);
            return makeHttpRequest("passenger/home-location", "PUT", json.toString());
        } catch (Exception e) {
            return "0";
        }
    }

    // 7. Get Notifications
    public String GetPsngrNotifications(String psngrId) {
        return makeHttpRequest("notifications?psngrId=" + psngrId, "GET", null);
    }

    // 8. Notifications Read Status
    public String PsngrNotificationNotified(String psngrId) {
        try {
            JSONObject json = new JSONObject();
            json.put("PsngrId", psngrId);
            return makeHttpRequest("notifications/read", "POST", json.toString());
        } catch (Exception e) {
            return "Failed";
        }
    }

    // 9. QR Code Resolution
    public String GetVehicleidByQRCode(String qrcode) {
        try {
            JSONObject json = new JSONObject();
            json.put("QRCode", qrcode);
            return makeHttpRequest("vehicle/resolve-qr", "POST", json.toString());
        } catch (Exception e) {
            return "Invalid QRCode";
        }
    }

    // 10. Cell Towers Lookup
    public String GetPsngrTowerLocations(String mobileno, String zone, String enteredkey) {
        return makeHttpRequest("location/towers?mobileno=" + mobileno + "&zone=" + zone + "&enteredkey=" + enteredkey, "GET", null);
    }

    // 11. Check Cell Tower Location
    public String CheckPsngrTowerLocation(String mobileno, String towerName) {
        try {
            JSONObject json = new JSONObject();
            json.put("MobileNo", mobileno);
            json.put("TowerName", towerName);
            return makeHttpRequest("location/check-tower", "POST", json.toString());
        } catch (Exception e) {
            return "false";
        }
    }

    // 12. GPS Age Check
    public String VehicleMobileGPSCheck(String vehicleId, String source, String sourceId, String lat, String lng) {
        try {
            JSONObject json = new JSONObject();
            json.put("VehicleId", vehicleId);
            json.put("Source", source);
            json.put("SourceId", sourceId);
            json.put("Lat", lat);
            json.put("Lng", lng);
            return makeHttpRequest("vehicle/gps-check", "POST", json.toString());
        } catch (Exception e) {
            return "GPS Fixed";
        }
    }

    // Overload for 4 parameters
    public String VehicleMobileGPSCheck(String vehicleId, String sourceId, String lat, String lng) {
        return VehicleMobileGPSCheck(vehicleId, "App", sourceId, lat, lng);
    }

    // 13. Proximity Check
    public String GetMobVehGpsCheck(String vehicleId, String source, String sourceId, String timeThreshold, String distThreshold, String lat, String lng) {
        try {
            JSONObject json = new JSONObject();
            json.put("VehicleId", vehicleId);
            json.put("Source", source);
            json.put("SourceId", sourceId);
            json.put("TimeThreshold", timeThreshold);
            json.put("DistThreshold", distThreshold);
            json.put("Lat", lat);
            json.put("Lng", lng);
            return makeHttpRequest("vehicle/proximity-check", "POST", json.toString());
        } catch (Exception e) {
            return "Within Range";
        }
    }

    // Overload for 6 parameters
    public String GetMobVehGpsCheck(String vehicleId, String sourceId, String timeThreshold, String distThreshold, String lat, String lng) {
        return GetMobVehGpsCheck(vehicleId, "App", sourceId, timeThreshold, distThreshold, lat, lng);
    }

    // 14. Vehicles By Account ID
    public String GetVehiclesByAccountId(String accountId) {
        return makeHttpRequest("vehicles/by-account?accountId=" + accountId, "GET", null);
    }

    // 15. Assign Vehicle
    public String UpdtPsngrAssgndVeh(String psngrId, String vehicleId) {
        try {
            JSONObject json = new JSONObject();
            json.put("PsngrId", psngrId);
            json.put("VehicleId", vehicleId);
            return makeHttpRequest("passenger/assign-vehicle", "POST", json.toString());
        } catch (Exception e) {
            return "0";
        }
    }

    // 16. Get DropDown For App
    public String GetDropDownForApp(String appName, String key) {
        return makeHttpRequest("checklist/dropdown?appName=" + appName + "&key=" + key, "GET", null);
    }

    // 17. User Activity Log
    public String KeepPassengerProuseractivitylog(String passengerId, String vehicleId, String page, String lat, String lng, String appVersion) {
        try {
            JSONObject json = new JSONObject();
            json.put("PassengerId", passengerId);
            json.put("VehicleId", vehicleId);
            json.put("Page", page);
            json.put("Lat", lat);
            json.put("Lng", lng);
            json.put("AppVersion", appVersion);
            return makeHttpRequest("logs/activity", "POST", json.toString());
        } catch (Exception e) {
            return "Logged";
        }
    }

    // 18. App Version Check
    public String GetAppVersion(String packageName) {
        return makeHttpRequest("version/check?packageName=" + packageName, "GET", null);
    }

    // 19. Insert Error Record
    public String InsertErrorRecord(String error, String dateTime) {
        try {
            JSONObject json = new JSONObject();
            json.put("Error", error);
            json.put("DateTime", dateTime);
            return makeHttpRequest("logs/error", "POST", json.toString());
        } catch (Exception e) {
            return "Success";
        }
    }

    // 20. OTP Authenticate Request
    public String PassengerProApp_Authenticate(String mobileno) {
        try {
            JSONObject json = new JSONObject();
            json.put("MobileNo", mobileno);
            return makeHttpRequest("auth/send-otp", "POST", json.toString());
        } catch (Exception e) {
            return "Failed";
        }
    }
}
