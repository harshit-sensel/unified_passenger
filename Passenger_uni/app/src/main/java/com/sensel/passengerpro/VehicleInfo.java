package com.sensel.passengerpro;

import android.Manifest;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.media.ExifInterface;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.os.StrictMode;
import android.provider.MediaStore;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import androidx.appcompat.widget.PopupMenu;

import android.text.Editable;
import android.text.TextWatcher;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.widget.AdapterView;
import android.widget.AutoCompleteTextView;
import android.widget.Button;
import android.widget.EditText;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.TextView;
import android.widget.Toast;

import com.android.volley.RequestQueue;
import com.android.volley.toolbox.Volley;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

/**
 * Created by MS on 16-Oct-17.
 */

public class VehicleInfo extends AppCompatActivity {
    AutoCompleteTextView autoCompleteTextView;
    AutoCompleteTextView autoCompleteVehTextView;
    ProgressDialog dialog;
    Button showBtn;
    String result="";
    String appdata;
    RadioButton rbAssigned;
    RadioButton rbChange;
    RadioGroup rgp;
    TextView txtType;
    String allDrivers="";
    String allZones="";
    String allTowers="";
    String passengerinfo;
    String mobileno;
    TextView txt;
    EditText editText;
    TextView dritxt;
    EditText drieditText;
    AutoCompleteTextView zoneautoTxt;
    AutoCompleteTextView twrautoTxt;
    TextView zonetxt;
    TextView twrtxt;
    //ImageView driver_image;
    int textvehiclecount=0;
    int drivtextvehiclecount=0;
    String dir_path=null;
    AppConstants appConstants=new AppConstants();
    WebServices webServices=new WebServices();
    Uri fileUri;
    private static final int MY_PERMISSIONS_REQUEST_READ_CONTACTS = 100;
    private static final int MY_PERMISSIONS_REQUEST_READ_LOCATION = 101;
    private static final int MY_PERMISSIONS_REQUEST = 102;
    private static final int MY_PERMISSIONS_REQUEST_CAMERA = 103;
    private static final int CAMERA_CAPTURE_IMAGE_REQUEST_CODE = 104;
    private static int MEDIA_TYPE_IMAGE=1;
    private String accountid;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_vehicleinfo);
        PassengerActivityLogger.log(this, "VehicleInfo");
        autoCompleteTextView=(AutoCompleteTextView) findViewById(R.id.autoText);
        autoCompleteVehTextView=(AutoCompleteTextView) findViewById(R.id.autoVehText);
        showBtn=(Button) findViewById(R.id.btn);
        rbAssigned=(RadioButton) findViewById(R.id.rbAssigned);
        rbChange=(RadioButton) findViewById(R.id.rbChange);
        rgp=(RadioGroup) findViewById(R.id.radioType);
        txtType=(TextView) findViewById(R.id.type);
        if (txtType != null) {
            boolean isAssignedChecked = rbAssigned != null && rbAssigned.isChecked();
            txtType.setText(isAssignedChecked ? "Assigned Driver" : "Driver Change");
        }
        txt=(TextView) findViewById(R.id.txt);
        editText=(EditText) findViewById(R.id.edittxt);
        dritxt=(TextView) findViewById(R.id.dritxt);
        drieditText=(EditText) findViewById(R.id.driedittxt);
        zoneautoTxt=(AutoCompleteTextView)findViewById(R.id.zoneautoTxt);
        twrautoTxt=(AutoCompleteTextView)findViewById(R.id.twrautoTxt);
        zonetxt=(TextView)findViewById(R.id.zonetxt);
        twrtxt=(TextView)findViewById(R.id.twrtxt);
        //driver_image=(ImageView) findViewById(R.id.driver_image);
        passengerinfo = appConstants.getShrdPrefValByKey(getApplicationContext(),"passengerinfo");
        mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(),"passengerinfo","MobileNo");
        accountid = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");

        android.widget.ImageView btnRefresh = findViewById(R.id.btn_refresh);
        android.widget.ImageView btnLogout = findViewById(R.id.btn_logout);

        if (btnRefresh != null) {
            btnRefresh.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    finish();
                    startActivity(getIntent());
                }
            });
        }

        if (btnLogout != null) {
            btnLogout.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    // Extract mobileNo and accountId BEFORE clearing preferences
                    final String mobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                    final String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                    int accId = 0;
                    try { if (accountIdStr != null) accId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                    final int accountId = accId;

                    // Log LOGOUT activity audit event
                    if (mobileNo != null && !mobileNo.trim().isEmpty()) {
                        new Thread(new Runnable() {
                            @Override
                            public void run() {
                                try {
                                    webServices.logAuditActivity(mobileNo, accountId, "LOGOUT", "", "");
                                } catch (Exception ignored) {}
                            }
                        }).start();
                    }

                    appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", "");
                    appConstants.putShrdPrefValWithKey(getApplicationContext(), "UserMenus", "");
                    appConstants.setJwtToken(getApplicationContext(), "");
                    WebServices.currentJwtToken = "";
                    Intent intent = new Intent(VehicleInfo.this, LoginActivity.class);
                    intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                    startActivity(intent);
                    finish();
                }
            });
        }

        if (rgp != null) {
            rgp.setOnCheckedChangeListener(new RadioGroup.OnCheckedChangeListener() {

                @Override
                public void onCheckedChanged(RadioGroup group, int checkedId)
                {
                    if (txtType != null) {
                        boolean isAssignedChecked = rbAssigned != null && rbAssigned.isChecked();
                        txtType.setText(isAssignedChecked ? "Assigned Driver" : "Driver Change");
                    }
                    try {
                    // TODO Auto-generated method stub
                    if (rbAssigned.isChecked()) {
                        dritxt.setVisibility(View.GONE);
                        drieditText.setVisibility(View.GONE);
                        try {
                            final JSONArray jArr = new JSONArray(result);
                            boolean foundDriver = false;
                            for (int j = 0; j < jArr.length(); j++) {
                                JSONObject data = jArr.getJSONObject(j);
                                String vId = data.optString("VehicleId", data.optString("VehicleID", data.optString("vehicleId", ""))).trim();

                                if (vId.length() > 0) {
                                    String cleanSelected = autoCompleteVehTextView.getText().toString().replaceAll("\\s+", "").toUpperCase();
                                    String cleanDbVeh = vId.replaceAll("\\s+", "").toUpperCase();
                                    if (cleanSelected.length() > 0 && cleanSelected.equals(cleanDbVeh)) {
                                        foundDriver = true;
                                        String drv = data.optString("Driver", "");
                                        if (!drv.isEmpty() && !"null".equalsIgnoreCase(drv))
                                            autoCompleteTextView.setText(drv);
                                        break;
                                    }
                                }
                            }
                            if (!foundDriver) {
                                autoCompleteTextView.setText("");
                            }
                            txtType.setText("Assigned Driver");
                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    autoCompleteTextView.setEnabled(false);
                                    autoCompleteTextView.setDropDownHeight(0);
                                }
                            });
                        }
                        catch (Exception e){
                            e.printStackTrace();
                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                        }
                    } else if (rbChange.isChecked()) {
                        txtType.setText("Driver Change");
                        dritxt.setVisibility(View.GONE);
                        drieditText.setVisibility(View.GONE);
                        String driverlist = "";
                        try {
                            if(allDrivers.contains("DriverId")) {
                                final JSONArray jArr = new JSONArray(allDrivers);
                                for (int j = 0; j < jArr.length(); j++) {
                                    JSONObject data = jArr.getJSONObject(j);

                                    if (data.getString("Driver").trim().length() > 0 && !data.getString("Driver").trim().equals("null")) {
                                        driverlist = driverlist + data.getString("Driver") + "#";
                                    }
                                }
                                driverlist=driverlist+"OTHER";
                            }
                            else
                                driverlist="OTHER";
                        } catch (Exception e) {
                            e.printStackTrace();
                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                        }
                        final String[] drivers = driverlist.split("\\#");
                        runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                List<Names> list=new ArrayList<Names>();
                                for(int i=0;i<drivers.length;i++){
                                    list.add(new Names(drivers[i]));
                                }
                                CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                                        VehicleInfo.this,
                                        android.R.layout.simple_list_item_1,
                                        R.id.lbl_name,
                                        list
                                );
                                autoCompleteTextView.setAdapter(adapter);
                                autoCompleteTextView.setHint("Type Driver Name");
                                autoCompleteTextView.setText("");
                                autoCompleteTextView.setOnClickListener(new View.OnClickListener() {
                                    public void onClick(View v) {
                                        autoCompleteTextView.showDropDown();//Show full list of driver
                                    }
                                });
                                autoCompleteTextView.setDropDownHeight(ActionBar.LayoutParams.WRAP_CONTENT);
                                autoCompleteTextView.setEnabled(true);
                                autoCompleteTextView.addTextChangedListener(new TextWatcher() {
                                    @Override
                                    public void beforeTextChanged(CharSequence s, int start, int count, int after) {

                                    }

                                    @Override
                                    public void onTextChanged(CharSequence s, int start, int before, int count) {

                                    }

                                    @Override
                                    public void afterTextChanged(Editable s) {
                                        if(autoCompleteTextView.getText().toString().length()!=drivtextvehiclecount) {
                                            drivtextvehiclecount = autoCompleteTextView.getText().toString().length();
                                            try{
                                                if(autoCompleteTextView.getText().toString().toUpperCase().equals("OTHER")){
                                                    dritxt.setVisibility(View.VISIBLE);
                                                    drieditText.setVisibility(View.VISIBLE);
                                                }
                                                else {
                                                    dritxt.setVisibility(View.GONE);
                                                    drieditText.setVisibility(View.GONE);
                                                }
                                            }
                                            catch (Exception e){
                                                e.printStackTrace();
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                            }
                                        }
                                    }
                                });
                                autoCompleteTextView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                                    @Override
                                    public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                                        try{
                                            if(autoCompleteTextView.getText().toString().toUpperCase().equals("OTHER")){
                                                dritxt.setVisibility(View.VISIBLE);
                                                drieditText.setVisibility(View.VISIBLE);
                                            }
                                            else {
                                                dritxt.setVisibility(View.GONE);
                                                drieditText.setVisibility(View.GONE);
                                            }
                                        }
                                        catch (Exception e){
                                            e.printStackTrace();
                                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                        }
                                    }
                                });
                            }
                        });
                    }
                }
                catch(Exception e){
                    e.printStackTrace();
                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                }
            }
        });
        }
        dialog = ProgressDialog.show(VehicleInfo.this, "", "Loading...", true);
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    if (passengerinfo == null) {
                        Intent i = new Intent(getApplicationContext(), LoginActivity.class);
                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        startActivity(i);
                    } else {
                        if (isNetworkAvailable()) {
                            result = webServices.GetPsngrInfoWithValidation(mobileno, "Vehicles");
                            allDrivers = webServices.GetPsngrInfoWithValidation(mobileno, "Drivers");
                            if(accountid.equals(UrlConfig.tata_accountid)) {
                                allZones = webServices.GetPsngrInfoWithValidation(mobileno, "Zones");
                                allTowers = webServices.GetPsngrInfoWithValidation(mobileno, "Towers");
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        zonetxt.setVisibility(View.VISIBLE);
                                        zoneautoTxt.setVisibility(View.VISIBLE);
                                        twrtxt.setVisibility(View.VISIBLE);
                                        twrautoTxt.setVisibility(View.VISIBLE);
                                    }
                                });
                            }
                            String vehiclelist = "";
                            String zonelist="";
                            String towerlist="";
                            try {
                                if (result.contains("VehicleId") || result.contains("VehicleID") || result.contains("vehicleId")) {
                                    final JSONArray jArr = new JSONArray(result);
                                    for (int j = 0; j < jArr.length(); j++) {
                                        JSONObject data = jArr.getJSONObject(j);
                                        String vId = data.optString("VehicleId", data.optString("VehicleID", data.optString("vehicleId", ""))).trim();
                                        if (vId.length() > 0) {
                                            vehiclelist = vehiclelist + vId + "#";
                                        }
                                    }
                                    vehiclelist = vehiclelist + "OTHER";
                                } else
                                    vehiclelist = "OTHER";
                                if (allZones.contains("zone")) {
                                    final JSONArray jArr = new JSONArray(allZones);
                                    for (int j = 0; j < jArr.length(); j++) {
                                        JSONObject data = jArr.getJSONObject(j);

                                        if (data.getString("zone").trim().length() > 0) {
                                            zonelist = zonelist + data.getString("zone") + "#";
                                        }
                                    }
                                }
                                if (allTowers.contains("TowerName")) {
                                    final JSONArray jArr = new JSONArray(allTowers);
                                    for (int j = 0; j < jArr.length(); j++) {
                                        JSONObject data = jArr.getJSONObject(j);

                                        if (data.getString("TowerName").trim().length() > 0) {
                                            towerlist = towerlist + data.getString("TowerName") + "#";
                                        }
                                    }
                                }
                                final String[] vehicles = vehiclelist.split("\\#");
                                final String[] zones = zonelist.split("\\#");
                                final String[] towers = towerlist.split("\\#");
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        List<Names> list = new ArrayList<Names>();
                                        for (int i = 0; i < vehicles.length; i++) {
                                            list.add(new Names(vehicles[i]));
                                        }
                                        CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                                                VehicleInfo.this,
                                                android.R.layout.simple_list_item_1,
                                                R.id.lbl_name,
                                                list
                                        );
                                         autoCompleteVehTextView.setAdapter(adapter);
                                         autoCompleteVehTextView.setText("");
                                         autoCompleteVehTextView.setOnClickListener(new View.OnClickListener() {
                                             public void onClick(View v) {
                                                 autoCompleteVehTextView.showDropDown();//Show full list of vehicle
                                             }
                                         });
                                         autoCompleteVehTextView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                                             @Override
                                             public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                                                 textvehiclecount = -1;
                                             }
                                         });
                                         autoCompleteVehTextView.addTextChangedListener(new TextWatcher() {
                                            @Override
                                            public void beforeTextChanged(CharSequence s, int start, int count, int after) {

                                            }

                                            @Override
                                            public void onTextChanged(CharSequence s, int start, int before, int count) {

                                            }

                                            @Override
                                            public void afterTextChanged(Editable s) {
                                                if (autoCompleteVehTextView.getText().toString().length() != textvehiclecount) {
                                                    textvehiclecount = autoCompleteVehTextView.getText().toString().length();
                                                    try {
                                                        if (autoCompleteVehTextView.getText().toString().toUpperCase().equals("OTHER")) {
                                                            autoCompleteTextView.setText("");
                                                            rbAssigned.setEnabled(false);
                                                            rbChange.setChecked(true);
                                                            txt.setVisibility(View.VISIBLE);
                                                            editText.setVisibility(View.VISIBLE);
                                                        } else {
                                                            txt.setVisibility(View.GONE);
                                                            editText.setVisibility(View.GONE);
                                                            dritxt.setVisibility(View.GONE);
                                                            drieditText.setVisibility(View.GONE);
                                                            android.util.Log.d("VehicleInfoDebug", "afterTextChanged selected=" + autoCompleteVehTextView.getText().toString() + " | result=" + result);
                                                            if (result != null && (result.contains("VehicleId") || result.contains("VehicleID") || result.contains("vehicleId"))) {
                                                                final JSONArray jArr = new JSONArray(result);
                                                                boolean foundMatch = false;
                                                                for (int j = 0; j < jArr.length(); j++) {
                                                                    JSONObject data = jArr.getJSONObject(j);
                                                                    String vId = data.optString("VehicleId", data.optString("VehicleID", data.optString("vehicleId", ""))).trim();

                                                                    if (vId.length() > 0) {
                                                                        String cleanSelected = autoCompleteVehTextView.getText().toString().replaceAll("\\s+", "").toUpperCase();
                                                                        String cleanDbVeh = vId.replaceAll("\\s+", "").toUpperCase();
                                                                        android.util.Log.d("VehicleInfoDebug", "Comparing cleanSelected='" + cleanSelected + "' vs cleanDbVeh='" + cleanDbVeh + "'");
                                                                        if (cleanSelected.length() > 0 && cleanSelected.equals(cleanDbVeh)) {
                                                                            foundMatch = true;
                                                                            String drv = data.optString("Driver", "");
                                                                            android.util.Log.d("VehicleInfoDebug", "Match found! Driver='" + drv + "'");
                                                                            if (!drv.isEmpty() && !"null".equalsIgnoreCase(drv)) {
                                                                                rbAssigned.setEnabled(true);
                                                                                rbAssigned.post(new Runnable() {
                                                                                    @Override
                                                                                    public void run() {
                                                                                        rbAssigned.setEnabled(true);
                                                                                        rbAssigned.setChecked(true);
                                                                                    }
                                                                                });
                                                                                autoCompleteTextView.setText(drv);
                                                                            } else {
                                                                                autoCompleteTextView.setText("");
                                                                                rbAssigned.setEnabled(false);
                                                                                rbChange.setChecked(true);
                                                                            }
                                                                            break;
                                                                        }
                                                                    }
                                                                }
                                                                if (!foundMatch) {
                                                                    android.util.Log.d("VehicleInfoDebug", "No match found for cleanSelected='" + autoCompleteVehTextView.getText().toString() + "'");
                                                                    autoCompleteTextView.setText("");
                                                                    rbAssigned.setEnabled(false);
                                                                    rbChange.setChecked(true);
                                                                }
                                                            } else {
                                                                autoCompleteTextView.setText("");
                                                                rbAssigned.setEnabled(false);
                                                                rbChange.setChecked(true);
                                                            }
                                                        }
                                                    } catch (Exception e) {
                                                        e.printStackTrace();
                                                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                                                    }
                                                }
                                            }
                                        });
                                        autoCompleteVehTextView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                                            @Override
                                            public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                                                try {
                                                    if (autoCompleteVehTextView.getText().toString().equals("OTHER")) {
                                                        autoCompleteTextView.setText("");
                                                        rbAssigned.setEnabled(false);
                                                        rbChange.setChecked(true);
                                                        txt.setVisibility(View.VISIBLE);
                                                        editText.setVisibility(View.VISIBLE);
                                                    } else {
                                                        txt.setVisibility(View.GONE);
                                                        editText.setVisibility(View.GONE);
                                                        dritxt.setVisibility(View.GONE);
                                                        drieditText.setVisibility(View.GONE);
                                                        if (result.contains("VehicleId")) {
                                                            final JSONArray jArr = new JSONArray(result);
                                                            for (int j = 0; j < jArr.length(); j++) {
                                                                JSONObject data = jArr.getJSONObject(j);

                                                                if (data.getString("VehicleId").trim().length() > 0) {
                                                                    if (autoCompleteVehTextView.getText().toString().trim().length() > 0 && data.getString("VehicleId").equals(autoCompleteVehTextView.getText().toString())) {
                                                                        if (!data.getString("Driver").equals("null")) {
                                                                            rbAssigned.setEnabled(true);
                                                                            rbAssigned.setChecked(true);
                                                                            autoCompleteTextView.setText(data.getString("Driver"));
                                                                        } else {
                                                                            autoCompleteTextView.setText("");
                                                                            rbAssigned.setEnabled(false);
                                                                            rbChange.setChecked(true);
                                                                        }
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        } else {
                                                            autoCompleteTextView.setText("");
                                                            rbAssigned.setEnabled(false);
                                                            rbChange.setChecked(true);
                                                        }
                                                    }
                                                } catch (Exception e) {
                                                    e.printStackTrace();
                                                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                                                }
                                            }
                                        });
                                    }
                                });
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        List<Names> list = new ArrayList<Names>();
                                        for (int i = 0; i < zones.length; i++) {
                                            list.add(new Names(zones[i]));
                                        }
                                        CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                                                VehicleInfo.this,
                                                android.R.layout.simple_list_item_1,
                                                R.id.lbl_name,
                                                list
                                        );
                                        zoneautoTxt.setAdapter(adapter);
                                        zoneautoTxt.setText("");
                                        zoneautoTxt.setOnClickListener(new View.OnClickListener() {
                                            public void onClick(View v) {
                                                zoneautoTxt.showDropDown();//Show full list of vehicle
                                            }
                                        });
                                        zoneautoTxt.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                                            @Override
                                            public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                                                dialog = ProgressDialog.show(VehicleInfo.this, "", "Loading...", true);
                                                try {
                                                    if(isNetworkAvailable()){
                                                        new Thread(new Runnable() {
                                                            @Override
                                                            public void run() {
                                                                final String towerlocations=webServices.GetPsngrTowerLocations(mobileno,zoneautoTxt.getText().toString(),"");
                                                                runOnUiThread(new Runnable() {
                                                                    @Override
                                                                    public void run() {
                                                                        String towerlist="";
                                                                        if (allTowers.contains("TowerName")) {
                                                                            try {
                                                                                final JSONArray jArr = new JSONArray(towerlocations);
                                                                                for (int j = 0; j < jArr.length(); j++) {
                                                                                    JSONObject data = jArr.getJSONObject(j);

                                                                                    if (data.getString("TowerName").trim().length() > 0) {
                                                                                        towerlist = towerlist + data.getString("TowerName") + "#";
                                                                                    }
                                                                                }
                                                                            }
                                                                            catch (Exception e){}
                                                                        }
                                                                        final String[] towers = towerlist.split("\\#");
                                                                        List<Names> list = new ArrayList<Names>();
                                                                        for (int i = 0; i < towers.length; i++) {
                                                                            list.add(new Names(towers[i]));
                                                                        }
                                                                        CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                                                                                VehicleInfo.this,
                                                                                android.R.layout.simple_list_item_1,
                                                                                R.id.lbl_name,
                                                                                list
                                                                        );
                                                                        twrautoTxt.setAdapter(adapter);
                                                                        twrautoTxt.setText("");
                                                                    }
                                                                });
                                                            }
                                                        }).start();
                                                    }
                                                    else{
                                                        runOnUiThread(new Runnable() {
                                                            public void run() {
                                                                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                        new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                                                                alertDialogBuilder.setIcon(R.drawable.error);
                                                                alertDialogBuilder.setTitle("Error ");
                                                                alertDialogBuilder.setMessage("Internet is not there. Please check your connection.")
                                                                        .setCancelable(false)
                                                                        .setPositiveButton("Ok",
                                                                                new DialogInterface.OnClickListener() {
                                                                                    public void onClick(DialogInterface dialog, int id) {
                                                                                        dialog.cancel();
                                                                                    }
                                                                                });
                                                                AlertDialog alert = alertDialogBuilder.create();
                                                                alert.show();
                                                            }
                                                        });
                                                    }
                                                } catch (Exception e) {
                                                    e.printStackTrace();
                                                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                                                }
                                                finally {
                                                    dialog.dismiss();
                                                }
                                            }
                                        });
                                    }
                                });
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        List<Names> list = new ArrayList<Names>();
                                        for (int i = 0; i < towers.length; i++) {
                                            list.add(new Names(towers[i]));
                                        }
                                        CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                                                VehicleInfo.this,
                                                android.R.layout.simple_list_item_1,
                                                R.id.lbl_name,
                                                list
                                        );
                                        twrautoTxt.setAdapter(adapter);
                                        twrautoTxt.setText("");
                                        twrautoTxt.setOnClickListener(new View.OnClickListener() {
                                            public void onClick(View v) {
                                                twrautoTxt.showDropDown();//Show full list of vehicle
                                            }
                                        });
                                        twrautoTxt.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                                            @Override
                                            public void onItemClick(AdapterView<?> parent, View view, int position, long id) {

                                            }
                                        });
                                    }
                                });
                            } catch (Exception e) {
                                e.printStackTrace();
                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                            }
                        } else {
                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setIcon(R.drawable.error);
                                    alertDialogBuilder.setTitle("Error ");
                                    alertDialogBuilder.setMessage("Internet is not there. Please check your connection.")
                                            .setCancelable(false)
                                            .setPositiveButton("Ok",
                                                    new DialogInterface.OnClickListener() {
                                                        public void onClick(DialogInterface dialog, int id) {
                                                            dialog.cancel();
                                                        }
                                                    });
                                    AlertDialog alert = alertDialogBuilder.create();
                                    alert.show();
                                }
                            });
                        }
                    }
                    appdata = webServices.GetAppVersion(getApplicationContext().getPackageName());
                    if (appdata != null) {
                        try {
                            if (appdata.contains("VersionCode")) {
                                JSONArray array = new JSONArray(appdata);
                                JSONObject data = new JSONObject(array.get(0).toString());
                                String _version = data.getString("VersionCode");
                                if (Integer.parseInt(_version) > BuildConfig.VERSION_CODE)
                                    showUpdateAlert(Integer.parseInt(data.getString("Priority")), Integer.parseInt(data.getString("StableVersion")));
                                if (data.getString("DomainUrl").contains("http") && !UrlConfig.DOMAINURL1.equals(data.getString("DomainUrl"))) {
                                    UrlConfig.DOMAINURL1 = data.getString("DomainUrl");
                                    if (data.getString("DomainUrl").contains("https://"))
                                        UrlConfig.DOMAINURL2 = data.getString("DomainUrl").replace("https://", "http://");
                                    else
                                        UrlConfig.DOMAINURL2 = data.getString("DomainUrl").replace("http://", "https://");
                                }
                            }
                        } catch (JSONException e) {
                            e.printStackTrace();
                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno + "-" + appdata);
                        }
                    } else {
                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                        errorRecordSendMail.errorrecordSendMail(appdata + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno + "-GetAppVersion(" + getApplicationContext().getPackageName() + ")");
                    }
                }
                catch (Exception e){
                    e.printStackTrace();
                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                }
                finally {
                    dialog.dismiss();
                }
            }
        }).start();
        showBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if(autoCompleteVehTextView.getText().toString().length()==0){
                    Toast.makeText(getApplicationContext(),"Please Select vehicleid",Toast.LENGTH_SHORT).show();
                    return;
                }
                else if(!autoCompleteVehTextView.getText().toString().toUpperCase().equals("OTHER")&&!result.toUpperCase().contains(autoCompleteVehTextView.getText().toString().toUpperCase())){
                    Toast.makeText(getApplicationContext(),"Invalid vehicleid, please select proper vehicleid",Toast.LENGTH_SHORT).show();
                    return;
                }
                else if(autoCompleteVehTextView.getText().toString().toUpperCase().equals("OTHER")&&editText.getText().toString().trim().length()==0){
                    Toast.makeText(getApplicationContext(),"Please enter vehicleid",Toast.LENGTH_SHORT).show();
                    return;
                }
                else {
                    String vehString=autoCompleteVehTextView.getText().toString().toUpperCase();
                    int vehFlag=0;
                    if(vehString.equals("OTHER")){
                        vehFlag=1;
                        vehString=editText.getText().toString().trim().replace(" ", "");
                    }
                    else {
                        boolean invalidVehicle = true;
                        try {
                            if (result != null && (result.contains("VehicleId") || result.contains("VehicleID") || result.contains("vehicleId"))) {
                                final JSONArray jArr = new JSONArray(result);
                                String cleanSelected = vehString.replaceAll("\\s+", "");
                                for (int j = 0; j < jArr.length(); j++) {
                                    JSONObject data = jArr.getJSONObject(j);
                                    String vId = data.optString("VehicleId", data.optString("VehicleID", data.optString("vehicleId", ""))).trim();
                                    if (vId.length() > 0 && cleanSelected.equals(vId.replaceAll("\\s+", "").toUpperCase())) {
                                        invalidVehicle = false;
                                        break;
                                    }
                                }
                            }
                        } catch (Exception ignored) {}
                        if (invalidVehicle) {
                            Toast.makeText(getApplicationContext(), "Invalid vehicleid, please select proper vehicle", Toast.LENGTH_SHORT).show();
                            return;
                        }
                    }
                    if(autoCompleteTextView.getText().toString().length()==0){
                        Toast.makeText(getApplicationContext(),"Please Select driver",Toast.LENGTH_SHORT).show();
                        return;
                    }
                    else if(!autoCompleteTextView.getText().toString().toUpperCase().equals("OTHER")&&!allDrivers.toUpperCase().contains(autoCompleteTextView.getText().toString().toUpperCase())){
                        Toast.makeText(getApplicationContext(),"Invalid driver, please select proper driver",Toast.LENGTH_SHORT).show();
                        return;
                    }
                    else if(autoCompleteTextView.getText().toString().toUpperCase().equals("OTHER")&&drieditText.getText().toString().trim().length()==0){
                        Toast.makeText(getApplicationContext(),"Please enter driver details",Toast.LENGTH_SHORT).show();
                        return;
                    }
                    else{
                        String driString=autoCompleteTextView.getText().toString().toUpperCase();
                        if(driString.equals("OTHER")){
                            driString=drieditText.getText().toString();
                        }
                        else{
                            boolean invalidDriver = true;
                            try {
                                if (allDrivers != null && (allDrivers.contains("Driver") || allDrivers.contains("DriverId"))) {
                                    final JSONArray jArr = new JSONArray(allDrivers);
                                    String cleanSelectedDri = driString.replaceAll("\\s+", "");
                                    for (int j = 0; j < jArr.length(); j++) {
                                        JSONObject data = jArr.getJSONObject(j);
                                        String dName = data.optString("Driver", "").trim();
                                        if (dName.length() > 0 && cleanSelectedDri.equals(dName.replaceAll("\\s+", "").toUpperCase())) {
                                            invalidDriver = false;
                                            break;
                                        }
                                    }
                                }
                            } catch (Exception ignored) {}
                            if (invalidDriver) {
                                Toast.makeText(getApplicationContext(), "Invalid driver, please select proper driver", Toast.LENGTH_SHORT).show();
                                return;
                            }
                        }
                        /*if(dir_path==null){
                            Toast.makeText(getApplicationContext(), "Driver Photo is missing. Please capture driver photo", Toast.LENGTH_SHORT).show();
                            return;
                        }*/
                        if(isNetworkAvailable()) {
                            String vehicleWithDriver = autoCompleteTextView.getText().toString().toUpperCase();
                            if (!vehicleWithDriver.equals("OTHER")) {
                                try {
                                    final JSONArray jArr = new JSONArray(allDrivers);
                                    for (int j = 0; j < jArr.length(); j++) {
                                        JSONObject data = jArr.getJSONObject(j);

                                        if (data.getString("Driver").toUpperCase().equals(vehicleWithDriver)) {
                                            vehicleWithDriver = vehString + "@#" + driString + "@#" + data.getString("DriverId") + "@#" + vehFlag;
                                            break;
                                        }
                                    }
                                    openChecklist(vehicleWithDriver, vehString);
                                } catch (Exception e) {
                                    e.printStackTrace();
                                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-VehicleInfo(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                                }
                            } else {
                                final String resultStr = vehString + "@#" + driString + ":-Not Mapped@#0" + "@#" + vehFlag;
                                final String finalVehString = vehString;
                                runOnUiThread(new Runnable() {
                                    public void run() {
                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                                        alertDialogBuilder.setIcon(R.drawable.error);
                                        alertDialogBuilder.setTitle("Alert");
                                        alertDialogBuilder.setMessage("This driver is not compliance.\nDo you want to continue?")
                                                .setCancelable(false)
                                                .setNegativeButton("Continue",
                                                        new DialogInterface.OnClickListener() {
                                                            @Override
                                                            public void onClick(DialogInterface dialog, int which) {
                                                                dialog.cancel();
                                                                openChecklist(resultStr, finalVehString);
                                                            }
                                                        })
                                                .setPositiveButton("Cancel",
                                                        new DialogInterface.OnClickListener() {
                                                            public void onClick(DialogInterface dialog, int id) {
                                                                dialog.cancel();
                                                            }
                                                        });
                                        AlertDialog alert = alertDialogBuilder.create();
                                        alert.show();
                                    }
                                });
                            }
                        }
                        else{
                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setIcon(R.drawable.error);
                                    alertDialogBuilder.setTitle("Error ");
                                    alertDialogBuilder.setMessage("Internet is not there. Please check your connection.")
                                            .setCancelable(false)
                                            .setPositiveButton("Ok",
                                                    new DialogInterface.OnClickListener() {
                                                        public void onClick(DialogInterface dialog, int id) {
                                                            dialog.cancel();
                                                        }
                                                    });
                                    AlertDialog alert = alertDialogBuilder.create();
                                    alert.show();
                                }
                            });
                        }
                    }
                }
            }
        });
    }

    public void onClick(View v) {
        // capture picture
        //driver_image=(ImageView) findViewById(v.getId());

        if (ContextCompat.checkSelfPermission(VehicleInfo.this,
                Manifest.permission.WRITE_EXTERNAL_STORAGE)
                != PackageManager.PERMISSION_GRANTED) {

            ActivityCompat.requestPermissions(VehicleInfo.this,
                    new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                    MY_PERMISSIONS_REQUEST);
        }
        else if (ContextCompat.checkSelfPermission(VehicleInfo.this,
                Manifest.permission.CAMERA)
                != PackageManager.PERMISSION_GRANTED) {

            ActivityCompat.requestPermissions(VehicleInfo.this,
                    new String[]{Manifest.permission.CAMERA},
                    MY_PERMISSIONS_REQUEST_CAMERA);
        }
        else {
            captureImage();
        }
    }
    private void captureImage() {
        StrictMode.VmPolicy.Builder builder = new StrictMode.VmPolicy.Builder();
        StrictMode.setVmPolicy(builder.build());
        Intent intent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);

        fileUri = getOutputMediaFileUri(1);

        intent.putExtra(MediaStore.EXTRA_OUTPUT, fileUri);

        // start the image capture Intent
        startActivityForResult(intent, CAMERA_CAPTURE_IMAGE_REQUEST_CODE);
    }

    public Uri getOutputMediaFileUri(int type) {
        return Uri.fromFile(getOutputMediaFile(type));
    }

    private static File getOutputMediaFile(int type) {

        // External sdcard location
        File mediaStorageDir = new File(
                Environment
                        .getExternalStoragePublicDirectory(Environment.DIRECTORY_PICTURES),
                "PassengerApp");

        // Create the storage directory if it does not exist
        if (!mediaStorageDir.exists()) {
            if (!mediaStorageDir.mkdirs()) {
                Log.d(VehicleInfo.class.getSimpleName(),"Oops! Failed create "
                        + UrlConfig.IMAGE_DIRECTORY_NAME + " directory");
                return null;
            }
        }

        // Create a media file name
        String timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss",
                Locale.getDefault()).format(new Date());
        File mediaFile;
        if (type == MEDIA_TYPE_IMAGE) {
            mediaFile = new File(mediaStorageDir.getPath() + File.separator
                    + "IMG_" + timeStamp + ".jpg");
        } else {
            return null;
        }

        return mediaFile;
    }

    private void launchUploadActivity(boolean isImage){
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize =2; //1/2 of original size
        //driver_image.setImageBitmap(BitmapFactory.decodeFile(fileUri.getPath(),options));
        dir_path=compressImage( fileUri.getPath());
    }

    public String compressImage(String imageUri) {

        // String filePath = getRealPathFromURI(imageUri);
        String filePath=imageUri;
        Bitmap scaledBitmap = null;

        BitmapFactory.Options options = new BitmapFactory.Options();

//      by setting this field as true, the actual bitmap pixels are not loaded in the memory. Just the bounds are loaded. If
//      you try the use the bitmap here, you will get null.
        options.inJustDecodeBounds = true;
        Bitmap bmp = BitmapFactory.decodeFile(filePath, options);

        int actualHeight = options.outHeight;
        int actualWidth = options.outWidth;

//      max Height and width values of the compressed image is taken as 816x612

        float maxHeight = 816.0f;
        float maxWidth = 612.0f;
        float imgRatio = actualWidth / actualHeight;
        float maxRatio = maxWidth / maxHeight;

//      width and height values are set maintaining the aspect ratio of the image

        if (actualHeight > maxHeight || actualWidth > maxWidth) {
            if (imgRatio < maxRatio) {
                imgRatio = maxHeight / actualHeight;
                actualWidth = (int) (imgRatio * actualWidth);
                actualHeight = (int) maxHeight;
            } else if (imgRatio > maxRatio) {
                imgRatio = maxWidth / actualWidth;
                actualHeight = (int) (imgRatio * actualHeight);
                actualWidth = (int) maxWidth;
            } else {
                actualHeight = (int) maxHeight;
                actualWidth = (int) maxWidth;

            }
        }

//      setting inSampleSize value allows to load a scaled down version of the original image

        options.inSampleSize = calculateInSampleSize(options, actualWidth, actualHeight);

//      inJustDecodeBounds set to false to load the actual bitmap
        options.inJustDecodeBounds = false;

//      this options allow android to claim the bitmap memory if it runs low on memory
        options.inPurgeable = true;
        options.inInputShareable = true;
        options.inTempStorage = new byte[16 * 1024];

        try {
//          load the bitmap from its path
            bmp = BitmapFactory.decodeFile(filePath, options);
        } catch (OutOfMemoryError exception) {
            exception.printStackTrace();

        }
        try {
            scaledBitmap = Bitmap.createBitmap(actualWidth, actualHeight,Bitmap.Config.ARGB_8888);
        } catch (OutOfMemoryError exception) {
            exception.printStackTrace();
        }

        float ratioX = actualWidth / (float) options.outWidth;
        float ratioY = actualHeight / (float) options.outHeight;
        float middleX = actualWidth / 2.0f;
        float middleY = actualHeight / 2.0f;

        Matrix scaleMatrix = new Matrix();
        scaleMatrix.setScale(ratioX, ratioY, middleX, middleY);

        Canvas canvas = new Canvas(scaledBitmap);
        canvas.setMatrix(scaleMatrix);
        canvas.drawBitmap(bmp, middleX - bmp.getWidth() / 2, middleY - bmp.getHeight() / 2, new Paint(Paint.FILTER_BITMAP_FLAG));

//      check the rotation of the image and display it properly
        ExifInterface exif;
        try {
            exif = new ExifInterface(filePath);

            int orientation = exif.getAttributeInt(
                    ExifInterface.TAG_ORIENTATION, 0);
            Log.d("EXIF", "Exif: " + orientation);
            Matrix matrix = new Matrix();
            if (orientation == 6) {
                matrix.postRotate(90);
                Log.d("EXIF", "Exif: " + orientation);
            } else if (orientation == 3) {
                matrix.postRotate(180);
                Log.d("EXIF", "Exif: " + orientation);
            } else if (orientation == 8) {
                matrix.postRotate(270);
                Log.d("EXIF", "Exif: " + orientation);
            }
            scaledBitmap = Bitmap.createBitmap(scaledBitmap, 0, 0,
                    scaledBitmap.getWidth(), scaledBitmap.getHeight(), matrix,
                    true);
        } catch (IOException e) {
            e.printStackTrace();
        }

        FileOutputStream out = null;
        String filename = getFilename();

        try {
            out = new FileOutputStream(filename);
//          write the compressed bitmap at the destination specified by filename.
            scaledBitmap.compress(Bitmap.CompressFormat.JPEG, 91, out);

        } catch (FileNotFoundException e) {
            e.printStackTrace();
        }

        return filename;

    }

    public String getFilename() {
        File file = new File(
                Environment
                        .getExternalStoragePublicDirectory(Environment.DIRECTORY_PICTURES),
                UrlConfig.IMAGE_DIRECTORY_NAME);
        // File file = new File(Environment.getExternalStorageDirectory().getPath(), "MyFolder/Images");
        if (!file.exists()) {
            file.mkdirs();
        }
        String uriSting = (file.getAbsolutePath() + "/Psngr_" + System.currentTimeMillis() + ".jpg");
        return uriSting;

    }

    public int calculateInSampleSize(BitmapFactory.Options options, int reqWidth, int reqHeight) {
        final int height = options.outHeight;
        final int width = options.outWidth;
        int inSampleSize = 1;

        if (height > reqHeight || width > reqWidth) {
            final int heightRatio = Math.round((float) height/ (float) reqHeight);
            final int widthRatio = Math.round((float) width / (float) reqWidth);
            inSampleSize = heightRatio < widthRatio ? heightRatio : widthRatio;      }
        final float totalPixels = width * height;
        final float totalReqPixelsCap = reqWidth * reqHeight * 2;
        while (totalPixels / (inSampleSize * inSampleSize) > totalReqPixelsCap) {
            inSampleSize++;
        }

        return inSampleSize;

    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == CAMERA_CAPTURE_IMAGE_REQUEST_CODE) {
            if (resultCode == RESULT_OK) {

                // successfully captured the image
                // launching upload activity
                launchUploadActivity(true);


            } else if (resultCode == RESULT_CANCELED) {

                // user cancelled Image capture
                Toast.makeText(getApplicationContext(),
                        "User cancelled image capture", Toast.LENGTH_SHORT)
                        .show();

            } else {
                // failed to capture image
                Toast.makeText(getApplicationContext(),
                        "Sorry! Failed to capture image", Toast.LENGTH_SHORT)
                        .show();
            }

        }
    }

    public void uploadfiles()
    {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(isNetworkAvailable()) {
                    final ProgressDialog progressDialog = new ProgressDialog(VehicleInfo.this);
                    progressDialog.setMessage("Loading please wait ...");
                    progressDialog.setCancelable(false);
                    progressDialog.show();
                    Thread splashThread = new Thread() {
                        @Override
                        public void run() {
                            try {
                                int waited = 0;
                                while (waited < 1000) {
                                    sleep(100);
                                    waited += 100;
                                    if (waited == 100) {
                                        if(dir_path!=null) {
                                            String imageName = "Psngr_" + System.currentTimeMillis() + ".jpg";
                                            RequestQueue mVolleyRequestQueue = Volley.newRequestQueue(getApplicationContext());
                                            String res = WebAccessor.getNewInstance().uploadImageService(mVolleyRequestQueue,
                                                    "UploadFile", dir_path, imageName, "c52d1b490c9ac9432532c4ffe7364155b171387c");
                                            try {
                                                if (!res.contains("success")) {
                                                    res = res = WebAccessor.getNewInstance().uploadImageService(mVolleyRequestQueue,
                                                            "UploadFile", dir_path, imageName, "c52d1b490c9ac9432532c4ffe7364155b171387c");
                                                    if (res.contains("success")) {
                                                        // legacy TagIn driver image is not used in PassengerPro flow
                                                    }
                                                }
                                                else{
                                                    // legacy TagIn driver image is not used in PassengerPro flow
                                                }
                                            } catch (Exception e) {

                                            }
                                        }
                                    }
                                }
                            } catch (Exception e) {
                                runOnUiThread(new Runnable() {
                                    public void run() {
                                        Toast.makeText(getApplicationContext(),"Internet not connected or poor connection",Toast.LENGTH_SHORT).show();
                                    }
                                });
                            } finally {
                                progressDialog.dismiss();
                            }
                        }

                    };

                    splashThread.start();
                }
            }
        });
    }

    @Override
    public void onRequestPermissionsResult(int requestCode,String permissions[], int[] grantResults) {
        switch (requestCode) {
            case MY_PERMISSIONS_REQUEST: {
                // If request is cancelled, the result arrays are empty.
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {


                } else {

                    final AlertDialog ad = new AlertDialog.Builder(new ContextThemeWrapper(this,android.R.style.Theme_Holo_Light_Dialog)).create();
                    ad.setTitle("Permission Need");
                    ad.setMessage("STORAGE permission is mandatory to run app.");
                    ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int which) {
                            dialog.dismiss();
                        }
                    });
                    ad.show();
                }
                return;
            }
            case MY_PERMISSIONS_REQUEST_CAMERA: {
                // If request is cancelled, the result arrays are empty.
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {

                } else {

                    final AlertDialog ad = new AlertDialog.Builder(this).create();
                    ad.setTitle("Permission Need");
                    ad.setMessage("Camera permission is mandatory to run app.");
                    ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int which) {
                            dialog.dismiss();
                        }
                    });
                    ad.show();
                }
                return;
            }

            // other 'case' lines to check for other
            // permissions this app might request
        }
    }

    public  void openChecklist(final String vehicleWithDriver, final String vehicle){
        runOnUiThread(new Runnable() {
            @Override
            public void run() {

                if(isNetworkAvailable()) {
                    if (ContextCompat.checkSelfPermission(getApplicationContext(),
                            Manifest.permission.READ_PHONE_STATE)
                            != PackageManager.PERMISSION_GRANTED) {
                        ActivityCompat.requestPermissions(VehicleInfo.this,
                                new String[]{Manifest.permission.READ_PHONE_STATE},
                                MY_PERMISSIONS_REQUEST_READ_CONTACTS);
                    } else if (ContextCompat.checkSelfPermission(getApplicationContext(),
                            Manifest.permission.ACCESS_FINE_LOCATION)
                            != PackageManager.PERMISSION_GRANTED) {
                        ActivityCompat.requestPermissions(VehicleInfo.this,
                                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                MY_PERMISSIONS_REQUEST_READ_LOCATION);
                    } else {
                        GPSTracker gpsTracker = new GPSTracker(getApplicationContext());
                        final String latlng = gpsTracker.getLocation();
                        final Thread thread1 = new Thread(new Runnable() {
                            @Override
                            public void run() {
                                String psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        dialog = ProgressDialog.show(VehicleInfo.this, "", "Loading...", true);
                                    }
                                });
                                final String result = webServices.VehicleMobileGPSCheck(vehicle, psngrId, latlng.split(",")[0], latlng.split(",")[1]);
                                dialog.dismiss();
                                if (result.contains("Allow")) {
                                    uploadfiles();
                                    Intent i = new Intent(getApplicationContext(), QRScannerActivity.class);
                                    i.putExtra("vehicleWithDriver", vehicleWithDriver+"@#"+result.split("@&@")[2]+"@#"+"Allowed");
                                    i.putExtra("towerName",twrautoTxt.getText().toString());
                                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                    startActivity(i);
                                } else if (result.contains("Block")) {
                                    runOnUiThread(new Runnable() {
                                        public void run() {
                                            LayoutInflater li = LayoutInflater.from(VehicleInfo.this);
                                            View promptsView = li.inflate(R.layout.vehgeofence_reason, null);
                                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                    new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                                            alertDialogBuilder.setView(promptsView);

                                            final EditText userInput = (EditText) promptsView
                                                    .findViewById(R.id.reason);
                                            final Button button = (Button) promptsView.findViewById(R.id.btn);
                                            final Button buttonCancel = (Button) promptsView.findViewById(R.id.btncancel);
                                            alertDialogBuilder.setTitle("Confirm");
                                            alertDialogBuilder.setMessage(result.split("@&@")[1])
                                                    .setCancelable(false);
                                            final AlertDialog alert = alertDialogBuilder.create();
                                            buttonCancel.setOnClickListener(new View.OnClickListener() {
                                                @Override
                                                public void onClick(View v) {
                                                    alert.dismiss();
                                                }
                                            });
                                            button.setOnClickListener(new View.OnClickListener() {
                                                @Override
                                                public void onClick(View v) {
                                                    if (userInput.getText().toString().trim().length() > 0) {
                                                        final String reason=userInput.getText().toString();
                                                        new Thread(new Runnable() {
                                                            @Override
                                                            public void run() {
                                                                try {
                                                                    uploadfiles();
                                                                    Intent i = new Intent(getApplicationContext(), QRScannerActivity.class);
                                                                    i.putExtra("vehicleWithDriver", vehicleWithDriver+"@#"+result.split("@&@")[2]+"@#"+reason);
                                                                    i.putExtra("towerName",twrautoTxt.getText().toString());
                                                                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                                    startActivity(i);
                                                                } catch (Exception e) {
                                                                    e.printStackTrace();
                                                                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                                                                }
                                                            }
                                                        }).start();
                                                    } else {
                                                        Toast.makeText(VehicleInfo.this, "Please enter reason for selecting unavailable vehicle in geofence", Toast.LENGTH_SHORT).show();
                                                    }
                                                }
                                            });

                                            alert.show();
                                        }
                                    });
                                } else {
                                    dialog.dismiss();
                                    runOnUiThread(new Runnable() {
                                        @Override
                                        public void run() {
                                            AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(VehicleInfo.this);
                                            alertDialogBuilder.setMessage("OOPs, Something went wrong. Try again.")
                                                    .setCancelable(false)
                                                    .setPositiveButton("Ok",
                                                            new DialogInterface.OnClickListener() {
                                                                public void onClick(DialogInterface dialog, int id) {
                                                                    dialog.cancel();
                                                                }
                                                            });
                                            AlertDialog alert = alertDialogBuilder.create();
                                            alert.show();
                                        }
                                    });
                                }
                            }
                        });
                        if (!latlng.equals("0.0,0.0")) {
                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    if (accountid.equals(UrlConfig.tata_accountid)) {
                                        new Thread(new Runnable() {
                                            @Override
                                            public void run() {
                                                String towerValid = webServices.CheckPsngrTowerLocation(mobileno, twrautoTxt.getText().toString());
                                                if (towerValid.equals("false")) {
                                                    dialog.dismiss();
                                                    runOnUiThread(new Runnable() {
                                                        @Override
                                                        public void run() {
                                                            Toast.makeText(getApplicationContext(), "Invalid Tower Location", Toast.LENGTH_SHORT).show();
                                                        }
                                                    });
                                                } else
                                                    thread1.start();
                                            }
                                        }).start();
                                    }
                                    else
                                        thread1.start();
                                }
                            });
                        }
                        else {
                            dialog.dismiss();
                            Toast.makeText(getApplicationContext(), "GPS is not fixed. Please try again", Toast.LENGTH_SHORT).show();
                        }
                    }
                }
                else{
                    runOnUiThread(new Runnable() {
                        public void run() {
                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                    new ContextThemeWrapper(getApplicationContext(), android.R.style.Theme_Holo_Light_Dialog));
                            alertDialogBuilder.setIcon(R.drawable.error);
                            alertDialogBuilder.setTitle("Error ");
                            alertDialogBuilder.setMessage("Internet is not there. Please check your connection.")
                                    .setCancelable(false)
                                    .setPositiveButton("Ok",
                                            new DialogInterface.OnClickListener() {
                                                public void onClick(DialogInterface dialog, int id) {
                                                    dialog.cancel();
                                                }
                                            });
                            AlertDialog alert = alertDialogBuilder.create();
                            alert.show();
                        }
                    });
                }
            }
        });
    }

    private boolean isNetworkAvailable() {
        try {
            ConnectivityManager connectivityManager
                    = (ConnectivityManager) getApplicationContext().getSystemService(Context.CONNECTIVITY_SERVICE);
            NetworkInfo activeNetworkInfo = connectivityManager.getActiveNetworkInfo();
            return activeNetworkInfo != null && activeNetworkInfo.isConnected();
        } catch (Exception e) {
            return true;
        }
    }

    @Override
    public void onBackPressed() {
        Intent intent = new Intent(Intent.ACTION_MAIN);
        intent.addCategory(Intent.CATEGORY_HOME);
        intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        startActivity(intent);
        finish();
        System.exit(0);
    }

    @Override
    public boolean onPrepareOptionsMenu(final Menu menu) {
        getMenuInflater().inflate(R.menu.menu, menu);
        MenuItem item = menu.findItem(R.id.menu_panic);
        item.setVisible(false);

        if(accountid.equals(UrlConfig.tata_accountid)) {
            item = menu.findItem(R.id.menu_tracking);
            item.setVisible(true);
        }
        return super.onCreateOptionsMenu(menu);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case R.id.menu_logout:
                runOnUiThread(new Runnable() {
                    public void run() {
                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                        alertDialogBuilder.setIcon(R.drawable.error);
                        alertDialogBuilder.setTitle("Action ");
                        alertDialogBuilder.setMessage("Are you sure?")
                                .setCancelable(false)
                                .setPositiveButton("Cancel",
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                dialog.cancel();
                                            }
                                        })
                                .setNeutralButton("Logout",
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                dialog.cancel();
                                                appConstants.putShrdPrefValWithKey(getApplicationContext(),"passengerinfo",null);
                                                Intent i = new Intent(getApplicationContext(), LoginActivity.class);
                                                i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                startActivity(i);
                                            }
                                        })
                                .setNegativeButton("Exit",
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                dialog.cancel();
                                                Intent intent = new Intent(Intent.ACTION_MAIN);
                                                intent.addCategory(Intent.CATEGORY_HOME);
                                                intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                                                startActivity(intent);
                                                finish();
                                                System.exit(0);
                                            }
                                        });
                        AlertDialog alert = alertDialogBuilder.create();
                        alert.show();
                    }
                });
                break;
            case R.id.menu_refresh:
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        finish();
                        startActivity(getIntent());
                    }
                });
                break;
            case R.id.menu_tracking:
                if(autoCompleteVehTextView.getText().toString().length()==0){
                    Toast.makeText(getApplicationContext(),"Please Select vehicleid",Toast.LENGTH_SHORT).show();
                }
                else if(autoCompleteVehTextView.getText().toString().toUpperCase().equals("OTHER")) {
                    Toast.makeText(getApplicationContext(), "For other vehicles, tracking won't work", Toast.LENGTH_SHORT).show();
                }
                else if(!result.toUpperCase().contains(autoCompleteVehTextView.getText().toString().toUpperCase())){
                    Toast.makeText(getApplicationContext(),"Invalid vehicleid, please select proper vehicleid",Toast.LENGTH_SHORT).show();
                }
                else {
                    String[] str = result.toUpperCase().split(autoCompleteVehTextView.getText().toString().toUpperCase());
                    if(str.length>1) {
                        boolean invalidVehicle=true;
                        for(int s=0;s<str.length-1;s++){
                            String last = str[s].substring(str[s].length() - 1);
                            String first = str[s+1].substring(0, 1);
                            if (last.indexOf('"') != -1 && first.indexOf('"') != -1) {
                                invalidVehicle=false;
                                break;
                            }
                        }
                        if(invalidVehicle) {
                            Toast.makeText(getApplicationContext(), "Invalid vehicleid, please select proper vehicle", Toast.LENGTH_SHORT).show();
                        }
                        else{
                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    Intent i = new Intent(getApplicationContext(), TrackOnMapWithSelection.class);
                                    i.putExtra("vehicleid", autoCompleteVehTextView.getText().toString());
                                    i.putExtra("sessionid", "44b1f48a32b4f5d38f30f6ec4edd18664b0ae63f");
                                    startActivity(i);
                                }
                            });
                        }
                    }
                    else{
                        Toast.makeText(getApplicationContext(), "Invalid vehicleid, please select proper vehicle", Toast.LENGTH_SHORT).show();
                    }
                }
                break;
        }
        return super.onOptionsItemSelected(item);
    }

    private void showUpdateAlert(final int priority,final int stableVersion)
    {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(BuildConfig.VERSION_CODE<stableVersion)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setTitle("You are using old version");
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
                else if(priority==1)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setTitle("New Version available");
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Cancel",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            dialog.cancel();
                                        }
                                    })
                            .setNegativeButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
                else if(priority==2)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(VehicleInfo.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setTitle("New Version available");
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
            }
        });

    }
}
