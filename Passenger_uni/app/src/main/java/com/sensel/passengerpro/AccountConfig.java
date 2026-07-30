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
    public String minRequiredVersion = "2.0.0";
    public boolean privacyPolicyEnabled = false;
    public String privacyPolicyText = "";

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
            config.minRequiredVersion = obj.optString("MinRequiredVersion", "2.0.0");
            config.privacyPolicyEnabled = obj.optInt("PrivacyPolicyEnabled", 0) == 1;
            config.privacyPolicyText = obj.optString("PrivacyPolicyText", "");
        } catch (Exception e) {
            e.printStackTrace();
        }

        return config;
    }
}
