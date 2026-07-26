package com.sensel.passengerpro;

import android.Manifest;
import android.content.Context;
import android.content.pm.PackageManager;
import android.location.Location;

import androidx.core.content.ContextCompat;

import com.google.android.gms.location.CurrentLocationRequest;
import com.google.android.gms.location.FusedLocationProviderClient;
import com.google.android.gms.location.LocationServices;
import com.google.android.gms.location.Priority;
import com.google.android.gms.tasks.CancellationTokenSource;

public class FastLocationProvider {

    public interface LocationCallback {
        void onLocationRetrieved(Location location);
    }

    public static void getCurrentLocation(Context context, LocationCallback callback) {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED &&
                ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            callback.onLocationRetrieved(null);
            return;
        }

        FusedLocationProviderClient fusedLocationClient = LocationServices.getFusedLocationProviderClient(context);

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        CurrentLocationRequest request = new CurrentLocationRequest.Builder()
                .setPriority(Priority.PRIORITY_HIGH_ACCURACY)
                .setDurationMillis(5000)
                .build();

        fusedLocationClient.getCurrentLocation(request, cancellationTokenSource.getToken())
                .addOnSuccessListener(location -> {
                    if (location != null) {
                        callback.onLocationRetrieved(location);
                    } else {
                        fusedLocationClient.getLastLocation().addOnSuccessListener(callback::onLocationRetrieved);
                    }
                })
                .addOnFailureListener(e -> {
                    fusedLocationClient.getLastLocation().addOnSuccessListener(callback::onLocationRetrieved);
                });
    }
}
