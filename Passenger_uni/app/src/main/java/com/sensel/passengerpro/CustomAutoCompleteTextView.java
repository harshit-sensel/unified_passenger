package com.sensel.passengerpro;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.Filter;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/**
 * Created by MS on 23-Dec-17.
 */

public class CustomAutoCompleteTextView extends ArrayAdapter<Names> {
    Context context;
    int resource, textViewResourceId;
    List<Names> items, tempItems, suggestions;

    public CustomAutoCompleteTextView(Context context, int resource, int textViewResourceId, List<Names> items) {
        super(context, resource, textViewResourceId, items);
        this.context = context;
        this.resource = resource;
        this.textViewResourceId = textViewResourceId;
        this.items = items;
        tempItems = new ArrayList<Names>(items); // this makes the difference.
        suggestions = new ArrayList<Names>();
    }

    @Override
    public View getView(int position, View convertView, ViewGroup parent) {
        View view = convertView;
        if (convertView == null) {
            LayoutInflater inflater = (LayoutInflater) context.getSystemService(Context.LAYOUT_INFLATER_SERVICE);
            view = inflater.inflate(R.layout.autocompleteview, parent, false);
        }
        Names names = items.get(position);
        if (names != null) {
            TextView lblName = (TextView) view.findViewById(R.id.lbl_name);
            if (lblName != null)
                lblName.setText(names.name);
        }
        return view;
    }

    @Override
    public Filter getFilter() {
        return nameFilter;
    }

    /**
     * Custom Filter implementation for custom suggestions we provide.
     */
    Filter nameFilter = new Filter() {
        @Override
        public CharSequence convertResultToString(Object resultValue) {
            String str = ((Names) resultValue).name;
            return str;
        }

        @Override
        protected FilterResults performFiltering(CharSequence constraint) {
            if (constraint != null) {
                suggestions.clear();
                for (Names names : tempItems) {
                    if (names.name.toLowerCase().contains(constraint.toString().toLowerCase())) {
                        suggestions.add(names);
                    }
                }
                FilterResults filterResults = new FilterResults();
                filterResults.values = suggestions;
                filterResults.count = suggestions.size();
                return filterResults;
            } else {
                return new FilterResults();
            }
        }

        @Override
        protected void publishResults(CharSequence constraint, FilterResults results) {
            List<Names> filterList = (ArrayList<Names>) results.values;
            if (results != null && results.count > 0) {
                clear();
                for (Names names : filterList) {
                    add(names);
                    notifyDataSetChanged();
                }
            }
        }
    };
}
class Names {
    public String name;
    public Names(String Name){
        this.name=Name;
    }
}

