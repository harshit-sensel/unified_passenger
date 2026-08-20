package com.sensel.passengerpro;

import android.Manifest;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import com.google.android.material.tabs.TabLayout;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.viewpager.widget.ViewPager;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import androidx.appcompat.widget.Toolbar;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.animation.AlphaAnimation;
import android.view.animation.Animation;
import android.view.animation.LinearInterpolator;
import android.widget.ImageView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * Created by MS on 23-May-18.
 */
public class TagTrack extends BaseActivity {
    AppConstants appConstants=new AppConstants();
    public static String vehicleid = "No Vehicle Assigned";
    public static String sessionid = "";
    public static String tagintime = "0";
    public static String tagtype = "TagIn";
    private static final int MY_PERMISSIONS_REQUEST_READ_CONTACTS = 100;
    private static final int MY_PERMISSIONS_REQUEST_READ_LOCATION = 101;
    WebServices webServices=new WebServices();
    ProgressDialog progressDialog;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        String resultFromPrev=getIntent().getStringExtra("tagDetails");
        super.onCreate(savedInstanceState);
        setContentView(R.layout.tag_track);
        PassengerActivityLogger.log(this, "TagTrack");
        Toolbar toolbar = (Toolbar) findViewById(R.id.toolbar);
        setSupportActionBar(toolbar);

        TabLayout tabLayout = (TabLayout) findViewById(R.id.tab_layout);
        tabLayout.addTab(tabLayout.newTab().setText("Status"));
        tabLayout.addTab(tabLayout.newTab().setText("Track"));
        tabLayout.setTabGravity(TabLayout.GRAVITY_FILL);

