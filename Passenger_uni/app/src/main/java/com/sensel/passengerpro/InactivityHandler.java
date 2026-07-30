package com.sensel.passengerpro;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/**
 * Inactivity Monitor enforcing Auto-Logout when enabled by Account Config.
 */
public class InactivityHandler {
    private static final String TAG = "InactivityHandler";
    private final Activity activity;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final long timeoutMs;
    private final Runnable logoutRunnable;
    private boolean isRunning = false;

    public InactivityHandler(final Activity activity, int timeoutMinutes) {
        this.activity = activity;
        this.timeoutMs = (long) timeoutMinutes * 60 * 1000;
        this.logoutRunnable = new Runnable() {
            @Override
            public void run() {
                if (activity == null || activity.isFinishing()) return;
                Log.w(TAG, "Inactivity timeout reached (" + timeoutMinutes + " mins). Executing Auto-Logout...");
                
                // Log AUTO_LOGOUT audit event
                new Thread(new Runnable() {
                    @Override
                    public void run() {
                        try {
                            AppConstants appConstants = new AppConstants();
                            WebServices webServices = new WebServices();
                            String mobileNo = appConstants.getShrdPrefValByKeyWithTag(activity.getApplicationContext(), "passengerinfo", "MobileNo");
                            String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(activity.getApplicationContext(), "passengerinfo", "AccountId");
                            int accountId = 0;
                            try { accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                            
                            webServices.logAuditActivity(mobileNo, accountId, "AUTO_LOGOUT", "", "");
                        } catch (Exception e) {
                            Log.e(TAG, "Error logging AUTO_LOGOUT", e);
                        }
                    }
                }).start();

                // Clear session preferences & JWT Token
                AppConstants appConstants = new AppConstants();
                appConstants.putShrdPrefValWithKey(activity.getApplicationContext(), "passengerinfo", "");
                appConstants.putShrdPrefValWithKey(activity.getApplicationContext(), "UserMenus", "");
                appConstants.setJwtToken(activity.getApplicationContext(), "");
                WebServices.currentJwtToken = "";

                // Redirect to LoginActivity
                Intent intent = new Intent(activity, LoginActivity.class);
                intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                activity.startActivity(intent);
                activity.finish();
            }
        };
    }

    public void start() {
        if (!isRunning) {
            isRunning = true;
            resetInactivityTimer();
        }
    }

    public void resetInactivityTimer() {
        if (!isRunning) return;
        handler.removeCallbacks(logoutRunnable);
        handler.postDelayed(logoutRunnable, timeoutMs);
    }

    public void stop() {
        isRunning = false;
        handler.removeCallbacks(logoutRunnable);
    }
}
