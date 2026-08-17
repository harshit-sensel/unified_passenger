package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.os.Environment;
import android.os.StatFs;
import android.util.Log;

import java.io.File;
import java.io.PrintWriter;
import java.io.StringWriter;
import java.io.Writer;
import java.lang.Thread.UncaughtExceptionHandler;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Global Uncaught Exception Handler.
 * Captures unexpected application crashes, collects hardware & OS diagnostics,
 * logs the complete stack trace to the backend REST endpoint (/api/logs/error),
 * and presents a graceful error dialog to the user.
 */
public class UnCaughtException implements UncaughtExceptionHandler {

    private static final String TAG = "UnCaughtException";
    private final Context context;
    private final Activity activity;

    public UnCaughtException(Context ctx, Activity act) {
        this.context = ctx;
        this.activity = act;
    }

    private StatFs getStatFs() {
        File path = Environment.getDataDirectory();
        return new StatFs(path.getPath());
    }

    private long getAvailableInternalMemorySize(StatFs stat) {
        long blockSize = stat.getBlockSizeLong();
        long availableBlocks = stat.getAvailableBlocksLong();
        return availableBlocks * blockSize;
    }

    private long getTotalInternalMemorySize(StatFs stat) {
        long blockSize = stat.getBlockSizeLong();
        long totalBlocks = stat.getBlockCountLong();
        return totalBlocks * blockSize;
    }

    private void addInformation(StringBuilder message) {
        message.append("Locale: ").append(Locale.getDefault()).append('\n');
        try {
            Context ctx = context != null ? context : MainActivity.context;
            if (ctx != null) {
                PackageManager pm = ctx.getPackageManager();
                PackageInfo pi = pm.getPackageInfo(ctx.getPackageName(), 0);
                message.append("Version: ").append(pi.versionName).append('\n');
                message.append("Package: ").append(pi.packageName).append('\n');
            }
        } catch (Exception e) {
            Log.e(TAG, "Error fetching version info", e);
            message.append("Could not get Version information for your app\n");
        }
        message.append("Phone Model: ").append(android.os.Build.MODEL).append('\n');
        message.append("Android Version: ").append(android.os.Build.VERSION.RELEASE).append('\n');
        message.append("Board: ").append(android.os.Build.BOARD).append('\n');
        message.append("Brand: ").append(android.os.Build.BRAND).append('\n');
        message.append("Device: ").append(android.os.Build.DEVICE).append('\n');
        message.append("Host: ").append(android.os.Build.HOST).append('\n');
        message.append("ID: ").append(android.os.Build.ID).append('\n');
        message.append("Product: ").append(android.os.Build.PRODUCT).append('\n');
        message.append("Type: ").append(android.os.Build.TYPE).append('\n');

        try {
            StatFs stat = getStatFs();
            message.append("Total Internal memory: ").append(getTotalInternalMemorySize(stat)).append('\n');
            message.append("Available Internal memory: ").append(getAvailableInternalMemorySize(stat)).append('\n');
        } catch (Exception ignored) {}
    }

    @Override
    public void uncaughtException(Thread t, final Throwable e) {
        try {
            final StringBuilder report = new StringBuilder();
            Date curDate = new Date();
            report.append("Error Report collected on : ").append(curDate).append('\n').append('\n');
            report.append("Device Diagnostics :\n");
            addInformation(report);
            report.append('\n').append("Stack Trace:\n");

            final Writer result = new StringWriter();
            final PrintWriter printWriter = new PrintWriter(result);
            e.printStackTrace(printWriter);
            report.append(result);
            printWriter.close();
            report.append('\n').append("**** End of Crash Report ***");

            Log.e(TAG, "Fatal uncaught crash: " + report);

            // Log crash to backend REST endpoint asynchronously
            new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        SimpleDateFormat sdf = new SimpleDateFormat("dd/MM/yyyy hh:mm:ss a", Locale.getDefault());
                        String datetime = sdf.format(new Date());
                        new WebServices().InsertErrorRecord(report.toString(), datetime);
                    } catch (Exception ex) {
                        Log.e(TAG, "Failed to send crash report to server", ex);
                    }
                }
            }).start();

            showCrashDialog();

        } catch (Exception ex) {
            Log.e(TAG, "Error handling uncaught exception", ex);
        }
    }

    private void showCrashDialog() {
        if (activity != null && !activity.isFinishing()) {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        new AlertDialog.Builder(activity)
                                .setIcon(R.drawable.error)
                                .setTitle("Sorry, App stopped working")
                                .setMessage("An unexpected error occurred. A diagnostic report has been logged.")
                                .setCancelable(false)
                                .setPositiveButton("Ok", new DialogInterface.OnClickListener() {
                                    @Override
                                    public void onClick(DialogInterface dialog, int id) {
                                        dialog.dismiss();
                                        activity.finish();
                                        System.exit(0);
                                    }
                                })
                                .show();
                    } catch (Exception ignored) {
                        activity.finish();
                        System.exit(0);
                    }
                }
            });
        }
    }
}