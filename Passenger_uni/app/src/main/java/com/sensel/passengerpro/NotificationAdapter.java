package com.sensel.passengerpro;

/**
 * Created by User on 29-04-2016.
 */

import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.widget.ArrayAdapter;
import android.widget.ImageView;
import android.widget.RelativeLayout;
import android.widget.TextView;

import java.util.List;
import java.util.regex.Pattern;


public class NotificationAdapter extends ArrayAdapter<RowItem> {

    Context context;

    public NotificationAdapter(Context context, int resourceId,
                               List<RowItem> items) {
        super(context, resourceId, items);
        this.context = context;
    }

    /*private view holder class*/
    private class ViewHolder {

        ImageView imageView;
        TextView txtTitle;
        // TextView txtinfo;
        WebView webView;
        TextView txtdate;
        View relativeLayout;
    }

    public View getView(int position, View convertView, ViewGroup parent) {
        ViewHolder holder = null;
        RowItem rowItem = getItem(position);

        LayoutInflater mInflater = (LayoutInflater) context
                .getSystemService(Activity.LAYOUT_INFLATER_SERVICE);
        if (convertView == null) {
            convertView = mInflater.inflate(R.layout.notificationlist, null);
            holder = new ViewHolder();


            holder.txtTitle = (TextView) convertView.findViewById(R.id.title);
            // holder.txtinfo=(TextView) convertView.findViewById(R.id.info);
            holder.txtdate=(TextView) convertView.findViewById(R.id.date);
            ImageView imageView=(ImageView) convertView.findViewById(R.id.icon);
            holder.webView=(WebView) convertView.findViewById(R.id.webview);
            holder.imageView=imageView;
            holder.relativeLayout=convertView.findViewById(R.id.relative);
            convertView.setTag(holder);
        } else
            holder = (ViewHolder) convertView.getTag();
        holder.txtTitle.setText(rowItem.getTitle());
        holder.txtdate.setText(rowItem.getDate());
        holder.imageView.setImageResource(rowItem.NgetImageId());
        WebView webview=(WebView) convertView.findViewById(R.id.webview);

        final WebSettings webSettings = webview.getSettings();
        webSettings.setDefaultFixedFontSize(14);
        webview.getSettings().setJavaScriptEnabled(true);
        webview.loadDataWithBaseURL("", rowItem.getDesc(), "text/html", "UTF-8", "");
        holder.webView=webview;
        if(!Boolean.valueOf(rowItem.getNotified())) {
            holder.relativeLayout.setBackgroundColor(Color.rgb(174, 214, 241));
            holder.webView.setBackgroundColor(Color.rgb(174, 214, 241));
        }
        //holder.imageView.setImageResource(rowItem.getImageId());
        return convertView;
    }

    public boolean isHtml(String input)
    {
        Pattern htmlPattern = Pattern.compile(".*\\<[^>]+>.*", Pattern.DOTALL);
        boolean isHTML = htmlPattern.matcher(input).matches();
        return isHTML;
    }
}
