package com.sensel.passengerpro;



import java.security.GeneralSecurityException;

/**
 * Created by User on 01-12-2016.
 */

public class Config {
    // File upload url (points to local .NET Core REST API backend /api/image/upload)
    public static final String FILE_UPLOAD_URL = UrlConfig.FILE_UPLOAD_URL;
    public static final String FILE_UPLOAD_URL_UP = UrlConfig.FILE_UPLOAD_URL;
    // Directory name to store captured images and videos
    public static final String IMAGE_DIRECTORY_NAME = "Passenger";
    public static String DOMAINURL1 = "https://fleetsmart3.ui.sensel.in";
    public static final String FILE_UPLOAD_URL_VOLLEY = DOMAINURL1+"/SenselRestService.svc/rest/v3/";


}
