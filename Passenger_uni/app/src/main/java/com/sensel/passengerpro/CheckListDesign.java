package com.sensel.passengerpro;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.net.Uri;
import android.os.Environment;
import android.provider.MediaStore;
import android.text.Editable;
import android.text.TextWatcher;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.TextView;
import android.widget.Toast;

import androidx.core.app.ActivityCompat;
import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;

/**
 * Created by vamsi on 25-Sep-17.
 */

public class CheckListDesign extends ArrayAdapter<String> {
    private final Activity context;
    private final String[] rules;
    private final String[] ruleIds;
    private final String[] ruleTypes;
    public static String[] strRules;
    public static HashMap<Integer, String> imagePaths = new HashMap<>(); // Store image paths by position

    public static final int CAMERA_CAPTURE_IMAGE_REQUEST_CODE = 100;
    static String imagecapturepath = "";
    ImageView imageView;

    public static ImageUpdateListener imageUpdateListener;

    public static String restoredWfmId = "";
    public static String restoredPtw = "";
    public static String restoredWfmTask = "";
    private final TagIn tagInContext;

    public CheckListDesign(TagIn context,
                           String[] rules, String[] ruleIds, String[] ruleTypes) {
        super(context, R.layout.checklist_design, rules);
        this.context = context;
        this.rules = rules;
        this.ruleIds = ruleIds;
        this.ruleTypes = ruleTypes;
        
        if (strRules == null || strRules.length != rules.length) {
            strRules = new String[rules.length];
        }
        if (imagePaths == null) {
            imagePaths = new HashMap<>();
        }
        
        // Restore existing draft for this vehicle if present
        if (context != null && context.vehicle != null) {
            restoreDraft(context, context.vehicle, rules.length);
        }
        this.tagInContext = context;
    }

