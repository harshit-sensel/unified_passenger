package com.sensel.passengerpro;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

/**
 * Inactivity Monitor enforcing Auto-Logout when enabled by Account Config.
 * Supports foreground idle detection, background/screen-off timeout evaluation,
 * and comprehensive session teardown with audit logging.
 */
public class InactivityHandler {
    private static final String TAG = "InactivityHandler";
    private final Activity activity;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final long timeoutMs;
    private final int timeoutMinutes;
    private final Runnable logoutRunnable;
    private final AppConstants appConstants = new AppConstants();
    private boolean isRunning = false;

    public InactivityHandler(final Activity activity, int timeoutMinutes) {
        this.activity = activity;
        this.timeoutMinutes = timeoutMinutes;
        this.timeoutMs = (long) timeoutMinutes * 60 * 1000;
        this.logoutRunnable = new Runnable() {
            @Override
            public void run() {
                executeLogout(activity);
            }
        };
    }

    /**
     * Immediately terminates session, logs AUTO_LOGOUT audit event, clears preferences, and redirects to LoginActivity.
     */
    public static void executeLogout(final Activity activity) {
        if (activity == null || activity.isFinishing()) return;
        Log.w(TAG, "Inactivity timeout reached. Executing Auto-Logout...");
        
        final Context appContext = activity.getApplicationContext();

        // Log AUTO_LOGOUT audit event
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    AppConstants appConstants = new AppConstants();
                    WebServices webServices = new WebServices();
                    String mobileNo = appConstants.getShrdPrefValByKeyWithTag(appContext, "passengerinfo", "MobileNo");
                    String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(appContext, "passengerinfo", "AccountId");
                    int accountId = 0;
                    try { accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                    
                    webServices.logAuditActivity(mobileNo, accountId, "AUTO_LOGOUT", "", "");
                } catch (Exception e) {
                    Log.e(TAG, "Error logging AUTO_LOGOUT", e);
                }
            }
        }).start();

        // Clear session preferences, interaction timestamp & JWT Token
        AppConstants appConstants = new AppConstants();
        appConstants.putShrdPrefValWithKey(appContext, "passengerinfo", "");
        appConstants.putShrdPrefValWithKey(appContext, "UserMenus", "");
        appConstants.setJwtToken(appContext, "");
        appConstants.setLastInteractionTime(appContext, 0);
        WebServices.currentJwtToken = "";

        // Redirect to LoginActivity
        Intent intent = new Intent(activity, LoginActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        activity.startActivity(intent);
        activity.finish();
    }

    /**
     * Starts inactivity monitoring.
     * Evaluates whether time spent in background or screen-off exceeded timeout.
     */
    public void start() {
        if (!isRunning) {
            isRunning = true;
        }

        if (activity == null || activity.isFinishing()) return;

        Context appContext = activity.getApplicationContext();
        long lastTime = appConstants.getLastInteractionTime(appContext);
        long now = System.currentTimeMillis();

        if (lastTime > 0) {
            long elapsed = now - lastTime;
            if (elapsed >= timeoutMs) {
                Log.w(TAG, "Elapsed time in background/inactivity (" + (elapsed / 1000) + "s) exceeded timeout (" + (timeoutMs / 1000) + "s). Triggering immediate logout.");
                executeLogout(activity);
                return;
            } else {
                long remaining = timeoutMs - elapsed;
                handler.removeCallbacks(logoutRunnable);
                handler.postDelayed(logoutRunnable, Math.max(1000, remaining));
                return;
            }
        }

        // If no prior timestamp recorded, record now and set full timer
        appConstants.setLastInteractionTime(appContext, now);
        resetInactivityTimer();
    }

    /**
     * Records new user interaction timestamp and resets the countdown timer.
     */
    public void recordUserInteraction() {
        if (activity != null && !activity.isFinishing()) {
            appConstants.setLastInteractionTime(activity.getApplicationContext(), System.currentTimeMillis());
        }
        resetInactivityTimer();
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
