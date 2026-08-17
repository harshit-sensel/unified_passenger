package com.sensel.passengerpro;

import org.json.JSONObject;

/**
 * Account-Level Remote Feature Flags & Settings Model.
 */
public class AccountConfig {
    public int accountId = 0;
    public boolean autoLogoutEnabled = false;
    public int autoLogoutTimeoutMinutes = 15;
    public boolean twoFactorAuthEnabled = false;
    public boolean activityLogEnabled = true;
    public boolean forceUpdateEnabled = false;
    public String minRequiredVersion = "1.0.0";
    public boolean privacyPolicyEnabled = false;
    public String privacyPolicyText = "";
    public boolean panicPressEmail = true;
    public boolean panicPressSMS = false;

    public static AccountConfig fromJson(String jsonStr) {
        AccountConfig config = new AccountConfig();
        if (jsonStr == null || jsonStr.isEmpty() || jsonStr.equals("No Data")) {
            return config;
        }

        try {
            JSONObject obj = new JSONObject(jsonStr);
            config.accountId = obj.optInt("AccountId", 0);
            config.autoLogoutEnabled = obj.optInt("AutoLogoutEnabled", 0) == 1;
            config.autoLogoutTimeoutMinutes = obj.optInt("AutoLogoutTimeoutMinutes", 15);
            config.twoFactorAuthEnabled = obj.optInt("TwoFactorAuthEnabled", 0) == 1;
            config.activityLogEnabled = obj.optInt("ActivityLogEnabled", 1) == 1;
            config.forceUpdateEnabled = obj.optInt("ForceUpdateEnabled", 0) == 1;
            config.minRequiredVersion = obj.optString("MinRequiredVersion", "1.0.0");
            config.privacyPolicyEnabled = obj.optInt("PrivacyPolicyEnabled", 0) == 1;
            config.privacyPolicyText = obj.optString("PrivacyPolicyText", "");
            config.panicPressEmail = obj.optInt("PanicPressEmail", 1) == 1;
            config.panicPressSMS = obj.optInt("PanicPressSMS", 0) == 1;
        } catch (Exception e) {
            e.printStackTrace();
        }

        return config;
    }

    /**
     * Checks if the currently installed app version is below the minimum required version.
     * @param currentVersion Current app version name (e.g. BuildConfig.VERSION_NAME "1.0.0")
     * @return true if force-update is enabled AND currentVersion < minRequiredVersion
     */
    public boolean isVersionDeprecated(String currentVersion) {
        if (!forceUpdateEnabled || minRequiredVersion == null || minRequiredVersion.trim().isEmpty()) {
            return false;
        }
        if (currentVersion == null || currentVersion.trim().isEmpty()) {
            return true;
        }
        return compareVersions(currentVersion.trim(), minRequiredVersion.trim()) < 0;
    }

    /**
     * Compares two semantic version strings (e.g. "1.0.0" vs "1.0.1").
     * Returns:
     *   negative if v1 < v2
     *   zero if v1 == v2
     *   positive if v1 > v2
     */
    public static int compareVersions(String v1, String v2) {
        String cleanV1 = v1.replaceAll("[^0-9.]", "");
        String cleanV2 = v2.replaceAll("[^0-9.]", "");

        String[] parts1 = cleanV1.split("\\.");
        String[] parts2 = cleanV2.split("\\.");

        int length = Math.max(parts1.length, parts2.length);
        for (int i = 0; i < length; i++) {
            int num1 = 0;
            int num2 = 0;
            if (i < parts1.length && !parts1[i].isEmpty()) {
                try {
                    num1 = Integer.parseInt(parts1[i]);
                } catch (NumberFormatException ignored) {}
            }
            if (i < parts2.length && !parts2[i].isEmpty()) {
                try {
                    num2 = Integer.parseInt(parts2[i]);
                } catch (NumberFormatException ignored) {}
            }
            if (num1 < num2) return -1;
            if (num1 > num2) return 1;
        }
        return 0;
    }
}
