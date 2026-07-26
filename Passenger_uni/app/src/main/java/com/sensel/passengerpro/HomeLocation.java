package com.sensel.passengerpro;

/**
 * Created by User on 29-04-2016.
 */

import android.content.Intent;
import android.os.Bundle;
import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import android.view.MenuItem;
import android.view.View;
import android.widget.Button;
import android.widget.TextView;


public class HomeLocation extends AppCompatActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.home_location);
        PassengerActivityLogger.log(this, "HomeLocation");

        ActionBar actionBar = getSupportActionBar();

        if (actionBar != null){
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }
        Button btnChangeLocation=(Button) findViewById(R.id.btnChangeLocation);
        TextView txtVehicleId=(TextView) findViewById(R.id.vehicleid);
        TextView txtLatLng=(TextView) findViewById(R.id.latlng);
        TextView txtAddress=(TextView) findViewById(R.id.address);
        AppConstants appConstants=new AppConstants();
        txtVehicleId.setText(appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AssignedVehicleId"));
        txtLatLng.setText(appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "HomeLatitude")+","+
                appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "HomeLongitude"));
        txtAddress.setText(appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "HomeLocationStr"));

        btnChangeLocation.setOnClickListener(
                new View.OnClickListener() {
                    @Override
                    public void onClick(View view) {
                        runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                Intent i = new Intent(getApplicationContext(), HomeLocationOnMap.class);
                                i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                startActivity(i);
                            }
                        });
                    }
                }
        );
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

}