package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.os.Build;
import android.os.Bundle;

import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;

import android.view.MenuItem;
import android.webkit.GeolocationPermissions;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

public class TrackOnMapWithSelection extends AppCompatActivity {
    String vehicleid="";
    String sessionid="";
    private boolean mapErrorToastShown = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        vehicleid=getIntent().getStringExtra("vehicleid");
        sessionid=getIntent().getStringExtra("sessionid");
        super.onCreate(savedInstanceState);
        setContentView(R.layout.track_on_map);
        PassengerActivityLogger.log(this, "TrackOnMapWithSelection");

        ActionBar actionBar = getSupportActionBar();
        if (actionBar != null){
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }

        final AlertDialog progressDialog =  ProgressDialog.show(TrackOnMapWithSelection.this, "", "Loading...", true);

        final WebView myWebView = (WebView) findViewById(R.id.showposition);

        myWebView.setWebViewClient(
                new WebViewClient() {
                    @Override
                    public boolean shouldOverrideUrlLoading(WebView view, String url) {
                        view.loadUrl(url);
                        return true;
                    }

                    @Override
                    public void onPageFinished(WebView view, String url) {
                        progressDialog.dismiss();
                    }
                    @Override
                    public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                        boolean isMainFrame = Build.VERSION.SDK_INT >= Build.VERSION_CODES.N && request.isForMainFrame();
                        if (!isMainFrame) return;
                        if (mapErrorToastShown) return;
                        mapErrorToastShown = true;
                        String msg = "Map could not load. Check your internet connection.";
                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && error != null && error.getDescription() != null) {
                            CharSequence desc = error.getDescription();
                            if (desc != null && desc.length() > 0) msg = msg + " " + desc;
                        }
                        Toast.makeText(getApplicationContext(), msg, Toast.LENGTH_LONG).show();
                    }
                });
        myWebView.setWebChromeClient(new WebChromeClient() {
            public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                callback.invoke(origin, true, false);
            }
        });

        WebSettings settings = myWebView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }
        //Added by Madhuri for migration to sdkversion33
        //settings.setAppCacheEnabled(true);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        call(myWebView, getApplicationContext());
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case android.R.id.home:
                finish();
                return true;
        }
        return super.onOptionsItemSelected(item);
    }

    @JavascriptInterface
    public void call(WebView my,Context context)
    {

        try {
            my.getSettings().setUseWideViewPort(true);
            if(sessionid!="") {
                // 50m zoom + server hints: draw blue route, animate vehicle
                my.loadUrl(UrlConfig.MAP_PAGE_URL + "?sessionid=" + sessionid + "&vehicleid=" + vehicleid + "&domain=" + UrlConfig.MAP_DOMAIN + "&defaultZoom=18&zoom=18&scaleMeters=50&drawRoute=1&showRouteLine=1&animateVehicle=1");
            }
            else
                my.loadUrl(UrlConfig.MAP_PAGE_URL);

        }
        catch (Exception e){
            if(!((Activity) context).isFinishing()) {
                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                        new ContextThemeWrapper(TrackOnMapWithSelection.this, android.R.style.Theme_Holo_Light_Dialog));
                alertDialogBuilder.setIcon(R.drawable.error);
                alertDialogBuilder.setTitle("Error ");
                alertDialogBuilder.setMessage("Unable to load, Please try again ")
                        .setCancelable(false)
                        .setPositiveButton("Ok",
                                new DialogInterface.OnClickListener() {
                                    public void onClick(DialogInterface dialog, int id) {
                                        dialog.cancel();
                                    }
                                });
                AlertDialog alert = alertDialogBuilder.create();
                if(!alert.isShowing())
                    alert.show();
            }
        }
    }
}
