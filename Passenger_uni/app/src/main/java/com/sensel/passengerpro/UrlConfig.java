package com.sensel.passengerpro;

/**
 * Server URL and Configuration Constants for the Unified Passenger Application.
 * Centralizes all backend REST endpoints, security headers, web map portals, and storage paths.
 */
public class UrlConfig {

    /**
     * Master Base URL for the .NET 8 REST Web API backend.
     * In Production: Update this to your live HTTPS domain (e.g., "https://passengerapi.sensel.in/api/").
     * In Local Testing: Points to the local development server via ADB reverse port forwarding.
     */
    public static String REST_BASE_URL = "http://127.0.0.1:5000/api/";

    /**
     * Security & Authentication Headers:
     * - API_SECURITY_KEY: Pre-shared secret passphrase sent in the "X-App-Key" header (matches backend "SecuritySettings:AppKeySecret").
     * - HEADER_APP_KEY: Header name for API security validation ("X-App-Key").
     * - HEADER_AUTHORIZATION: Header name for JWT Bearer authentication tokens ("Authorization").
     */
    public static final String API_SECURITY_KEY = "Passenger_SecretPassphrase_2026";
    public static final String HEADER_APP_KEY = "X-App-Key";
    public static final String HEADER_AUTHORIZATION = "Authorization";

    /**
     * Dynamic Tracking Portal URLs (HTTPS/HTTP).
     * These act as defaults and are dynamically updated at runtime if the user's account
     * provides a specific tracking server domain in the login/vehicle response.
     */
    public static String DOMAINURL1 = "https://ui.vehicle-tracking.co.in/";
    public static String DOMAINURL2 = "http://ui.vehicle-tracking.co.in/";
    public static String DOMAINURL3 = "https://ui.vehicle-tracking.co.in/";

    /**
     * Live GPS Map Portal URL used in TrackOnMap.java.
     * Renders real-time vehicle movement, 50m zoom, and route path drawing inside an in-app WebView.
     */
    public static final String MAP_PAGE_BASE_URL = "https://ui.vehicle-tracking.co.in";
    public static final String MAP_PAGE_URL = MAP_PAGE_BASE_URL + "/hybridapp_pages/index-min1.html";
    public static final String MAP_DOMAIN = "https://ui.vehicle-tracking.co.in";

    /**
     * Specific REST Endpoints:
     * - FILE_UPLOAD_URL / FILE_UPLOAD_URL_VOLLEY: Multipart photo upload endpoint for vehicle & checklist images ("image/upload").
     * - PASSENGER_PRO_AUTHENTICATE_URL: OTP verification endpoint for Tag-In authentication ("auth/send-otp").
     */
    public static String FILE_UPLOAD_URL = REST_BASE_URL + "image/upload";
    public static String FILE_UPLOAD_URL_VOLLEY = REST_BASE_URL + "image/upload";
    public static final String PASSENGER_PRO_AUTHENTICATE_URL = REST_BASE_URL + "auth/send-otp";

    /**
     * Local storage subfolder name on the phone where captured vehicle/inspection photos are saved.
     */
    public static String IMAGE_DIRECTORY_NAME = "PassengerApp";

    /**
     * Specific Account ID for Tata Motors enterprise fleet integration rules in VehicleInfo.java.
     */
    public static String tata_accountid = "4315";
}
