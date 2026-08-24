package com.sensel.passengerpro;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.os.IBinder;
import androidx.annotation.Nullable;
import androidx.core.app.NotificationCompat;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.Timer;
import java.util.TimerTask;

public class AlarmService extends Service {

    public static final String NOTIFICATION_CHANNEL_ID = "passenger_school_alerts";

    WebServices webServices=new WebServices();
    AppConstants appConstants=new AppConstants();
    public AlarmService(Context applicationContext) {
        super();
    }

    public AlarmService() {
    }
    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        super.onStartCommand(intent, flags, startId);
        startTimer();
        return START_STICKY;
    }
    @Override
    public void onDestroy() {
        super.onDestroy();
        Intent broadcastIntent = new Intent("RestartSensor");
        sendBroadcast(broadcastIntent);
        stoptimertask();
    }

    private Timer timer;
    private TimerTask timerTask;
    long oldTime=0;
    public void startTimer() {
        //set a new Timer
        timer = new Timer();

        //initialize the TimerTask's job
        timerTask = new TimerTask() {
            public void run() {
                String userMenus = appConstants.getShrdPrefValByKey(getApplicationContext(), "UserMenus");
                boolean isSchoolBusTracking = (userMenus != null && userMenus.contains("school_bus_tracking"));
                if (isSchoolBusTracking) {
                    String psngrId = appConstants.getShrdPrefValByKeyWithTag(getApplicationContext(), "passengerinfo", "PsngrId");
                    String result = webServices.GetPsngrNotifications(psngrId);
                    if (result != "") {
                        try {
                            int latest_index = -1;
                            final JSONArray jArr = new JSONArray(result);
                            for (int j = 0; j <= jArr.length(); j++) {
                                JSONObject data = jArr.getJSONObject(j);
                                if (!Boolean.valueOf(data.getString("IsNotified"))) {
                                    latest_index = j;
                                    break;
                                }
                            }
                            if (latest_index != -1) {
                                JSONObject data = jArr.getJSONObject(latest_index);
                                String Subject = data.getString("Subject");
                                String Info = data.getString("Info");

                                NotificationManager manager = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
                                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                    NotificationChannel channel = new NotificationChannel(
                                            NOTIFICATION_CHANNEL_ID,
                                            "Passenger School Alerts",
                                            NotificationManager.IMPORTANCE_HIGH);
                                    channel.setDescription("Notifications for school bus tracking and student updates");
                                    if (manager != null) {
                                        manager.createNotificationChannel(channel);
                                    }
                                }

                                NotificationCompat.Builder builder =
                                        new NotificationCompat.Builder(getApplicationContext(), NOTIFICATION_CHANNEL_ID)
                                                .setContentTitle(Subject)
                                                .setContentText(Info)
                                                .setDefaults(Notification.DEFAULT_ALL)
                                                .setPriority(NotificationCompat.PRIORITY_HIGH)
                                                .setSmallIcon(R.drawable.noti_icon)
                                                .setColor(getResources().getColor(R.color.colorPrimary))
                                                .setOnlyAlertOnce(true)
                                                .setAutoCancel(true);

                                Intent notificationIntent = new Intent(getApplicationContext(), Notifications.class);
                                int pendingFlags = PendingIntent.FLAG_UPDATE_CURRENT;
                                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.M) {
                                    pendingFlags |= PendingIntent.FLAG_IMMUTABLE;
                                }
                                PendingIntent contentIntent = PendingIntent.getActivity(getApplicationContext(), 0, notificationIntent,
                                        pendingFlags);
                                builder.setContentIntent(contentIntent);

                                // Add as notification
                                if (manager != null) {
                                    manager.notify(0, builder.build());
                                }
                            }
                        } catch (JSONException js) {
                            // Toast.makeText(cont,"Oops something went wrong, Try again",Toast.LENGTH_SHORT).show();
                        }
                    }
                }
            }
        };

        timer.schedule(timerTask, 1000, 3*60*1000); //
    }

    /**
     * not needed
     */
    public void stoptimertask() {
        //stop the timer, if it's not already null
        if (timer != null) {
            timer.cancel();
            timer = null;
        }
    }

    @Nullable
    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}