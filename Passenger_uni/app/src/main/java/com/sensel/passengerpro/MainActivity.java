package com.sensel.passengerpro;

import android.Manifest;
import android.content.Context;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.telephony.TelephonyManager;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.AdapterView;
import android.widget.EditText;
import android.widget.ScrollView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import java.util.ArrayList;
import java.util.List;

import com.android.volley.AuthFailureError;
import com.android.volley.Request;
import com.android.volley.RequestQueue;
import com.android.volley.Response;
import com.android.volley.VolleyError;
import com.android.volley.toolbox.JsonObjectRequest;
import com.android.volley.toolbox.Volley;

import org.json.JSONObject;
import androidx.appcompat.view.ContextThemeWrapper;
import java.util.HashMap;
import java.util.Map;

/**
 * Post-login menu: 3-column grid with bordered cards, icon + label.
 * Items: Tag In with QR, Tag In with OTP, Track Your Vehicle, Panic, Tagout, Logout.
 */
public class MainActivity extends BaseActivity {
    private static final int MY_PERMISSIONS_REQUEST_LOCATION_OTP = 1403;
    /** Ask for location once on main menu so Tag In (QR), Tagout, panic, and activity logs can use GPS — not only when opening Tag In. */
    private static final int MY_PERMISSIONS_REQUEST_LOCATION_MAIN_MENU = 1404;

    private static final String[] MENU_LABELS = {
            "Tag In with QR",
            "Tag In with OTP",
            "Track Your Vehicle",
            "Panic",
            "Tagout",
            "Logout"
    };

    private static final int[] MENU_ICONS = {
            R.drawable.ic_tag_in_qr,
            R.drawable.ic_tag_in_otp,
            R.drawable.ic_track_vehicle_map,
            R.drawable.ic_panic_modern,
            R.drawable.ic_tagout,
            R.drawable.ic_logout_modern
    };

