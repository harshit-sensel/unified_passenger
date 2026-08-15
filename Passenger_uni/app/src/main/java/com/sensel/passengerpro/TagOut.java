package com.sensel.passengerpro;

import android.Manifest;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.bluetooth.BluetoothAdapter;
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
import android.nfc.Tag;
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
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.animation.AlphaAnimation;
import android.view.animation.Animation;
import android.view.animation.LinearInterpolator;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

import com.android.volley.RequestQueue;
import com.android.volley.toolbox.Volley;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Created by MS on 16-Oct-17.
 */

public class TagOut extends AppCompatActivity {
    String vehicleid="";
    String tagInTime="";
    String taginOMR="";
    TextView txtVehicle;
    TextView txtTagInTime;
    TextView txtTagInOMR;
    Button tagOut;
    String mobileno;
    ProgressDialog progressDialog;
    String psngrId = "";
    private static final int MY_PERMISSIONS_REQUEST_READ_CONTACTS = 104;
    private static final int MY_PERMISSIONS_REQUEST_READ_LOCATION = 101;
    String latlng="0,0";
    AppConstants appConstants=new AppConstants();
    WebServices webServices=new WebServices();
    String OMR="";
    static ImageView imageView;
    static String imagecapturepath = "";

    private static final int MY_PERMISSIONS_REQUEST_CAMERA = 102;
    private static final int MY_PERMISSIONS_REQUEST_STORAGE = 103;
    private static final int CAMERA_CAPTURE_IMAGE_REQUEST_CODE = 100;
    private static final int CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE = 105;
    public static final int MEDIA_TYPE_IMAGE = 1;
    String images="";
    private Uri fileUri;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_tagout);
        PassengerActivityLogger.log(this, "TagOut");

        ActionBar actionBar = getSupportActionBar();
        if (actionBar != null) {
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }
        txtVehicle=(TextView) findViewById(R.id.vehicleid);
        txtTagInTime=(TextView) findViewById(R.id.tagInTime);
        txtTagInOMR=(TextView) findViewById(R.id.tagInOMR);
        tagOut=(Button) findViewById(R.id.btnTagOut);
        String result=getIntent().getExtras().getString("details");
        imageView = findViewById(R.id.image_camera);

        // Set onClickListener for the ImageView
        imageView.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if (Build.VERSION.SDK_INT >= 23) {
                    if (ContextCompat.checkSelfPermission(TagOut.this,
                            Manifest.permission.WRITE_EXTERNAL_STORAGE)
                            != PackageManager.PERMISSION_GRANTED) {

                        ActivityCompat.requestPermissions(TagOut.this,
                                new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                                MY_PERMISSIONS_REQUEST_STORAGE);
                    }
                    else if (ContextCompat.checkSelfPermission(TagOut.this,
                            Manifest.permission.CAMERA)
                            != PackageManager.PERMISSION_GRANTED) {

                        ActivityCompat.requestPermissions(TagOut.this,
                                new String[]{Manifest.permission.CAMERA},
                                MY_PERMISSIONS_REQUEST_CAMERA);
                    }
                    // isCameraEnabled =  handlePermissions(Manifest.permission.CAMERA, MY_PERMISSIONS_REQUEST_CAMERA  );
                }
                if(isDeviceSupportCamera())
                {
                    if(isDeviceSupportCamera()) {
                        if (Build.VERSION.SDK_INT <= 32) {
                            if (ContextCompat.checkSelfPermission(TagOut.this,
                                    Manifest.permission.CAMERA)
                                    != PackageManager.PERMISSION_GRANTED) {

                                ActivityCompat.requestPermissions(TagOut.this,
                                        new String[]{Manifest.permission.CAMERA},
                                        MY_PERMISSIONS_REQUEST_CAMERA);
                            } else if (ContextCompat.checkSelfPermission(TagOut.this,
                                    Manifest.permission.WRITE_EXTERNAL_STORAGE)
                                    != PackageManager.PERMISSION_GRANTED) {
                                ActivityCompat.requestPermissions(TagOut.this,
                                        new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE},
                                        MY_PERMISSIONS_REQUEST_STORAGE);
                            } else if (ContextCompat.checkSelfPermission(TagOut.this,
                                    Manifest.permission.READ_EXTERNAL_STORAGE)
                                    != PackageManager.PERMISSION_GRANTED) {
                                ActivityCompat.requestPermissions(TagOut.this,
                                        new String[]{Manifest.permission.READ_EXTERNAL_STORAGE},
                                        MY_PERMISSIONS_REQUEST_STORAGE);
                            } else {
                                //captureImage();
                                dispatchTakePictureIntent();
                            }
                        }
                        else{
                            if (ContextCompat.checkSelfPermission(TagOut.this,
                                    Manifest.permission.CAMERA)
                                    != PackageManager.PERMISSION_GRANTED) {

                                ActivityCompat.requestPermissions(TagOut.this,
                                        new String[]{Manifest.permission.CAMERA},
                                        MY_PERMISSIONS_REQUEST_CAMERA);
                            } else {
                                //captureImage();
                                dispatchTakePictureIntent();
                            }

                        }
                    }
                    else
                        Toast.makeText(getApplicationContext(), "This device not supporting camera", Toast.LENGTH_SHORT).show();

                }
            }
        });
        progressDialog=ProgressDialog.show(TagOut.this, "", "Loading...", true);
        try {
            psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(),"passengerinfo","PsngrId");
            mobileno = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(),"passengerinfo","MobileNo");
            JSONArray jArr = new JSONArray(result);
            for (int j = 0; j < jArr.length(); j++) {
                JSONObject data = jArr.getJSONObject(j);

                if (data.getString("VehicleId").trim().length() > 0) {
                    vehicleid = data.getString("VehicleId");
                }
                if (data.getString("TagInTime").trim().length() > 0) {
                    tagInTime = data.getString("TagInTime");
                }
                if (data.getString("TagInOMR").trim().length() > 0) {
                    taginOMR = data.getString("TagInOMR");
                }
            }
            txtVehicle.setText(vehicleid);
            tagInTime=tagInTime.substring(tagInTime.indexOf("(")+1,tagInTime.indexOf(")"));
            String dateString = new SimpleDateFormat("dd/MM/yyyy HH:mm:ss").format(new Date(Long.parseLong(tagInTime)));
            txtTagInTime.setText(dateString);
            txtTagInOMR.setText(taginOMR);
            String ruleImageDirectory ="/storage/emulated/0/Android/data/com.sensel.hardware.camera/files/Pictures" + File.separator ;;
            File dir = new File(ruleImageDirectory);
            if (!dir.exists()) dir.mkdirs();
            SimpleDateFormat sdfDate = new SimpleDateFormat("HHmmss");//dd/MM/yyyy
            Date now = new Date();
            String strDate = sdfDate.format(now);

            imagecapturepath = vehicleid + "_" + strDate + ".PNG";
            imagecapturepath = imagecapturepath.replaceAll(" ", "");
            imagecapturepath = imagecapturepath.replaceAll("[:\\\\/*\"?|<>]", "_");

            tagOut.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    runOnUiThread(new Runnable() {
                        public void run() {
                            LayoutInflater li = LayoutInflater.from(TagOut.this);
                            View promptsView = li.inflate(R.layout.odometer_prompt, null);
                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                    new ContextThemeWrapper(TagOut.this, android.R.style.Theme_Holo_Light_Dialog));
                            alertDialogBuilder.setView(promptsView);

                            final EditText userInput = (EditText) promptsView.findViewById(R.id.odo);
                            final Button button = (Button) promptsView.findViewById(R.id.btn);
                            button.setText("TagOut");
                            final Button buttonCancel = (Button) promptsView.findViewById(R.id.btncancel);
                            alertDialogBuilder.setTitle("Tag Out");
                            alertDialogBuilder.setMessage("Are You Sure?\n\nVehicle - "+vehicleid)
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
                                    //if (userInput.getText().toString().trim().length() > 0 && (Long.valueOf(taginOMR)<=Long.valueOf(userInput.getText().toString()))) {
                                     if (userInput.getText().toString().trim().length() > 0 && (Long.valueOf(taginOMR)<=Long.valueOf(userInput.getText().toString()))) {
                                         if (imagecapturepath == null || imagecapturepath.isEmpty() || !new File(imagecapturepath).exists()) {
                                             Toast.makeText(TagOut.this, "Please capture Tag-Out Odometer photo before tagging out", Toast.LENGTH_SHORT).show();
                                             return;
                                         }
                                         OMR = userInput.getText().toString();
                                        if (Long.valueOf(OMR) - Long.valueOf(taginOMR) < 999) {
                                            new Thread(new Runnable() {
                                                @Override
                                                public void run() {
                                                    try {
                                                        if (isNetworkAvailable()) {
                                                            if (ContextCompat.checkSelfPermission(TagOut.this,
                                                                    Manifest.permission.READ_PHONE_STATE)
                                                                    != PackageManager.PERMISSION_GRANTED) {
                                                                ActivityCompat.requestPermissions(TagOut.this,
                                                                        new String[]{Manifest.permission.READ_PHONE_STATE},
                                                                        MY_PERMISSIONS_REQUEST_READ_CONTACTS);
                                                            } else if (ContextCompat.checkSelfPermission(TagOut.this,
                                                                    Manifest.permission.ACCESS_FINE_LOCATION)
                                                                    != PackageManager.PERMISSION_GRANTED) {
                                                                ActivityCompat.requestPermissions(TagOut.this,
                                                                        new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                                                        MY_PERMISSIONS_REQUEST_READ_LOCATION);
                                                            } else
                                                                InsertTaging();
                                                        } else {
                                                            runOnUiThread(new Runnable() {
                                                                public void run() {
                                                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                            new ContextThemeWrapper(TagOut.this, android.R.style.Theme_Holo_Light_Dialog));
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
                                        } else if (userInput.getText().toString().trim().length() == 0) {
                                            Toast.makeText(TagOut.this, "Please enter odometer reading", Toast.LENGTH_SHORT).show();
                                        } else {
                                            Toast.makeText(TagOut.this, "Difference in Odometer reading between tagin and tagout is more than 1000", Toast.LENGTH_SHORT).show();
                                        }
                                    }
                                    else{
                                        Toast.makeText(TagOut.this, "Entered reading is less than TagIn Odometer reading", Toast.LENGTH_SHORT).show();
                                        }
                                }
                            });

                            alert.show();
                        }
                    });
                }
            });
        }
        catch (Exception e){
            e.printStackTrace();
            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
            errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagOut("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
        }
        finally {
            progressDialog.dismiss();
        }
    }
    private void dispatchTakePictureIntent() {
        if (ActivityCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(this, new String[]{Manifest.permission.CAMERA}, MY_PERMISSIONS_REQUEST_CAMERA);
        } else {
            Intent takePictureIntent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
            // Ensure that there's a camera activity to handle the intent
            if (takePictureIntent.resolveActivity(getPackageManager()) != null) {
                // Create the File where the photo should go
                File photoFile = null;
                try {
                    photoFile = createImageFile();
                } catch (IOException ex) {
                    // Error occurred while creating the File
                    ex.printStackTrace();
                }
                // Continue only if the File was successfully created
                if (photoFile != null) {
                    Uri photoURI = FileProvider.getUriForFile(this,
                            getPackageName() + ".provider",
                            photoFile);
                    takePictureIntent.putExtra(MediaStore.EXTRA_OUTPUT, photoURI);
                    startActivityForResult(takePictureIntent, CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE);
                }
            }
        }
    }
    private boolean isDeviceSupportCamera() {
        if (getApplicationContext().getPackageManager().hasSystemFeature(
                PackageManager.FEATURE_CAMERA)) {
            // this device has a camera
            return true;
        } else {
            // no camera on this device
            return false;
        }
    }
    // Handle the result of the camera capture
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE) {
            if (requestCode == CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE && resultCode == RESULT_OK) {
                try
                {
                    File fl = new File(imagecapturepath);
                    if (fl.exists()) {
                        BitmapFactory.Options options = new BitmapFactory.Options();
                        options.inPreferredConfig = Bitmap.Config.ARGB_8888;
                        Bitmap bitmap = BitmapFactory.decodeFile(imagecapturepath, options);
                        options.inSampleSize = 4; //1/2 of original size
                        imageView.setImageBitmap(BitmapFactory.decodeFile(imagecapturepath, options));
                        FileUpload file_upload=new FileUpload();
                        String path = file_upload.compressImage(fileUri.getPath());
                        imageView.setTag(path);
                    }
                }
                catch(Exception ex)
                {
                    ex.printStackTrace();
                }

            }else if (resultCode == RESULT_CANCELED) {

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

    @Override
    public void onRequestPermissionsResult(int requestCode,String permissions[], int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        switch (requestCode) {
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
                        dispatchTakePictureIntent();
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
                }
                return;
            }
        }
    }
    private File createImageFile() throws IOException {
        // Create an image file name
        String timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss").format(new Date());
        //String imageFileName =  timeStamp;
        File storageDir =getExternalFilesDir(Environment.DIRECTORY_PICTURES);
        //File storageDir = new File(RULE.ruleImageDirectory);
        Log.d("StorageDirectory","StorageDirectory"+storageDir);
        storageDir.mkdirs(); // make sure you call mkdirs() and not mkdir()
        String imageFileName=getFileNameFromFullPath();
        String[] FileName=getFileNameAndFormat(imageFileName);

        /*File image = File.createTempFile(
                FileName[0],  *//* prefix *//*
                ".PNG" ,*//* suffix *//*
                storageDir      *//* directory *//*
        );*/
        File image = new File(storageDir, FileName[0] + ".PNG");
        imagecapturepath = image.getAbsolutePath();
        return image;
    }
    private String[] getFileNameAndFormat(String fileName) {
        return new String[]{fileName, "PNG"};
    }

    private String getFileNameFromFullPath(){
        return imagecapturepath.substring(imagecapturepath.lastIndexOf('/')+1,imagecapturepath.length());
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

    public void InsertTaging() {
        try {
            LocationManager locationManager = (LocationManager) getApplicationContext().getSystemService(Context.LOCATION_SERVICE);
            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)){
                final Thread thread=new Thread(new Runnable() {
                    @Override
                    public void run() {
                        runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                progressDialog = ProgressDialog.show(TagOut.this, "", "Loading...", true);
                            }
                        });
                        String fileName = "";
                        try {
                            if (imagecapturepath != null && !imagecapturepath.isEmpty() && new File(imagecapturepath).exists()) {
                                fileName = "TagOut_" + psngrId + "_" + System.currentTimeMillis() + ".jpg";
                                FileUpload fileUpload = new FileUpload();
                                String compressedPath = fileUpload.compressImage(imagecapturepath);
                                String targetPath = (compressedPath != null && !compressedPath.isEmpty()) ? compressedPath : imagecapturepath;
                                fileUpload.uploadFileWithName(targetPath, fileName);

                                // Post-upload local file cleanup from Android storage
                                try {
                                    new File(imagecapturepath).delete();
                                    if (compressedPath != null) {
                                        new File(compressedPath).delete();
                                    }
                                } catch (Exception ignored) {}
                            }
                        }
                        catch (Exception ex) {
                            ex.printStackTrace(); // For logging
                        }
                        String Imei = getIMEI();
                        String[] str=latlng.split(",");
                        String result = webServices.InsertPsngrChecklist(psngrId, "", "TagOut", "", "", "","",Imei,str[0],str[1],"","",OMR,"","","","","","",fileName);
                        if (result.contains("Inserted Successfully")) {
                            // Log TAG_OUT activity audit event
                            new Thread(new Runnable() {
                                @Override
                                public void run() {
                                    try {
                                        String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                        int accountId = 0;
                                        try { if (accountIdStr != null) accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                                        webServices.logAuditActivity(mobileno, accountId, "TAG_OUT", str[0], str[1]);
                                    } catch (Exception ignored) {}
                                }
                            }).start();

                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(TagOut.this, android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setTitle("Status");
                                    alertDialogBuilder.setMessage("TagOut Done Successfully")
                                            .setPositiveButton("Ok",
                                                    new DialogInterface.OnClickListener() {
                                                        public void onClick(DialogInterface dialog, int id) {
                                                            progressDialog = ProgressDialog.show(TagOut.this, "", "Loading...", true);
                                                            dialog.cancel();
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
                                                                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagOut("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
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
                        } else {
                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(result + "-TagOut("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno+"-InsertPsngrChecklist("+psngrId+", \"\", \"TagOut\", \"\", \"\", \"\")");
                            runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(TagOut.this, android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setTitle("Status");
                                    alertDialogBuilder.setMessage("TagOut is Failed")
                                            .setPositiveButton("Ok",
                                                    new DialogInterface.OnClickListener() {
                                                        public void onClick(DialogInterface dialog, int id) {
                                                            dialog.cancel();
                                                            progressDialog = ProgressDialog.show(TagOut.this, "", "Loading...", true);
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
                                                                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagOut("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
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
                        AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(TagOut.this);
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
                                                final AlertDialog ad=new AlertDialog.Builder(TagOut.this).create();
                                                ad.setTitle("Permission Need");
                                                ad.setMessage("GPS Location is mandatory to TagOut.");
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
    public boolean onPrepareOptionsMenu(final Menu menu) {
        getMenuInflater().inflate(R.menu.menu, menu);
        MenuItem item = menu.findItem(R.id.menu_refresh);
        item.setVisible(false);
        item = menu.findItem(R.id.menu_panic);
        ImageView imgView = new ImageView(this);
        imgView.setBackground(getResources().getDrawable(R.drawable.panic));
        item.setActionView(imgView);
        Animation mAnimation = new AlphaAnimation(1, 0);
        mAnimation.setDuration(500);
        mAnimation.setInterpolator(new LinearInterpolator());
        mAnimation.setRepeatCount(Animation.INFINITE);
        mAnimation.setRepeatMode(Animation.REVERSE);
        item.getActionView().startAnimation(mAnimation);
        imgView.setOnClickListener(new View.OnClickListener() {

            @Override
            public void onClick(View v) {
                runOnUiThread(new Runnable() {
                    public void run() {
                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                new ContextThemeWrapper(TagOut.this, android.R.style.Theme_Holo_Light_Dialog));
                        alertDialogBuilder.setTitle("Panic ");
                        alertDialogBuilder.setIcon(R.drawable.panic);
                        alertDialogBuilder.setMessage("Are you really Panic?")
                                .setCancelable(false)
                                .setPositiveButton("No",
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                dialog.cancel();
                                            }
                                        })
                                .setNegativeButton("Yes",
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                progressDialog = ProgressDialog.show(TagOut.this, "", "Loading...", true);
                                                new Thread(new Runnable() {
                                                    @Override
                                                    public void run() {
                                                        final String res=webServices.InsertPanicAlertFromApp(psngrId,vehicleid,"Passenger");
                                                        runOnUiThread(new Runnable() {
                                                            @Override
                                                            public void run() {
                                                                if(res.contains("Inserted Successfully"))
                                                                    Toast.makeText(getApplicationContext(),"Panic alert sent successfully",Toast.LENGTH_SHORT).show();
                                                                else {
                                                                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                    errorRecordSendMail.errorrecordSendMail(res + "-TagOut("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno+"-InsertPanicAlertFromApp("+psngrId+","+vehicleid+",\"Passenger\")");
                                                                    Toast.makeText(getApplicationContext(), "Panic alert failed to send", Toast.LENGTH_SHORT).show();
                                                                }
                                                                progressDialog.dismiss();
                                                            }
                                                        });
                                                    }
                                                }).start();
                                                dialog.cancel();
                                            }
                                        });
                        AlertDialog alert = alertDialogBuilder.create();
                        alert.show();
                    }
                });
            }
        });
        return super.onCreateOptionsMenu(menu);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case android.R.id.home:
                finish();
                return true;
            case R.id.menu_logout:
                runOnUiThread(new Runnable() {
                    public void run() {
                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                new ContextThemeWrapper(TagOut.this, android.R.style.Theme_Holo_Light_Dialog));
                        alertDialogBuilder.setIcon(R.drawable.error);
                        alertDialogBuilder.setTitle("Logout ");
                        alertDialogBuilder.setMessage("First you need to do TagOut and then try LogOut.")
                                .setCancelable(false)
                                .setPositiveButton("OK",
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                dialog.cancel();
                                            }
                                        });
                        AlertDialog alert = alertDialogBuilder.create();
                        alert.show();
                    }
                });
                break;
        }
        return super.onOptionsItemSelected(item);
    }
}
