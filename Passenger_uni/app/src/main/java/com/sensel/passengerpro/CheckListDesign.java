package com.sensel.passengerpro;

import android.Manifest;
import android.app.Activity;
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

import java.io.File;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.HashMap;
import java.util.LinkedHashMap;
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
    //public static Map<Integer, String> imagePaths = new LinkedHashMap<>();

    public static final int CAMERA_CAPTURE_IMAGE_REQUEST_CODE = 100;
    static String imagecapturepath = "";
    ImageView imageView;

    public static ImageUpdateListener imageUpdateListener;

    private final TagIn tagInContext;
    public CheckListDesign(TagIn context,
                           String[] rules,String[] ruleIds,String[] ruleTypes) {
        super(context, R.layout.checklist_design, rules);
        this.context = context;
        this.rules = rules;
        this.ruleIds = ruleIds;
        this.ruleTypes=ruleTypes;
        strRules=new String[rules.length];
        this.tagInContext = context; // Directly assign without casting
    }
    @Override
    public int getCount() {
        // TODO Auto-generated method stub
        return ruleIds.length;
    }
    @Override
    public String getItem(int position) {
        // TODO Auto-generated method stub
        return ruleIds[position];
    }
    @Override
    public long getItemId(int position) {
        // TODO Auto-generated method stub
        return 0;
    }
    @Override
    public View getView(final int position, final View view, ViewGroup parent) {
        LayoutInflater inflater = context.getLayoutInflater();
        final View rowView= inflater.inflate(R.layout.checklist_design, null, true);
        TextView txtTitle = (TextView) rowView.findViewById(R.id.txt);
        RadioGroup rgp=(RadioGroup) rowView.findViewById(R.id.radioType);
        final RadioButton pass=(RadioButton) rowView.findViewById(R.id.pass);
        final RadioButton fail=(RadioButton) rowView.findViewById(R.id.fail);
        final TextView status=(TextView) rowView.findViewById(R.id.status);
        final EditText editText=(EditText) rowView.findViewById(R.id.edittxt);
        //Added By Madhuri for Nokia-06122024
        imageView = rowView.findViewById(R.id.image_camera);
        String ruleImageDirectory ="/storage/emulated/0/Android/data/com.sensel.hardware.camera/files/Pictures" + File.separator ;
        File dir = new File(ruleImageDirectory);
        if (!dir.exists()) dir.mkdirs();
        SimpleDateFormat sdfDate = new SimpleDateFormat("HHmmss");//dd/MM/yyyy
        Date now = new Date();
        String strDate = sdfDate.format(now);

        AppConstants appConstants = new AppConstants();
        String pId = appConstants.getShrdPrefValByKeyWithTag(context, "passengerinfo", "PsngrId");
        imagecapturepath = "TagIn_" + (pId != null ? pId : "0") + "_" + strDate + "_" + position;
        imagecapturepath = imagecapturepath.replaceAll(" ", "");
        imagecapturepath = imagecapturepath.replaceAll("[:\\\\/*\"?|<>]", "_");
        txtTitle.setText(rules[position]);
        if(ruleTypes[position].equals("Radio")){
            rgp.setVisibility(View.VISIBLE);
            editText.setVisibility(View.GONE);
            imageView.setVisibility(View.GONE);
        }
        else if(ruleTypes[position].equals("Text")){
            rgp.setVisibility(View.GONE);
            status.setVisibility(View.GONE);
            editText.setVisibility(View.VISIBLE);
            imageView.setVisibility(View.GONE);
        }
        //Added By Madhuri For photo upload
        else if(ruleTypes[position].equals("FileUpload")) {
            rgp.setVisibility(View.GONE);
            status.setVisibility(View.GONE);
            editText.setVisibility(View.GONE);
            imageView.setVisibility(View.VISIBLE);
            // Display the captured image if available
            // Display the captured image if available
            // Check if an image is available for this position and update the ImageView
            if (imagePaths.containsKey(position)) {
                Bitmap bitmap = BitmapFactory.decodeFile(imagePaths.get(position));
                if (bitmap != null) {
                    imageView.setImageBitmap(bitmap);
                }
            } else {
                imageView.setImageResource(R.drawable.file_add); // Default image or placeholder
            }

            imageView.setOnClickListener(v -> {
                dispatchTakePictureIntent(position); // Capture new image for this item
            });
        }
        else {
            rgp.setVisibility(View.GONE);
            editText.setVisibility(View.GONE);
            imageView.setVisibility(View.GONE);
            strRules[position] = "No Configuration";
        }

        if (strRules[position] != null) {
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
            }
            else if (ruleTypes[position].equals("FileUpload")) {
                imageView.setVisibility(View.VISIBLE);

                // Check if an image is available for this position
                if (imagePaths.containsKey(position)) {
                    Bitmap bitmap = BitmapFactory.decodeFile(imagePaths.get(position));
                    if (bitmap != null) {
                        imageView.setImageBitmap(bitmap);
                    }
                } else {
                    imageView.setImageResource(R.drawable.file_add); // Placeholder for no image
                }

                // Handle image capture
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
                    // TODO Auto-generated method stub
                    if (pass.isChecked()) {
                        strRules[position] = "YES";
                        status.setText("Passed");
                        status.setTextColor(Color.parseColor("#008000"));
                    } else if (fail.isChecked()) {
                        strRules[position] = "NO";
                        status.setText("Failed");
                        status.setTextColor(Color.RED);
                    }
                } catch (Exception e) {
                    e.printStackTrace();
                }
            }
        });
        // Set OnClickListener for the ImageView
            /*imageView.setOnClickListener(v -> {
                dispatchTakePictureIntent(position);
            });*/


        editText.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {

            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {

            }

            @Override
            public void afterTextChanged(Editable s) {
                strRules[position]=editText.getText().toString();
            }
        });
        return rowView;
    }
    public interface ImageUpdateListener {
        void onImageUpdated(int position);
    }
    public static void saveCapturedImage(int position, String imagePath) {
        imagePaths.put(position, imagePath); // Map the image to the correct position
        strRules[position] = "ImageCaptured"; // Mark this rule as completed with an image

        // Notify the adapter to refresh the specific row
        if (imageUpdateListener != null) {
            imageUpdateListener.onImageUpdated(position);
        }
    }
    private void dispatchTakePictureIntent(int position) {
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
                //Added for Android 14 - 13012025
                takePictureIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                takePictureIntent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                takePictureIntent.putExtra("position", position); // Pass the position explicitly
                ((Activity) context).startActivityForResult(takePictureIntent, CAMERA_CAPTURE_IMAGE_REQUEST_CODE + position);
            }
        }
    }
    private String getFileNameFromFullPath(){
        return imagecapturepath.substring(imagecapturepath.lastIndexOf('/')+1,imagecapturepath.length());
    }
    private String[] getFileNameAndFormat(String fileName) {
        return new String[]{fileName, "PNG"};
    }
    private File createImageFile() throws IOException {
        // Create an image file name
        String timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss").format(new Date());
        //String imageFileName =  timeStamp;
        File storageDir =((Activity) context).getExternalFilesDir(Environment.DIRECTORY_PICTURES);
        //File storageDir = new File(RULE.ruleImageDirectory);
        Log.d("StorageDirectory","StorageDirectory"+storageDir);
        storageDir.mkdirs(); // make sure you call mkdirs() and not mkdir()
        String imageFileName=getFileNameFromFullPath();
        String[] FileName=getFileNameAndFormat(imageFileName);

        /*File image = File.createTempFile(
                FileName[0],  *//* prefix *//*
                ".PNG" ,*//* suffix *//*
                storageDir      *//* directory *//*
        );*/
        File image = new File(storageDir, FileName[0] + ".PNG");
        imagecapturepath = image.getAbsolutePath();
        return image;
    }

}