    public static void saveDraft(Context ctx, String vehicleId) {
        if (ctx == null || vehicleId == null || vehicleId.trim().isEmpty()) return;
        try {
            AppConstants appConstants = new AppConstants();
            JSONObject obj = new JSONObject();

            // Check if there is an existing draft saved on disk
            JSONObject existingObj = null;
            String existingJson = appConstants.getShrdPrefValByKey(ctx, "TAGIN_DRAFT_" + vehicleId.trim());
            if (existingJson != null && !existingJson.trim().isEmpty()) {
                try {
                    existingObj = new JSONObject(existingJson);
                } catch (Exception ignored) {}
            }
            
            if (strRules != null && strRules.length > 0) {
                JSONArray arr = new JSONArray();
                for (String r : strRules) {
                    arr.put(r == null ? "" : r);
                }
                obj.put("strRules", arr);
            } else if (existingObj != null && existingObj.has("strRules")) {
                // Preserve previous answers if in-memory array is not initialized yet
                obj.put("strRules", existingObj.getJSONArray("strRules"));
            }
            
            if (imagePaths != null && !imagePaths.isEmpty()) {
                JSONObject imgObj = new JSONObject();
                for (Map.Entry<Integer, String> entry : imagePaths.entrySet()) {
                    if (entry.getValue() != null && !entry.getValue().trim().isEmpty()) {
                        imgObj.put(String.valueOf(entry.getKey()), entry.getValue());
                    }
                }
                obj.put("imagePaths", imgObj);
            } else if (existingObj != null && existingObj.has("imagePaths")) {
                // Preserve previous image paths if in-memory map is empty
                obj.put("imagePaths", existingObj.getJSONObject("imagePaths"));
            }
            
            if (imagecapturepath != null && !imagecapturepath.isEmpty()) {
                obj.put("imagecapturepath", imagecapturepath);
            } else if (existingObj != null && existingObj.has("imagecapturepath")) {
                obj.put("imagecapturepath", existingObj.optString("imagecapturepath", ""));
            }

            if (ctx instanceof TagIn) {
                TagIn tagIn = (TagIn) ctx;
                if (tagIn.wfm != null && tagIn.wfm.getText() != null) {
                    obj.put("wfmId", tagIn.wfm.getText().toString().trim());
                } else if (existingObj != null && existingObj.has("wfmId")) {
                    obj.put("wfmId", existingObj.optString("wfmId", ""));
                }

                if (tagIn.ptw != null && tagIn.ptw.getText() != null) {
                    obj.put("ptw", tagIn.ptw.getText().toString().trim());
                } else if (existingObj != null && existingObj.has("ptw")) {
                    obj.put("ptw", existingObj.optString("ptw", ""));
                }

                if (tagIn.chosenWfmTask != null && !tagIn.chosenWfmTask.isEmpty()) {
                    obj.put("wfmTask", tagIn.chosenWfmTask.trim());
                } else if (tagIn.wfmTask != null && tagIn.wfmTask.getSelectedItem() != null) {
                    String sel = tagIn.wfmTask.getSelectedItem().toString().trim();
                    if (!"Select".equalsIgnoreCase(sel)) {
                        obj.put("wfmTask", sel);
                    } else if (existingObj != null && existingObj.has("wfmTask")) {
                        obj.put("wfmTask", existingObj.optString("wfmTask", ""));
                    }
                } else if (existingObj != null && existingObj.has("wfmTask")) {
                    obj.put("wfmTask", existingObj.optString("wfmTask", ""));
                }
            }
            
            appConstants.putShrdPrefValWithKey(ctx, "TAGIN_DRAFT_" + vehicleId.trim(), obj.toString());
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public static boolean restoreDraft(Context ctx, String vehicleId, int ruleCount) {
        if (ctx == null || vehicleId == null || vehicleId.trim().isEmpty()) return false;
        try {
            AppConstants appConstants = new AppConstants();
            String json = appConstants.getShrdPrefValByKey(ctx, "TAGIN_DRAFT_" + vehicleId.trim());
            if (json == null || json.trim().isEmpty()) return false;

            JSONObject obj = new JSONObject(json);
            if (obj.has("strRules")) {
                JSONArray arr = obj.getJSONArray("strRules");
                int targetCount = (ruleCount > 0) ? ruleCount : arr.length();
                if (targetCount > 0) {
                    if (strRules == null || strRules.length != targetCount) {
                        strRules = new String[targetCount];
                    }
                    for (int i = 0; i < arr.length() && i < targetCount; i++) {
                        String val = arr.optString(i, "");
                        if (!val.isEmpty()) {
                            strRules[i] = val;
                        }
                    }
                }
            }

            if (obj.has("imagePaths")) {
                JSONObject imgObj = obj.getJSONObject("imagePaths");
                if (imagePaths == null) {
                    imagePaths = new HashMap<>();
                }
                Iterator<String> keys = imgObj.keys();
                while (keys.hasNext()) {
                    String k = keys.next();
                    try {
                        int pos = Integer.parseInt(k);
                        String pth = imgObj.getString(k);
                        if (pth != null && new File(pth).exists()) {
                            imagePaths.put(pos, pth);
                            if (strRules != null && pos < strRules.length) {
                                strRules[pos] = "ImageCaptured";
                            }
                        }
                    } catch (Exception ignored) {}
                }
            }

            if (obj.has("imagecapturepath")) {
                imagecapturepath = obj.optString("imagecapturepath", "");
            }

            if (obj.has("wfmId")) {
                restoredWfmId = obj.optString("wfmId", "");
            }
            if (obj.has("ptw")) {
                restoredPtw = obj.optString("ptw", "");
            }
            if (obj.has("wfmTask")) {
                restoredWfmTask = obj.optString("wfmTask", "");
            }

            return true;
        } catch (Exception e) {
            e.printStackTrace();
            return false;
        }
    }

    public static void clearDraft(Context ctx, String vehicleId) {
        if (ctx == null) return;
        try {
            AppConstants appConstants = new AppConstants();
            if (vehicleId != null && !vehicleId.trim().isEmpty()) {
                appConstants.putShrdPrefValWithKey(ctx, "TAGIN_DRAFT_" + vehicleId.trim(), "");
            }
            if (strRules != null) {
                for (int i = 0; i < strRules.length; i++) {
                    strRules[i] = null;
                }
            }
            restoredWfmId = "";
            restoredPtw = "";
            restoredWfmTask = "";
            if (imagePaths != null) {
                imagePaths.clear();
            }
            imagecapturepath = "";
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    @Override
    public int getCount() {
        return ruleIds != null ? ruleIds.length : 0;
    }

    @Override
    public String getItem(int position) {
        return (ruleIds != null && position < ruleIds.length) ? ruleIds[position] : "";
    }

    @Override
    public long getItemId(int position) {
        return position;
    }

    @Override
    public View getView(final int position, final View view, ViewGroup parent) {
        LayoutInflater inflater = context.getLayoutInflater();
        final View rowView = inflater.inflate(R.layout.checklist_design, null, true);
        TextView txtTitle = (TextView) rowView.findViewById(R.id.txt);
        RadioGroup rgp = (RadioGroup) rowView.findViewById(R.id.radioType);
        final RadioButton pass = (RadioButton) rowView.findViewById(R.id.pass);
        final RadioButton fail = (RadioButton) rowView.findViewById(R.id.fail);
        final TextView status = (TextView) rowView.findViewById(R.id.status);
        final EditText editText = (EditText) rowView.findViewById(R.id.edittxt);
        imageView = rowView.findViewById(R.id.image_camera);

        txtTitle.setText(rules[position]);
        if (ruleTypes[position].equals("Radio")) {
            rgp.setVisibility(View.VISIBLE);
            editText.setVisibility(View.GONE);
            imageView.setVisibility(View.GONE);
        } else if (ruleTypes[position].equals("Text")) {
            rgp.setVisibility(View.GONE);
            status.setVisibility(View.GONE);
            editText.setVisibility(View.VISIBLE);
            imageView.setVisibility(View.GONE);
        } else if (ruleTypes[position].equals("FileUpload")) {
            rgp.setVisibility(View.GONE);
            status.setVisibility(View.GONE);
            editText.setVisibility(View.GONE);
            imageView.setVisibility(View.VISIBLE);

            if (imagePaths != null && imagePaths.containsKey(position)) {
                Bitmap bitmap = BitmapFactory.decodeFile(imagePaths.get(position));
                if (bitmap != null) {
                    imageView.setImageBitmap(bitmap);
                } else {
                    imageView.setImageResource(R.drawable.file_add);
                }
            } else {
                imageView.setImageResource(R.drawable.file_add);
            }

            imageView.setOnClickListener(v -> {
                dispatchTakePictureIntent(position);
            });
        } else {
            rgp.setVisibility(View.GONE);
            editText.setVisibility(View.GONE);
            imageView.setVisibility(View.GONE);
            if (strRules != null && position < strRules.length) {
                strRules[position] = "No Configuration";
            }
        }

        if (strRules != null && position < strRules.length && strRules[position] != null) {
            if (ruleTypes[position].equals("Radio")) {
                if (strRules[position].contains("NO")) {
                    fail.setChecked(true);
                    status.setText("Failed");
                    status.setTextColor(Color.RED);
                } else if (strRules[position].contains("YES")) {
                    pass.setChecked(true);
                    status.setText("Passed");
                    status.setTextColor(Color.parseColor("#008000"));
                }
            } else if (ruleTypes[position].equals("Text")) {
                editText.setText(strRules[position]);
            } else if (ruleTypes[position].equals("FileUpload")) {
                imageView.setVisibility(View.VISIBLE);
                if (imagePaths != null && imagePaths.containsKey(position)) {
                    Bitmap bitmap = BitmapFactory.decodeFile(imagePaths.get(position));
                    if (bitmap != null) {
                        imageView.setImageBitmap(bitmap);
                    } else {
                        imageView.setImageResource(R.drawable.file_add);
                    }
                } else {
                    imageView.setImageResource(R.drawable.file_add);
                }
                imageView.setOnClickListener(v -> {
                    dispatchTakePictureIntent(position);
                });
            } else {
                imageView.setVisibility(View.GONE);
            }
        }

        rgp.setOnCheckedChangeListener(new RadioGroup.OnCheckedChangeListener() {
            @Override
            public void onCheckedChanged(RadioGroup group, int checkedId) {
                try {
                    if (pass.isChecked()) {
                        strRules[position] = "YES";
                        status.setText("Passed");
                        status.setTextColor(Color.parseColor("#008000"));
                    } else if (fail.isChecked()) {
                        strRules[position] = "NO";
                        status.setText("Failed");
                        status.setTextColor(Color.RED);
                    }
                    if (context instanceof TagIn) {
                        saveDraft(context, ((TagIn) context).vehicle);
                        ((TagIn) context).checkTagInFormValidation();
                    }
                } catch (Exception e) {
                    e.printStackTrace();
                }
            }
        });

        editText.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {}

            @Override
            public void afterTextChanged(Editable s) {
                if (strRules != null && position < strRules.length) {
                    strRules[position] = editText.getText().toString();
                    if (context instanceof TagIn) {
                        saveDraft(context, ((TagIn) context).vehicle);
                        ((TagIn) context).checkTagInFormValidation();
                    }
                }
            }
        });

        return rowView;
    }

    public interface ImageUpdateListener {
        void onImageUpdated(int position);
    }

    public static void saveCapturedImage(int position, String imagePath, Context ctx, String vehicleId) {
        if (imagePaths == null) {
            imagePaths = new HashMap<>();
        }
        imagePaths.put(position, imagePath);
        if (strRules != null && position < strRules.length) {
            strRules[position] = "ImageCaptured";
        }
        if (ctx != null && vehicleId != null) {
            saveDraft(ctx, vehicleId);
        }
        if (imageUpdateListener != null) {
            imageUpdateListener.onImageUpdated(position);
        }
    }

    public static void saveCapturedImage(int position, String imagePath) {
        saveCapturedImage(position, imagePath, null, null);
    }

    private void dispatchTakePictureIntent(int position) {
        AppConstants appConstants = new AppConstants();
        String pId = appConstants.getShrdPrefValByKeyWithTag(context, "passengerinfo", "PsngrId");
        String chkIdStr = (ruleIds != null && position < ruleIds.length) ? ruleIds[position] : String.valueOf(position);
        String strDate = new SimpleDateFormat("HHmmss").format(new Date());
        imagecapturepath = "TagIn_" + (pId != null ? pId : "0") + "_" + strDate + "_" + chkIdStr;
        imagecapturepath = imagecapturepath.replaceAll(" ", "").replaceAll("[:\\\\/*\"?|<>]", "_");

        Intent takePictureIntent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
        if (takePictureIntent.resolveActivity(context.getPackageManager()) != null) {
            File photoFile = null;
            try {
                photoFile = createImageFile();
            } catch (IOException ex) {
                ex.printStackTrace();
            }
            if (photoFile != null) {
                Uri photoURI = FileProvider.getUriForFile(getContext(),
                        getContext().getPackageName() + ".provider",
                        photoFile);
                takePictureIntent.putExtra(MediaStore.EXTRA_OUTPUT, photoURI);
                takePictureIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                takePictureIntent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                takePictureIntent.putExtra("position", position);
                
                // Save current state to draft before launching camera
                if (context instanceof TagIn) {
                    saveDraft(context, ((TagIn) context).vehicle);
                }
                
                ((Activity) context).startActivityForResult(takePictureIntent, CAMERA_CAPTURE_IMAGE_REQUEST_CODE + position);
            }
        }
    }

    private String getFileNameFromFullPath() {
        return imagecapturepath.substring(imagecapturepath.lastIndexOf('/') + 1);
    }

    private String[] getFileNameAndFormat(String fileName) {
        return new String[]{fileName, "PNG"};
    }

    private File createImageFile() throws IOException {
        File storageDir = ((Activity) context).getExternalFilesDir(Environment.DIRECTORY_PICTURES);
        if (storageDir != null) {
            storageDir.mkdirs();
        }
        String imageFileName = getFileNameFromFullPath();
        String[] FileName = getFileNameAndFormat(imageFileName);

        File image = new File(storageDir, FileName[0] + ".PNG");
        imagecapturepath = image.getAbsolutePath();
        return image;
    }
}
