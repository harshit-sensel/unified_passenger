package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.view.ContextThemeWrapper;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.TextView;
import android.widget.Toast;

/**
 * Dynamic Material Dialog rendering Account Privacy Policy terms.
 */
public class PrivacyPolicyDialog {

    public interface OnPrivacyPolicyAcceptedListener {
        void onAccepted();
    }

    public static void show(final Activity activity, final String mobileNo, final int accountId, final String policyText, final OnPrivacyPolicyAcceptedListener listener) {
        if (activity == null || activity.isFinishing()) return;

        AlertDialog.Builder builder = new AlertDialog.Builder(new ContextThemeWrapper(activity, android.R.style.Theme_Holo_Light_Dialog));
        builder.setTitle("📜 Terms & Privacy Policy");
        builder.setCancelable(false);

        TextView textView = new TextView(activity);
        int paddingPx = (int) (16 * activity.getResources().getDisplayMetrics().density);
        textView.setPadding(paddingPx, paddingPx, paddingPx, paddingPx);
        textView.setTextSize(14);
        textView.setMovementMethod(LinkMovementMethod.getInstance());
        
        if (policyText != null && !policyText.isEmpty()) {
            textView.setText(Html.fromHtml(policyText));
        } else {
            textView.setText("Please review and accept the organization Privacy Policy terms to proceed.");
        }

        builder.setView(textView);

        builder.setPositiveButton("I AGREE", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialog, int which) {
                dialog.dismiss();
                // Log PRIVACY_POLICY_ACCEPTED audit event
                new Thread(new Runnable() {
                    @Override
                    public void run() {
                        WebServices webServices = new WebServices();
                        webServices.logAuditActivity(mobileNo, accountId, "PRIVACY_POLICY_ACCEPTED", "", "");
                    }
                }).start();

                if (listener != null) {
                    listener.onAccepted();
                }
            }
        });

        builder.setNegativeButton("DECLINE", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(final DialogInterface dialog, int which) {
                dialog.dismiss();
                // Log PRIVACY_POLICY_DECLINED audit event
                new Thread(new Runnable() {
                    @Override
                    public void run() {
                        WebServices webServices = new WebServices();
                        webServices.logAuditActivity(mobileNo, accountId, "PRIVACY_POLICY_DECLINED", "", "");
                    }
                }).start();

                Toast.makeText(activity, "Acceptance of Privacy Policy is required to use Passenger App.", Toast.LENGTH_LONG).show();

                // Clear session preferences and JWT token
                AppConstants appConstants = new AppConstants();
                appConstants.putShrdPrefValWithKey(activity.getApplicationContext(), "passengerinfo", "");
                appConstants.putShrdPrefValWithKey(activity.getApplicationContext(), "UserMenus", "");
                appConstants.setJwtToken(activity.getApplicationContext(), "");
                WebServices.currentJwtToken = "";

                Intent intent = new Intent(activity, LoginActivity.class);
                intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
                activity.startActivity(intent);
                activity.finish();
            }
        });

        AlertDialog alert = builder.create();
        alert.show();
    }
}
