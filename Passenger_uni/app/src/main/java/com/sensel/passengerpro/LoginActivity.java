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
import android.text.Editable;
import android.text.TextWatcher;
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

    android.widget.LinearLayout otpContainer;
    EditText otpBox1, otpBox2, otpBox3, otpBox4;
    TextView btnResendOtp;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_login);
        phno=(EditText) findViewById(R.id.input_phNo);
        errorLogin=(TextView)findViewById(R.id.Login_Attempts_txt);
        header=(TextView)findViewById(R.id.header);
        btnLogin=(Button) findViewById(R.id.btn_login);
        infotxt=(TextView) findViewById(R.id.info);

        otpContainer = (android.widget.LinearLayout) findViewById(R.id.otp_section_container);
        otpBox1 = (EditText) findViewById(R.id.otp_digit_1);
        otpBox2 = (EditText) findViewById(R.id.otp_digit_2);
        otpBox3 = (EditText) findViewById(R.id.otp_digit_3);
        otpBox4 = (EditText) findViewById(R.id.otp_digit_4);
        btnResendOtp = (TextView) findViewById(R.id.btn_resend_otp);

        setupOtpAutoAdvance();
        passengerinfo=appConstants.getShrdPrefValByKey(getApplicationContext(),"passengerinfo");
        IMEI=getIMEI();
        if(passengerinfo!=null && passengerinfo.contains("PsngrId")){
            dialog = ProgressDialog.show(LoginActivity.this, "", "Loading...", true);
            new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        if (isNetworkAvailable()) {
                            String mobNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                            appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", webServices.GetPsngrInfoWithValidation(mobNo, "Validate"));
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
                            String userMenus = null;
                            try {
                                String accId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                userMenus = webServices.GetMenusByUser(accId);
                                if (userMenus != null && !userMenus.isEmpty() && !userMenus.contains("No Data")) {
                                    appConstants.putShrdPrefValWithKey(getApplicationContext(), "UserMenus", userMenus);
                                }
                            } catch (Exception ex) {
                                android.util.Log.e("LoginActivity", "Error fetching UserMenus", ex);
                            }
                             String tagResult = webServices.GetPsngrInfoWithValidation(mobNo, "Tag");
                             Intent i;
                             if (userMenus != null && userMenus.contains("dashboard")) {
                                 // Dashboard present: Always route to MainActivity (Grid Dashboard)
                                 i = new Intent(getApplicationContext(), MainActivity.class);
                             } else if (userMenus != null && userMenus.contains("school_bus_tracking")) {
                                 // Legacy School Bus Tracking: Go directly to live map (TrackOnMap)
                                 i = new Intent(getApplicationContext(), TrackOnMap.class);
                                 i.putExtra("tagDetails", tagResult);
                             } else if (userMenus != null && userMenus.contains("assigned_veh_tracking")) {
                                 // SmartTrack flow: go directly to TagTrack (Status + Map tabs)
                                 i = new Intent(getApplicationContext(), TagTrack.class);
                                 i.putExtra("tagDetails", tagResult);
                             } else if (tagResult != null && tagResult.contains("TagOut")) {
                                 i = new Intent(getApplicationContext(), TagOut.class);
                                 i.putExtra("details", tagResult);
                             } else if (userMenus != null && userMenus.contains("checklist")) {
                                 i = new Intent(getApplicationContext(), VehicleInfo.class);
                             } else {
                                 i = new Intent(getApplicationContext(), MainActivity.class);
                             }
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
                            String bTxt = btnLogin.getText().toString().toLowerCase();
                            if (bTxt.contains("verify") || bTxt.contains("submit")) {
                                runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        performLoginWithOtp();
                                    }
                                });
                            } else if (bTxt.contains("login") || bTxt.contains("send") || bTxt.contains("otp")) {
                                if (Number_Validate(phno.getText().toString())) {
                                    phoneno = phno.getText().toString().trim();
                                    tempMobileNo = phoneno;
                                    if (isNetworkAvailable()) {
                                        final String res = webServices.GetPsngrInfoWithValidation(phoneno, "Validate");
                                        passengerinfo = res;
                                        runOnUiThread(new Runnable() {
                                            @Override
                                            public void run() {
                                                if (res != null && res.contains("PsngrId")) {
                                                    errorLogin.setVisibility(View.GONE);
                                                    new Thread(new Runnable() {
                                                        @Override
                                                        public void run() {
                                                            submitOtp();
                                                        }
                                                    }).start();
                                                } else if (res != null && res.contains("No Data")) {
                                                    errorLogin.setVisibility(View.VISIBLE);
                                                    errorLogin.setText("This phone number is not registered to this " + getString(R.string.app_name) + ".");
                                                    errorLogin.setTextColor(Color.RED);
                                                } else {
                                                    errorLogin.setVisibility(View.VISIBLE);
                                                    errorLogin.setText("Connectivity issue. Please try again.");
                                                    errorLogin.setTextColor(Color.RED);
                                                }
                                            }
                                        });
                                    } else {
                                        runOnUiThread(new Runnable() {
                                            public void run() {
                                                Toast.makeText(LoginActivity.this, "No internet connection.", Toast.LENGTH_SHORT).show();
                                            }
                                        });
                                    }
                                } else {
                                    runOnUiThread(new Runnable() {
                                        @Override
                                        public void run() {
                                            errorLogin.setVisibility(View.VISIBLE);
                                            errorLogin.setText("Please enter a valid 10-digit mobile number.");
                                            errorLogin.setTextColor(Color.RED);
                                        }
                                    });
                                }
                            }
                        } catch (Exception e) {
                            e.printStackTrace();
                        } finally {
                            if (dialog != null && dialog.isShowing()) dialog.dismiss();
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

    private void setupOtpAutoAdvance() {
        if (otpBox1 == null) return;
        otpBox1.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                if (s.length() == 1) otpBox2.requestFocus();
            }
            public void afterTextChanged(Editable s) {}
        });
        otpBox2.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                if (s.length() == 1) otpBox3.requestFocus();
                else if (s.length() == 0) otpBox1.requestFocus();
            }
            public void afterTextChanged(Editable s) {}
        });
        otpBox3.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                if (s.length() == 1) otpBox4.requestFocus();
                else if (s.length() == 0) otpBox2.requestFocus();
            }
            public void afterTextChanged(Editable s) {}
        });
        otpBox4.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                if (s.length() == 1) {
                    performLoginWithOtp();
                } else if (s.length() == 0) {
                    otpBox3.requestFocus();
                }
            }
            public void afterTextChanged(Editable s) {}
        });

        if (btnResendOtp != null) {
            btnResendOtp.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    errorLogin.setVisibility(View.GONE);
                    dialog = ProgressDialog.show(LoginActivity.this, "", "Resending OTP...", true);
                    new Thread(new Runnable() {
                        @Override
                        public void run() {
                            try {
                                submitOtp();
                            } catch (Exception e) {
                                e.printStackTrace();
                            } finally {
                                if (dialog != null && dialog.isShowing()) dialog.dismiss();
                            }
                        }
                    }).start();
                }
            });
        }
    }

    private void performLoginWithOtp() {
        String enteredOtp = (otpBox1 != null ? otpBox1.getText().toString().trim() : "") +
                (otpBox2 != null ? otpBox2.getText().toString().trim() : "") +
                (otpBox3 != null ? otpBox3.getText().toString().trim() : "") +
                (otpBox4 != null ? otpBox4.getText().toString().trim() : "");

        if (enteredOtp.length() != 4) {
            errorLogin.setVisibility(View.VISIBLE);
            errorLogin.setText("Please enter complete 4-digit OTP.");
            errorLogin.setTextColor(Color.RED);
            return;
        }

        String savedOtp = appConstants.getShrdPrefValByKey(getApplicationContext(), "otp");
        if (savedOtp != null && savedOtp.contains(enteredOtp)) {
            dialog = ProgressDialog.show(LoginActivity.this, "", "Logging in...", true);
            new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        if (isNetworkAvailable()) {
                            try {
                                passengerinfo = webServices.GetPsngrInfoWithValidation(tempMobileNo, "Validate");
                                if (passengerinfo != null && !passengerinfo.isEmpty() && !passengerinfo.contains("No Data")) {
                                    appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", passengerinfo);
                                }
                            } catch (Exception ex) {
                                android.util.Log.e("LoginActivity", "Error fetching passengerinfo on OTP submit", ex);
                            }
                            String userMenus = null;
                            try {
                                String accId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                                userMenus = webServices.GetMenusByUser(accId);
                                if (userMenus != null && !userMenus.isEmpty() && !userMenus.contains("No Data")) {
                                    appConstants.putShrdPrefValWithKey(getApplicationContext(), "UserMenus", userMenus);
                                }
                            } catch (Exception ex) {
                                android.util.Log.e("LoginActivity", "Error fetching UserMenus on OTP submit", ex);
                            }
                            String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                            int accountId = 0;
                            try { if (accountIdStr != null) accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}

                            AccountConfig accountConfig = null;
                            boolean privacyAccepted = true;
                            try {
                                if (accountId > 0) {
                                    String configJson = webServices.GetAccountConfig(accountId);
                                    accountConfig = AccountConfig.fromJson(configJson);
                                    
                                    // Log LOGIN activity audit event
                                    if (accountConfig != null && accountConfig.activityLogEnabled) {
                                        webServices.logAuditActivity(tempMobileNo, accountId, "LOGIN", "", "");
                                    }

                                    // Check Privacy Policy Acceptance Status
                                    if (accountConfig != null && accountConfig.privacyPolicyEnabled) {
                                        String privacyResp = webServices.CheckPrivacyAccepted(tempMobileNo);
                                        if (privacyResp != null && privacyResp.contains("\"accepted\":false")) {
                                            privacyAccepted = false;
                                        }
                                    }
                                }
                            } catch (Exception ex) {
                                android.util.Log.e("LoginActivity", "Error fetching AccountConfig / Privacy status", ex);
                            }

                            String tagResultOtp = webServices.GetPsngrInfoWithValidation(tempMobileNo, "Tag");
                            final Intent targetIntent;
                            if (userMenus != null && userMenus.contains("dashboard")) {
                                // Dashboard present: Always route to MainActivity (Grid Dashboard)
                                targetIntent = new Intent(getApplicationContext(), MainActivity.class);
                            } else if (userMenus != null && userMenus.contains("school_bus_tracking")) {
                                // Legacy School Bus Tracking: Go directly to live map (TrackOnMap)
                                targetIntent = new Intent(getApplicationContext(), TrackOnMap.class);
                                targetIntent.putExtra("tagDetails", tagResultOtp);
                            } else if (userMenus != null && userMenus.contains("assigned_veh_tracking")) {
                                // SmartTrack flow: go directly to TagTrack (Status + Map tabs)
                                targetIntent = new Intent(getApplicationContext(), TagTrack.class);
                                targetIntent.putExtra("tagDetails", tagResultOtp);
                            } else if (tagResultOtp != null && tagResultOtp.contains("TagOut")) {
                                targetIntent = new Intent(getApplicationContext(), TagOut.class);
                                targetIntent.putExtra("details", tagResultOtp);
                            } else if (userMenus != null && userMenus.contains("checklist")) {
                                targetIntent = new Intent(getApplicationContext(), VehicleInfo.class);
                            } else {
                                targetIntent = new Intent(getApplicationContext(), MainActivity.class);
                            }
                            targetIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);

                            final boolean finalPrivacyAccepted = privacyAccepted;
                            final AccountConfig finalAccountConfig = accountConfig;
                            final int finalAccountId = accountId;

                            runOnUiThread(new Runnable() {
                                @Override
                                public void run() {
                                    if (dialog != null && dialog.isShowing()) dialog.dismiss();
                                    scheduleLoginActivityLogDelayed("Login_Success", 1200);

                                    if (!finalPrivacyAccepted && finalAccountConfig != null && finalAccountConfig.privacyPolicyEnabled) {
                                        PrivacyPolicyDialog.show(LoginActivity.this, tempMobileNo, finalAccountId, finalAccountConfig.privacyPolicyText, new PrivacyPolicyDialog.OnPrivacyPolicyAcceptedListener() {
                                            @Override
                                            public void onAccepted() {
                                                startActivity(targetIntent);
                                                finish();
                                            }
                                        });
                                    } else {
                                        startActivity(targetIntent);
                                        finish();
                                    }
                                }
                            });
                        } else {
                            if (dialog != null && dialog.isShowing()) dialog.dismiss();
                            Intent i = new Intent(getApplicationContext(), OOPs.class);
                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                            i.putExtra("message", "OOPs..! Internet is not there. Please check the connection and Try again.");
                            startActivity(i);
                        }
                    } catch (Exception e) {
                        if (dialog != null && dialog.isShowing()) dialog.dismiss();
                        e.printStackTrace();
                    }
                }
            }).start();
        } else {
            errorLogin.setVisibility(View.VISIBLE);
            errorLogin.setText("Invalid OTP entered. Try again.");
            errorLogin.setTextColor(Color.RED);
        }
    }

    private void submitOtp(){
        if(isNetworkAvailable()) {
            final String res = webServices.PassengerProApp_Authenticate(tempMobileNo);
            try {
                if (res != null && res.startsWith("{")) {
                    org.json.JSONObject obj = new org.json.JSONObject(res);
                    if (obj.has("otp")) {
                        String backendOtp = obj.getString("otp");
                        appConstants.putShrdPrefValWithKey(getApplicationContext(), "otp", backendOtp);
                    }
                }
            } catch (Exception ignored) {}
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (res != null && (res.contains("OTP Sent Successfully") || res.contains("SMS Send Successfully"))) {
                        phno.setEnabled(false);
                        if (otpContainer != null) otpContainer.setVisibility(View.VISIBLE);
                        btnLogin.setText("VERIFY & SUBMIT  ➔");
                        String last4 = phoneno.length() >= 4 ? phoneno.substring(phoneno.length() - 4) : phoneno;
                        infotxt.setText("OTP is sent to mobile number ending in ******" + last4);
                        if (otpBox1 != null) {
                            otpBox1.setText("");
                            if (otpBox2 != null) otpBox2.setText("");
                            if (otpBox3 != null) otpBox3.setText("");
                            if (otpBox4 != null) otpBox4.setText("");
                            otpBox1.requestFocus();
                        }
                    } else {
                        errorLogin.setVisibility(View.VISIBLE);
                        errorLogin.setText("Could not send OTP. Try again.");
                        errorLogin.setTextColor(Color.RED);
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
