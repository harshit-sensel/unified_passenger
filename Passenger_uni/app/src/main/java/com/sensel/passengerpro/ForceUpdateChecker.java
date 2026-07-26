package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;

import androidx.appcompat.view.ContextThemeWrapper;

import org.json.JSONArray;
import org.json.JSONObject;

/** Centralized app-version update check (same behavior as legacy Force Download flow). */
public final class ForceUpdateChecker {

    private ForceUpdateChecker() {}

    public static void checkAndPrompt(final Activity activity) {
        if (activity == null) return;
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    WebServices webServices = new WebServices();
                    String appData = webServices.GetAppVersion(activity.getPackageName());
                    if (appData == null || !appData.contains("VersionCode")) return;

                    JSONArray array = new JSONArray(appData);
                    if (array.length() == 0) return;
                    JSONObject data = new JSONObject(array.get(0).toString());

                    int latestVersion = parseIntSafe(data.optString("VersionCode", "0"));
                    int priority = parseIntSafe(data.optString("Priority", "0"));
                    int stableVersion = parseIntSafe(data.optString("StableVersion", "0"));
                    if (latestVersion > BuildConfig.VERSION_CODE) {
                        showUpdateAlert(activity, priority, stableVersion);
                    }
                } catch (Exception ignored) {
                }
            }
        }, "ForceUpdateCheck").start();
    }

    private static int parseIntSafe(String value) {
        try {
            return Integer.parseInt(value);
        } catch (Exception e) {
            return 0;
        }
    }

    private static void showUpdateAlert(final Activity activity, final int priority, final int stableVersion) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (activity.isFinishing()) return;
                // Same rules as FleetSmart MainActivity.showUpdateAlert: only an "Update" action (no Cancel).
                // - Below StableVersion: "You are using old version" — must update.
                // - Priority 1 or 2 (and already >= StableVersion): "New Version available" — still only Update.
                // - Other priority values with version >= stable: no dialog (server did not flag this client).
                if (BuildConfig.VERSION_CODE < stableVersion) {
                    new AlertDialog.Builder(new ContextThemeWrapper(activity, android.R.style.Theme_Holo_Light_Dialog))
                            .setTitle("You are using old version")
                            .setIcon(R.drawable.error)
                            .setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update", new DialogInterface.OnClickListener() {
                                @Override
                                public void onClick(DialogInterface dialog, int id) {
                                    openStoreAndExit(activity);
                                }
                            })
                            .show();
                } else if (priority == 1 || priority == 2) {
                    new AlertDialog.Builder(new ContextThemeWrapper(activity, android.R.style.Theme_Holo_Light_Dialog))
                            .setTitle("New Version available")
                            .setIcon(R.drawable.error)
                            .setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update", new DialogInterface.OnClickListener() {
                                @Override
                                public void onClick(DialogInterface dialog, int id) {
                                    openStoreAndExit(activity);
                                }
                            })
                            .show();
                }
            }
        });
    }

    private static void openStoreAndExit(Activity activity) {
        final String appPackageName = activity.getPackageName();
        try {
            activity.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
            System.exit(0);
        } catch (Exception ignored) {
            activity.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
        }
    }
}
