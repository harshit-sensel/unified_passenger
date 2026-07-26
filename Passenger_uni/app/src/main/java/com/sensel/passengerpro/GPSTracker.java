package com.sensel.passengerpro;

import android.Manifest;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Build;
import android.os.Bundle;
import android.os.IBinder;
import android.os.Looper;
import android.util.Log;

import androidx.core.content.ContextCompat;

import com.google.android.gms.location.FusedLocationProviderClient;
import com.google.android.gms.location.LocationServices;
import com.google.android.gms.location.Priority;
import com.google.android.gms.tasks.CancellationTokenSource;
import com.google.android.gms.tasks.Tasks;

import java.util.concurrent.TimeUnit;



/**
 * Created by Mallikarjun on 22-10-2015.
 */

public class GPSTracker extends Service implements LocationListener {

    // Get Class Name
    private static String TAG = GPSTracker.class.getName();

    private final Context mContext;
    private FusedLocationProviderClient fusedClient;

    // flag for GPS Status
    boolean isGPSEnabled = false;

    // flag for network status
    boolean isNetworkEnabled = false;

    // flag for GPS Tracking is enabled
    boolean isGPSTrackingEnabled = false;

    Location location;
    double latitude;
    double longitude;

    // How many Geocoder should return our GPSTracker
    int geocoderMaxResults = 1;

    // The minimum distance to change updates in meters
    private static final long MIN_DISTANCE_CHANGE_FOR_UPDATES = 5; // 10 meters

    // The minimum time between updates in milliseconds
    private static final long MIN_TIME_BW_UPDATES = 1000 * 10 * 1; // 20 sec

    // Declaring a Location Manager
    protected LocationManager locationManager;

    // Store LocationManager.GPS_PROVIDER or LocationManager.NETWORK_PROVIDER information
    private String provider_info;

    private final Object waitLock = new Object();

    public GPSTracker(Context context) {
        this.mContext = context;
        this.fusedClient = LocationServices.getFusedLocationProviderClient(context);
        getLocation();
    }

    /**
     * Try to get my current location by GPS or Network Provider
     */
    @android.annotation.SuppressLint("MissingPermission")
    public String getLocation() {
        // Prefer Google Fused last-known location for faster response on Android 13+ and below.
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M
                || ContextCompat.checkSelfPermission(mContext, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED) {
            try {
                Location fusedLast = Tasks.await(fusedClient.getLastLocation(), 1200, TimeUnit.MILLISECONDS);
                if (fusedLast != null) {
                    latitude = fusedLast.getLatitude();
                    longitude = fusedLast.getLongitude();
                    return latitude + "," + longitude;
                }
            } catch (Exception ignored) {
                // Fall back to legacy providers below.
            }
        }
        return getLocationLegacy();
    }

    private String getLocationLegacy() {
        try {
            locationManager = (LocationManager) mContext
                    .getSystemService(LOCATION_SERVICE);

            isGPSEnabled = locationManager != null
                    && locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER);
            isNetworkEnabled = locationManager != null
                    && locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER);

            if (!isGPSEnabled && !isNetworkEnabled) {
                // no provider enabled
            } else {
                if (isNetworkEnabled) {
                    locationManager.requestLocationUpdates(
                            LocationManager.NETWORK_PROVIDER,
                            MIN_TIME_BW_UPDATES,
                            MIN_DISTANCE_CHANGE_FOR_UPDATES, this);
                    Log.d("Network", "Network Enabled");
                    if (locationManager != null) {
                        location = locationManager.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);
                        if (location != null) {
                            latitude = location.getLatitude();
                            longitude = location.getLongitude();
                        }
                    }
                }
                if (isGPSEnabled && location == null) {
                    locationManager.requestLocationUpdates(
                            LocationManager.GPS_PROVIDER,
                            MIN_TIME_BW_UPDATES,
                            MIN_DISTANCE_CHANGE_FOR_UPDATES, this);
                    Log.d("GPS", "GPS Enabled");
                    if (locationManager != null) {
                        location = locationManager.getLastKnownLocation(LocationManager.GPS_PROVIDER);
                        if (location != null) {
                            latitude = location.getLatitude();
                            longitude = location.getLongitude();
                        }
                    }
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
        return latitude + "," + longitude;
    }

    /**
     * Get location with retries: try getLastKnownLocation a few times with delay,
     * then request updates and wait for fix (same as DriverApp-NonTotal). Call from background thread.
     */
    @android.annotation.SuppressLint("MissingPermission")
    public String getLocationWithWait(long timeoutMs) {
        getLocation();
        if (latitude != 0 || longitude != 0) return latitude + "," + longitude;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M
                && ContextCompat.checkSelfPermission(mContext, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            return "0.0,0.0";
        }

        // Try Fused current location first with timeout. This is more reliable than raw GPS manager.
        try {
            CancellationTokenSource cts = new CancellationTokenSource();
            final CancellationTokenSource finalCts = cts;
            new Thread(() -> {
                try { Thread.sleep(timeoutMs); } catch (InterruptedException ignored) { }
                try { finalCts.cancel(); } catch (Exception ignored) { }
            }).start();
            Location current = Tasks.await(
                    fusedClient.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, cts.getToken()),
                    timeoutMs + 300,
                    TimeUnit.MILLISECONDS
            );
            if (current != null) {
                latitude = current.getLatitude();
                longitude = current.getLongitude();
                return latitude + "," + longitude;
            }
        } catch (Exception ignored) {
            // Fall back to legacy wait flow below.
        }

        if (locationManager == null || (!isGPSEnabled && !isNetworkEnabled)) {
            getLocationLegacy();
        }
        if (locationManager == null || (!isGPSEnabled && !isNetworkEnabled)) return "0.0,0.0";

        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                Looper looper = Looper.getMainLooper();
                if (isNetworkEnabled) {
                    locationManager.requestLocationUpdates(LocationManager.NETWORK_PROVIDER, 0, 0, this, looper);
                }
                if (isGPSEnabled) {
                    locationManager.requestLocationUpdates(LocationManager.GPS_PROVIDER, 0, 0, this, looper);
                }
            } else {
                if (isNetworkEnabled) {
                    locationManager.requestLocationUpdates(LocationManager.NETWORK_PROVIDER, 0, 0, this);
                }
                if (isGPSEnabled) {
                    locationManager.requestLocationUpdates(LocationManager.GPS_PROVIDER, 0, 0, this);
                }
            }
            long start = System.currentTimeMillis();
            while (System.currentTimeMillis() - start < timeoutMs) {
                synchronized (waitLock) {
                    if (latitude != 0 || longitude != 0) break;
                    try { waitLock.wait(400); } catch (InterruptedException e) { break; }
                }
            }
            try { locationManager.removeUpdates(this); } catch (Exception ignored) {}
        } catch (Exception e) {
            Log.e(TAG, "getLocationWithWait", e);
        }
        return latitude + "," + longitude;
    }

    @Override
    public void onLocationChanged(Location location) {
        if (location != null) {
            latitude = location.getLatitude();
            longitude = location.getLongitude();
            synchronized (waitLock) { waitLock.notifyAll(); }
        }
    }

    @Override
    public void onStatusChanged(String provider, int status, Bundle extras) {
    }

    @Override
    public void onProviderEnabled(String provider) {
    }

    @Override
    public void onProviderDisabled(String provider) {
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}