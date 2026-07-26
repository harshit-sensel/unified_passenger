package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.os.Build;
import android.os.Bundle;
import androidx.fragment.app.Fragment;
import androidx.appcompat.view.ContextThemeWrapper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.GeolocationPermissions;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

/**
 * Created by MS on 23-May-18.
 */

public class MapFragment extends Fragment {
    private boolean mapErrorToastShown = false;

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        Thread.setDefaultUncaughtExceptionHandler(new UnCaughtException(getContext(), getActivity()));
        View rootView = inflater.inflate(R.layout.map_tab, container, false);
        final AlertDialog progressDialog =  ProgressDialog.show(getContext(), "", "Loading...", true);


        final WebView myWebView = (WebView) rootView.findViewById(R.id.showposition);

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
                        Toast.makeText(getActivity(), msg, Toast.LENGTH_LONG).show();
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
        call(myWebView, getContext());

        return rootView;
    }
    @JavascriptInterface
    public void call(WebView my,Context context)
    {

        try {
            my.getSettings().setUseWideViewPort(true);
            if(TagTrack.sessionid!="")
                my.loadUrl(UrlConfig.MAP_PAGE_URL + "?sessionid="+TagTrack.sessionid+"&vehicleid="+TagTrack.vehicleid+"&domain="+UrlConfig.MAP_DOMAIN+"&defaultZoom=18&zoom=18&scaleMeters=50&drawRoute=1&showRouteLine=1&animateVehicle=1");
            else
                my.loadUrl(UrlConfig.MAP_PAGE_URL);

        }
        catch (Exception e){
            if(!((Activity) context).isFinishing()) {
                final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                        new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
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