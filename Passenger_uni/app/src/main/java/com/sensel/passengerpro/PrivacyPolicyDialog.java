package com.sensel.passengerpro;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.view.ContextThemeWrapper;
import android.view.View;
import android.widget.Button;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

/**
 * Dynamic Material Dialog rendering Account Privacy Policy terms with high-contrast dark text.
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

        // Scrollable container for long terms & conditions
        ScrollView scrollView = new ScrollView(activity);
        int paddingPx = (int) (16 * activity.getResources().getDisplayMetrics().density);
        scrollView.setPadding(paddingPx, paddingPx, paddingPx, paddingPx);

        TextView textView = new TextView(activity);
        textView.setTextSize(14);

        // Enforce high-contrast dark text (#1E293B) and Sensel primary blue links (#0284C7)
        textView.setTextColor(Color.parseColor("#1E293B"));
        textView.setLinkTextColor(Color.parseColor("#0284C7"));
        textView.setMovementMethod(LinkMovementMethod.getInstance());
        
        String formattedContent = policyText;
        if (formattedContent != null && !formattedContent.isEmpty()) {
            // Ensure white text in HTML strings is styled to high-contrast dark text
            formattedContent = formattedContent.replace("color: white", "color: #1E293B")
                                               .replace("color:white", "color: #1E293B")
                                               .replace("color: #FFFFFF", "color: #1E293B")
                                               .replace("color:#FFFFFF", "color: #1E293B")
                                               .replace("color: #ffffff", "color: #1E293B")
                                               .replace("color:#ffffff", "color: #1E293B");
            textView.setText(Html.fromHtml(formattedContent));
        } else {
            textView.setText(Html.fromHtml("<b>Terms of Use & Privacy Policy</b><br/><br/>Please review and accept the organization Privacy Policy terms to proceed using Passenger App."));
        }

        scrollView.addView(textView);
        builder.setView(scrollView);

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

        // Style dialog action buttons for clear visual hierarchy
        Button posBtn = alert.getButton(DialogInterface.BUTTON_POSITIVE);
        if (posBtn != null) {
            posBtn.setTextColor(Color.parseColor("#0284C7"));
            posBtn.setTypeface(null, Typeface.BOLD);
        }

        Button negBtn = alert.getButton(DialogInterface.BUTTON_NEGATIVE);
        if (negBtn != null) {
            negBtn.setTextColor(Color.parseColor("#64748B"));
        }
    }
}
