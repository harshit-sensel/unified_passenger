package com.sensel.passengerpro;

import android.app.Activity;
import android.content.Context;

import androidx.appcompat.view.ContextThemeWrapper;

import android.webkit.JavascriptInterface;
import android.webkit.WebView;

/**
 * Created by Vamsi on 22-10-2017.
 */
public class JavaScriptInterface {
    Activity activity;
    WebView webView;
    public static Context con;

    public JavaScriptInterface(Activity _contxt, WebView _webView, Context _cont) {
        activity = _contxt;
        webView = _webView;
        con = _cont;
    }

    @JavascriptInterface
    public String GetPlace() {
        return  HomeLocationOnMap.latlng;
    }

    @JavascriptInterface
    public String GetLocation() {
        GPSTracker gpsTracker=new GPSTracker(con);
        return  gpsTracker.getLocation();
    }

    @JavascriptInterface
    public void setPlace(String latlng) {
        HomeLocationOnMap.latlng=latlng;
    }
}

