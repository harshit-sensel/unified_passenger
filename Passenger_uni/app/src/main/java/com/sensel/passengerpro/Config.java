package com.sensel.passengerpro;

/**
 * Upload & Local Storage Directory Configuration.
 */
public class Config {
    // File upload url (points to local .NET Core REST API backend /api/image/upload)
    public static final String FILE_UPLOAD_URL = UrlConfig.FILE_UPLOAD_URL;
    public static final String FILE_UPLOAD_URL_UP = UrlConfig.FILE_UPLOAD_URL;

    // Directory name to store captured images locally
    public static final String IMAGE_DIRECTORY_NAME = "Passenger";
}
