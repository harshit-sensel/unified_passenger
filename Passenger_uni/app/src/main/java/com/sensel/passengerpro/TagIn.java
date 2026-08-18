package com.sensel.passengerpro;

import android.Manifest;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.ContentResolver;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.os.StrictMode;
import android.provider.MediaStore;
import android.provider.Settings;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import androidx.core.content.FileProvider;
import android.view.MenuItem;

import android.telephony.TelephonyManager;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ListView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Map;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Bundle;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;

import androidx.appcompat.app.AppCompatActivity;

/**
 * Created by Vamsi on 14-Oct-17.
 */

public class TagIn extends AppCompatActivity {
    AppConstants appConstants=new AppConstants();
    String vehicle;
    String towername;
    TextView vehicleid;
    TextView drivername;
    TextView license;
    String Name="";
    String LicenceNo="";
    String psngrId="";
    String mobileno="";
    String[] ruleIds;
    String[] rules;
    String[] ruleTypes;
    EditText wfm;
    EditText ptw;
    Spinner wfmTask;
    String chosenWfmTask = "";
    Button btn_tagin_ref;
    TextView txt_checklist_status_ref;
    String passengerinfo;
    ProgressDialog progressDialog;
    String[] validateRules;
    String[] resultFromPrev;
    public static String driverImage="";
    private static final int MY_PERMISSIONS_REQUEST_READ_CONTACTS = 100;
    private static final int MY_PERMISSIONS_REQUEST_READ_LOCATION = 101;
    private static final int MY_PERMISSIONS_REQUEST_CAMERA = 102;

    private static final int CAMERA_CAPTURE_IMAGE_REQUEST_CODE = 100;
    private static final int MY_PERMISSIONS_REQUEST_STORAGE = 103;
    private static final int CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE = 105;
    String latlng="0,0";
    int failedLogCount=0;
    WebServices webServices=new WebServices();
    String OMR="";

