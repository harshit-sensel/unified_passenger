package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import android.view.Menu;
import android.view.MenuItem;
import android.webkit.GeolocationPermissions;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

public class TrackOnMap extends BaseActivity {

    /** Map must track backend-assigned vehicle only, not the tag-in VehicleId. */
    private static String optAssignedVehicleId(JSONObject data) {
        if (data == null) return "";
        String[] keys = {"AssignedVehicleId", "AssignedVehicleID", "AssignedVehicleid", "assignedVehicleId"};
        for (String k : keys) {
            String v = data.optString(k, "").trim();
            if (!v.isEmpty() && !"null".equalsIgnoreCase(v)) return v;
        }
        return "";
    }

    /** Last non-empty wins (same as refresh loop). */
    private static void extractLastAssignedAndSession(JSONArray jArr, String[] outAssigned, String[] outSession) throws JSONException {
        String lastAssigned = "";
        String lastSession = "";
        for (int j = 0; j < jArr.length(); j++) {
            JSONObject data = jArr.getJSONObject(j);
            String a = optAssignedVehicleId(data);
            if (!a.isEmpty()) lastAssigned = a;
            String s = data.optString("sessionid", "").trim();
            if (!s.isEmpty() && !"null".equalsIgnoreCase(s)) lastSession = s;
        }
        outAssigned[0] = lastAssigned;
        outSession[0] = lastSession;
    }
    String vehicleid = "";
    String sessionid = "";
    AppConstants appConstants=new AppConstants();
    WebServices webServices=new WebServices();
    String resultFromPrev;
    private boolean mapErrorToastShown = false;
    /** Zoom 18 ≈ 50m: close enough to see vehicle, not 20m (causes blank map on mobile). */
    private static final int MAP_ZOOM_LEVEL = 18;
    private final Handler zoomHandler = new Handler(Looper.getMainLooper());
    private WebView trackMapWebView;

    // Re-allocate handling:
    // Backend updates `AssignedVehicleId` in real-time, so we must refresh while this screen is open
    // and reload the map when the assigned vehicle changes.
    private static final long ASSIGNED_VEHICLE_REFRESH_MS = 20000; // keep same as map position polling
    private final Handler assignedVehicleHandler = new Handler(Looper.getMainLooper());
    private Runnable assignedVehicleRunnable;
    private String passengerMobileNo = "";
    private volatile boolean assignedVehicleRefreshRunning = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        resultFromPrev=getIntent().getStringExtra("tagDetails");
        super.onCreate(savedInstanceState);
        setContentView(R.layout.track_on_map);
        // Activity log: MainActivity already logs "Menu:Track Your Vehicle" — avoid duplicate row.
        try {
            if (resultFromPrev == null || resultFromPrev.trim().isEmpty()) {
                resultFromPrev = "[]";
            }
            JSONArray jArr = new JSONArray(resultFromPrev);
            String[] outA = new String[1];
            String[] outS = new String[1];
            extractLastAssignedAndSession(jArr, outA, outS);
            vehicleid = outA[0].isEmpty() ? "No Vehicle Assigned" : outA[0];
            sessionid = outS[0] != null ? outS[0] : "";
        } catch (JSONException e) {
            e.printStackTrace();
        }
        final AlertDialog progressDialog =  ProgressDialog.show(TrackOnMap.this, "", "Loading...", true);


        trackMapWebView = (WebView) findViewById(R.id.showposition);
        final WebView myWebView = trackMapWebView;

