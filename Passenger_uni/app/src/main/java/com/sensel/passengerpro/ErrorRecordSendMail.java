package com.sensel.passengerpro;

import android.util.Log;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Modernized Crash & Error Logging Client.
 * Routes all client-side exceptions asynchronously to the backend REST endpoint (/api/logs/error).
 */
public class ErrorRecordSendMail {
    private static final String TAG = "ErrorRecordSendMail";
    private final WebServices webServices = new WebServices();

    public void errorrecordSendMail(final String error) {
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    SimpleDateFormat sdf = new SimpleDateFormat("dd/MM/yyyy hh:mm:ss a", Locale.getDefault());
                    String datetime = sdf.format(new Date());
                    String response = webServices.InsertErrorRecord(error, datetime);
                    Log.d(TAG, "Error record posted to backend: " + response);
                } catch (Exception ex) {
                    Log.e(TAG, "Failed to post error record to backend", ex);
                }
            }
        }).start();
    }
}