        final ViewPager viewPager = (ViewPager) findViewById(R.id.pager);
        final PagerAdapter adapter = new PagerAdapter
                (getSupportFragmentManager(), tabLayout.getTabCount());
        viewPager.setAdapter(adapter);
        viewPager.addOnPageChangeListener(new TabLayout.TabLayoutOnPageChangeListener(tabLayout));
        tabLayout.setOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                CharSequence t = tab.getText();
                PassengerActivityLogger.log(TagTrack.this, "TagTrack_Tab_" + (t != null ? t : ""));
                viewPager.setCurrentItem(tab.getPosition());
            }

            @Override
            public void onTabUnselected(TabLayout.Tab tab) {

            }

            @Override
            public void onTabReselected(TabLayout.Tab tab) {

            }
        });
        try {
            if (resultFromPrev != null && !resultFromPrev.trim().isEmpty() && !"No Data".equalsIgnoreCase(resultFromPrev.trim())) {
                JSONArray jArr = new JSONArray(resultFromPrev);
                for (int j = 0; j < jArr.length(); j++) {
                    JSONObject data = jArr.getJSONObject(j);

                    String vId = data.optString("VehicleId", "").trim();
                    if (!vId.isEmpty() && !"null".equalsIgnoreCase(vId)) {
                        vehicleid = vId;
                    } else {
                        vehicleid = "No Vehicle Assigned";
                    }

                    String sId = data.optString("sessionid", "").trim();
                    if (!sId.isEmpty() && !"null".equalsIgnoreCase(sId)) {
                        sessionid = sId;
                    } else {
                        sessionid = "";
                    }

                    String tTime = data.optString("TagInTime", "").trim();
                    if (!tTime.isEmpty() && !"null".equalsIgnoreCase(tTime) && tTime.contains("(") && tTime.contains(")")) {
                        tagintime = tTime.substring(tTime.indexOf("(") + 1, tTime.indexOf(")"));
                    } else if (!tTime.isEmpty() && !"null".equalsIgnoreCase(tTime)) {
                        tagintime = tTime;
                    } else {
                        tagintime = "0";
                    }

                    String st = data.optString("Status", "").trim();
                    if (!st.isEmpty() && !"null".equalsIgnoreCase(st)) {
                        tagtype = st;
                    } else {
                        tagtype = "TagIn";
                    }
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        }

        // Safety fallback: if vehicleid is still unassigned, retrieve AssignedVehicleId from saved passenger profile
        if (vehicleid == null || vehicleid.trim().isEmpty() || "No Vehicle Assigned".equalsIgnoreCase(vehicleid) || "null".equalsIgnoreCase(vehicleid)) {
            String savedVeh = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "AssignedVehicleId");
            if (savedVeh != null && !savedVeh.trim().isEmpty() && !"null".equalsIgnoreCase(savedVeh)) {
                vehicleid = savedVeh.trim();
            }
        }
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
    @Override
    public boolean onPrepareOptionsMenu(final Menu menu) {
        getMenuInflater().inflate(R.menu.menu, menu);
        MenuItem item = menu.findItem(R.id.menu_refresh);
        item.setVisible(false);
        if(vehicleid=="No Vehicle Assigned" || tagtype.equals("TagIn")) {
            item = menu.findItem(R.id.menu_panic);
            item.setVisible(false);
        }
        else{
            item = menu.findItem(R.id.menu_panic);
            item.setVisible(true);
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
                                    new ContextThemeWrapper(TagTrack.this, android.R.style.Theme_Holo_Light_Dialog));
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
                                                    progressDialog = ProgressDialog.show(TagTrack.this, "", "Loading...", true);
                                                    new Thread(new Runnable() {
                                                        @Override
                                                        public void run() {
                                                            final String res=webServices.InsertPanicAlertFromApp(appConstants.getShrdPrefValByKeyWithTag(TagTrack.this,"passengerinfo","PsngrId"),vehicleid,"Passenger");
                                                            runOnUiThread(new Runnable() {
                                                                @Override
                                                                public void run() {
                                                                    if(res.contains("Inserted Successfully"))
                                                                        Toast.makeText(getApplicationContext(),"Panic alert sent successfully",Toast.LENGTH_SHORT).show();
                                                                    else {
                                                                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                        errorRecordSendMail.errorrecordSendMail(res + "-TagOut("+new Exception().getStackTrace()[0].getLineNumber()+")-" + appConstants.getShrdPrefValByKeyWithTag(TagTrack.this,"passengerinfo","MobileNo")+"-InsertPanicAlertFromApp("+appConstants.getShrdPrefValByKeyWithTag(TagTrack.this,"passengerinfo","PsngrId")+","+vehicleid+",\"Passenger\")");
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
        }
        return super.onCreateOptionsMenu(menu);
    }
    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case R.id.menu_logout:
                if(vehicleid=="No Vehicle Assigned" || tagtype.equals("TagIn")){
                    runOnUiThread(new Runnable() {
                        public void run() {
                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                    new ContextThemeWrapper(TagTrack.this, android.R.style.Theme_Holo_Light_Dialog));
                            alertDialogBuilder.setIcon(R.drawable.error);
                            alertDialogBuilder.setTitle("Action ");
                            alertDialogBuilder.setMessage("Are you sure?")
                                    .setCancelable(false)
                                    .setPositiveButton("Cancel",
                                            new DialogInterface.OnClickListener() {
                                                public void onClick(DialogInterface dialog, int id) {
                                                    dialog.cancel();
                                                }
                                            })
                                    .setNeutralButton("Logout",
                                            new DialogInterface.OnClickListener() {
                                                public void onClick(DialogInterface dialog, int id) {
                                                    dialog.cancel();
                                                    appConstants.putShrdPrefValWithKey(getApplicationContext(), "passengerinfo", null);
                                                    Intent i = new Intent(getApplicationContext(), LoginActivity.class);
                                                    i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                    startActivity(i);
                                                }
                                            })
                                    .setNegativeButton("Exit",
                                            new DialogInterface.OnClickListener() {
                                                public void onClick(DialogInterface dialog, int id) {
                                                    dialog.cancel();
                                                    Intent intent = new Intent(Intent.ACTION_MAIN);
                                                    intent.addCategory(Intent.CATEGORY_HOME);
                                                    intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                                                    startActivity(intent);
                                                    finish();
                                                    System.exit(0);
                                                }
                                            });
                            AlertDialog alert = alertDialogBuilder.create();
                            alert.show();
                        }
                    });
                }
                else {
                    runOnUiThread(new Runnable() {
                        public void run() {
                            final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                    new ContextThemeWrapper(TagTrack.this, android.R.style.Theme_Holo_Light_Dialog));
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
                }
                break;
        }
        return super.onOptionsItemSelected(item);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode,String permissions[], int[] grantResults) {
        switch (requestCode) {
            case MY_PERMISSIONS_REQUEST_READ_CONTACTS: {
                // If request is cancelled, the result arrays are empty.
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    if (ContextCompat.checkSelfPermission(TagTrack.this,
                            Manifest.permission.ACCESS_FINE_LOCATION)
                            != PackageManager.PERMISSION_GRANTED) {
                        ActivityCompat.requestPermissions(TagTrack.this,
                                new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                MY_PERMISSIONS_REQUEST_READ_LOCATION);
                    } else {
/*                        new Thread(new Runnable() {
                            @Override
                            public void run() {
                                StatusFragment statusFragment=new StatusFragment();
                                statusFragment.InsertTaging();
                            }
                        }).start();*/
                    }
                } else {
                    final AlertDialog ad = new AlertDialog.Builder(TagTrack.this).create();
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
            case MY_PERMISSIONS_REQUEST_READ_LOCATION: {
                if (grantResults.length > 0
                        && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                    /*new Thread(new Runnable() {
                        @Override
                        public void run() {
                            StatusFragment statusFragment=new StatusFragment();
                            statusFragment.InsertTaging();
                        }
                    }).start();*/
                } else {
                    final AlertDialog ad = new AlertDialog.Builder(TagTrack.this).create();
                    ad.setTitle("Permission Need");
                    ad.setMessage("Location permission is mandatory to TagIn.");
                    ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                        public void onClick(DialogInterface dialog, int which) {
                            dialog.dismiss();
                        }
                    });
                    ad.show();
                }
                return;
            }
            // other 'case' lines to check for other
            // permissions this app might request
        }
    }
}
