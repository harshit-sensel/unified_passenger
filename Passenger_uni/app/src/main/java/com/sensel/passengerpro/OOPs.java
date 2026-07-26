package com.sensel.passengerpro;

import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.os.Bundle;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import android.view.Menu;
import android.view.MenuItem;
import android.widget.TextView;

/**
 * Created by MS on 17-Oct-17.
 */

public class OOPs extends AppCompatActivity {
    AppConstants appConstants=new AppConstants();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_oops);
        PassengerActivityLogger.log(this, "OOPs");
        String message=getIntent().getExtras().getString("message");
        TextView txt=(TextView) findViewById(R.id.mesage);
        txt.setText(message);
    }
    @Override
    public void onBackPressed() {
        moveTaskToBack(true);
        /*Intent intent = new Intent(Intent.ACTION_MAIN);
        intent.addCategory(Intent.CATEGORY_HOME);
        intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        startActivity(intent);
        finish();
        System.exit(0);*/
    }

    @Override
    public boolean onPrepareOptionsMenu(final Menu menu) {
        getMenuInflater().inflate(R.menu.menu, menu);
        MenuItem item = menu.findItem(R.id.menu_panic);
        item.setVisible(false);
        item = menu.findItem(R.id.menu_refresh);
        item.setVisible(false);
        return super.onCreateOptionsMenu(menu);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case R.id.menu_logout:
                runOnUiThread(new Runnable() {
                    public void run() {
                        final AlertDialog.Builder alertDialogBuilder = new AlertDialog.Builder(
                                new ContextThemeWrapper(OOPs.this, android.R.style.Theme_Holo_Light_Dialog));
                        alertDialogBuilder.setIcon(R.drawable.error);
                        alertDialogBuilder.setTitle("Action");
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
                                                appConstants.putShrdPrefValWithKey(getApplicationContext(),"passengerinfo",null);
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
                break;
        }
        return super.onOptionsItemSelected(item);
    }
}
