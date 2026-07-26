package com.sensel.passengerpro;

public class RowItem {
    private int imageId;
    private String title;
    private String desc;
    private String date;
    private String notified;

    public RowItem(int imageId, String title, String desc, String date,String notified) {
        this.imageId = imageId;
        this.title = title;
        this.desc = desc;
        this.date=date;
        this.notified=notified;
    }

    public int NgetImageId() {
        return imageId;
    }
    public String getDesc() {
        return desc;
    }
    public String getTitle() {
        return this.title;
    }
    public void setTitle(String title) {
        this.title = title;
    }
    public String getDate() {
        return date;
    }
    public String getNotified() {
        return notified;
    }
    @Override
    public String toString() {
        return title + " " + desc +" "+date;
    }
}