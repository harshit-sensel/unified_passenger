package com.sensel.passengerpro;



import java.security.GeneralSecurityException;

/**
 * Created by User on 01-12-2016.
 */

public class Config {
    // File upload url (replace the ip with your server address)
    public static final String FILE_UPLOAD_URL = "http://fleetsmart3.ui.sensel.in/uploadify.ashx";
    public static final String FILE_UPLOAD_URL_UP = "https://fleetsmart3.ui.sensel.in/uploadify_up.ashx";
    // Directory name to store captured images and videos
    public static final String IMAGE_DIRECTORY_NAME = "Passenger";
    public static String DOMAINURL1 = "https://fleetsmart3.ui.sensel.in";
    public static final String FILE_UPLOAD_URL_VOLLEY = DOMAINURL1+"/SenselRestService.svc/rest/v3/";


}
