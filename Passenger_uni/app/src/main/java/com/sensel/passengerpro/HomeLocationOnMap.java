package com.sensel.passengerpro;

import android.Manifest;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.ComponentName;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Bundle;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.core.content.IntentCompat;
import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import android.view.Menu;
import android.view.MenuItem;
import android.webkit.GeolocationPermissions;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

public class HomeLocationOnMap extends AppCompatActivity {
    public static String latlng=null;
    private static final int MY_PERMISSIONS_REQUEST_LOCATION = 10;
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.home_location_on_map);
        PassengerActivityLogger.log(this, "HomeLocationOnMap");

        ActionBar actionBar = getSupportActionBar();

        if (actionBar != null){
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }
        if (ContextCompat.checkSelfPermission(HomeLocationOnMap.this,
                Manifest.permission.ACCESS_FINE_LOCATION)
                != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(HomeLocationOnMap.this,
                    new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                    MY_PERMISSIONS_REQUEST_LOCATION);
        } else {
            final ProgressDialog progressDialog = new ProgressDialog(HomeLocationOnMap.this);
            progressDialog.setCancelable(false);
            progressDialog.setMessage("Loading Please wait...");
            progressDialog.show();

            WebView myWebView = (WebView) findViewById(R.id.map_webview);
            myWebView.setWebViewClient(
                    new WebViewClient() {
                        @Override
                        public boolean shouldOverrideUrlLoading(WebView view, String url) {
                            view.loadUrl(url);
                            return true;
                        }
                    });
            myWebView.setWebChromeClient(new WebChromeClient() {
                public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                    callback.invoke(origin, true, false);
                }
            });
            myWebView.setWebViewClient(new WebViewClient() {

                public void onPageFinished(WebView view, String url) {
                    progressDialog.dismiss();
                }
            });


            WebSettings settings = myWebView.getSettings();
            settings.setJavaScriptEnabled(true);
            settings.setDomStorageEnabled(true);
            settings.setAllowFileAccess(true);
            //settings.setAppCacheEnabled(true);
            settings.setCacheMode(WebSettings.LOAD_CACHE_ELSE_NETWORK);
            JavaScriptInterface jsInterface = new JavaScriptInterface(this, myWebView, getApplicationContext());
            myWebView.addJavascriptInterface(jsInterface, "JSInterface");
            myWebView.loadUrl("file:///android_asset/DeviationReport.html");
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode,String permissions[], int[] grantResults) {
        switch (requestCode) {
            case MY_PERMISSIONS_REQUEST_LOCATION: {
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    final ProgressDialog progressDialog = new ProgressDialog(HomeLocationOnMap.this);
                    progressDialog.setCancelable(false);
                    progressDialog.setMessage("Loading Please wait...");
                    progressDialog.show();

                    WebView myWebView = (WebView) findViewById(R.id.webview);
                    myWebView.setWebViewClient(
                            new WebViewClient() {
                                @Override
                                public boolean shouldOverrideUrlLoading(WebView view, String url) {
                                    view.loadUrl(url);
                                    return true;
                                }
                            });
                    myWebView.setWebChromeClient(new WebChromeClient() {
                        public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                            callback.invoke(origin, true, false);
                        }
                    });
                    myWebView.setWebViewClient(new WebViewClient() {

                        public void onPageFinished(WebView view, String url) {
                            progressDialog.dismiss();
                        }
                    });


                    WebSettings settings = myWebView.getSettings();
                    settings.setJavaScriptEnabled(true);
                    settings.setDomStorageEnabled(true);
                    settings.setAllowFileAccess(true);
                    //Added by Madhuri for migration to sdkversion33
                    //settings.setAppCacheEnabled(true);
                    settings.setCacheMode(WebSettings.LOAD_CACHE_ELSE_NETWORK);
                    JavaScriptInterface jsInterface = new JavaScriptInterface(this, myWebView, getApplicationContext());
                    myWebView.addJavascriptInterface(jsInterface, "JSInterface");
                    myWebView.loadUrl("file:///android_asset/DeviationReport.html");
                }
                else {
                    final AlertDialog ad=new AlertDialog.Builder(new ContextThemeWrapper(this, android.R.style.Theme_Holo_Light_Dialog)).create();
                    ad.setIcon(R.drawable.error);
                    ad.setTitle("Need Location Permission");
                    ad.setMessage("Need Location permission to run app.");
                    ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int which) {
                            dialog.dismiss();
                            finish();
                        }
                    });
                    ad.show();
                }
            }
        }
    }

    @Override
    public boolean onPrepareOptionsMenu(final Menu menu) {
        getMenuInflater().inflate(R.menu.menu_submit, menu);
        return super.onCreateOptionsMenu(menu);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case android.R.id.home:
                latlng=null;
                finish();
                break;
            case R.id.menu_add:
                if(HomeLocationOnMap.latlng==null){
                    Toast.makeText(getApplicationContext(),"Choose your home location in map",Toast.LENGTH_LONG).show();
                }
                else {
                    runOnUiThread(new Runnable() {
                        public void run() {
                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                    new ContextThemeWrapper(HomeLocationOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
                            alertDialogBuilder.setIcon(R.drawable.error);
                            alertDialogBuilder.setTitle("Action ");
                            alertDialogBuilder.setMessage("Are you sure to change your home location?")
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
                                                    dialog.cancel();
                                                    if(isNetworkAvailable()){
                                                        final ProgressDialog progressDialog = new ProgressDialog(HomeLocationOnMap.this);
                                                        progressDialog.setCancelable(false);
                                                        progressDialog.setMessage("Loading Please wait...");
                                                        progressDialog.show();
                                                        new Thread(new Runnable() {
                                                            @Override
                                                            public void run() {
                                                                AppConstants appConstants=new AppConstants();
                                                                String psngrId=appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                                                                WebServices webServices=new WebServices();
                                                                String result=webServices.UpdatePsngrHomeLocation(psngrId,latlng.split(",")[0],latlng.split(",")[1]);
                                                                if(result.contains("Successfully")) {
                                                                    runOnUiThread(new Runnable() {
                                                                        @Override
                                                                        public void run() {
                                                                            Toast.makeText(getApplicationContext(), "Home location updated successfully", Toast.LENGTH_LONG).show();
                                                                        }
                                                                    });
                                                                    try { Thread.sleep(1200); } catch (Exception ignored) {}
                                                                    Intent mainIntent = new Intent(HomeLocationOnMap.this, LoginActivity.class);
                                                                    mainIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                                                                    startActivity(mainIntent);
                                                                    finish();
                                                                }
                                                                else{
                                                                    runOnUiThread(new Runnable() {
                                                                        @Override
                                                                        public void run() {
                                                                            Toast.makeText(getApplicationContext(),"Failed to update home location",Toast.LENGTH_LONG).show();
                                                                        }
                                                                    });
                                                                }
                                                                progressDialog.cancel();
                                                            }
                                                        }).start();
                                                    }
                                                    else{
                                                        Toast.makeText(getApplicationContext(),"No internet connection",Toast.LENGTH_LONG).show();
                                                    }
                                                }
                                            });
                            AlertDialog alert = alertDialogBuilder.create();
                            alert.show();
                        }
                    });
                }
                break;
        }
        return super.onOptionsItemSelected(item);
    }

    private boolean isNetworkAvailable() {
        ConnectivityManager connectivityManager
                = (ConnectivityManager) getSystemService(Context.CONNECTIVITY_SERVICE);
        NetworkInfo activeNetworkInfo = connectivityManager.getActiveNetworkInfo();
        return activeNetworkInfo != null && activeNetworkInfo.isConnected();
    }
}
