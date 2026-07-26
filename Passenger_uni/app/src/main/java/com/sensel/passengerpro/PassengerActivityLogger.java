package com.sensel.passengerpro;

import android.content.Context;
import android.Manifest;
import android.content.pm.PackageManager;

import androidx.core.content.ContextCompat;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * Fire-and-forget activity logging for Passenger Pro (parity with DriverApp Keepuseractivitylog).
 * Never throws; runs on a background thread so UI flows are unchanged.
 * <p>
 * Vehicle id: live {@code GetPsngrInfoWithValidation(mobile, "Validate")} first (same as TrackOnMap reassignment
 * refresh), then tagged vehicle pref, cached passenger JSON, then {@code GetPsngrInfoWithValidation(mobile, "Tag")}.
 * Lat/lng: cached last fix, then fused/legacy quick location, then short {@link GPSTracker#getLocationWithWait(long)}.
 */
public final class PassengerActivityLogger {

    private PassengerActivityLogger() {
    }

    /** Log using passengerId / resolved assigned vehicle from prefs and optional Tag refresh. */
    public static void log(Context context, String page) {
        log(context, page, null, null);
    }

    /**
     * Log with explicit passenger / vehicle ids (e.g. logout before prefs are cleared).
     * Pass null for either id to fall back to normal resolution inside the worker thread.
     */
    public static void log(Context context, String page, String passengerId, String vehicleId) {
        if (context == null) return;
        final Context app = context.getApplicationContext();
        final String pageSafe = page == null ? "" : page.trim();
        if (pageSafe.isEmpty()) return;
        final String pidOverride = passengerId;
        final String vidOverride = vehicleId;
        new Thread(() -> {
            try {
                AppConstants ac = new AppConstants();
                String pid = pidOverride;
                if (pid == null) {
                    pid = ac.getShrdPrefValByKeyWithTag(app, "passengerinfo", "PsngrId");
                }
                if (pid == null || pid.isEmpty()) pid = "0";

                String vid = vidOverride;
                if (vid == null) {
                    vid = resolveAssignedVehicleIdForLog(app, ac);
                } else if (vid.isEmpty()) {
                    vid = resolveAssignedVehicleIdForLog(app, ac);
                }
                if (vid == null) vid = "";

                String[] ll = resolveLatLngForLog(app, ac);
                String lat = ll[0];
                String lng = ll[1];

                // versionCode first so dashboards that show only a leading integer see the build (e.g. 8 not 2 from "1.8").
                String appVersion = BuildConfig.VERSION_CODE + "(" + BuildConfig.VERSION_NAME + ")";
                WebServices ws = new WebServices();
                ws.KeepPassengerProuseractivitylog(pid, vid, pageSafe, lat, lng, appVersion);
            } catch (Exception ignored) {
            }
        }, "PassengerActivityLog").start();
    }

    /**
     * Log logout using snapshots taken on the UI thread before prefs are cleared (avoids race with async log).
     */
    public static void logLogout(Context context, String passengerIdSnapshot, String taggedVehicleIdSnapshot, String passengerInfoJsonSnapshot) {
        if (context == null) return;
        final Context app = context.getApplicationContext();
        final String pidSnap = passengerIdSnapshot != null ? passengerIdSnapshot : "";
        final String taggedSnap = taggedVehicleIdSnapshot != null ? taggedVehicleIdSnapshot.trim() : "";
        final String infoSnap = passengerInfoJsonSnapshot;
        new Thread(() -> {
            try {
                String pid = pidSnap.isEmpty() ? "0" : pidSnap;
                String vid = taggedSnap;
                if (vid.isEmpty()) {
                    vid = parseAssignedVehicleFromJsonArray(infoSnap);
                }
                if (vid.isEmpty() && infoSnap != null && infoSnap.contains("MobileNo")) {
                    try {
                        JSONArray jArr = new JSONArray(infoSnap);
                        if (jArr.length() > 0) {
                            String mobile = jArr.getJSONObject(0).optString("MobileNo", "").trim();
                            if (!mobile.isEmpty()) {
                                WebServices ws = new WebServices();
                                String validate = ws.GetPsngrInfoWithValidation(mobile, "Validate");
                                vid = parseAssignedVehicleFromJsonArray(validate);
                                if (vid.isEmpty()) {
                                    String tag = ws.GetPsngrInfoWithValidation(mobile, "Tag");
                                    vid = parseAssignedVehicleFromJsonArray(tag);
                                }
                            }
                        }
                    } catch (Exception ignored) {
                    }
                }
                AppConstants ac = new AppConstants();
                String[] ll = resolveLatLngForLog(app, ac);
                String appVersion = BuildConfig.VERSION_CODE + "(" + BuildConfig.VERSION_NAME + ")";
                WebServices ws = new WebServices();
                ws.KeepPassengerProuseractivitylog(pid, vid != null ? vid : "", "Logout", ll[0], ll[1], appVersion);
            } catch (Exception ignored) {
            }
        }, "PassengerActivityLog").start();
    }

    /** Same AssignedVehicleId keys as {@link TrackOnMap#optAssignedVehicleId(JSONObject)}. */
    private static String optAssignedVehicleId(JSONObject data) {
        if (data == null) return "";
        String[] keys = {"AssignedVehicleId", "AssignedVehicleID", "AssignedVehicleid", "assignedVehicleId"};
        for (String k : keys) {
            String v = data.optString(k, "").trim();
            if (!v.isEmpty() && !"null".equalsIgnoreCase(v)) return v;
        }
        return "";
    }

    /** Last non-empty AssignedVehicleId wins (same idea as TrackOnMap.extractLastAssignedAndSession). */
    private static String parseAssignedVehicleFromJsonArray(String json) {
        if (json == null || json.trim().isEmpty() || !json.trim().startsWith("[")) return "";
        try {
            JSONArray jArr = new JSONArray(json);
            String last = "";
            for (int j = 0; j < jArr.length(); j++) {
                JSONObject row = jArr.optJSONObject(j);
                if (row == null) continue;
                String a = optAssignedVehicleId(row);
                if (!a.isEmpty()) last = a;
            }
            return last;
        } catch (JSONException e) {
            return "";
        }
    }

    /**
     * Resolve vehicle for activity log: live Validate (current AssignedVehicleId / reallocations), then
     * tagged pref, cached passengerinfo, then Tag API — mirrors {@link TrackOnMap} refresh semantics.
     */
    private static String resolveAssignedVehicleIdForLog(Context app, AppConstants ac) {
        String mobile = ac.getShrdPrefValByKeyWithTag(app, "passengerinfo", "MobileNo");
        if (mobile != null && !mobile.trim().isEmpty()) {
            try {
                WebServices ws = new WebServices();
                String validate = ws.GetPsngrInfoWithValidation(mobile.trim(), "Validate");
                String fromValidate = parseAssignedVehicleFromJsonArray(validate);
                if (!fromValidate.isEmpty()) {
                    return fromValidate;
                }
            } catch (Exception ignored) {
            }
        }

        String tagged = ac.getShrdPrefValByKey(app, AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID);
        if (tagged != null) {
            tagged = tagged.trim();
            if (!tagged.isEmpty() && !"null".equalsIgnoreCase(tagged)) return tagged;
        }

        String passengerJson = ac.getShrdPrefValByKey(app, "passengerinfo");
        String fromInfo = parseAssignedVehicleFromJsonArray(passengerJson);
        if (!fromInfo.isEmpty()) return fromInfo;

        if (mobile == null || mobile.trim().isEmpty()) return "";
        try {
            WebServices ws = new WebServices();
            String tag = ws.GetPsngrInfoWithValidation(mobile.trim(), "Tag");
            return parseAssignedVehicleFromJsonArray(tag);
        } catch (Exception e) {
            return "";
        }
    }

    private static boolean hasFineLocation(Context app) {
        return ContextCompat.checkSelfPermission(app, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED;
    }

    private static boolean isNonZeroLatLng(String lat, String lng) {
        if (lat == null || lng == null) return false;
        try {
            double la = Double.parseDouble(lat.trim());
            double lo = Double.parseDouble(lng.trim());
            return Math.abs(la) > 1e-8 || Math.abs(lo) > 1e-8;
        } catch (Exception e) {
            return false;
        }
    }

    private static String[] resolveLatLngForLog(Context app, AppConstants ac) {
        String lat = "0";
        String lng = "0";
        try {
            String cached = ac.getShrdPrefValByKey(app, AppConstants.KEY_LAST_VALID_LATLNG);
            if (cached != null && cached.contains(",")) {
                String[] p = cached.split(",");
                if (p.length >= 2) {
                    lat = p[0].trim();
                    lng = p[1].trim();
                }
            }
        } catch (Exception ignored) {
        }
        if (isNonZeroLatLng(lat, lng)) {
            return new String[]{lat, lng};
        }

        if (!hasFineLocation(app)) {
            return new String[]{lat, lng};
        }

        try {
            GPSTracker g = new GPSTracker(app);
            String ll = g.getLocation();
            if (ll != null && ll.contains(",")) {
                String[] p = ll.split(",");
                if (p.length >= 2) {
                    lat = p[0].trim();
                    lng = p[1].trim();
                }
            }
            if (!isNonZeroLatLng(lat, lng)) {
                // Cold first fix after install/login: allow longer than menu taps that already have cache.
                ll = g.getLocationWithWait(8500);
                if (ll != null && ll.contains(",")) {
                    String[] p = ll.split(",");
                    if (p.length >= 2) {
                        lat = p[0].trim();
                        lng = p[1].trim();
                    }
                }
            }
        } catch (Exception ignored) {
        }
        return new String[]{lat, lng};
    }
}
