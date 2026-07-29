package com.sensel.passengerpro;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.ImageView;
import android.widget.TextView;

/**
 * Adapter for the passenger menu grid (3 columns, icon + label per cell).
 * Matches DriverApp-NonTotal grid style: bordered cards, icon on top, text below.
 */
public class PassengerMenuGridAdapter extends BaseAdapter {

    private final Context context;
    private final String[] labels;
    private final int[] iconResIds;
    private final boolean[] isEmpty;

    public PassengerMenuGridAdapter(Context context, String[] labels, int[] iconResIds, boolean[] isEmpty) {
        this.context = context;
        this.labels = labels;
        this.iconResIds = iconResIds;
        this.isEmpty = isEmpty;
    }

    @Override
    public int getCount() {
        return labels.length;
    }

    @Override
    public Object getItem(int position) {
        return null;
    }

    @Override
    public long getItemId(int position) {
        return position;
    }

    @Override
    public View getView(int position, View convertView, ViewGroup parent) {
        View grid;
        LayoutInflater inflater = (LayoutInflater) context.getSystemService(Context.LAYOUT_INFLATER_SERVICE);
        grid = inflater.inflate(R.layout.gridview_custom_layout, null);

        TextView textView = grid.findViewById(R.id.gridview_text);
        ImageView imageView = grid.findViewById(R.id.gridview_image);
        View iconBox = grid.findViewById(R.id.icon_box_container);

        textView.setText(labels[position]);
        if (isEmpty != null && position < isEmpty.length && isEmpty[position]) {
            imageView.setVisibility(View.GONE);
            grid.setClickable(false);
            grid.setFocusable(false);
        } else {
            imageView.setVisibility(View.VISIBLE);
            imageView.setImageResource(iconResIds[position]);

            String label = labels[position];
            if (iconBox != null && label != null) {
                if (label.contains("QR")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFEEF2FF));
                } else if (label.contains("OTP")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE0F2FE));
                } else if (label.contains("Track")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFF3E8FF));
                } else if (label.contains("Tagout") || label.contains("Tag Out")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFDCFCE7));
                } else if (label.contains("Location")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFEF3C7));
                } else if (label.contains("Notif")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE0E7FF));
                } else if (label.contains("Panic")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFEE2E2));
                } else if (label.contains("Logout")) {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFFE4E6));
                } else {
                    iconBox.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFCCFBF1));
                }
            }
        }
        return grid;
    }
}