        myWebView.setWebViewClient(
                new WebViewClient() {
                    @Override
                    public boolean shouldOverrideUrlLoading(WebView view, String url) {
                        view.loadUrl(url);
                        return true;
                    }

                    @Override
                    public void onPageFinished(WebView view, String url) {
                        progressDialog.dismiss();
                        // Set zoom once to 50m (no repeated loop: that caused 20m then 100m jump). One apply + one retry.
                        if (url != null && (url.contains("index-min.html") || url.contains("index-min1.html")) && sessionid != null && !sessionid.isEmpty()) {
                            zoomHandler.postDelayed(new Runnable() {
                                @Override
                                public void run() {
                                    if (view != null && !isFinishing()) applyZoom50m(view);
                                }
                            }, 2500);
                            zoomHandler.postDelayed(new Runnable() {
                                @Override
                                public void run() {
                                    if (view != null && !isFinishing()) applyZoom50m(view);
                                }
                            }, 6000);
                        }
                    }
                    @Override
                    public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                        // Only show for main frame to avoid repeated toasts for images/scripts
                        boolean isMainFrame = Build.VERSION.SDK_INT >= Build.VERSION_CODES.N && request.isForMainFrame();
                        if (!isMainFrame) return;
                        if (mapErrorToastShown) return;
                        mapErrorToastShown = true;
                        String msg = "Map could not load. Check your internet connection.";
                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && error != null && error.getDescription() != null) {
                            CharSequence desc = error.getDescription();
                            if (desc != null && desc.length() > 0) msg = msg + " " + desc;
                        }
                        Toast.makeText(getApplicationContext(), msg, Toast.LENGTH_LONG).show();
                    }
                });
        myWebView.setWebChromeClient(new WebChromeClient() {
            public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                callback.invoke(origin, true, false);
            }
        });

        WebSettings settings = myWebView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }
        //Added by Madhuri for migration to sdkversion33
        //settings.setAppCacheEnabled(true);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);

        passengerMobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
        // First paint must use AssignedVehicleId from GetPsngrInfoWithValidation — not tag VehicleId.
        if (passengerMobileNo != null && !passengerMobileNo.trim().isEmpty()) {
            new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        final String res = webServices.GetPsngrInfoWithValidation(passengerMobileNo.trim(), "Validate");
                        if (res != null && res.trim().startsWith("[")) {
                            JSONArray arr = new JSONArray(res);
                            String[] oa = new String[1];
                            String[] os = new String[1];
                            extractLastAssignedAndSession(arr, oa, os);
                            final String a = oa[0];
                            final String s = os[0];
                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    if (!a.isEmpty()) vehicleid = a;
                                    if (s != null && !s.isEmpty()) sessionid = s;
                                    call(trackMapWebView, getApplicationContext());
                                    startAssignedVehicleRefresh();
                                }
                            });
                            return;
                        }
                    } catch (Exception ignored) {
                    }
                    runOnUiThread(new Runnable() {
                        @Override
                        public void run() {
                            call(trackMapWebView, getApplicationContext());
                            startAssignedVehicleRefresh();
                        }
                    });
                }
            }).start();
        } else {
            call(myWebView, getApplicationContext());
            startAssignedVehicleRefresh();
        }

        new Thread(new Runnable() {
            @Override
            public void run() {
                String appdata = webServices.GetAppVersion(getApplicationContext().getPackageName());
                if (appdata != null) {
                    try {
                        if(appdata.contains("VersionCode")) {
                            JSONArray array = new JSONArray(appdata);
                            JSONObject data = new JSONObject(array.get(0).toString());
                            String _version = data.getString("VersionCode");
                            if (Integer.parseInt(_version) > BuildConfig.VERSION_CODE)
                                showUpdateAlert(Integer.parseInt(data.getString("Priority")), Integer.parseInt(data.getString("StableVersion")));
                            if(data.getString("DomainUrl").contains("http") && !UrlConfig.DOMAINURL1.equals(data.getString("DomainUrl"))) {
                                UrlConfig.DOMAINURL1 = data.getString("DomainUrl");
                                if (data.getString("DomainUrl").contains("https://"))
                                    UrlConfig.DOMAINURL2 = data.getString("DomainUrl").replace("https://", "http://");
                                else
                                    UrlConfig.DOMAINURL2 = data.getString("DomainUrl").replace("http://", "https://");
                            }
                        }
                    } catch (JSONException e) {
                        e.printStackTrace();
                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagTrack("+new Exception().getStackTrace()[0].getLineNumber()+")--"+appdata);
                    }
                }
                else{
                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                    errorRecordSendMail.errorrecordSendMail(appdata + "-TagTrack("+new Exception().getStackTrace()[0].getLineNumber()+")--GetAppVersion("+getApplicationContext().getPackageName()+")");
                }
            }
        }).start();
    }

    /** Set map zoom to ~50m once (no loop) so no 20m-then-100m jump. */
    private void applyZoom50m(WebView view) {
        if (view == null || isFinishing()) return;
        try {
            view.evaluateJavascript("(function(){ try { if(window.map&&typeof window.map.setZoom==='function') window.map.setZoom(" + MAP_ZOOM_LEVEL + "); } catch(e){} })();", null);
        } catch (Exception ignored) {}
    }

    @Override
    protected void onDestroy() {
        zoomHandler.removeCallbacksAndMessages(null);
        assignedVehicleRefreshRunning = false;
        assignedVehicleHandler.removeCallbacksAndMessages(null);
        trackMapWebView = null;
        super.onDestroy();
    }

    private void startAssignedVehicleRefresh() {
        assignedVehicleRefreshRunning = true;
        if (assignedVehicleRunnable != null) assignedVehicleHandler.removeCallbacks(assignedVehicleRunnable);
        assignedVehicleRunnable = new Runnable() {
            @Override
            public void run() {
                if (!assignedVehicleRefreshRunning || isFinishing()) return;
                refreshAssignedVehicleIdFromBackend();
                assignedVehicleHandler.postDelayed(this, ASSIGNED_VEHICLE_REFRESH_MS);
            }
        };
        assignedVehicleHandler.postDelayed(assignedVehicleRunnable, ASSIGNED_VEHICLE_REFRESH_MS);
    }

    private void refreshAssignedVehicleIdFromBackend() {
        // Network call must not run on UI thread.
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    final String res = webServices.GetPsngrInfoWithValidation(passengerMobileNo, "Validate");
                    if (res == null || res.trim().isEmpty() || !res.trim().startsWith("[")) return;

                    JSONArray jArr = new JSONArray(res);
                    String latestAssignedVehicleId = "";
                    String latestSessionId = "";

                    for (int j = 0; j < jArr.length(); j++) {
                        JSONObject data = jArr.getJSONObject(j);
                        String assigned = optAssignedVehicleId(data);
                        if (!assigned.isEmpty()) latestAssignedVehicleId = assigned;
                        String sess = data.optString("sessionid", "").trim();
                        if (!sess.isEmpty() && !"null".equalsIgnoreCase(sess)) {
                            latestSessionId = sess;
                        }
                    }

                    final String newVehicleId = latestAssignedVehicleId;
                    final String latestSessionIdFinal = latestSessionId;

                    if (newVehicleId == null || newVehicleId.trim().isEmpty() || "null".equalsIgnoreCase(newVehicleId)) {
                        return;
                    }

                    runOnUiThread(new Runnable() {
                        @Override
                        public void run() {
                            if (isFinishing()) return;

                            boolean vehicleChanged = newVehicleId != null && !newVehicleId.equals(vehicleid);
                            boolean sessionChanged = latestSessionIdFinal != null && !latestSessionIdFinal.isEmpty() && !latestSessionIdFinal.equals(sessionid);

                            if (vehicleChanged) vehicleid = newVehicleId;
                            if (sessionChanged) sessionid = latestSessionIdFinal;

                            // Reload only if something changed, otherwise keep current map polling.
                            if (vehicleChanged || sessionChanged) {
                                call(trackMapWebView, getApplicationContext());
                            }
                        }
                    });
                } catch (Exception ignored) {
                    // If this refresh fails, we keep showing the current tracked vehicle.
                }
            }
        }).start();
    }

    @JavascriptInterface
    public void call(WebView my,Context context)
    {

        try {
            my.getSettings().setUseWideViewPort(true);
            if(sessionid!="") {
                // Home location not shown on Track Your Vehicle map
                String mapParams = "sessionid=" + sessionid + "&vehicleid=" + vehicleid + "&domain=" + UrlConfig.MAP_DOMAIN
                        + "&defaultZoom=" + MAP_ZOOM_LEVEL + "&zoom=" + MAP_ZOOM_LEVEL + "&scaleMeters=50"
                        + "&drawRoute=1&showRouteLine=1&animateVehicle=1";
                my.loadUrl(UrlConfig.MAP_PAGE_URL + "?" + mapParams);
            }
            else
                my.loadUrl(UrlConfig.MAP_PAGE_URL);

        }
        catch (Exception e){
            if(!((Activity) context).isFinishing()) {
                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                        new ContextThemeWrapper(TrackOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
                alertDialogBuilder.setIcon(R.drawable.error);
                alertDialogBuilder.setTitle("Error ");
                alertDialogBuilder.setMessage("Unable to load, Please try again ")
                        .setCancelable(false)
                        .setPositiveButton("Ok",
                                new DialogInterface.OnClickListener() {
                                    public void onClick(DialogInterface dialog, int id) {
                                        dialog.cancel();
                                    }
                                });
                AlertDialog alert = alertDialogBuilder.create();
                if(!alert.isShowing())
                    alert.show();
            }
        }
    }

    @Override
    public void onBackPressed() {
        String userMenus = appConstants.getShrdPrefValByKey(getApplicationContext(), "UserMenus");
        if (userMenus != null && userMenus.contains("dashboard")) {
            Intent intent = new Intent(TrackOnMap.this, MainActivity.class);
            intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(intent);
            finish();
        } else {
            Intent intent = new Intent(Intent.ACTION_MAIN);
            intent.addCategory(Intent.CATEGORY_HOME);
            intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
            startActivity(intent);
            finish();
        }
    }

    @Override
    public boolean onPrepareOptionsMenu(final Menu menu) {
        menu.clear();
        getMenuInflater().inflate(R.menu.menu_options, menu);
        return super.onCreateOptionsMenu(menu);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case R.id.menu_notification:
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        Intent i = new Intent(getApplicationContext(), Notifications.class);
                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        startActivity(i);
                    }
                });
                break;
            case R.id.menu_home_location_marker:
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        Intent i = new Intent(getApplicationContext(), HomeLocation.class);
                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        startActivity(i);
                    }
                });
                break;
            case R.id.menu_logout:
                new AlertDialog.Builder(TrackOnMap.this)
                        .setTitle("Logout")
                        .setMessage("Are you sure you want to logout?")
                        .setPositiveButton("Yes", new DialogInterface.OnClickListener() {
                            @Override
                            public void onClick(DialogInterface d, int id) {
                                final String mobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                                final String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                int accId = 0;
                                try { if (accountIdStr != null) accId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                                final int accountId = accId;

                                if (mobileNo != null && !mobileNo.trim().isEmpty()) {
                                    new Thread(new Runnable() {
                                        @Override
                                        public void run() {
                                            try {
                                                webServices.logAuditActivity(mobileNo, accountId, "LOGOUT", "", "");
                                            } catch (Exception ignored) {}
                                        }
                                    }).start();
                                }

                                appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", null);
                                appConstants.putShrdPrefValWithKey(getApplicationContext(), "UserMenus", null);
                                appConstants.setJwtToken(getApplicationContext(), "");
                                WebServices.currentJwtToken = "";
                                Intent i = new Intent(TrackOnMap.this, LoginActivity.class);
                                i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                                startActivity(i);
                                finish();
                            }
                        })
                        .setNegativeButton("Cancel", null)
                        .show();
                break;
        }
        return super.onOptionsItemSelected(item);
    }

    private void showUpdateAlert(final int priority,final int stableVersion)
    {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(BuildConfig.VERSION_CODE<stableVersion)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(TrackOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setTitle("You are using old version");
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = TrackOnMap.this.getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")");
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
                else if(priority==1)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(TrackOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setTitle("New Version available");
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Cancel",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            dialog.cancel();
                                        }
                                    })
                            .setNegativeButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getApplicationContext().getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-");
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
                else if(priority==2)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(TrackOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setTitle("New Version available");
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getApplicationContext().getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-TagTrack("+new Exception().getStackTrace()[0].getLineNumber()+")-");
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
            }
        });
    }
}
