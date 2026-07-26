package com.sensel.passengerpro;

import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Bundle;
import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.MenuItem;
import android.view.View;
import android.widget.AdapterView;
import android.widget.AutoCompleteTextView;
import android.widget.TextView;
import android.widget.Toast;

import com.mmi.LicenceManager;
import com.mmi.MapView;
import com.mmi.MapmyIndiaMapView;
import com.mmi.layers.BasicInfoWindow;
import com.mmi.layers.Marker;
import com.mmi.util.GeoPoint;

import org.json.JSONArray;
import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;
import java.util.Timer;
import java.util.TimerTask;

public class TrackRegionVehiclesOnMap extends AppCompatActivity {
    AutoCompleteTextView autoCompleteVehTextView;
    WebServices webServices=new WebServices();
    AppConstants appConstants=new AppConstants();
    ProgressDialog dialog;
    MapView mMapView;
    Marker marker;
    BasicInfoWindow infoWindow;
    String trackingVehicle="";
    Timer timer = new Timer();

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        ActionBar actionBar = getSupportActionBar();

        if (actionBar != null){
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }
        LicenceManager.getInstance().setRestAPIKey("rvu8ga55okjz3u9qf76rsvgomzdmdd2h");
        LicenceManager.getInstance().setMapSDKKey("9bzttjsyzyp9nt5zv64xhmkhulvjgow1");
        setContentView(R.layout.mmi_region_veh_track);
        PassengerActivityLogger.log(this, "TrackRegionVehiclesOnMap");
        MapmyIndiaMapView mapMyIndiaMapView = (MapmyIndiaMapView)  findViewById(R.id.map);
        mMapView = mapMyIndiaMapView.getMapView();
        GeoPoint centerPoint= new GeoPoint(new GeoPoint(28.114938767,77.36472600));
        mMapView.setCenter(centerPoint);
        marker=new Marker(mMapView);
        infoWindow = new BasicInfoWindow(R.layout.mmi_tooltip, mMapView);
        infoWindow.setTipColor(R.color.black);
        autoCompleteVehTextView=(AutoCompleteTextView) findViewById(R.id.autoVehText);
        final TextView textView=(TextView) findViewById(R.id.autoVehText);
        String result=getIntent().getExtras().getString("details");
        String vehiclelist = "";
        timer.scheduleAtFixedRate(new TimerTask()
        {
            public void run()
            {
                if(!trackingVehicle.equals(""))  {
                    loadVehicleData();
                }// display the data
            }
        }, 1000, 20000);
        try {
            if (result.contains("VehicleId")) {
                final JSONArray jArr = new JSONArray(result);
                for (int j = 0; j < jArr.length(); j++) {
                    JSONObject data = jArr.getJSONObject(j);

                    if (data.getString("VehicleId").trim().length() > 0) {
                        vehiclelist = vehiclelist + data.getString("VehicleId") + "#";
                    }
                }
            }
            final String[] vehicles = vehiclelist.split("\\#");
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    List<Names> list = new ArrayList<Names>();
                    for (int i = 0; i < vehicles.length; i++) {
                        list.add(new Names(vehicles[i]));
                    }
                    CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                            TrackRegionVehiclesOnMap.this,
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
                    autoCompleteVehTextView.addTextChangedListener(new TextWatcher() {
                        @Override
                        public void beforeTextChanged(CharSequence s, int start, int count, int after) {

                        }

                        @Override
                        public void onTextChanged(CharSequence s, int start, int before, int count) {

                        }

                        @Override
                        public void afterTextChanged(Editable s) {

                        }
                    });
                    autoCompleteVehTextView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                        @Override
                        public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                            if (isNetworkAvailable()) {
                                dialog = ProgressDialog.show(TrackRegionVehiclesOnMap.this, "", "Loading...", true);
                                new Thread(new Runnable() {
                                    @Override
                                    public void run() {
                                        try {
                                            loadVehicleData();
                                        }
                                        catch (final Exception e){
                                            e.printStackTrace();
                                            runOnUiThread(new Runnable() {
                                                public void run() {
                                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                            new ContextThemeWrapper(TrackRegionVehiclesOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
                                                    alertDialogBuilder.setIcon(R.drawable.error);
                                                    alertDialogBuilder.setTitle("Error ");
                                                    alertDialogBuilder.setMessage("Failed to load data. Please try again")
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
                                        finally {
                                            dialog.dismiss();
                                        }
                                    }
                                }).start();
                            }
                            else{
                                runOnUiThread(new Runnable() {
                                    public void run() {
                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                new ContextThemeWrapper(TrackRegionVehiclesOnMap.this, android.R.style.Theme_Holo_Light_Dialog));
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
            });
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public void loadVehicleData(){
        final String psngrID = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
        final String res = webServices.GetVehiclePositionForPsngrApp(psngrID, autoCompleteVehTextView.getText().toString());
        //final String res = "[{\"Select\":null,\"VehicleID\":\"UP32 GN 9091\",\"DateTime\":\"21 Oct 20, 02:48:41 PM\",\"Time\":null,\"LAt\":\"26.968537\",\"longi\":\"81.419505\",\"Speed\":\"0\",\"id\":\"1603291721\",\"VehicleInfo\":\"64945 Swiftdzire 915754080394740 \",\"Location\":\"Saidanpur - 225206, Barabanki, Uttar Pradesh\",\"direction\":\"412\",\"idlingInstance\":\"NC\",\"remarks\":\"VI,\",\"Status\":null,\"HaltDuration\":\"10/21/2020 2:18:44 PM\",\"DistfrmHome\":null,\"DirtoHome\":null,\"IconType\":\"Default\",\"AuxInput\":null,\"DriverInfo\":null,\"TripState\":null,\"TripStatus\":null,\"Capacity\":null,\"Battery\":\"4090\",\"IconSet\":null,\"UID\":null,\"Circle\":null}]";
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                addMarker(res);
            }
        });
    }

    public void addMarker(String res){
        if(res.contains("VehicleID")) {
            Date lastUpdt=null;
            try {
                lastUpdt = new SimpleDateFormat("dd MMM yy, hh:mm:ss a").parse(appConstants.getValueFromJSonByKey(res, "DateTime"));
            }
            catch(Exception e){}
            trackingVehicle=appConstants.getValueFromJSonByKey(res, "VehicleID");
            GeoPoint geoPoint = new GeoPoint(
                    new GeoPoint(Double.parseDouble( appConstants.getValueFromJSonByKey(res, "LAt")),
                            Double.parseDouble( appConstants.getValueFromJSonByKey(res, "longi"))));
            marker.remove(mMapView);
            marker.setTitle(trackingVehicle);
            marker.setDescription(appConstants.dateFormat.format(lastUpdt));
            marker.setSubDescription("<b>Address : </b>"+appConstants.getValueFromJSonByKey(res, "Location")+"<br>"+"<b>Status : </b>"
                    +appConstants.getVehicleStatusByCode( appConstants.getValueFromJSonByKey(res, "remarks"))+"<br>"
                    +"<b>Speed : </b>" +appConstants.getValueFromJSonByKey(res, "Speed")+"kmph<br><b>Last Sync : </b>"
                    +appConstants.dateFormat.format(Calendar.getInstance().getTime()));
            marker.setPosition(geoPoint);
            marker.setInfoWindow(infoWindow);
            marker.closeInfoWindow();
            mMapView.getOverlays().add(marker);
            mMapView.invalidate();
            mMapView.setCenter(geoPoint);
            mMapView.setZoom(15);
        }
        else{
            Toast.makeText(this,"Failed to get data",Toast.LENGTH_SHORT).show();
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
    public void onBackPressed() {
        super.onBackPressed();
        if(timer!=null){
            timer.cancel();
        }
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case android.R.id.home:
                onBackPressed();
                return true;
        }
        return super.onOptionsItemSelected(item);
    }
}
