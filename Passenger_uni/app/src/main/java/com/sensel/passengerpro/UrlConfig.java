package com.sensel.passengerpro;

/**
 * Server URL Configuration for Unified Passenger Application.
 */

public class UrlConfig {
    public static String WSDL_TARGET_NAMESPACE = "http://tempuri.org/";

    // New .NET Core REST API Base URL (10.0.2.2 for Android Studio Emulator)
    public static String REST_BASE_URL = "http://10.0.2.2:5228/api/";

    public static String DOMAINURL1 = "https://ui.vehicle-tracking.co.in/";
    public static String DOMAINURL2 = "http://ui.vehicle-tracking.co.in/";
    public static String DOMAINURL3 = "https://ui.vehicle-tracking.co.in/";

    /** Map page (Track Your Vehicle) – production with route draw / 50m zoom. */
    public static final String MAP_PAGE_BASE_URL = "https://ui.vehicle-tracking.co.in";
    public static final String MAP_PAGE_URL = MAP_PAGE_BASE_URL + "/hybridapp_pages/index-min1.html";
    public static final String MAP_DOMAIN = "https://ui.vehicle-tracking.co.in";

    public static String FILE_UPLOAD_URL = REST_BASE_URL + "image/upload";
    public static String FILE_UPLOAD_URL_VOLLEY = REST_BASE_URL + "image/upload";
    public static final String PASSENGER_PRO_AUTHENTICATE_URL = REST_BASE_URL + "auth/send-otp";

    public static String IMAGE_DIRECTORY_NAME = "PassengerApp";
    public static String tata_accountid = "4315";

    public static final String ADMINPANELHTTPSURL = "https://adminpanel.mysensel.com/Forms/Service.asmx";
    public static final String ADMINPANELHTTPURL = "http://adminpanel.mysensel.com/Forms/Service.asmx";
}