    static Uri fileUri;
    public static final int MEDIA_TYPE_IMAGE = 1;
    private OnBackInvokedCallback backInvokedCallback;


    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            backInvokedCallback = this::handleOnBackPressed;
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(
                    OnBackInvokedDispatcher.PRIORITY_DEFAULT,
                    backInvokedCallback
            );
        }
        setContentView(R.layout.activity_tagin);
        ActionBar actionBar = getSupportActionBar();
        if (actionBar != null) {
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }
        resultFromPrev=getIntent().getStringExtra("vehicleWithDriver").split("@#");
        vehicle=resultFromPrev[0];
        towername=getIntent().getStringExtra("towerName");
        final View headerView = getLayoutInflater().inflate(R.layout.chklist_header, null);
        vehicleid=(TextView) headerView.findViewById(R.id.vehicleid);
        drivername=(TextView) headerView.findViewById(R.id.drivername);
        license=(TextView) headerView.findViewById(R.id.license);
        wfm=(EditText) headerView.findViewById(R.id.wfm);
        ptw=(EditText) headerView.findViewById(R.id.ptw);
        wfmTask=(Spinner) headerView.findViewById(R.id.wfmTask);
        vehicleid.setText(vehicle);

        //Added By Madhuri 12-12-24
        // Display captured images

        displayCapturedImages();

        progressDialog=ProgressDialog.show(TagIn.this, "", "Loading...", true);
        passengerinfo = appConstants.getShrdPrefValByKey(getApplicationContext(),"passengerinfo");
        new Thread(new Runnable() {
            @Override
            public void run() {
                try{
                    if(passengerinfo==null){
                        Intent i = new Intent(getApplicationContext(), LoginActivity.class);
                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        startActivity(i);
                    }
                    else{
                        if(isNetworkAvailable()){
                            String dropDwn = webServices.GetDropDownForApp("com.sensel.passenger");
                            ArrayList options = new ArrayList();
                            options.add("Select");
                            if (dropDwn != null && dropDwn.startsWith("[")) {
                                JSONArray jArr = new JSONArray(dropDwn);
                                for (int j = 0; j < jArr.length(); j++) {
                                    JSONObject data = jArr.getJSONObject(j);
                                    if (data.has("DropDown") && data.getString("DropDown").trim().length() > 0) {
                                        options.add(data.getString("DropDown"));
                                    }
                                }
                            }
                            if (options.size() == 1) {
                                options.add("NA");
                            }
                            final ArrayAdapter adapter = new ArrayAdapter(getApplicationContext(), R.layout.spinner, options);
                            adapter.setDropDownViewResource(R.layout.spinner_dropdown_item);
                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    wfmTask.setAdapter(adapter);
                                    wfmTask.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
                                        @Override
                                        public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                                            if (parent != null && parent.getItemAtPosition(position) != null) {
                                                String item = parent.getItemAtPosition(position).toString().trim();
                                                chosenWfmTask = "Select".equalsIgnoreCase(item) ? "" : item;
                                            }
                                            checkTagInFormValidation();
                                        }

                                        @Override
                                        public void onNothingSelected(AdapterView<?> parent) {
                                            chosenWfmTask = "";
                                            checkTagInFormValidation();
                                        }
                                    });
                                }
                            });
                            String result=resultFromPrev[1];
                            String[] driveNameLic = (result != null && result.contains(":-")) ? result.split(":-") : new String[]{result != null ? result : "Assigned Driver", ""};
                            Name = driveNameLic[0];
                            LicenceNo = driveNameLic.length > 1 ? driveNameLic[1] : "";
                            license.setText(LicenceNo);
                            drivername.setText(Name);
                            mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(),"passengerinfo","MobileNo");
                            psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(),"passengerinfo","PsngrId");
                            result = webServices.GetPsngrInfoWithValidation(mobileno, "CheckList");
                            if(result.contains("PsngrChkId")) {
                                String ruleidslist = "";
                                String ruleslist = "";
                                String rulestypes = "";
                                JSONArray jArr = new JSONArray(result);
                                for (int j = 0; j < jArr.length(); j++) {
                                    JSONObject data = jArr.getJSONObject(j);
                                    if (data.getString("PsngrChkId").trim().length() > 0) {
                                        if (jArr.length() - 1 != j)
                                            ruleidslist = ruleidslist + data.getString("PsngrChkId") + "#";
                                        else
                                            ruleidslist = ruleidslist + data.getString("PsngrChkId");
                                    }
                                    if (data.getString("ChkName").trim().length() > 0) {
                                        if (jArr.length() - 1 != j)
                                            ruleslist = ruleslist + data.getString("ChkName") + "#";
                                        else
                                            ruleslist = ruleslist + data.getString("ChkName");
                                    }
                                    if (data.getString("Type").trim().length() > 0) {
                                        if (jArr.length() - 1 != j)
                                            rulestypes = rulestypes + data.getString("Type") + "#";
                                        else
                                            rulestypes = rulestypes + data.getString("Type");
                                    }
                                }
                                ruleIds = ruleidslist.split("\\#");
                                rules = ruleslist.split("\\#");
                                ruleTypes = rulestypes.split("\\#");
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        //CheckListDesign adapter = new CheckListDesign(TagIn.this, rules, ruleIds,ruleTypes);
                                        CheckListDesign adapter = new CheckListDesign(TagIn.this, rules, ruleIds,ruleTypes);
                                        View footerView = getLayoutInflater().inflate(R.layout.activity_button, null);
                                        ListView list = (ListView) findViewById(R.id.listRules);
                                         btn_tagin_ref = (Button) footerView.findViewById(R.id.btn_tagin);
                                         txt_checklist_status_ref = (TextView) footerView.findViewById(R.id.txt_checklist_status);
                                         final Button btn_tagin = btn_tagin_ref;
                                         final TextView txt_checklist_status = txt_checklist_status_ref;

                                         checkTagInFormValidation();

                                         btn_tagin.setOnClickListener(new View.OnClickListener() {
                                             @Override
                                             public void onClick(View arg0) {
                                                 try {
                                                      String taskFromSpinner = (wfmTask != null && wfmTask.getSelectedItem() != null) ? wfmTask.getSelectedItem().toString().trim() : "";
                                                      if ("Select".equalsIgnoreCase(taskFromSpinner)) taskFromSpinner = "";

                                                      final String capturedWfmTask = (chosenWfmTask != null && !chosenWfmTask.isEmpty()) ? chosenWfmTask : taskFromSpinner;
                                                      final String capturedWfmId = (wfm != null && wfm.getText() != null) ? wfm.getText().toString().trim() : "";
                                                      final String capturedPtw = (ptw != null && ptw.getText() != null) ? ptw.getText().toString().trim() : "";

                                                     new Thread(new Runnable() {
                                                         @Override
                                                         public void run() {
                                                             validateRules = CheckListDesign.strRules;
                                                             boolean validation = true;
                                                             if (capturedWfmTask.isEmpty()) {
                                                                 validation = false;
                                                             }
                                                             else {
                                                                 for (int i = 0; i < validateRules.length; i++) {
                                                                     if (validateRules[i] == null && ruleTypes[i].equals("Radio")) {
                                                                         validation = false;
                                                                         break;
                                                                     } else if ((validateRules[i] == null || validateRules[i].trim().isEmpty() || "No Configuration".equalsIgnoreCase(validateRules[i])) && ruleTypes[i].equals("FileUpload")) {
                                                                         validation = false;
                                                                         break;
                                                                     } else if (validateRules[i] == null && ruleTypes[i].equals("Text"))
                                                                         validateRules[i] = " ";
                                                                 }
                                                            }
                                                            if (validation) {
                                                                runOnUiThread(new Runnable() {
                                                                    public void run() {
                                                                        LayoutInflater li = LayoutInflater.from(TagIn.this);
                                                                        View promptsView = li.inflate(R.layout.odometer_prompt, null);
                                                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                                new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
                                                                        alertDialogBuilder.setView(promptsView);

                                                                        final EditText userInput = (EditText) promptsView
                                                                                .findViewById(R.id.odo);
                                                                        final Button button = (Button) promptsView.findViewById(R.id.btn);
                                                                        final Button buttonCancel = (Button) promptsView.findViewById(R.id.btncancel);
                                                                        alertDialogBuilder.setTitle("Tag In");
                                                                        alertDialogBuilder.setMessage("Are You Sure?\n\nVehicle - " + vehicle)
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
                                                                                    OMR=userInput.getText().toString();
                                                                                    new Thread(new Runnable() {
                                                                                        @Override
                                                                                        public void run() {
                                                                                            try {
                                                                                                if (isNetworkAvailable()) {
                                                                                                    if (ContextCompat.checkSelfPermission(TagIn.this,
                                                                                                            Manifest.permission.READ_PHONE_STATE)
                                                                                                            != PackageManager.PERMISSION_GRANTED) {
                                                                                                        ActivityCompat.requestPermissions(TagIn.this,
                                                                                                                new String[]{Manifest.permission.READ_PHONE_STATE},
                                                                                                                MY_PERMISSIONS_REQUEST_READ_CONTACTS);
                                                                                                    } else if (ContextCompat.checkSelfPermission(TagIn.this,
                                                                                                            Manifest.permission.ACCESS_FINE_LOCATION)
                                                                                                            != PackageManager.PERMISSION_GRANTED) {
                                                                                                        ActivityCompat.requestPermissions(TagIn.this,
                                                                                                                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                                                                                                MY_PERMISSIONS_REQUEST_READ_LOCATION);
                                                                                                    }else if (ContextCompat.checkSelfPermission(TagIn.this,
                                                                                                            Manifest.permission.CAMERA)
                                                                                                            != PackageManager.PERMISSION_GRANTED) {

                                                                                                        ActivityCompat.requestPermissions(TagIn.this,
                                                                                                                new String[]{Manifest.permission.CAMERA},
                                                                                                                MY_PERMISSIONS_REQUEST_CAMERA);
                                                                                                    } else if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU &&
                                                                                                            ContextCompat.checkSelfPermission(TagIn.this,
                                                                                                                    Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                                                                                                        // Only request WRITE_EXTERNAL_STORAGE for Android versions below 13
                                                                                                        ActivityCompat.requestPermissions(TagIn.this,
                                                                                                                new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                                                                                                                MY_PERMISSIONS_REQUEST_STORAGE);
                                                                                                    }
                                                                                                    else
                                                                                                        InsertTaging(capturedWfmTask, capturedWfmId, capturedPtw);
                                                                                                } else {
                                                                                                    runOnUiThread(new Runnable() {
                                                                                                        public void run() {
                                                                                                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                                                                    new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
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
                                                                                                errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn(" + new Exception().getStackTrace()[0].getLineNumber() + ")-" + mobileno);
                                                                                            }
                                                                                        }
                                                                                    }).start();
                                                                                } else {
                                                                                    Toast.makeText(TagIn.this, "Please enter odometer reading", Toast.LENGTH_SHORT).show();
                                                                                }
                                                                            }
                                                                        });

                                                                        alert.show();
                                                                    }
                                                                });
                                                            } else {
                                                                runOnUiThread(new Runnable() {
                                                                    @Override
                                                                    public void run() {
                                                                        Toast.makeText(TagIn.this, "Please complete all rules and capture required photos", Toast.LENGTH_SHORT).show();
                                                                    }
                                                                });
                                                            }
                                                        }
                                                    }).start();
                                                }
                                                catch (Exception e){
                                                    e.printStackTrace();
                                                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                }
                                            }
                                        });
                                        list.addFooterView(footerView,null,false);
                                        list.addHeaderView(headerView,null,false);
                                        list.setAdapter(adapter);
                                        // Set the image update listener
                                        CheckListDesign.imageUpdateListener = new CheckListDesign.ImageUpdateListener() {
                                            @Override
                                            public void onImageUpdated(int position) {
                                                // Refresh the specific row in the ListView
                                                adapter.notifyDataSetChanged();
                                            }
                                        };
                                    }
                                });
                            }
                            else{
                                runOnUiThread(new Runnable() {
                                    public void run() {
                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
                                        alertDialogBuilder.setIcon(R.drawable.error);
                                        alertDialogBuilder.setTitle("Error ");
                                        alertDialogBuilder.setMessage("Connectivity issue. Please try again.")
                                                .setCancelable(false)
                                                .setPositiveButton("Ok",
                                                        new DialogInterface.OnClickListener() {
                                                            public void onClick(DialogInterface dialog, int id) {
                                                                dialog.cancel();
                                                                Intent i = new Intent(getApplicationContext(), VehicleInfo.class);
                                                                i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                                startActivity(i);
                                                            }
                                                        });
                                        AlertDialog alert = alertDialogBuilder.create();
                                        alert.show();
                                    }
                                });
                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                errorRecordSendMail.errorrecordSendMail(result.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno+"-GetPsngrInfoWithValidation("+mobileno+", \"CheckList\")");
                            }
                        }
                        else{
                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
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
                catch (Exception e){
                    e.printStackTrace();
                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                    errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                }
                finally {
                    progressDialog.dismiss();
                }
            }
        }).start();
        if (Build.VERSION.SDK_INT >= 23) {
            if (ContextCompat.checkSelfPermission(TagIn.this,
                    Manifest.permission.WRITE_EXTERNAL_STORAGE)
                    != PackageManager.PERMISSION_GRANTED) {

                ActivityCompat.requestPermissions(TagIn.this,
                        new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                        MY_PERMISSIONS_REQUEST_STORAGE);
            }
            else if (ContextCompat.checkSelfPermission(TagIn.this,
                    Manifest.permission.CAMERA)
                    != PackageManager.PERMISSION_GRANTED) {

                ActivityCompat.requestPermissions(TagIn.this,
                        new String[]{Manifest.permission.CAMERA},
                        MY_PERMISSIONS_REQUEST_CAMERA);
            }
            // isCameraEnabled =  handlePermissions(Manifest.permission.CAMERA, MY_PERMISSIONS_REQUEST_CAMERA  );
        }
        if (Build.VERSION.SDK_INT <= 32) {
            if (ContextCompat.checkSelfPermission(TagIn.this,
                    Manifest.permission.CAMERA)
                    != PackageManager.PERMISSION_GRANTED) {

                ActivityCompat.requestPermissions(TagIn.this,
                        new String[]{Manifest.permission.CAMERA},
                        MY_PERMISSIONS_REQUEST_CAMERA);
            } else if (ContextCompat.checkSelfPermission(TagIn.this,
                    Manifest.permission.WRITE_EXTERNAL_STORAGE)
                    != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(TagIn.this,
                        new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                        MY_PERMISSIONS_REQUEST_STORAGE);
            } else if (ContextCompat.checkSelfPermission(TagIn.this,
                    Manifest.permission.READ_EXTERNAL_STORAGE)
                    != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(TagIn.this,
                        new String[]{Manifest.permission.READ_EXTERNAL_STORAGE},
                        MY_PERMISSIONS_REQUEST_STORAGE);
            } /*else {
                        //captureImage();
                        dispatchTakePictureIntent();
                    }*/
        }
        else{
            if (ContextCompat.checkSelfPermission(TagIn.this,
                    Manifest.permission.CAMERA)
                    != PackageManager.PERMISSION_GRANTED) {

                ActivityCompat.requestPermissions(TagIn.this,
                        new String[]{Manifest.permission.CAMERA},
                        MY_PERMISSIONS_REQUEST_CAMERA);
            } /*else {
                        //captureImage();
                        dispatchTakePictureIntent();
                    }
*/
        }
    }
    public void displayCapturedImages() {
        LinearLayout imageContainer = findViewById(R.id.image_container); // Make sure this is defined in your XML
        imageContainer.removeAllViews(); // Clear existing images

        for (Map.Entry<Integer, String> entry : CheckListDesign.imagePaths.entrySet()) {
            String imagePath = entry.getValue();
            Bitmap bitmap = BitmapFactory.decodeFile(imagePath);

            ImageView imageView = new ImageView(this);
            imageView.setImageBitmap(bitmap);
            imageView.setPadding(8, 8, 8, 8);

            imageContainer.addView(imageView);
        }
    }
    public void InsertTaging() {
        String taskStr = (wfmTask != null && wfmTask.getSelectedItem() != null) ? wfmTask.getSelectedItem().toString().trim() : "";
        if ("Select".equalsIgnoreCase(taskStr)) taskStr = "";
        String wfmIdStr = (wfm != null && wfm.getText() != null) ? wfm.getText().toString().trim() : "";
        String ptwStr = (ptw != null && ptw.getText() != null) ? ptw.getText().toString().trim() : "";
        InsertTaging(taskStr, wfmIdStr, ptwStr);
    }

    public void InsertTaging(final String taskStr, final String wfmIdStr, final String ptwStr) {
        try {
            LocationManager locationManager = (LocationManager) getApplicationContext().getSystemService(Context.LOCATION_SERVICE);
            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)){
                final Thread thread=new Thread(new Runnable() {
                    @Override
                    public void run() {
                        runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                progressDialog = ProgressDialog.show(TagIn.this, "", "Loading...", true);
                            }
                        });
                        String strRules = "";
                        for (int i = 0; i < validateRules.length; i++) {
                            strRules = strRules + ruleIds[i] + "|" + validateRules[i];
                            if (i != validateRules.length - 1)
                                strRules = strRules + "@#";
                        }
                        TelephonyManager telephonyManager = (TelephonyManager) getSystemService(Context.TELEPHONY_SERVICE);
                        String Imei = getIMEI();
                        String[] str=latlng.split(",");
                        String driverDetail="";
                        if(resultFromPrev[2].equals("0"))
                            driverDetail=resultFromPrev[1].split(":-")[0];

                        String vehiclePhoto = "";
                        String taginOdometerPhoto = "";
                        String driverPhoto = "";

                        if (CheckListDesign.imagePaths != null) {
                            for (Map.Entry<Integer, String> entry : CheckListDesign.imagePaths.entrySet()) {
                                int pos = entry.getKey();
                                String fullPath = entry.getValue();
                                if (fullPath != null && !fullPath.isEmpty()) {
                                    String fname = fullPath.substring(fullPath.lastIndexOf('/') + 1);
                                    String chkId = (ruleIds != null && pos < ruleIds.length) ? ruleIds[pos] : "";
                                    String chkName = (rules != null && pos < rules.length) ? rules[pos].toLowerCase() : "";

                                    if ("16".equals(chkId) || chkName.contains("vehicle")) {
                                        vehiclePhoto = fname;
                                    } else if ("17".equals(chkId) || chkName.contains("driver")) {
                                        driverPhoto = fname;
                                    } else if ("18".equals(chkId) || chkName.contains("odometer")) {
                                        taginOdometerPhoto = fname;
                                    } else {
                                        if (vehiclePhoto.isEmpty()) {
                                            vehiclePhoto = fname;
                                        } else if (taginOdometerPhoto.isEmpty()) {
                                            taginOdometerPhoto = fname;
                                        } else if (driverPhoto.isEmpty()) {
                                            driverPhoto = fname;
                                        }
                                    }
                                }
                            }
                        }

                        final String result = webServices.InsertPsngrChecklist(psngrId, vehicle, "TagIn", strRules,
                                wfmIdStr+"@&"+taskStr, ptwStr,
                                resultFromPrev[2],Imei,str[0],str[1],resultFromPrev[3],driverDetail,OMR,resultFromPrev[4],resultFromPrev[5],driverPhoto,towername,vehiclePhoto,taginOdometerPhoto,"");
                        if (result.contains("Inserted Successfully")) {
                            // Upload captured images to backend server & delete local temporary files from Android storage
                            if (CheckListDesign.imagePaths != null) {
                                for (Map.Entry<Integer, String> entry : CheckListDesign.imagePaths.entrySet()) {
                                    String fullPath = entry.getValue();
                                    if (fullPath != null && !fullPath.isEmpty()) {
                                        try {
                                            String fname = fullPath.substring(fullPath.lastIndexOf('/') + 1);
                                            FileUpload fileUpload = new FileUpload();
                                            String compressedPath = fileUpload.compressImage(fullPath);
                                            String targetPath = (compressedPath != null && !compressedPath.isEmpty()) ? compressedPath : fullPath;
                                            fileUpload.uploadFileWithName(targetPath, fname);
                                            
                                            // Cleanup local temporary image files
                                            File originalFile = new File(fullPath);
                                            if (originalFile.exists()) {
                                                originalFile.delete();
                                            }
                                            if (compressedPath != null) {
                                                File compFile = new File(compressedPath);
                                                if (compFile.exists()) {
                                                    compFile.delete();
                                                }
                                            }
                                        } catch (Exception ex) {
                                            Log.e("TagIn", "Error uploading/deleting captured photo: " + fullPath, ex);
                                        }
                                    }
                                }
                            }
                            if (CheckListDesign.imagePaths != null) {
                                CheckListDesign.imagePaths.clear();
                            }

                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setTitle("Status");
                                    alertDialogBuilder.setMessage("TagIn Done Successfully")
                                            .setPositiveButton("Ok",
                                                    new DialogInterface.OnClickListener() {
                                                        public void onClick(DialogInterface dialog, int id) {
                                                            dialog.cancel();
                                                            progressDialog = ProgressDialog.show(TagIn.this, "", "Loading...", true);
                                                            new Thread(new Runnable() {
                                                                @Override
                                                                public void run() {
                                                                    try {
                                                                        Intent i = new Intent(getApplicationContext(), LoginActivity.class);
                                                                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                                        startActivity(i);
                                                                    } catch (Exception e) {
                                                                        e.printStackTrace();
                                                                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                                    } finally {
                                                                        progressDialog.dismiss();
                                                                    }
                                                                }
                                                            }).start();
                                                        }
                                                    });
                                    AlertDialog alert = alertDialogBuilder.create();
                                    alert.show();
                                }
                            });
                        }
                        else if(result.contains("PsngrMessage-")){
                            progressDialog.dismiss();
                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setTitle("Status");
                                    alertDialogBuilder.setMessage(result.replace("PsngrMessage-",""))
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
                        else {
                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(result.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno + "-InsertPsngrChecklistForPsngr("+psngrId+", "+vehicle+", \"TagIn\", "+strRules+", "+wfm.getText().toString()+", "+ptw.getText().toString()+","+resultFromPrev[2]+")");
                            failedLogCount++;
                            if(failedLogCount>5) {
                                failedLogCount=0;
                                runOnUiThread(new Runnable() {
                                    public void run() {
                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
                                        alertDialogBuilder.setTitle("Status");
                                        alertDialogBuilder.setMessage("TagIn is Failed")
                                                .setPositiveButton("Ok",
                                                        new DialogInterface.OnClickListener() {
                                                            public void onClick(DialogInterface dialog, int id) {
                                                                dialog.cancel();
                                                                progressDialog = ProgressDialog.show(TagIn.this, "", "Loading...", true);
                                                                new Thread(new Runnable() {
                                                                    @Override
                                                                    public void run() {
                                                                        try {
                                                                            Intent i = new Intent(getApplicationContext(), LoginActivity.class);
                                                                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                                            startActivity(i);
                                                                        } catch (Exception e) {
                                                                            e.printStackTrace();
                                                                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                                        } finally {
                                                                            progressDialog.dismiss();
                                                                        }
                                                                    }
                                                                }).start();
                                                            }
                                                        });
                                        AlertDialog alert = alertDialogBuilder.create();
                                        alert.show();
                                    }
                                });
                            }
                            else
                                InsertTaging();
                        }
                    }
                });
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        GPSTracker gpsTracker = new GPSTracker(getApplicationContext());
                        latlng = gpsTracker.getLocation();
                        thread.start();
                    }
                });
            }
            else {
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(TagIn.this);
                        alertDialogBuilder.setMessage("GPS is disabled in your device. Would you like to enable it?")
                                .setCancelable(false)
                                .setNegativeButton("Goto Settings",
                                        new DialogInterface.OnClickListener(){
                                            public void onClick(DialogInterface dialog, int id){
                                                Intent callGPSSettingIntent = new Intent(
                                                        android.provider.Settings.ACTION_LOCATION_SOURCE_SETTINGS);
                                                startActivity(callGPSSettingIntent);
                                            }
                                        })
                                .setPositiveButton("Cancel",
                                        new DialogInterface.OnClickListener(){
                                            public void onClick(DialogInterface dialog, int id){
                                                dialog.cancel();
                                                final AlertDialog ad=new AlertDialog.Builder(TagIn.this).create();
                                                ad.setTitle("Permission Need");
                                                ad.setMessage("GPS Location is mandatory to TagIn.");
                                                ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                                                    public void onClick(DialogInterface dialog, int which) {
                                                        dialog.dismiss();
                                                    }
                                                });
                                                ad.show();
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
            errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
        } finally {
            progressDialog.dismiss();
        }
    }

    public String getIMEI() {
        if (android.os.Build.VERSION.SDK_INT <= Build.VERSION_CODES.P) {
            try {
                TelephonyManager tm = (TelephonyManager) getSystemService(Context.TELEPHONY_SERVICE);
                String device_id = tm.getDeviceId();
                return device_id;
            } catch (Exception ex) {
                Toast.makeText(getApplicationContext(), "Give READ_PHONE_STATE permission", Toast.LENGTH_LONG).show();
                return null;
            }
        }
        else{
            String android_id= Settings.Secure.getString(this.getContentResolver(),
                    Settings.Secure.ANDROID_ID);
            String device_id = "";
            if (android_id != null) {
                device_id =android_id;
            }
            return device_id;
        }
    }
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (resultCode == RESULT_OK) {
            try {
                int position = requestCode - CheckListDesign.CAMERA_CAPTURE_IMAGE_REQUEST_CODE; // Get position from requestCode
                File fl = new File(CheckListDesign.imagecapturepath);
                if (fl.exists()) {
                    // Save the captured image
                    CheckListDesign.saveCapturedImage(position, CheckListDesign.imagecapturepath);

                    // Get ListView and its current visible items
                    ListView listView = findViewById(R.id.listRules);
                    int firstVisiblePosition = listView.getFirstVisiblePosition();
                    int lastVisiblePosition = listView.getLastVisiblePosition();

                    // Check if the position is visible
                    if (position >= firstVisiblePosition && position <= lastVisiblePosition) {
                        // Get the specific row (view) for the captured item
                        View rowView = listView.getChildAt(position - firstVisiblePosition);

                        if (rowView != null) {
                            // Find the ImageView for that row and set the image
                            ImageView imageView = rowView.findViewById(R.id.image_camera);
                            Bitmap bitmap = BitmapFactory.decodeFile(CheckListDesign.imagecapturepath);
                            if (bitmap != null) {
                                imageView.setImageBitmap(bitmap);
                                // Optionally, you could compress the image or save it again before deleting
                                FileUpload file_upload = new FileUpload();
                                String path = file_upload.compressImage(CheckListDesign.imagecapturepath); // Compress the image if needed
                                imageView.setTag(path);
                            }
                        }
                    }
                    // Notify the adapter if necessary for non-visible rows
                    /*CheckListDesign adapter = (CheckListDesign) listView.getAdapter();
                    if (adapter != null) {
                        adapter.notifyDataSetChanged();
                    }*/
                    /*if (fl.exists()) {
                        fl.delete();
                    }*/

                }
            } catch (Exception ex) {
                ex.printStackTrace();
            }
        }
    }
    @Override
    public void onRequestPermissionsResult(int requestCode,String permissions[], int[] grantResults) {
        switch (requestCode) {
            case MY_PERMISSIONS_REQUEST_READ_CONTACTS: {
                // If request is cancelled, the result arrays are empty.
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    if(ContextCompat.checkSelfPermission(TagIn.this,
                            Manifest.permission.ACCESS_FINE_LOCATION)
                            != PackageManager.PERMISSION_GRANTED) {
                        ActivityCompat.requestPermissions(TagIn.this,
                                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                MY_PERMISSIONS_REQUEST_READ_LOCATION);
                    }
                    else {
                        runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                InsertTaging();
                            }
                        });
                    }
                } else {
                    final AlertDialog ad=new AlertDialog.Builder(this).create();
                    ad.setTitle("Permission Need");
                    ad.setMessage("PHONE STATE permission is mandatory to TagIn.");
                    ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int which) {
                            dialog.dismiss();
                        }
                    });
                    ad.show();
                }
                return;
            }
            case MY_PERMISSIONS_REQUEST_READ_LOCATION:
            {
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    runOnUiThread(new Runnable() {
                        @Override
                        public void run() {
                            InsertTaging();
                        }
                    });
                } else {
                    final AlertDialog ad=new AlertDialog.Builder(this).create();
                    ad.setTitle("Permission Need");
                    ad.setMessage("Location permission is mandatory to TagIn.");
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
            case MY_PERMISSIONS_REQUEST_CAMERA: {
                // If request is cancelled, the result arrays are empty.

                if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    if (ContextCompat.checkSelfPermission(this,
                            Manifest.permission.WRITE_EXTERNAL_STORAGE)
                            != PackageManager.PERMISSION_GRANTED) {

                        ActivityCompat.requestPermissions(this,
                                new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                                MY_PERMISSIONS_REQUEST_STORAGE);
                    } else {
                        //dispatchTakePictureIntent();
                    }
                }
                    /*else {

                        final AlertDialog ad = new AlertDialog.Builder(this).create();
                        ad.setTitle("Permission Need");
                        ad.setMessage("CAMERA permission is mandatory to run app.");
                        ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                            public void onClick(DialogInterface dialog, int which) {
                                dialog.dismiss();
                            }
                        });
                        ad.show();
                    }*/

                return;
            }
            case MY_PERMISSIONS_REQUEST_STORAGE: {
                // If request is cancelled, the result arrays are empty.

                if (ContextCompat.checkSelfPermission(this,
                        Manifest.permission.CAMERA)
                        != PackageManager.PERMISSION_GRANTED) {

                    ActivityCompat.requestPermissions(this,
                            new String[]{Manifest.permission.CAMERA},
                            MY_PERMISSIONS_REQUEST_CAMERA);
                } //else
                //dispatchTakePictureIntent();
                //  captureImage();

                return;
            }
        }
    }
    private static File getOutputMediaFile(int type) {

        // External sdcard location
        File mediaStorageDir = new File(
                Environment
                        .getExternalStoragePublicDirectory(Environment.DIRECTORY_PICTURES),
                Config.IMAGE_DIRECTORY_NAME);

        // Create the storage directory if it does not exist
        if (!mediaStorageDir.exists()) {
            if (!mediaStorageDir.mkdirs()) {
                Log.d(TagIn.class.getSimpleName(), "Oops! Failed create "
                        + Config.IMAGE_DIRECTORY_NAME + " directory");
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
    @Override
    public void onBackPressed() {
        runOnUiThread(new Runnable() {
            public void run() {
                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                        new ContextThemeWrapper(TagIn.this, android.R.style.Theme_Holo_Light_Dialog));
                alertDialogBuilder.setTitle("Data Loss Alert");
                alertDialogBuilder.setMessage("You may loose your data.. \n \nDo you want to continue..?")
                        .setPositiveButton("Cancel",
                                new DialogInterface.OnClickListener() {
                                    public void onClick(DialogInterface dialog, int id) {
                                        dialog.cancel();
                                    }
                                })
                        .setNegativeButton("Continue",
                                new DialogInterface.OnClickListener() {
                                    public void onClick(DialogInterface dialog, int id) {
                                        dialog.cancel();
                                        if (CheckListDesign.imagePaths != null) {
                                            CheckListDesign.imagePaths.clear();
                                        }
                                        finish();
                                    }
                                });
                AlertDialog alert = alertDialogBuilder.create();
                alert.show();
            }
        });
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        if (item.getItemId() == android.R.id.home) {
            onBackPressed();
            return true;
        }
        return super.onOptionsItemSelected(item);
    }

    public void checkTagInFormValidation() {
        if (btn_tagin_ref == null) return;
        String taskFromSpinner = (wfmTask != null && wfmTask.getSelectedItem() != null) ? wfmTask.getSelectedItem().toString().trim() : "";
        if ("Select".equalsIgnoreCase(taskFromSpinner)) taskFromSpinner = "";
        String capturedWfmTask = (chosenWfmTask != null && !chosenWfmTask.isEmpty()) ? chosenWfmTask : taskFromSpinner;

        String[] currentRules = CheckListDesign.strRules;
        boolean isFormValid = true;

        if (capturedWfmTask.isEmpty()) {
            isFormValid = false;
        } else if (currentRules != null && ruleTypes != null) {
            for (int i = 0; i < currentRules.length; i++) {
                if (i < ruleTypes.length && currentRules[i] == null && "Radio".equals(ruleTypes[i])) {
                    isFormValid = false;
                    break;
                }
            }
        }

        final boolean valid = isFormValid;
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (btn_tagin_ref != null) btn_tagin_ref.setEnabled(valid);
                if (txt_checklist_status_ref != null) {
                    if (valid) {
                        txt_checklist_status_ref.setText("✅ Checklist Complete — Ready for Tag-In");
                        txt_checklist_status_ref.setTextColor(android.graphics.Color.parseColor("#2563EB"));
                    } else {
                        txt_checklist_status_ref.setText("⏳ Form Incomplete — Select WFM Task & Answer Checklist Items");
                        txt_checklist_status_ref.setTextColor(android.graphics.Color.parseColor("#64748B"));
                    }
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
    protected void onDestroy() {
        super.onDestroy();
        if (CheckListDesign.imagePaths != null) {
            CheckListDesign.imagePaths.clear();
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU && backInvokedCallback != null) {
            getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(backInvokedCallback);
        }
    }
    private void handleOnBackPressed() {
        onBackPressed();
    }
}
