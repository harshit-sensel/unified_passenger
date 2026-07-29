# 🚀 Production Deployment Checklist - Unified Passenger Application

This document outlines the step-by-step checklist to execute when deploying the **Unified Passenger Application (`com.sensel.passenger`)** and **.NET Core REST Backend** to production.

---

## 1. 🌐 Backend Server Base URL (`UrlConfig.java`)
- [ ] Update `REST_BASE_URL` in `Passenger_uni/app/src/main/java/com/sensel/passengerpro/UrlConfig.java`:
  ```java
  // Change from local emulator loopback:
  // public static String REST_BASE_URL = "http://10.0.2.2:5228/api/";
  
  // To production live HTTPS domain:
  public static String REST_BASE_URL = "https://api.sensel.in/api/";
  ```

---

## 2. ☁️ Live Cloud Storage Integration (`Program.cs`)
- [ ] Configure AWS S3 credentials (or Azure Blob / IIS upload folder) in `backend/Program.cs` `/api/image/upload` endpoint:
  ```csharp
  // Plug in production AWS S3 upload helper:
  AmazonS3Upload.UploadFiles(filePath, fileName, "db-flatfile-backup", s3Path, "PublicRead");
  ```
- [ ] Save generated S3 image URL into MySQL database (`psngr_chklist.Vehiclephoto`, `psngr_chklist.TaginOdometerPhoto`).

---

## 3. 🗄️ Database Connection String (`appsettings.json` / `Program.cs`)
- [ ] Update MySQL connection string in `backend/appsettings.json` or `Program.cs` to point to the production MySQL RDS server instance:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-rds.sensel.in;Port=3306;Database=sensel_passenger;Uid=prod_user;Pwd=ProdPassword123!;"
  }
  ```

---

## 4. 🔒 HTTPS & Security Settings (`AndroidManifest.xml`)
- [ ] Enforce strict HTTPS in `Passenger_uni/app/src/main/res/xml/network_security_config.xml`:
  ```xml
  <domain-config cleartextTrafficPermitted="false">
      <domain includeSubdomains="true">api.sensel.in</domain>
  </domain-config>
  ```
- [ ] Verify `android:authorities="${applicationId}.provider"` remains in `AndroidManifest.xml` to prevent installation conflicts.

---

## 5. 📱 Signed Release APK Compilation
- [ ] Build release APK / App Bundle (AAB) using production signing keystore:
  ```powershell
  $env:JAVA_HOME="C:\Program Files\Android\Android Studio\jbr"
  .\gradlew assembleRelease
  ```
- [ ] Test signed release APK on physical target devices.

---

**Last Updated**: July 29, 2026
