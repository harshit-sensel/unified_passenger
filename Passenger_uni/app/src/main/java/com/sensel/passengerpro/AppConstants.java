package com.sensel.passengerpro;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.text.DateFormat;
import java.text.SimpleDateFormat;

import static android.content.Context.MODE_PRIVATE;

/**
 * Created by MS on 24-May-18.
 */

public class AppConstants {
    public final String shrdPrefTag="passenger";
    /** Key for vehicle ID when user Tag In with QR - used by Panic API. */
    public static final String KEY_CURRENT_TAGGED_VEHICLE_ID = "current_tagged_vehicle_id";
    /** Key for last valid location ("lat,lng") used as quick fallback. */
    public static final String KEY_LAST_VALID_LATLNG = "last_valid_latlng";
    public final DateFormat dateFormat=new SimpleDateFormat("dd/MM/yyyy, hh:mm:ss a");

    public void putShrdPrefValWithKey(Context context,String key,String value) {
        SharedPreferences pref = context.getSharedPreferences(shrdPrefTag, MODE_PRIVATE);
        SharedPreferences.Editor editor = pref.edit();
        editor.putString(key, value);
        editor.apply();
    }
    public String getShrdPrefValByKey(Context context,String key){
        SharedPreferences pref = context.getSharedPreferences(shrdPrefTag, MODE_PRIVATE);
        return pref.getString(key,null);
    }
    public String getShrdPrefValByKeyWithTag(Context context,String Key,String Tag){
        SharedPreferences pref = context.getSharedPreferences(shrdPrefTag, MODE_PRIVATE);
        String info = pref.getString(Key,null);
        if(info!=null) {
            try {
                JSONArray jArr = new JSONArray(info);
                for (int j = 0; j < jArr.length(); j++) {
                    JSONObject data = jArr.getJSONObject(j);

                    if (data.getString(Tag).trim().length() > 0 && data.getString(Tag).trim()!="null") {
                        return data.getString(Tag);
                    }
                }
            } catch (JSONException e) {
                e.printStackTrace();
            }
        }
        return "";
    }

    public String getValueFromJSonByKey(String jsonString,String key){
        if(jsonString!=null) {
            try {
                JSONArray jArr = new JSONArray(jsonString);
                for (int j = 0; j < jArr.length(); j++) {
                    JSONObject data = jArr.getJSONObject(j);
                    String val=data.getString(key);
                    if (val.trim().length() > 0 && val.trim()!="null") {
                        return val;
                    }
                }
            } catch (JSONException e) {
                e.printStackTrace();
            }
        }
        return "";
    }

    public String getVehicleStatusByCode(String code)
    {
        /*code=code.replace(",","");
        if (code.equals("VH"))
            return "Vehicle Halted";
        else if (code.equals("VI"))
            return "Vehicle Idling";
        else if (code.equals("VM"))
            return "Vehicle Moving";
        else if (code.equals("NR"))
            return "Not Reachable";
        else if (code.equals("OS"))
            return "Over Speeding";
        else if (code.equals("PBP"))
            return "Panic Button Pressed";
        else if (code.equals("GNA"))
            return "GPS Not Active";
        else if (code.equals("TPON"))
            return "Tipper ON";
        else if (code.equals("VO"))
            return "Vehicle ON";
        else if (code.equals("VF"))
            return "Vehicle OFF";
        else
            return code;*/
        return code;
    }
}
