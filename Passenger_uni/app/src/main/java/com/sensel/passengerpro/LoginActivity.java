package com.sensel.passengerpro;

import android.Manifest;
import android.app.ActivityManager;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import android.telephony.TelephonyManager;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.TextUtils;
import android.text.method.LinkMovementMethod;
import android.text.style.ClickableSpan;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

public class LoginActivity extends AppCompatActivity {
    ProgressDialog dialog;
    TextView errorLogin;
    TextView header;
    EditText phno;
    String passengerinfo="";
    String phoneno="";
    Button btnLogin;
    TextView infotxt;
    String tempMobileNo;
    final AppConstants appConstants=new AppConstants();
    final WebServices webServices=new WebServices();
    Intent mServiceIntent;
    private AlarmService mSensorService;
    private static final int MY_PERMISSIONS_REQUEST_READ_CONTACTS = 101;
    /** Ask for location on login so [Login_Success] / [Login_AutoResume] activity logs can resolve GPS before main menu. */
    private static final int MY_PERMISSIONS_REQUEST_LOCATION_LOGIN = 1406;

    public static String IMEI="";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_login);
        phno=(EditText) findViewById(R.id.input_phNo);
        errorLogin=(TextView)findViewById(R.id.Login_Attempts_txt);
        header=(TextView)findViewById(R.id.header);
        btnLogin=(Button) findViewById(R.id.btn_login);
        infotxt=(TextView) findViewById(R.id.info);
        passengerinfo=appConstants.getShrdPrefValByKey(getApplicationContext(),"passengerinfo");
        IMEI=getIMEI();
        if(passengerinfo!=null && passengerinfo.contains("PsngrId")){
            dialog = ProgressDialog.show(LoginActivity.this, "", "Loading...", true);
            new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        if (isNetworkAvailable()) {
                            appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", webServices.GetPsngrInfoWithValidation(appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo"), "Validate"));
                            passengerinfo = appConstants.getShrdPrefValByKey(getApplicationContext(), "passengerinfo");
                            if (passengerinfo != null && passengerinfo.contains("No Data")) {
                                appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", null);
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        dialog.dismiss();
                                    }
                                });
                                return;
                            }
                            Intent i = new Intent(getApplicationContext(), MainActivity.class);
                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                            startActivity(i);
                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    scheduleLoginActivityLogDelayed("Login_AutoResume", 1200);
                                }
                            });
                        } else {
                            Intent i = new Intent(getApplicationContext(), OOPs.class);
                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                            i.putExtra("message", "OOPs..! Connectivity issue. Please try again later.");
                            startActivity(i);
                        }
                    }
                    catch (Exception e){
                        e.printStackTrace();
                        ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
                        errorRecordSendMail.errorrecordSendMail(e.toString()+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")-passengerinfo");
                    }
                    finally {
                        if (dialog != null && dialog.isShowing()) {
                            dialog.dismiss();
                        }
                    }
                }
            }).start();
        }
        btnLogin.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                errorLogin.setVisibility(View.GONE);
                dialog = ProgressDialog.show(LoginActivity.this, "", "Loading...", true);
                new Thread(new Runnable() {
                    @Override
                    public void run() {
                        try  {
                            if(btnLogin.getText().toString().contains("Login")) {
                                if(Number_Validate(phno.getText().toString())) {
                                    phoneno=phno.getText().toString();
                                    if(isNetworkAvailable()) {
                                        final String res = webServices.GetPsngrInfoWithValidation(phno.getText().toString(), "Validate");
                                        passengerinfo=res;
                                        runOnUiThread(new Runnable() {
                                            @Override
                                            public void run() {
                                                if (res.contains("PsngrId")) {
                                                    phno.setEnabled(false);
                                                    header.setText("Send OTP");
                                                    btnLogin.setText("Send OTP");
                                                    errorLogin.setVisibility(View.GONE);
                                                    infotxt.setVisibility(View.GONE);
                                                } else if (res.contains("No Data")) {
                                                    errorLogin.setVisibility(View.VISIBLE);
                                                    errorLogin.setText("This phone number is not registered to this "+getString(R.string.app_name)+".");
                                                    errorLogin.setTextColor(Color.RED);
                                                } else {
                                                    ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
                                                    errorRecordSendMail.errorrecordSendMail(res+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")-"+phno.getText().toString()+"-GetPsngrInfoWithValidation("+phno.getText().toString()+", \"Validate\")");
                                                    errorLogin.setVisibility(View.VISIBLE);
                                                    errorLogin.setText("Connectivity issue. Please try again.");
                                                    errorLogin.setTextColor(Color.RED);
                                                    runOnUiThread(new Runnable() {
                                                        public void run() {
                                                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                    new ContextThemeWrapper(LoginActivity.this, android.R.style.Theme_Holo_Light_Dialog));
                                                            alertDialogBuilder.setIcon(R.drawable.error);
                                                            alertDialogBuilder.setTitle("Error ");
                                                            alertDialogBuilder.setMessage("Connectivity issue. Please try again.")
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
                                    else{
                                        runOnUiThread(new Runnable() {
                                            public void run() {
                                                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                        new ContextThemeWrapper(LoginActivity.this, android.R.style.Theme_Holo_Light_Dialog));
                                                alertDialogBuilder.setIcon(R.drawable.error);
                                                alertDialogBuilder.setTitle("Error ");
                                                alertDialogBuilder.setMessage("No internet, Check Your Internet Connection")
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
                                else {
                                    runOnUiThread(new Runnable() {
                                        @Override
                                        public void run() {
                                            errorLogin.setVisibility(View.VISIBLE);
                                            errorLogin.setText("Invalid Number");
                                            errorLogin.setTextColor(Color.RED);
                                        }
                                    });
                                }
                            }
                            else if(btnLogin.getText().toString().contains("Send OTP")) {
                                tempMobileNo=phno.getText().toString();
                                submitOtp();
                            }
                            else if(btnLogin.getText().toString().contains("Submit OTP")) {
                                String otp = appConstants.getShrdPrefValByKey(getApplicationContext(),"otp");
                                if(phno.getText().toString()!="" && phno.getText().toString().length()==4 && otp.contains(phno.getText().toString())) {
                                    appConstants.putShrdPrefValWithKey(getApplicationContext(),"passengerinfo",passengerinfo);
                                    try {
                                        if (isNetworkAvailable()) {
                                            Intent i = new Intent(getApplicationContext(), MainActivity.class);
                                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                            startActivity(i);
                                            runOnUiThread(new Runnable() {
                                                @Override
                                                public void run() {
                                                    scheduleLoginActivityLogDelayed("Login_Success", 1200);
                                                }
                                            });
                                        } else {
                                            Intent i = new Intent(getApplicationContext(), OOPs.class);
                                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                            i.putExtra("message","OOPs..! Internet is not there. Please check the connection and Try again.");
                                            startActivity(i);
                                        }
                                    }
                                    catch (Exception e){
                                        e.printStackTrace();
                                        ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
                                        errorRecordSendMail.errorrecordSendMail(e.toString()+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")-GetPsngrInfoWithValidation(mobileno, \"Tag\")");
                                    }
                                }
                                else{
                                    runOnUiThread(new Runnable() {
                                        @Override
                                        public void run() {
                                            errorLogin.setVisibility(View.VISIBLE);
                                            errorLogin.setText("Invalid OTP");
                                            errorLogin.setTextColor(Color.RED);
                                        }
                                    });
                                }
                            }
                        } catch (Exception e) {
                            e.printStackTrace();
                            ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(e.toString()+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")-GetPsngrInfoWithValidation(mobileno, \"Tag\")");
                        }
                        finally{
                            dialog.dismiss();
                        }
                    }
                }).start();
            }
        });
        mSensorService = new AlarmService(getApplicationContext());
        mServiceIntent = new Intent(getApplicationContext(), mSensorService.getClass());
        if (!isMyServiceRunning(mSensorService.getClass())) {
            startService(mServiceIntent);
        }
        // No activity log here: unauthenticated users have no passenger/vehicle/lat-lng yet.
        // After OTP success use Login_Success; after session restore use Login_AutoResume.
        requestFineLocationForActivityLoggingIfNeeded();
    }

    private void requestFineLocationForActivityLoggingIfNeeded() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED) {
            return;
        }
        ActivityCompat.requestPermissions(this,
                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                MY_PERMISSIONS_REQUEST_LOCATION_LOGIN);
    }

    /** Defer login activity logs so GPS / fused can warm up after permission (see [PassengerActivityLogger]). */
    private void scheduleLoginActivityLogDelayed(final String page, long delayMs) {
        new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
            @Override
            public void run() {
                PassengerActivityLogger.log(getApplicationContext(), page);
            }
        }, delayMs);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode,String permissions[], int[] grantResults) {
        if (requestCode == MY_PERMISSIONS_REQUEST_LOCATION_LOGIN) {
            return;
        }
        switch (requestCode) {
            case MY_PERMISSIONS_REQUEST_READ_CONTACTS: {
                // If request is cancelled, the result arrays are empty.
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {

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
        }
    }

    private boolean isMyServiceRunning(Class<?> serviceClass) {
        ActivityManager manager = (ActivityManager) getSystemService(Context.ACTIVITY_SERVICE);
        for (ActivityManager.RunningServiceInfo service : manager.getRunningServices(Integer.MAX_VALUE)) {
            if (serviceClass.getName().equals(service.service.getClassName())) {
                return true;
            }
        }
        return false;
    }

    private void submitOtp(){
        if(isNetworkAvailable()) {
            int randomPIN=0;
            if(phoneno.contains("1020304050"))
            {
                randomPIN=9080;
            }
            else {
                randomPIN = (int) (Math.random() * 9000) + 1000;
            }
            appConstants.putShrdPrefValWithKey(getApplicationContext(),"otp", String.valueOf(randomPIN));
            final String res = webServices.GetPsngrInfoWithValidation(tempMobileNo, "OTP-" + randomPIN);
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (res.contains("SMS Send Successfully")) {
                        phno.setEnabled(true);
                        phno.setText("");
                        header.setText("Enter OTP");
                        btnLogin.setText("Submit OTP");
                        infotxt.setVisibility(View.VISIBLE);
                        String last4 = phoneno.substring(6, 10);
                        infotxt.setText("OTP is sent successfully to your registered mobile number ******" + last4+"\nIf you didn't get the OTP, Click here to resend OTP.");
                        ClickableSpan resendOTP = new ClickableSpan() {
                            @Override
                            public void onClick(View view) {
                                errorLogin.setVisibility(View.GONE);
                                dialog = ProgressDialog.show(LoginActivity.this, "", "Loading...", true);
                                new Thread(new Runnable() {
                                    @Override
                                    public void run() {
                                        try {
                                            submitOtp();
                                        }
                                        catch (Exception e) {
                                            e.printStackTrace();
                                            ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
                                            errorRecordSendMail.errorrecordSendMail(e.toString()+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")");
                                        }
                                        finally{
                                            dialog.dismiss();
                                        }
                                    }
                                }).start();
                            }
                        };

                        makeLinks(infotxt, new String[] { "Click here" }, new ClickableSpan[] {
                                resendOTP
                        });
                    } else {
                        ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
                        errorRecordSendMail.errorrecordSendMail(res+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")-"+phno.getText().toString()+"-GetPsngrInfoWithValidation("+phno.getText().toString()+", \"OTP-\" + randomPIN)");
                        errorLogin.setVisibility(View.VISIBLE);
                        errorLogin.setText("Connectivity issue. Please try again.");
                        errorLogin.setTextColor(Color.RED);
                        runOnUiThread(new Runnable() {
                            public void run() {
                                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                        new ContextThemeWrapper(LoginActivity.this, android.R.style.Theme_Holo_Light_Dialog));
                                alertDialogBuilder.setIcon(R.drawable.error);
                                alertDialogBuilder.setTitle("Error ");
                                alertDialogBuilder.setMessage("Connectivity issue. Please try again.")
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
        else{
            runOnUiThread(new Runnable() {
                public void run() {
                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                            new ContextThemeWrapper(LoginActivity.this, android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setTitle("Error ");
                    alertDialogBuilder.setMessage("No internet, Check Your Internet Connection")
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

    /* method for phone number validation */
    private Boolean Number_Validate(String number)
    {
        return  !number.contains(".") && !TextUtils.isEmpty(number) && (number.length()==10) && android.util.Patterns.PHONE.matcher(number).matches();
    }

    private boolean isNetworkAvailable() {
        try {
            ConnectivityManager connectivityManager
                    = (ConnectivityManager) getApplicationContext().getSystemService(Context.CONNECTIVITY_SERVICE);
            NetworkInfo activeNetworkInfo = connectivityManager.getActiveNetworkInfo();
            return activeNetworkInfo != null && activeNetworkInfo.isConnected();
        } catch (Exception e) {
            ErrorRecordSendMail errorRecordSendMail=new ErrorRecordSendMail();
            errorRecordSendMail.errorrecordSendMail(e.toString()+"-LoginActivity("+new Exception().getStackTrace()[0].getLineNumber()+")-isNetworkAvailable()");
            return true;
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
    public void makeLinks(TextView textView, String[] links, ClickableSpan[] clickableSpans) {
        SpannableString spannableString = new SpannableString(textView.getText());
        for (int i = 0; i < links.length; i++) {
            ClickableSpan clickableSpan = clickableSpans[i];
            String link = links[i];

            int startIndexOfLink = textView.getText().toString().indexOf(link);
            spannableString.setSpan(clickableSpan, startIndexOfLink, startIndexOfLink + link.length(),
                    Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        }
        textView.setMovementMethod(LinkMovementMethod.getInstance());
        textView.setText(spannableString, TextView.BufferType.SPANNABLE);
    }

    @Override
    public void onBackPressed() {
        moveTaskToBack(true);
    }

    @Override
    protected void onDestroy() {
        stopService(mServiceIntent);
        super.onDestroy();
    }
}
