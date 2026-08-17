package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;

import androidx.appcompat.view.ContextThemeWrapper;

/**
 * Account-Driven Remote Force Update Guard.
 * Enforces minimum version requirements configured in table mobile_app_configurable per corporate account.
 */
public final class ForceUpdateChecker {

    private ForceUpdateChecker() {}

    /**
     * Checks if the currently running app version satisfies the Account's minimum required version.
     * If deprecated and ForceUpdateEnabled == 1, prompts a non-dismissible Play Store update dialog.
     *
     * @param activity Current calling Activity
     * @param config The AccountConfig object for the user's AccountId
     * @return true if update is required and dialog is shown; false if version is acceptable
     */
    public static boolean checkAndPrompt(final Activity activity, final AccountConfig config) {
        if (activity == null || config == null || !config.forceUpdateEnabled) {
            return false;
        }

        String currentVersion = BuildConfig.VERSION_NAME; // e.g. "1.0.0"
        if (config.isVersionDeprecated(currentVersion)) {
            showUpdateAlert(activity, config.minRequiredVersion);
            return true;
        }
        return false;
    }

    /**
     * Asynchronously fetches AccountConfig from the backend and checks version requirements.
     * Used on Dashboard (MainActivity) startup for already logged-in users.
     */
    public static void checkAndPromptForAccount(final Activity activity, final int accountId) {
        if (activity == null || accountId <= 0) return;

        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    WebServices webServices = new WebServices();
                    String configJson = webServices.GetAccountConfig(accountId);
                    final AccountConfig config = AccountConfig.fromJson(configJson);
                    if (config != null && config.forceUpdateEnabled) {
                        String currentVersion = BuildConfig.VERSION_NAME;
                        if (config.isVersionDeprecated(currentVersion)) {
                            activity.runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    showUpdateAlert(activity, config.minRequiredVersion);
                                }
                            });
                        }
                    }
                } catch (Exception ignored) {
                }
            }
        }, "AccountForceUpdateCheck").start();
    }

    private static void showUpdateAlert(final Activity activity, final String minRequiredVersion) {
        if (activity == null || activity.isFinishing()) return;

        new AlertDialog.Builder(new ContextThemeWrapper(activity, android.R.style.Theme_Holo_Light_Dialog))
                .setTitle("App Update Required")
                .setIcon(R.drawable.error)
                .setMessage("Please update the app to version " + minRequiredVersion + " or higher to continue.")
                .setCancelable(false)
                .setPositiveButton("Update", new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int id) {
                        openStoreAndExit(activity);
                    }
                })
                .show();
    }

    private static void openStoreAndExit(Activity activity) {
        if (activity == null) return;
        final String appPackageName = activity.getPackageName();
        try {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName));
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(intent);
        } catch (Exception ignored) {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName));
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(intent);
        }
        activity.finishAffinity();
        System.exit(0);
    }
}
