package com.sensel.passengerpro;

import android.os.Bundle;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

/**
 * BaseActivity for all authenticated screens.
 * Manages account-level inactivity auto-logout monitoring dynamically based on mobile_app_configurable.
 */
public abstract class BaseActivity extends AppCompatActivity {

    protected InactivityHandler inactivityHandler;
    private final AppConstants appConstants = new AppConstants();

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    @Override
    protected void onResume() {
        super.onResume();
        checkAndStartInactivityTimer();
    }

    @Override
    protected void onPause() {
        super.onPause();
        if (inactivityHandler != null) {
            inactivityHandler.stop();
        }
    }

    @Override
    public void onUserInteraction() {
        super.onUserInteraction();
        if (inactivityHandler != null) {
            inactivityHandler.recordUserInteraction();
        }
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (inactivityHandler != null) {
            inactivityHandler.stop();
            inactivityHandler = null;
        }
    }

    /**
     * Inspects cached AccountConfig. If AutoLogoutEnabled == true, initializes and starts InactivityHandler.
     */
    public void checkAndStartInactivityTimer() {
        try {
            AccountConfig config = appConstants.getAccountConfig(this);
            if (config != null && config.autoLogoutEnabled && config.autoLogoutTimeoutMinutes > 0) {
                if (inactivityHandler == null) {
                    inactivityHandler = new InactivityHandler(this, config.autoLogoutTimeoutMinutes);
                }
                inactivityHandler.start();
            } else {
                if (inactivityHandler != null) {
                    inactivityHandler.stop();
                    inactivityHandler = null;
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
    }
}
