package com.sensel.passengerpro;

/**
 * Created by User on 29-04-2016.
 */

import android.app.ProgressDialog;
import android.content.Context;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Bundle;
import androidx.appcompat.app.ActionBar;
import androidx.appcompat.app.AppCompatActivity;
import android.view.MenuItem;
import android.view.View;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;


public class Notifications extends AppCompatActivity {

    Context cont;
    public String result;
    List<RowItem> rowItems;
    TextView textView;
    int count=0;
    WebServices webServices=new WebServices();
    AppConstants appConstants=new AppConstants();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.notifications);
        PassengerActivityLogger.log(this, "Notifications");

        // Log CLICK_NOTIFICATIONS activity audit event
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    String mobileNo = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "MobileNo");
                    String accountIdStr = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AccountId");
                    int accountId = 0;
                    try { if (accountIdStr != null) accountId = Integer.parseInt(accountIdStr); } catch (Exception ignored) {}
                    webServices.logAuditActivity(mobileNo, accountId, "CLICK_NOTIFICATIONS", "", "");
                } catch (Exception ignored) {}
            }
        }).start();

        ActionBar actionBar = getSupportActionBar();

        if (actionBar != null){
            actionBar.setDisplayHomeAsUpEnabled(true);
            actionBar.setHomeButtonEnabled(true);
        }
        cont=this.getApplicationContext();
        View btnBack = findViewById(R.id.btn_back);
        if (btnBack != null) {
            btnBack.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    finish();
                }
            });
        }
        rowItems = new ArrayList<RowItem>();
        textView = (TextView) findViewById(R.id.text);
        textView.setVisibility(View.GONE);
        final ProgressDialog progressDialog = new ProgressDialog(Notifications.this);
        progressDialog.setCancelable(false);
        progressDialog.setMessage("Loading ...  ");
        progressDialog.show();

        if(isNetworkAvailable()) {
            Thread splashThread = new Thread() {
                @Override
                public void run() {
                    try {
                        int waited = 0;
                        while (waited < 1000) {
                            sleep(100);
                            waited += 100;
                            if (waited == 100) {
                                String psngrId=appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(),"passengerinfo","PsngrId");
                                result = webServices.GetPsngrNotifications(psngrId);
                                createnotification();
                                webServices.PsngrNotificationNotified(psngrId);
                            }
                        }
                    } catch (Exception e) {
                        runOnUiThread(new Runnable() {
                            public void run() {
                                Toast.makeText(getApplicationContext(),"OOPS Unable to contact server",Toast.LENGTH_SHORT).show();
                            }
                        });
                    } finally {
                        progressDialog.dismiss();
                    }
                }

            };

            splashThread.start();
        }
        else {
            Toast.makeText(cont,"No internet connection",Toast.LENGTH_SHORT).show();
        }
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


    //Create notification panel
    public  void createnotification()
    {
        try {
            final  JSONArray jArr = new JSONArray(result);
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    for (int j = 0; j < jArr.length(); j++) {
                        try {
                            JSONObject data = jArr.getJSONObject(j);
                            String date = data.optString("DateTime", "");
                            if (date.contains("(") && date.contains(")")) {
                                try {
                                    date = date.substring(date.indexOf("(") + 1, date.indexOf(")"));
                                    date = new SimpleDateFormat("dd/MM/yyyy HH:mm:ss").format(new Date(Long.parseLong(date)));
                                } catch (Exception ignored) {}
                            }
                            RowItem item = new RowItem(R.drawable.ic_launcher, data.optString("Subject", "Notification"), data.optString("Info", ""), date, data.optString("IsNotified", "0"));
                            rowItems.add(item);
                            count++;

                        } catch (Exception js) {
                            // Toast.makeText(cont,"Oops something went wrong, Try again",Toast.LENGTH_SHORT).show();
                        }
                    }

                    ListView listView = (ListView) findViewById(R.id.list);
                    NotificationAdapter adapter = new NotificationAdapter(Notifications.this,
                            R.layout.notificationlist, rowItems);
                    listView.setAdapter(adapter);
                }

            });
        }
        catch (JSONException e){
            // Toast.makeText(cont,"Oops something went wrong, Try again",Toast.LENGTH_SHORT).show();
        }
        this.runOnUiThread(new Runnable() {
            public void run() {
                if (count < 1)
                    textView.setVisibility(View.VISIBLE);
            }
        });
    }

    private boolean isNetworkAvailable() {
        ConnectivityManager connectivityManager
                = (ConnectivityManager) getSystemService(Context.CONNECTIVITY_SERVICE);
        NetworkInfo activeNetworkInfo = connectivityManager.getActiveNetworkInfo();
        return activeNetworkInfo != null && activeNetworkInfo.isConnected();
    }
}