    public static Context context;
    private AppConstants appConstants = new AppConstants();
    private WebServices webServices = new WebServices();
    private ProgressDialog dialog;
    private String[] currentMenuLabels;
    private int[] currentMenuIcons;
    private String pendingOtpMobileNo;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    /** Main menu log once per activity instance (scheduled after location permission / short GPS warm-up). */
    private boolean mainMenuActivityLogPosted;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getSupportActionBar() != null) {
            getSupportActionBar().hide();
        }
        setContentView(R.layout.activity_main);
        context = getApplicationContext();
        String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(context, "passengerinfo", "AccountId");
        int accountId = 0;
        try { if (accountIdStr != null) accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
        if (accountId > 0) {
            ForceUpdateChecker.checkAndPromptForAccount(this, accountId);
        }

        applyMainMenuWindowInsetsApi35();

        buildMenuByPanicFlag();
        ExpandableHeightGridView grid = findViewById(R.id.passenger_menu_grid);
        grid.setExpanded(true);
        grid.setAdapter(new PassengerMenuGridAdapter(this, currentMenuLabels, currentMenuIcons, null));

        updateStatusBadge();

        requestLocationPermissionOnMainMenuIfNeeded();
        // Log after permission is known: without ACCESS_FINE_LOCATION, [PassengerActivityLogger] skips GPS and sends 0,0.
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED) {
            scheduleMainMenuActivityLog();
        }

        grid.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                String label = currentMenuLabels[position];
                // Logout: only log on confirm via [PassengerActivityLogger.logLogout] ("Logout") — avoid duplicate "Menu:Logout".
                if (!"Logout".equals(label)) {
                    PassengerActivityLogger.log(MainActivity.this, "Menu:" + label);
                }
                if ("Tag In with QR".equals(label)) {
                    if (isNetworkAvailable()) {
                        Intent i = new Intent(MainActivity.this, QRScannerActivity.class);
                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        startActivity(i);
                    } else {
                        Toast.makeText(MainActivity.this, "No internet", Toast.LENGTH_SHORT).show();
                    }
                } else if ("Tag In with OTP".equals(label)) {
                    if (!isNetworkAvailable()) {
                        Toast.makeText(MainActivity.this, "No internet. Check your connection.", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    String mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                    if (mobileno == null || mobileno.trim().isEmpty()) {
                        Toast.makeText(MainActivity.this, "Please login first.", Toast.LENGTH_LONG).show();
                        return;
                    }
                    pendingOtpMobileNo = mobileno.trim();
                    if (ContextCompat.checkSelfPermission(MainActivity.this, Manifest.permission.ACCESS_FINE_LOCATION)
                            != PackageManager.PERMISSION_GRANTED) {
                        ActivityCompat.requestPermissions(MainActivity.this,
                                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                MY_PERMISSIONS_REQUEST_LOCATION_OTP);
                        return;
                    }
                    sendOtpRequestAndTagIn(pendingOtpMobileNo);
                } else if ("Pre-Trip Checklist".equals(label)) {
                    Intent i = new Intent(MainActivity.this, VehicleInfo.class);
                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(i);
                } else if ("Home Location".equals(label)) {
                    Intent i = new Intent(MainActivity.this, HomeLocation.class);
                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(i);
                } else if ("Notifications".equals(label)) {
                    Intent i = new Intent(MainActivity.this, Notifications.class);
                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(i);
                } else if ("Track Your Vehicle".equals(label)) {
                    if (!isNetworkAvailable()) {
                        Toast.makeText(MainActivity.this, "No internet. Check your connection.", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    dialog = ProgressDialog.show(MainActivity.this, "", "Loading...", true);
                    new Thread(new Runnable() {
                        @Override
                        public void run() {
                            try {
                                String mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                                if (mobileno == null || mobileno.isEmpty()) {
                                    runOnUiThread(() -> { if (dialog != null && dialog.isShowing()) dialog.dismiss(); Toast.makeText(MainActivity.this, "Please login first.", Toast.LENGTH_LONG).show(); });
                                    return;
                                }
                                final String tagResult = webServices.GetPsngrInfoWithValidation(mobileno, "Tag");
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        if (dialog != null && dialog.isShowing()) dialog.dismiss();
                                        if (tagResult != null && (tagResult.contains("TagOut") || tagResult.contains("TagIn")) && tagResult.trim().startsWith("[")) {
                                            String userMenus = appConstants.getShrdPrefValByKey(getApplicationContext(), "UserMenus");
                                            Intent i;
                                            if (userMenus != null && userMenus.contains("assigned_veh_tracking")) {
                                                i = new Intent(MainActivity.this, TagTrack.class);
                                            } else {
                                                i = new Intent(MainActivity.this, TrackOnMap.class);
                                            }
                                            i.putExtra("tagDetails", tagResult);
                                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                            startActivity(i);
                                        } else {
                                            Toast.makeText(MainActivity.this, "No vehicle assigned to track. Please tag in first.", Toast.LENGTH_LONG).show();
                                        }
                                    }
                                });
                            } catch (Exception e) {
                                runOnUiThread(() -> { if (dialog != null && dialog.isShowing()) dialog.dismiss(); Toast.makeText(MainActivity.this, "Error loading track. Try again.", Toast.LENGTH_SHORT).show(); });
                            }
                        }
                    }).start();
                } else if ("Panic".equals(label)) {
                    if (!isNetworkAvailable()) {
                        Toast.makeText(MainActivity.this, "No internet. Check your connection.", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    String taggedVehicle = appConstants.getShrdPrefValByKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID);
                    if (taggedVehicle == null || taggedVehicle.trim().isEmpty() || "0".equals(taggedVehicle.trim())) {
                        new AlertDialog.Builder(MainActivity.this)
                                .setIcon(R.drawable.panic)
                                .setTitle("Tag In Required")
                                .setMessage("You are not tagged in to any vehicle. Please Tag In first to trigger a Panic Alert.")
                                .setPositiveButton("OK", new DialogInterface.OnClickListener() {
                                    @Override
                                    public void onClick(DialogInterface d, int id) {
                                        d.dismiss();
                                    }
                                })
                                .show();
                        return;
                    }
                    new AlertDialog.Builder(new ContextThemeWrapper(MainActivity.this, android.R.style.Theme_Holo_Light_Dialog))
                            .setIcon(R.drawable.panic)
                            .setTitle("Panic")
                            .setMessage("Are you in an emergency? Send panic alert?")
                            .setCancelable(true)
                            .setPositiveButton("Send", new DialogInterface.OnClickListener() {
                                @Override
                                public void onClick(DialogInterface d, int id) {
                                    sendPanicAlert();
                                }
                            })
                            .setNegativeButton("Cancel", new DialogInterface.OnClickListener() {
                                @Override
                                public void onClick(DialogInterface d, int id) {
                                    d.dismiss();
                                }
                            })
                            .show();
                } else if ("Tagout".equals(label)) {
                    if (!isNetworkAvailable()) {
                        Toast.makeText(MainActivity.this, "No internet. Check your connection.", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    new AlertDialog.Builder(MainActivity.this)
                            .setTitle("Tagout")
                            .setMessage("Are you sure you want to Tag Out?")
                            .setPositiveButton("Yes", new DialogInterface.OnClickListener() {
                                @Override
                                public void onClick(DialogInterface dialogInterface, int which) {
                                    dialog = ProgressDialog.show(MainActivity.this, "", "Loading...", true);
                                    new Thread(new Runnable() {
                                        @Override
                                        public void run() {
                                            try {
                                                String mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                                                String result = webServices.GetPsngrInfoWithValidation(mobileno, "Tag");
                                                if (result == null || !result.contains("TagOut")) {
                                                    runOnUiThread(new Runnable() {
                                                        @Override
                                                        public void run() {
                                                            dialog.dismiss();
                                                            Toast.makeText(MainActivity.this, "You are not tagged in. Please Tag In first.", Toast.LENGTH_LONG).show();
                                                        }
                                                    });
                                                    return;
                                                }
                                                String psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                                                if (psngrId == null || psngrId.isEmpty()) {
                                                    runOnUiThread(() -> { dialog.dismiss(); Toast.makeText(MainActivity.this, "Please login first.", Toast.LENGTH_LONG).show(); });
                                                    return;
                                                }
                                                if (!isLocationAvailable()) {
                                                    runOnUiThread(() -> { dialog.dismiss(); Toast.makeText(MainActivity.this, "Enable GPS for Tagout.", Toast.LENGTH_SHORT).show(); });
                                                    return;
                                                }
                                                String latlngStr = "0.0,0.0";
                                                try {
                                                    GPSTracker gpsTracker = new GPSTracker(getApplicationContext());
                                                    latlngStr = gpsTracker.getLocation();
                                                    if (!isLocationFixed(latlngStr)) {
                                                        latlngStr = gpsTracker.getLocationWithWait(1500);
                                                    }
                                                    if (latlngStr == null || latlngStr.isEmpty()) latlngStr = "0.0,0.0";
                                                } catch (Exception ignored) { }
                                                if (!isLocationFixed(latlngStr)) {
                                                    latlngStr = getCachedLatLng();
                                                }
                                                if (!isLocationFixed(latlngStr)) {
                                                    runOnUiThread(() -> { dialog.dismiss(); Toast.makeText(MainActivity.this, "GPS not fixed. Please try again.", Toast.LENGTH_SHORT).show(); });
                                                    return;
                                                }
                                                saveLatLngIfValid(latlngStr);
                                                String[] latlng = latlngStr.split(",");
                                                String lat = latlng.length > 0 ? latlng[0].trim() : "0";
                                                String lng = latlng.length > 1 ? latlng[1].trim() : "0";
                                                String imei = getDeviceIdSafe();
                                                if (imei == null) imei = "";
                                                final String insertResult = webServices.InsertPsngrChecklist(psngrId, "", "TagOut", "", "", "", "", imei, lat, lng, "", "", "", "", "", "", "", "", "", "");
                                                runOnUiThread(new Runnable() {
                                                    @Override
                                                    public void run() {
                                                        dialog.dismiss();
                                                        if (insertResult != null && insertResult.contains("Inserted Successfully")) {
                                                            appConstants.putShrdPrefValWithKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID, "");
                                                            Toast.makeText(MainActivity.this, "Tagout done successfully.", Toast.LENGTH_LONG).show();
                                                        } else {
                                                            String msg = insertResult != null && insertResult.contains("PsngrMessage-") ? insertResult.replace("PsngrMessage-", "").trim() : "Tagout failed. Try again.";
                                                            Toast.makeText(MainActivity.this, msg, Toast.LENGTH_LONG).show();
                                                        }
                                                    }
                                                });
                                            } catch (Exception e) {
                                                runOnUiThread(new Runnable() {
                                                    @Override
                                                    public void run() {
                                                        if (dialog != null && dialog.isShowing()) dialog.dismiss();
                                                        Toast.makeText(MainActivity.this, "Error. Please try again.", Toast.LENGTH_SHORT).show();
                                                    }
                                                });
                                            }
                                        }
                                    }).start();
                                }
                            })
                            .setNegativeButton("Cancel", null)
                            .show();
                } else if ("Logout".equals(label)) {
                    new AlertDialog.Builder(MainActivity.this)
                            .setTitle("Logout")
                            .setMessage("Are you sure you want to logout?")
                            .setPositiveButton("Yes", new DialogInterface.OnClickListener() {
                                @Override
                                public void onClick(DialogInterface d, int id) {
                                    // Extract mobileNo and accountId BEFORE clearing preferences
                                    final String mobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                                    final String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                    int accId = 0;
                                    try { if (accountIdStr != null) accId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                                    final int accountId = accId;

                                    // Log LOGOUT activity audit event
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
                                     appConstants.setLastInteractionTime(getApplicationContext(), 0);
                                     WebServices.currentJwtToken = "";
                                    Intent i = new Intent(MainActivity.this, LoginActivity.class);
                                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                                    startActivity(i);
                                    finish();
                                }
                            })
                            .setNegativeButton("No", null)
                            .show();
                }
            }
        });
    }

    /**
     * Prompt for fine location on the home menu so QR Tag In, Tagout, and background activity logging
     * can obtain coordinates without the user having opened Tag In first.
     */
    private void requestLocationPermissionOnMainMenuIfNeeded() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED) {
            return;
        }
        ActivityCompat.requestPermissions(this,
                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                MY_PERMISSIONS_REQUEST_LOCATION_MAIN_MENU);
    }

    /**
     * Defer "MainMenu" log slightly so fused / GPS can return a fix (first launch has no cached latlng).
     */
    private void scheduleMainMenuActivityLog() {
        if (mainMenuActivityLogPosted) return;
        mainMenuActivityLogPosted = true;
        mainHandler.postDelayed(new Runnable() {
            @Override
            public void run() {
                PassengerActivityLogger.log(MainActivity.this, "MainMenu");
            }
        }, 900);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == MY_PERMISSIONS_REQUEST_LOCATION_MAIN_MENU) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                scheduleMainMenuActivityLog();
            } else {
                // User denied: still record menu open; coordinates may stay 0 until a later flow caches a fix.
                if (!mainMenuActivityLogPosted) {
                    mainMenuActivityLogPosted = true;
                    PassengerActivityLogger.log(this, "MainMenu");
                }
            }
            return;
        }
        if (requestCode == MY_PERMISSIONS_REQUEST_LOCATION_OTP) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                if (pendingOtpMobileNo != null && !pendingOtpMobileNo.trim().isEmpty()) {
                    sendOtpRequestAndTagIn(pendingOtpMobileNo.trim());
                } else {
                    Toast.makeText(MainActivity.this, "Please try Tag In with OTP again.", Toast.LENGTH_SHORT).show();
                }
            } else {
                Toast.makeText(MainActivity.this, "Location permission is mandatory for Tag In with OTP.", Toast.LENGTH_LONG).show();
            }
        }
    }

    /**
     * Android 15+ (API 35): content can draw under system bars; push the menu grid down
     * using status/navigation bar insets so the grid is not clipped at the top.
     */
    private void applyMainMenuWindowInsetsApi35() {
        if (Build.VERSION.SDK_INT < 35) return;
        final ScrollView scroll = findViewById(R.id.main_menu_scroll);
        if (scroll == null) return;
        final float density = getResources().getDisplayMetrics().density;
        final int padL = Math.round(5 * density);
        final int padT = Math.round(8 * density);
        final int padR = Math.round(5 * density);
        final int padB = Math.round(5 * density);
        ViewCompat.setOnApplyWindowInsetsListener(scroll, (v, windowInsets) -> {
            // Use int insets (works with older androidx.core) instead of androidx.core.graphics.Insets.
            int barLeft = windowInsets.getSystemWindowInsetLeft();
            int barTop = windowInsets.getSystemWindowInsetTop();
            int barRight = windowInsets.getSystemWindowInsetRight();
            int barBottom = windowInsets.getSystemWindowInsetBottom();
            v.setPadding(
                    padL + barLeft,
                    padT + barTop,
                    padR + barRight,
                    padB + barBottom
            );
            return windowInsets;
        });
        ViewCompat.requestApplyInsets(scroll);
    }

    private void buildMenuByPanicFlag() {
        String jsonMenus = appConstants.getShrdPrefValByKey(getApplicationContext(), "UserMenus");
        
        List<String> labels = new ArrayList<>();
        List<Integer> icons = new ArrayList<>();

        if (jsonMenus != null && !jsonMenus.isEmpty() && !jsonMenus.contains("No Data")) {
            try {
                org.json.JSONArray arr = new org.json.JSONArray(jsonMenus);
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject obj = arr.getJSONObject(i);
                    String key = obj.optString("menukey", "");
                    switch (key) {
                        case "qr_scanner":
                            if (!labels.contains("Tag In with QR")) {
                                labels.add("Tag In with QR");
                                icons.add(R.drawable.ic_tag_in_qr);
                            }
                            break;
                        case "tag_in_otp":
                            if (!labels.contains("Tag In with OTP")) {
                                labels.add("Tag In with OTP");
                                icons.add(R.drawable.ic_tag_in_otp);
                            }
                            break;
                        case "live_tracking":
                        case "assigned_veh_tracking":
                        case "school_bus_tracking":
                            if (!labels.contains("Track Your Vehicle")) {
                                labels.add("Track Your Vehicle");
                                icons.add(R.drawable.ic_track_vehicle_map);
                            }
                            break;
                        case "panic_sos":
                            String panicFlag = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PanicFlag");
                            if ("1".equals(panicFlag)) {
                                if (!labels.contains("Panic")) {
                                    labels.add("Panic");
                                    icons.add(R.drawable.panic);
                                }
                            }
                            break;
                        case "checklist":
                            if (!labels.contains("Pre-Trip Checklist")) {
                                labels.add("Pre-Trip Checklist");
                                icons.add(android.R.drawable.ic_menu_agenda);
                            }
                            break;
                        case "proximity_check":
                            if (!labels.contains("Proximity Check")) {
                                labels.add("Proximity Check");
                                icons.add(android.R.drawable.ic_menu_compass);
                            }
                            break;
                        case "vehicle_change":
                            if (!labels.contains("Change Vehicle")) {
                                labels.add("Change Vehicle");
                                icons.add(android.R.drawable.ic_menu_directions);
                            }
                            break;
                        case "home_location":
                            if (!labels.contains("Home Location")) {
                                labels.add("Home Location");
                                icons.add(android.R.drawable.ic_menu_mylocation);
                            }
                            break;
                        case "notifications":
                            if (!labels.contains("Notifications")) {
                                labels.add("Notifications");
                                icons.add(R.drawable.ic_notifications_modern);
                            }
                            break;
                        case "tag_out":
                            if (!labels.contains("Tagout")) {
                                labels.add("Tagout");
                                icons.add(R.drawable.ic_tagout);
                            }
                            break;
                    }
                }
            } catch (Exception e) {
                android.util.Log.e("MainActivity", "Error parsing UserMenus", e);
            }
        }

        // Fallback default menu if no dynamic menus parsed
        if (labels.isEmpty()) {
            String panicFlag = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PanicFlag");
            boolean panicEnabled = "1".equals(panicFlag);
            for (int i = 0; i < MENU_LABELS.length; i++) {
                if ("Logout".equals(MENU_LABELS[i])) continue;
                if (!panicEnabled && "Panic".equals(MENU_LABELS[i])) continue;
                labels.add(MENU_LABELS[i]);
                icons.add(MENU_ICONS[i]);
            }
        }

        // Always append Logout button
        if (!labels.contains("Logout")) {
            labels.add("Logout");
            icons.add(R.drawable.ic_logout_modern);
        }

        currentMenuLabels = labels.toArray(new String[0]);
        currentMenuIcons = new int[icons.size()];
        for (int i = 0; i < icons.size(); i++) {
            currentMenuIcons[i] = icons.get(i);
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateStatusBadge();
    }

    private void updateStatusBadge() {
        try {
            android.widget.TextView tvGreeting = findViewById(R.id.tv_passenger_greeting);
            android.widget.TextView tvStatusTitle = findViewById(R.id.tv_status_title);
            android.widget.TextView tvStatusVehicle = findViewById(R.id.tv_status_vehicle);

            String name = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrName");
            if (name != null && !name.trim().isEmpty() && tvGreeting != null) {
                tvGreeting.setText("Hello, " + name.trim() + "! 👋");
            }

            String taggedVeh = appConstants.getShrdPrefValByKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID);
            if (taggedVeh == null || taggedVeh.isEmpty()) {
                taggedVeh = resolveAssignedVehicleIdForOtpTagIn();
            }

            if (taggedVeh != null && !taggedVeh.isEmpty() && !"0".equals(taggedVeh)) {
                if (tvStatusTitle != null) tvStatusTitle.setText("Currently Active");
                if (tvStatusVehicle != null) tvStatusVehicle.setText("Vehicle ID: " + taggedVeh);
            } else {
                if (tvStatusTitle != null) tvStatusTitle.setText("Status: Not Tagged In");
                if (tvStatusVehicle != null) tvStatusVehicle.setText("Tag in via QR or OTP to track vehicle");
            }
        } catch (Exception ignored) { }
    }

    private void sendPanicAlert() {
        String taggedVeh = appConstants.getShrdPrefValByKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID);
        if (taggedVeh == null || taggedVeh.trim().isEmpty() || "0".equals(taggedVeh.trim())) {
            Toast.makeText(MainActivity.this, "You are not tagged in to any vehicle. Please Tag In first to trigger a Panic Alert.", Toast.LENGTH_LONG).show();
            return;
        }
        final ProgressDialog pd = ProgressDialog.show(this, "", "Sending...", true);
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    String psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                    if (psngrId == null || psngrId.isEmpty()) psngrId = "0";
                    String vehicleId = appConstants.getShrdPrefValByKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID);
                    if (vehicleId == null || vehicleId.isEmpty()) vehicleId = "0";
                    final String res = webServices.InsertPanicAlertFromApp(psngrId, vehicleId, "Passenger");
                    runOnUiThread(new Runnable() {
                        @Override
                        public void run() {
                            pd.dismiss();
                            if (res != null && res.toLowerCase().contains("success")) {
                                Toast.makeText(MainActivity.this, "Panic alert sent successfully.", Toast.LENGTH_SHORT).show();
                                // Log PANIC_ALERT activity audit event
                                new Thread(new Runnable() {
                                    @Override
                                    public void run() {
                                        try {
                                            String mobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                                            String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                            int accountId = 0;
                                            try { if (accountIdStr != null) accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                                            webServices.logAuditActivity(mobileNo, accountId, "PANIC_ALERT", "", "");
                                        } catch (Exception ignored) {}
                                    }
                                }).start();
                            } else {
                                Toast.makeText(MainActivity.this, "Failed to send panic alert. Try again.", Toast.LENGTH_SHORT).show();
                            }
                        }
                    });
                } catch (Exception e) {
                    runOnUiThread(new Runnable() {
                        @Override
                        public void run() {
                            pd.dismiss();
                            Toast.makeText(MainActivity.this, "Error sending panic alert.", Toast.LENGTH_SHORT).show();
                        }
                    });
                }
            }
        }).start();
    }

    private boolean isNetworkAvailable() {
        try {
            ConnectivityManager cm = (ConnectivityManager) getSystemService(CONNECTIVITY_SERVICE);
            NetworkInfo net = cm != null ? cm.getActiveNetworkInfo() : null;
            return net != null && net.isConnected();
        } catch (Exception e) {
            return false;
        }
    }

    private boolean isLocationAvailable() {
        try {
            LocationManager lm = (LocationManager) getSystemService(Context.LOCATION_SERVICE);
            if (lm == null) return false;
            return lm.isProviderEnabled(LocationManager.GPS_PROVIDER)
                    || lm.isProviderEnabled(LocationManager.NETWORK_PROVIDER);
        } catch (Exception e) { return false; }
    }

    private boolean isLocationFixed(String locStr) {
        if (locStr == null || locStr.isEmpty()) return false;
        String s = locStr.trim();
        if (s.startsWith("0,0") || s.startsWith("0.0,0.0")) return false;
        return true;
    }

    private String getCachedLatLng() {
        String cached = appConstants.getShrdPrefValByKey(getApplicationContext(), AppConstants.KEY_LAST_VALID_LATLNG);
        return cached == null ? "0.0,0.0" : cached;
    }

    private void saveLatLngIfValid(String latlng) {
        if (isLocationFixed(latlng)) {
            appConstants.putShrdPrefValWithKey(getApplicationContext(), AppConstants.KEY_LAST_VALID_LATLNG, latlng.trim());
        }
    }

    private String getDeviceIdSafe() {
        try {
            TelephonyManager tm = (TelephonyManager) getSystemService(Context.TELEPHONY_SERVICE);
            if (tm != null && Build.VERSION.SDK_INT <= Build.VERSION_CODES.P) {
                String id = tm.getDeviceId();
                if (id != null && !id.isEmpty()) return id;
            }
        } catch (Exception ignored) { }
        return Settings.Secure.getString(getContentResolver(), Settings.Secure.ANDROID_ID);
    }

    private String resolveAssignedVehicleIdForOtpTagIn() {
        String[] keys = {"AssignedVehicleId", "AssignedVehicleID", "AssignedVehicleid", "VehicleId", "VehicleID", "vehicleid"};
        for (String key : keys) {
            String val = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", key);
            if (val != null) {
                val = val.trim();
                if (!val.isEmpty() && !"null".equalsIgnoreCase(val)) return val;
            }
        }
        return "";
    }

    private void refreshPassengerInfoForOtpTagIn(String mobileno) {
        try {
            if (mobileno == null || mobileno.trim().isEmpty()) return;
            String latestInfo = webServices.GetPsngrInfoWithValidation(mobileno.trim(), "Validate");
            if (latestInfo != null && latestInfo.contains("PsngrId")) {
                appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", latestInfo);
            }
        } catch (Exception ignored) { }
    }

    private void sendOtpRequestAndTagIn(final String mobileno) {
        dialog = ProgressDialog.show(MainActivity.this, "", "Sending OTP...", true);
        try {
            JSONObject body = new JSONObject();
            body.put("mobileno", mobileno);
            body.put("type", "otp_request");
            RequestQueue queue = Volley.newRequestQueue(MainActivity.this);
            JsonObjectRequest req = new JsonObjectRequest(Request.Method.POST,
                    UrlConfig.PASSENGER_PRO_AUTHENTICATE_URL,
                    body,
                    new Response.Listener<JSONObject>() {
                        @Override
                        public void onResponse(JSONObject response) {
                            if (dialog != null && dialog.isShowing()) dialog.dismiss();
                            String result = response.optString("result", "");
                            String serverOtp = response.optString("otp", "1234");
                            if (result != null && (result.toLowerCase().contains("otp") || result.toLowerCase().contains("success") || result.toLowerCase().contains("send"))) {
                                promptUserForOtpAndTagIn(serverOtp);
                            } else {
                                Toast.makeText(MainActivity.this, result.isEmpty() ? "Could not send OTP." : result, Toast.LENGTH_LONG).show();
                            }
                        }
                    },
                    new Response.ErrorListener() {
                        @Override
                        public void onErrorResponse(VolleyError error) {
                            if (dialog != null && dialog.isShowing()) dialog.dismiss();
                            String msg = error.getMessage();
                            if (msg == null) msg = "Could not send OTP. Try again.";
                            Toast.makeText(MainActivity.this, msg, Toast.LENGTH_LONG).show();
                        }
                    }) {
                @Override
                public Map<String, String> getHeaders() throws AuthFailureError {
                    Map<String, String> headers = new HashMap<>();
                    headers.put("X-App-Key", UrlConfig.API_SECURITY_KEY);
                    return headers;
                }
            };
            queue.add(req);
        } catch (Exception e) {
            if (dialog != null && dialog.isShowing()) dialog.dismiss();
            Toast.makeText(MainActivity.this, "Error sending OTP.", Toast.LENGTH_SHORT).show();
        }
    }

    private void promptUserForOtpAndTagIn(final String serverOtp) {
        try {
            View dialogView = LayoutInflater.from(MainActivity.this).inflate(R.layout.dialog_tag_in_otp, null);
            final AlertDialog customDialog = new AlertDialog.Builder(MainActivity.this)
                    .setView(dialogView)
                    .setCancelable(true)
                    .create();

            if (customDialog.getWindow() != null) {
                customDialog.getWindow().setBackgroundDrawableResource(android.R.color.transparent);
            }

            final android.widget.TextView b1 = dialogView.findViewById(R.id.dialog_otp_1);
            final android.widget.TextView b2 = dialogView.findViewById(R.id.dialog_otp_2);
            final android.widget.TextView b3 = dialogView.findViewById(R.id.dialog_otp_3);
            final android.widget.TextView b4 = dialogView.findViewById(R.id.dialog_otp_4);
            View btnResend = dialogView.findViewById(R.id.dialog_btn_resend);
            View btnBack = dialogView.findViewById(R.id.dialog_btn_back);

            String otpStr = (serverOtp != null && serverOtp.length() >= 4) ? serverOtp : "1234";
            b1.setText(String.valueOf(otpStr.charAt(0)));
            b2.setText(String.valueOf(otpStr.charAt(1)));
            b3.setText(String.valueOf(otpStr.charAt(2)));
            b4.setText(String.valueOf(otpStr.charAt(3)));

            btnResend.setOnClickListener(v -> {
                customDialog.dismiss();
                if (pendingOtpMobileNo != null && !pendingOtpMobileNo.isEmpty()) {
                    sendOtpRequestAndTagIn(pendingOtpMobileNo);
                }
            });

            btnBack.setOnClickListener(v -> customDialog.dismiss());

            customDialog.show();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void performOtpTagIn() {
        final ProgressDialog tagInDialog = ProgressDialog.show(MainActivity.this, "", "Tag In in progress...", true);
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    String mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                    refreshPassengerInfoForOtpTagIn(mobileno);
                    String psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                    if (psngrId == null || psngrId.isEmpty()) {
                        runOnUiThread(() -> {
                            if (tagInDialog.isShowing()) tagInDialog.dismiss();
                            Toast.makeText(MainActivity.this, "Please login first.", Toast.LENGTH_LONG).show();
                        });
                        return;
                    }
                    String vehicleId = resolveAssignedVehicleIdForOtpTagIn();
                    if (vehicleId.isEmpty()) {
                        runOnUiThread(() -> {
                            if (tagInDialog.isShowing()) tagInDialog.dismiss();
                            Toast.makeText(MainActivity.this, "No assigned vehicle found for OTP Tag In.", Toast.LENGTH_LONG).show();
                        });
                        return;
                    }
                    if (!isLocationAvailable()) {
                        runOnUiThread(() -> {
                            if (tagInDialog.isShowing()) tagInDialog.dismiss();
                            Toast.makeText(MainActivity.this, "Enable GPS for Tag In.", Toast.LENGTH_SHORT).show();
                        });
                        return;
                    }
                    String latlngStr = "0.0,0.0";
                    try {
                        GPSTracker gpsTracker = new GPSTracker(getApplicationContext());
                        latlngStr = gpsTracker.getLocation();
                        if (!isLocationFixed(latlngStr)) {
                            latlngStr = gpsTracker.getLocationWithWait(1500);
                        }
                        if (latlngStr == null || latlngStr.isEmpty()) latlngStr = "0.0,0.0";
                    } catch (Exception ignored) { }
                    if (!isLocationFixed(latlngStr)) {
                        latlngStr = getCachedLatLng();
                    }
                    if (!isLocationFixed(latlngStr)) {
                        runOnUiThread(() -> {
                            if (tagInDialog.isShowing()) tagInDialog.dismiss();
                            Toast.makeText(MainActivity.this, "GPS not fixed. Please try again.", Toast.LENGTH_SHORT).show();
                        });
                        return;
                    }
                    saveLatLngIfValid(latlngStr);
                    String[] latlng = latlngStr.split(",");
                    String lat = latlng.length > 0 ? latlng[0].trim() : "0";
                    String lng = latlng.length > 1 ? latlng[1].trim() : "0";
                    String imei = getDeviceIdSafe();
                    if (imei == null) imei = "";
                    final String insertResult = webServices.InsertPsngrChecklist(psngrId, vehicleId, "TagIn", "", "", "", "", imei, lat, lng, "", "", "", "", "", "", "", "", "", "");
                    runOnUiThread(() -> {
                        if (tagInDialog.isShowing()) tagInDialog.dismiss();
                        if (insertResult != null && insertResult.contains("Inserted Successfully")) {
                            appConstants.putShrdPrefValWithKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID, vehicleId);
                            Toast.makeText(MainActivity.this, "Tag In done successfully.", Toast.LENGTH_LONG).show();
                        } else {
                            String msg = insertResult != null && insertResult.contains("PsngrMessage-")
                                    ? insertResult.replace("PsngrMessage-", "").trim()
                                    : "Tag In failed after OTP. Try again.";
                            Toast.makeText(MainActivity.this, msg, Toast.LENGTH_LONG).show();
                        }
                    });
                } catch (Exception e) {
                    runOnUiThread(() -> {
                        if (tagInDialog.isShowing()) tagInDialog.dismiss();
                        Toast.makeText(MainActivity.this, "Error during OTP Tag In. Try again.", Toast.LENGTH_SHORT).show();
                    });
                }
            }
        }).start();
    }
}
