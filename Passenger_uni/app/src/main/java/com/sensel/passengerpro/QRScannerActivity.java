package com.sensel.passengerpro;

import android.Manifest;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.telephony.TelephonyManager;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.google.zxing.integration.android.IntentIntegrator;
import com.google.zxing.integration.android.IntentResult;

/**
 * Tag In with QR: scan QR, validate vehicle via GetVehicleidByQRCode, then InsertPsngrChecklist (TagIn).
 */
public class QRScannerActivity extends BaseActivity {

    private static final int MY_PERMISSIONS_REQUEST_CAMERA = 102;
    private static final int MY_PERMISSIONS_REQUEST_LOCATION = 103;

    private IntentIntegrator qrScan;
    private final AppConstants appConstants = new AppConstants();
    private final WebServices webServices = new WebServices();

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

    private boolean isNetworkAvailable() {
        ConnectivityManager cm = (ConnectivityManager) getSystemService(Context.CONNECTIVITY_SERVICE);
        NetworkInfo net = cm != null ? cm.getActiveNetworkInfo() : null;
        return net != null && net.isConnected();
    }

    private void startScanIfReady() {
        if (!isNetworkAvailable()) {
            Toast.makeText(this, "No internet connection", Toast.LENGTH_SHORT).show();
            return;
        }
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(this, new String[]{Manifest.permission.CAMERA}, MY_PERMISSIONS_REQUEST_CAMERA);
            return;
        }
        qrScan.setCaptureActivity(PortraitCaptureActivity.class);
        qrScan.setOrientationLocked(true);
        qrScan.setDesiredBarcodeFormats(IntentIntegrator.QR_CODE);
        qrScan.setPrompt("Align QR Code inside frame to scan");
        qrScan.initiateScan();
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_qr_scanner);
        // Activity log: MainActivity already logs "Menu:Tag In with QR" — avoid duplicate row.
        qrScan = new IntentIntegrator(this);
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(this, new String[]{Manifest.permission.ACCESS_FINE_LOCATION}, MY_PERMISSIONS_REQUEST_LOCATION);
        } else {
            startScanIfReady();
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == MY_PERMISSIONS_REQUEST_LOCATION || requestCode == MY_PERMISSIONS_REQUEST_CAMERA) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                startScanIfReady();
            } else {
                Toast.makeText(this, "Camera and location are needed for Tag In with QR", Toast.LENGTH_LONG).show();
                finish();
            }
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        IntentResult result = IntentIntegrator.parseActivityResult(requestCode, resultCode, data);
        if (result != null) {
            if (result.getContents() == null) {
                // User pressed back or cancelled without scanning -> go to main menu
                goBackToMainMenu();
            } else {
                handleQrResult(result.getContents());
            }
        } else {
            super.onActivityResult(requestCode, resultCode, data);
        }
    }

    /** Go back to main menu (when user presses back without scanning). */
    private void goBackToMainMenu() {
        Intent i = new Intent(this, MainActivity.class);
        i.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        startActivity(i);
        finish();
    }

    @Override
    public void onBackPressed() {
        goBackToMainMenu();
    }

    private void handleQrResult(final String qrcode) {
        final ProgressDialog progressDialog = ProgressDialog.show(this, "", "Validating...", true);
        progressDialog.setCancelable(false);
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    if (!isLocationAvailable()) {
                        runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "Enable GPS for Tag In.", Toast.LENGTH_SHORT).show(); startScanIfReady(); });
                        return;
                    }
                    String passengerinfo = appConstants.getShrdPrefValByKey(getApplicationContext(), "passengerinfo");
                    if (passengerinfo == null || !passengerinfo.contains("PsngrId")) {
                        runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "Please login first.", Toast.LENGTH_LONG).show(); finish(); });
                        return;
                    }
                    String psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                    if (psngrId == null || psngrId.isEmpty()) {
                        runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "Please login first.", Toast.LENGTH_LONG).show(); finish(); });
                        return;
                    }

                    // Start GPS lookup in parallel while QR is validated on server.
                    // Prefer last-known location first to avoid waiting several seconds.
                    final String[] latlngHolder = new String[]{"0.0,0.0"};
                    Thread gpsThread = new Thread(() -> {
                        try {
                            GPSTracker gpsTracker = new GPSTracker(getApplicationContext());
                            String loc = gpsTracker.getLocation();
                            if (loc != null && !loc.isEmpty()) latlngHolder[0] = loc;
                            if (!isLocationFixed(latlngHolder[0])) {
                                // Keep fallback short so Tag In remains responsive.
                                loc = gpsTracker.getLocationWithWait(1500);
                                if (loc != null && !loc.isEmpty()) latlngHolder[0] = loc;
                            }
                        } catch (Exception ignored) { }
                    });
                    gpsThread.start();

                    final String vehicleIdResult = webServices.GetVehicleidByQRCode(qrcode);
                    if (vehicleIdResult == null || vehicleIdResult.contains("Exception") || vehicleIdResult.equals("Server Error")) {
                        runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "Server not responding. Try later.", Toast.LENGTH_LONG).show(); startScanIfReady(); });
                        return;
                    }
                    if (vehicleIdResult.equals("Invalid QRCode") || vehicleIdResult.trim().equalsIgnoreCase("No vehicle")) {
                        runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "Invalid QR code. Scan a valid Sensel QR code.", Toast.LENGTH_LONG).show(); startScanIfReady(); });
                        return;
                    }
                    final String vehicleId = vehicleIdResult.trim();
                    if (vehicleId.isEmpty()) {
                        runOnUiThread(() -> { progressDialog.dismiss(); startScanIfReady(); });
                        return;
                    }
                    // Tag In: vehicle from QR + lat/lng only (no checklist / photo upload — see InsertPsngrChecklist below).
                    try { gpsThread.join(1800); } catch (InterruptedException ignored) { }
                    String latlngStr = latlngHolder[0];
                    if (!isLocationFixed(latlngStr)) {
                        latlngStr = getCachedLatLng();
                    }
                    if (!isLocationFixed(latlngStr)) {
                        runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "GPS not fixed yet. Move to open sky and retry.", Toast.LENGTH_SHORT).show(); startScanIfReady(); });
                        return;
                    }
                    saveLatLngIfValid(latlngStr);
                    String[] latlng = latlngStr.split(",");
                    String lat = latlng.length > 0 ? latlng[0].trim() : "0";
                    String lng = latlng.length > 1 ? latlng[1].trim() : "0";
                    String imei = getDeviceIdSafe();
                    if (imei == null) imei = "";
                    final String insertResult = webServices.InsertPsngrChecklist(psngrId, vehicleId, "TagIn", "", "", "", "", imei, lat, lng, "", "", "", "", "", "", "", "", "", "");
                    runOnUiThread(new Runnable() {
                        @Override
                        public void run() {
                            progressDialog.dismiss();
                            if (insertResult != null && insertResult.contains("Inserted Successfully")) {
                                appConstants.putShrdPrefValWithKey(getApplicationContext(), AppConstants.KEY_CURRENT_TAGGED_VEHICLE_ID, vehicleId);
                                Toast.makeText(QRScannerActivity.this, "Tag In done successfully.", Toast.LENGTH_LONG).show();
                                
                                // Log TAG_IN activity audit event
                                new Thread(new Runnable() {
                                    @Override
                                    public void run() {
                                        try {
                                            String mobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                                            String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                            int accountId = 0;
                                            try { if (accountIdStr != null) accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                                            webServices.logAuditActivity(mobileNo, accountId, "TAG_IN", lat, lng);
                                        } catch (Exception ignored) {}
                                    }
                                }).start();

                                Intent i = new Intent(QRScannerActivity.this, MainActivity.class);
                                i.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
                                startActivity(i);
                                finish();
                            } else {
                                String msg = insertResult != null && insertResult.contains("PsngrMessage-") ? insertResult.replace("PsngrMessage-", "").trim() : "Tag In failed. Tag out first and Try again.";
                                Toast.makeText(QRScannerActivity.this, msg, Toast.LENGTH_LONG).show();
                                startScanIfReady();
                            }
                        }
                    });
                } catch (Exception e) {
                    runOnUiThread(() -> { progressDialog.dismiss(); Toast.makeText(QRScannerActivity.this, "Error. Try again.", Toast.LENGTH_SHORT).show(); startScanIfReady(); });
                }
            }
        }).start();
    }
}
