package com.sensel.passengerpro;

import android.Manifest;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.net.Uri;
import android.os.Bundle;

import androidx.core.app.ActivityCompat;
import androidx.fragment.app.Fragment;
import androidx.core.content.ContextCompat;
import androidx.appcompat.view.ContextThemeWrapper;

import android.text.Html;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.method.LinkMovementMethod;
import android.text.style.ClickableSpan;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AutoCompleteTextView;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;

/**
 * Created by MS on 23-May-18.
 */

public class StatusFragment extends Fragment {
    WebServices webServices=new WebServices();
    private static final int MY_PERMISSIONS_REQUEST_READ_CONTACTS = 100;
    private static final int MY_PERMISSIONS_REQUEST_READ_LOCATION = 101;
    public static Button btnTag;
    ProgressDialog progressDialog;
    String latlng="0,0";
    AppConstants appConstants=new AppConstants();
    int failedLogCount=0;
    String mobileno;
    int timeThreshold = 5;
    int distThreshold = 100;
    boolean tagInCondition=false;
    String vehList;

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        View rootView = inflater.inflate(R.layout.status_tab, container, false);
        final TextView txtvehicleid=(TextView) rootView.findViewById(R.id.vehicleid);
        TextView txttagInTime=(TextView) rootView.findViewById(R.id.tagInTime);
        btnTag=(Button) rootView.findViewById(R.id.btntag);
        TextView tagrow=(TextView) rootView.findViewById(R.id.tagrow);
        TextView comment=(TextView) rootView.findViewById(R.id.comment);
        final TextView taginCondition=(TextView) rootView.findViewById(R.id.taginCondition);
        TextView vehChangeLink=(TextView) rootView.findViewById(R.id.vehChangeLink);
        txtvehicleid.setText(TagTrack.vehicleid);
        if (TagTrack.tagintime != null && !TagTrack.tagintime.trim().isEmpty()) {
            try {
                long timeMs = Long.parseLong(TagTrack.tagintime.trim());
                txttagInTime.setText(new SimpleDateFormat("dd/MM/yyyy HH:mm:ss").format(new Date(timeMs)));
            } catch (Exception e) {
                txttagInTime.setText(TagTrack.tagintime);
            }
        } else {
            txttagInTime.setText("");
        }
        if ("No Vehicle Assigned".equalsIgnoreCase(TagTrack.vehicleid)) {
            btnTag.setText("Tag");
            btnTag.setVisibility(View.GONE);
            tagrow.setVisibility(View.GONE);
            txttagInTime.setVisibility(View.GONE);
            comment.setText("***Please contact your admin***");
            comment.setTextColor(Color.GRAY);
        }
        else if ("TagIn".equalsIgnoreCase(TagTrack.tagtype)) {
            btnTag.setText("TagIn");
            btnTag.setVisibility(View.VISIBLE);
            tagrow.setVisibility(View.GONE);
            txttagInTime.setVisibility(View.GONE);
            String userMenus = appConstants.getShrdPrefValByKey(getActivity().getApplicationContext(), "UserMenus");
            String appKeyWord = appConstants.getShrdPrefValByKeyWithTag(getActivity().getApplicationContext(), "passengerinfo", "AppKeyWord");
            boolean hasProximityCheck = (userMenus != null && userMenus.contains("proximity_check")) || (appKeyWord != null && appKeyWord.contains("-VLU") && appKeyWord.contains("-DT"));
            boolean hasVehicleChange = (userMenus != null && userMenus.contains("vehicle_change")) || (appKeyWord != null && appKeyWord.contains("-AVC1"));

            if (hasProximityCheck) {
                tagInCondition = true;
                timeThreshold = 15;
                distThreshold = 50;
                try {
                    if (appKeyWord != null && appKeyWord.contains("-VLU") && appKeyWord.contains("-DT")) {
                        String[] str = appKeyWord.split("-");
                        timeThreshold = Integer.valueOf(str[1].replace("VLU", ""));
                        distThreshold = Integer.valueOf(str[2].replace("DT", ""));
                    }
                } catch (Exception e) {
                    timeThreshold = 15;
                    distThreshold = 50;
                }
                String params="TagIn allowed only when <br><b>1)</b> Atleast one update of <b>"+TagTrack.vehicleid+"</b> has to be there in last <b>"
                        +timeThreshold+" minutes</b>.<br><b>2) "+TagTrack.vehicleid+"</b> has to be there in the range of <b>"+distThreshold+" meters</b>.";
                taginCondition.setText(Html.fromHtml(params));
                taginCondition.setVisibility(View.VISIBLE);
            }
            else {
                tagInCondition = false;
                taginCondition.setVisibility(View.GONE);
            }
            if (hasVehicleChange) {
                vehChangeLink.setVisibility(View.VISIBLE);
                new Thread(new Runnable() {
                    @Override
                    public void run() {
                        String accountId=appConstants.getShrdPrefValByKeyWithTag(getActivity().getApplicationContext(), "passengerinfo", "AccountId");
                        vehList =webServices.GetVehiclesByAccountId(accountId);
                    }
                }).start();
                ClickableSpan avc = new ClickableSpan() {
                    @Override
                    public void onClick(View view) {
                        new Thread(new Runnable() {
                            @Override
                            public void run() {
                                getActivity().runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        try {
                                            final JSONArray jArr = new JSONArray(vehList);
                                            getActivity().runOnUiThread(new Runnable() {
                                                @Override
                                                public void run() {
                                                    final List<Names> list = new ArrayList<Names>();
                                                    for (int j = 0; j <= jArr.length(); j++) {
                                                        try {
                                                            JSONObject data = jArr.getJSONObject(j);
                                                            if(!TagTrack.vehicleid.equals(data.getString("VehicleID")))
                                                                list.add(new Names(data.getString("VehicleID")));
                                                        } catch (Exception e) {
                                                        }
                                                    }
                                                    CustomAutoCompleteTextView adapter = new CustomAutoCompleteTextView(
                                                            getContext(),
                                                            android.R.layout.simple_list_item_1,
                                                            R.id.lbl_name,
                                                            list
                                                    );

                                                    LayoutInflater li = LayoutInflater.from(getContext());
                                                    View promptsView = li.inflate(R.layout.change_vehicle, null);
                                                    final AutoCompleteTextView autoCompleteVehTextView=(AutoCompleteTextView) promptsView.findViewById(R.id.autoVehText);
                                                    final Button changeBtn=(Button) promptsView.findViewById(R.id.btn);
                                                    final Button cancelBtn=(Button) promptsView.findViewById(R.id.btncancel);
                                                    autoCompleteVehTextView.setAdapter(adapter);
                                                    autoCompleteVehTextView.setText("");
                                                    autoCompleteVehTextView.setOnClickListener(new View.OnClickListener() {
                                                        public void onClick(View v) {
                                                            autoCompleteVehTextView.showDropDown();//Show full list of vehicle
                                                        }
                                                    });
                                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                                                    alertDialogBuilder.setView(promptsView);
                                                    alertDialogBuilder.setTitle("Change Assigned Vehicle");
                                                    alertDialogBuilder.setCancelable(false);
                                                    final AlertDialog alert = alertDialogBuilder.create();
                                                    alert.show();
                                                    cancelBtn.setOnClickListener(new View.OnClickListener() {
                                                        @Override
                                                        public void onClick(View v) {
                                                            alert.dismiss();
                                                        }
                                                    });
                                                    changeBtn.setOnClickListener(new View.OnClickListener() {
                                                        @Override
                                                        public void onClick(View v) {
                                                            boolean exists=false;
                                                            int index=0;
                                                            for(int i=index;i<list.size();i++){
                                                                if(list.get(i).name.toUpperCase().equals(autoCompleteVehTextView.getText().toString().toUpperCase())){
                                                                    exists=true;
                                                                    index=i;
                                                                    break;
                                                                }
                                                            }
                                                            if(exists){
                                                                final int finalIndex = index;
                                                                new Thread(new Runnable() {
                                                                    @Override
                                                                    public void run() {
                                                                        getActivity().runOnUiThread(new Runnable() {
                                                                            @Override
                                                                            public void run() {
                                                                                progressDialog = ProgressDialog.show(getContext(), "", "Loading...", true);
                                                                            }
                                                                        });
                                                                        String psngrId=appConstants.getShrdPrefValByKeyWithTag(getActivity().getApplicationContext(), "passengerinfo", "PsngrId");
                                                                        String res=webServices.UpdtPsngrAssgndVeh(psngrId,list.get(finalIndex).name);
                                                                        progressDialog.dismiss();
                                                                        if(res.equals("Updated Successfully")){
                                                                            getActivity().runOnUiThread(new Runnable() {
                                                                                public void run() {
                                                                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                                                                                    alertDialogBuilder.setTitle("Success ");
                                                                                    alertDialogBuilder.setMessage("Assigned Vehicle is changed")
                                                                                            .setCancelable(false)
                                                                                            .setPositiveButton("Ok",
                                                                                                    new DialogInterface.OnClickListener() {
                                                                                                        public void onClick(DialogInterface dialog, int id) {
                                                                                                            dialog.cancel();
                                                                                                            Intent i = getActivity().getBaseContext().getPackageManager().getLaunchIntentForPackage(getActivity().getBaseContext().getPackageName());
                                                                                                            i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                                                                                                            startActivity(i);
                                                                                                            getActivity().finish();
                                                                                                        }
                                                                                                    });
                                                                                    AlertDialog alert = alertDialogBuilder.create();
                                                                                    alert.show();
                                                                                }
                                                                            });
                                                                        }
                                                                        else{
                                                                            getActivity().runOnUiThread(new Runnable() {
                                                                                public void run() {
                                                                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                                                                                    alertDialogBuilder.setIcon(R.drawable.error);
                                                                                    alertDialogBuilder.setTitle("Error ");
                                                                                    alertDialogBuilder.setMessage("Assigned Vehicle Change is Failed")
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
                                                                }).start();
                                                            }
                                                            else{
                                                                Toast.makeText(getContext(),"Please select proper vehicle",Toast.LENGTH_SHORT).show();
                                                            }
                                                        }
                                                    });
                                                }
                                            });
                                        }
                                        catch (Exception e){}
                                    }
                                });
                            }
                        }).start();
                    }
                };
                makeLinks(vehChangeLink, new String[] { vehChangeLink.getText().toString() }, new ClickableSpan[] {
                        avc
                });
            }
            else
                vehChangeLink.setVisibility(View.GONE);
            comment.setText("***Click On TagIn to start your trip***");
            comment.setTextColor(Color.rgb(0,128,0));
        }
        else{
            btnTag.setText("TagOut");
            btnTag.setVisibility(View.VISIBLE);
            tagrow.setVisibility(View.VISIBLE);
            txttagInTime.setVisibility(View.VISIBLE);
            comment.setText("***Click On TagOut to end your trip***");
            comment.setTextColor(Color.RED);
        }
        btnTag.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                getActivity().runOnUiThread(new Runnable() {
                    public void run() {
                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                        alertDialogBuilder.setTitle(btnTag.getText());
                        alertDialogBuilder.setMessage("Are You Sure?")
                                .setCancelable(false)
                                .setPositiveButton("Cancel",
                                        new DialogInterface.OnClickListener(){
                                            public void onClick(DialogInterface dialog,int id){
                                                dialog.cancel();
                                            }
                                        })
                                .setNegativeButton(btnTag.getText(),
                                        new DialogInterface.OnClickListener() {
                                            public void onClick(DialogInterface dialog, int id) {
                                                new Thread(new Runnable() {
                                                    @Override
                                                    public void run() {
                                                        try {
                                                            if(isNetworkAvailable()) {
                                                                if (ContextCompat.checkSelfPermission(getContext(),
                                                                        Manifest.permission.READ_PHONE_STATE)
                                                                        != PackageManager.PERMISSION_GRANTED) {
                                                                    ActivityCompat.requestPermissions(getActivity(),
                                                                            new String[]{Manifest.permission.READ_PHONE_STATE},
                                                                            MY_PERMISSIONS_REQUEST_READ_CONTACTS);
                                                                }
                                                                else if(ContextCompat.checkSelfPermission(getContext(),
                                                                        Manifest.permission.ACCESS_FINE_LOCATION)
                                                                        != PackageManager.PERMISSION_GRANTED) {
                                                                    ActivityCompat.requestPermissions(getActivity(),
                                                                            new String[]{Manifest.permission.ACCESS_FINE_LOCATION},
                                                                            MY_PERMISSIONS_REQUEST_READ_LOCATION);
                                                                }
                                                                else{
                                                                    InsertTaging();
                                                                }
                                                            }
                                                            else{
                                                                getActivity().runOnUiThread(new Runnable() {
                                                                    public void run() {
                                                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                                                new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
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
                                                        catch (Exception e){
                                                            e.printStackTrace();
                                                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-"+btnTag.getText()+"("+new Exception().getStackTrace()[0].getLineNumber()+")-" + TagTrack.vehicleid);
                                                        }
                                                    }
                                                }).start();
                                            }
                                        });
                        AlertDialog alert = alertDialogBuilder.create();
                        alert.show();
                    }
                });
            }
        });
        mobileno = appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","MobileNo");
        new Thread(new Runnable() {
            @Override
            public void run() {
                String appdata = webServices.GetAppVersion(getContext().getPackageName());
                if (appdata != null) {
                    try {
                        if(appdata.contains("VersionCode")) {
                            JSONArray array = new JSONArray(appdata);
                            JSONObject data = new JSONObject(array.get(0).toString());
                            String _version = data.getString("VersionCode");
                            if (Integer.parseInt(_version) > BuildConfig.VERSION_CODE)
                                showUpdateAlert(Integer.parseInt(data.getString("Priority")), Integer.parseInt(data.getString("StableVersion")));
                            if(data.getString("DomainUrl").contains("http") && !UrlConfig.DOMAINURL1.equals(data.getString("DomainUrl"))) {
                                UrlConfig.DOMAINURL1 = data.getString("DomainUrl");
                                if (data.getString("DomainUrl").contains("https://"))
                                    UrlConfig.DOMAINURL2 = data.getString("DomainUrl").replace("https://", "http://");
                                else
                                    UrlConfig.DOMAINURL2 = data.getString("DomainUrl").replace("http://", "https://");
                            }
                        }
                    } catch (JSONException e) {
                        e.printStackTrace();
                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagTrack("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno+"-"+appdata);
                    }
                }
                else{
                    ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                    errorRecordSendMail.errorrecordSendMail(appdata + "-TagTrack("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno + "-GetAppVersion("+getContext().getPackageName()+")");
                }
            }
        }).start();
        return rootView;
    }

    private boolean isNetworkAvailable() {
        try {
            ConnectivityManager connectivityManager
                    = (ConnectivityManager) getActivity().getApplicationContext().getSystemService(Context.CONNECTIVITY_SERVICE);
            NetworkInfo activeNetworkInfo = connectivityManager.getActiveNetworkInfo();
            return activeNetworkInfo != null && activeNetworkInfo.isConnected();
        } catch (Exception e) {
            return true;
        }
    }

    public void InsertTaging() {
        try {
            LocationManager locationManager = (LocationManager) getActivity().getSystemService(Context.LOCATION_SERVICE);
            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)){
                final Thread thread=new Thread(new Runnable() {
                    @Override
                    public void run() {
                        getActivity().runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                progressDialog = ProgressDialog.show(getContext(), "", "Loading...", true);
                            }
                        });
                        String strRules = "";
                        //TelephonyManager telephonyManager = (TelephonyManager) getActivity().getSystemService(Context.TELEPHONY_SERVICE);
                        //String Imei = telephonyManager.getDeviceId();
                        String Imei=LoginActivity.IMEI;
                        String[] str=latlng.split(",");
                        String driverDetail="";
                        String result="";
                        result = webServices.InsertPsngrChecklist(appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","PsngrId")
                                , TagTrack.vehicleid, btnTag.getText().toString(), strRules, "", "", "0",Imei,str[0],str[1],"0",driverDetail,"0","","","","","","","");
                        if (result.contains("Inserted Successfully")) {
                            getActivity().runOnUiThread(new Runnable() {
                                public void run() {
                                    final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                                    alertDialogBuilder.setTitle("Status");
                                    alertDialogBuilder.setMessage(btnTag.getText()+" Done Successfully")
                                            .setPositiveButton("Ok",
                                                    new DialogInterface.OnClickListener() {
                                                        public void onClick(DialogInterface dialog, int id) {
                                                            dialog.cancel();
                                                            progressDialog = ProgressDialog.show(getContext(), "", "Loading...", true);
                                                            new Thread(new Runnable() {
                                                                @Override
                                                                public void run() {
                                                                    try {
                                                                        Intent i = new Intent(getContext(), LoginActivity.class);
                                                                        i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                                        startActivity(i);
                                                                    } catch (Exception e) {
                                                                        e.printStackTrace();
                                                                        ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                        errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","MobileNo"));
                                                                    } finally {
                                                                        if( progressDialog != null)
                                                                            progressDialog.dismiss();
                                                                    }
                                                                }
                                                            }).start();
                                                        }
                                                    });
                                    AlertDialog alert = alertDialogBuilder.create();
                                    alert.show();
                                }
                            });
                        } else {
                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                            errorRecordSendMail.errorrecordSendMail(result.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","MobileNo") + "-InsertPsngrChecklistForPsngr("+appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","PsngrId")+", "+TagTrack.vehicleid+", \""+btnTag.getText().toString()+"\", "+strRules+", "+", "+","+")");
                            failedLogCount++;
                            if(failedLogCount>5) {
                                failedLogCount=0;
                                getActivity().runOnUiThread(new Runnable() {
                                    public void run() {
                                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                                new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                                        alertDialogBuilder.setTitle("Status");
                                        alertDialogBuilder.setMessage(btnTag.getText()+" is Failed")
                                                .setPositiveButton("Ok",
                                                        new DialogInterface.OnClickListener() {
                                                            public void onClick(DialogInterface dialog, int id) {
                                                                dialog.cancel();
                                                                progressDialog = ProgressDialog.show(getContext(), "", "Loading...", true);
                                                                new Thread(new Runnable() {
                                                                    @Override
                                                                    public void run() {
                                                                        try {
                                                                            Intent i = new Intent(getContext(), LoginActivity.class);
                                                                            i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                                                            startActivity(i);
                                                                        } catch (Exception e) {
                                                                            e.printStackTrace();
                                                                            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                                            errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","MobileNo"));
                                                                        } finally {
                                                                            if( progressDialog != null)
                                                                                progressDialog.dismiss();
                                                                        }
                                                                    }
                                                                }).start();
                                                            }
                                                        });
                                        AlertDialog alert = alertDialogBuilder.create();
                                        alert.show();
                                    }
                                });
                            }
                            else
                                InsertTaging();
                        }
                    }
                });
                getActivity().runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        getActivity().runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                progressDialog = ProgressDialog.show(getContext(), "", "Loading...", true);
                            }
                        });
                        GPSTracker gpsTracker = new GPSTracker(getContext());
                        latlng = gpsTracker.getLocation();
                        if(tagInCondition){
                            Thread thread1=new Thread(new Runnable() {
                                @Override
                                public void run() {
                                    String psngrId = appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","PsngrId");
                                    final String result=webServices.GetMobVehGpsCheck(TagTrack.vehicleid,psngrId,String.valueOf( timeThreshold),String.valueOf(distThreshold),latlng.split(",")[0],latlng.split(",")[1]);
                                    if(result.contains("Allow")) {
                                        thread.start();
                                    }
                                    else if(result.contains("Block")){
                                        progressDialog.dismiss();
                                        getActivity().runOnUiThread(new Runnable() {
                                            @Override
                                            public void run() {
                                                AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(getContext());
                                                alertDialogBuilder.setMessage(result.split("-")[1])
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
                                    else {
                                        progressDialog.dismiss();
                                        getActivity().runOnUiThread(new Runnable() {
                                            @Override
                                            public void run() {
                                                AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(getContext());
                                                alertDialogBuilder.setMessage("OOPs, Something went wrong. Try again.")
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
                            if(!latlng.equals("0.0,0.0"))
                                thread1.start();
                            else {
                                progressDialog.dismiss();
                                Toast.makeText(getContext(), "GPS is not fixed. Please try again", Toast.LENGTH_SHORT).show();
                            }
                        }
                        else {
                            thread.start();
                        }
                    }
                });
            }
            else {
                getActivity().runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(getContext());
                        alertDialogBuilder.setMessage("GPS is disabled in your device. Would you like to enable it?")
                                .setCancelable(false)
                                .setNegativeButton("Goto Settings",
                                        new DialogInterface.OnClickListener(){
                                            public void onClick(DialogInterface dialog, int id){
                                                Intent callGPSSettingIntent = new Intent(
                                                        android.provider.Settings.ACTION_LOCATION_SOURCE_SETTINGS);
                                                startActivity(callGPSSettingIntent);
                                            }
                                        })
                                .setPositiveButton("Cancel",
                                        new DialogInterface.OnClickListener(){
                                            public void onClick(DialogInterface dialog, int id){
                                                dialog.cancel();
                                                final AlertDialog ad=new AlertDialog.Builder(getContext()).create();
                                                ad.setTitle("Permission Need");
                                                ad.setMessage("GPS Location is mandatory to "+btnTag.getText()+".");
                                                ad.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new DialogInterface.OnClickListener() {
                                                    public void onClick(DialogInterface dialog, int which) {
                                                        dialog.dismiss();
                                                    }
                                                });
                                                ad.show();
                                            }
                                        });
                        AlertDialog alert = alertDialogBuilder.create();
                        alert.show();
                    }
                });
            }
        } catch (Exception e) {
            e.printStackTrace();
            ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
            errorRecordSendMail.errorrecordSendMail(e.toString() + "-TagIn("+new Exception().getStackTrace()[0].getLineNumber()+")-" + appConstants.getShrdPrefValByKeyWithTag(getContext(),"passengerinfo","MobileNo"));
        } finally {
            if( progressDialog != null)
                progressDialog.dismiss();
        }
    }
    private void showUpdateAlert(final int priority,final int stableVersion)
    {
        getActivity().runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(BuildConfig.VERSION_CODE<stableVersion)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setTitle("You are using old version");
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getActivity().getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
                else if(priority==1)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setTitle("New Version available");
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Cancel",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            dialog.cancel();
                                        }
                                    })
                            .setNegativeButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getContext().getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-VehicleInfo("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
                else if(priority==2)
                {
                    final AlertDialog.Builder alertDialogBuilder =  new AlertDialog.Builder(
                            new ContextThemeWrapper(getContext(), android.R.style.Theme_Holo_Light_Dialog));
                    alertDialogBuilder.setIcon(R.drawable.error);
                    alertDialogBuilder.setTitle("New Version available");
                    alertDialogBuilder.setMessage("Please update the app to new version")
                            .setCancelable(false)
                            .setPositiveButton("Update",
                                    new DialogInterface.OnClickListener() {
                                        public void onClick(DialogInterface dialog, int id) {
                                            final String appPackageName = getContext().getPackageName(); // getPackageName() from Context or Activity object
                                            try {
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + appPackageName)));
                                                System.exit(0);
                                            } catch (android.content.ActivityNotFoundException anfe) {
                                                ErrorRecordSendMail errorRecordSendMail = new ErrorRecordSendMail();
                                                errorRecordSendMail.errorrecordSendMail(anfe.toString() + "-TagTrack("+new Exception().getStackTrace()[0].getLineNumber()+")-" + mobileno);
                                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + appPackageName)));
                                            }
                                            dialog.cancel();
                                        }
                                    });

                    AlertDialog alert = alertDialogBuilder.create();
                    alert.show();
                }
            }
        });
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

}