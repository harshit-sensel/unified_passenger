# 🚀 Passenger Backend Deployment & Server Setup Guide

This guide explains how to deploy the .NET 8 Backend API to a Windows Test Server and connect it directly to your existing database.

---

## 🏗️ 1. Architecture Overview

```
┌────────────────────────┐         HTTP / HTTPS          ┌────────────────────────┐
│   Android Mobile App   │ ────────────────────────────► │   Backend .NET 8 API   │
│  (Passenger Unified)   │                               │  (On Windows Server)   │
└────────────────────────┘                               └───────────┬────────────┘
                                                                     │
                                                                     │ Remote MySQL (Port 3306)
                                                                     │ Server=172.16.15.30
                                                                     ▼
                                                         ┌────────────────────────┐
                                                         │      Your Laptop       │
                                                         │  (MySQL Database)      │
                                                         │  Database: rds2_psngr  │
                                                         └────────────────────────┘
```

> [!NOTE]
> You do **not** need to dump or transfer the database. The backend on the test server will query and update your laptop's MySQL database directly in real time.

---

## 📦 2. Files to Share with Your Manager

* **Backend Package**: [`d:\passenger\passenger\backend\PassengerBackend_Publish.zip`](file:///d:/passenger/passenger/backend/PassengerBackend_Publish.zip) *(Size: ~2 MB)*
* **Contents**:
  * `backend.exe` (Self-contained executable)
  * `backend.dll`
  * `web.config` (Pre-configured for IIS)
  * `appsettings.json` (Configuration)
  * All dependencies (`Dapper`, `MySqlConnector`, `SwaggerUI`, etc.)

---

## 💻 3. Laptop Preparation (One-Time Setup)

Your laptop is already configured to accept remote MySQL connections.

### Step 1: Open Port 3306 in Windows Firewall
On your laptop, open **PowerShell as Administrator** and run:
```powershell
netsh advfirewall firewall add rule name="MySQL 3306" dir=in action=allow protocol=TCP localport=3306
```

### Step 2: Note Your Laptop's Network Details
* **IP Address**: `172.16.15.30`
* **MySQL Port**: `3306`
* **Database**: `rds2_psngr`
* **Username**: `root`
* **Password**: `12345`

---

## 🖥️ 4. Test Server Deployment Steps (For Your Manager)

### Step 1: Install .NET 8 Hosting Bundle (One-Time)
Download and install the **.NET 8.0 Hosting Bundle** on the Windows Server:
* [Download .NET 8 Hosting Bundle from Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)

---

### Step 2: Extract the Package
1. Create a folder on the server: `C:\PassengerApi`.
2. Extract all files from `PassengerBackend_Publish.zip` into `C:\PassengerApi\`.

---

### Step 3: Configure Database Connection
Open `C:\PassengerApi\appsettings.json` in Notepad and update the connection string to point to your laptop:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=172.16.15.30;Port=3306;Database=rds2_psngr;User Id=root;Password=12345;"
  },
  "SecuritySettings": {
    "AppKeySecret": "Passenger_SecretPassphrase_2026"
  },
  "JwtSettings": {
    "SecretKey": "PassengerApp_SuperSecret_JWT_Signing_Key_2026_SenselTelematics!",
    "Issuer": "SenselBackend",
    "Audience": "PassengerApp",
    "ExpirationDays": 7
  }
}
```

---

### Step 4: Run the Backend API (via PowerShell / CMD)

1. Open **PowerShell** or **Command Prompt** (Run as Administrator) on the server.
2. Run:
   ```powershell
   cd C:\PassengerApi
   .\backend.exe --urls "http://0.0.0.0:5000"
   ```
3. The server will output:
   ```text
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://0.0.0.0:5000
   info: Microsoft.Hosting.Lifetime[0]
         Application started. Press Ctrl+C to shut down.
   ```

> [!TIP]
> * **Automatic Folder Creation**: The `Uploads/` directory will be created automatically inside `C:\PassengerApi\Uploads\` as soon as photos are uploaded.
> * **Live Logging**: All API calls, TagIn requests, TagOut events, and errors stream in real-time in the PowerShell window.

---

### Step 5: Allow Inbound Port 5000 on Server Firewall
In PowerShell as Administrator on the server:
```powershell
netsh advfirewall firewall add rule name="Passenger API 5000" dir=in action=allow protocol=TCP localport=5000
```

---

### Step 6: Verify via Swagger UI
Open a web browser on any device in the office network:
```text
http://<TEST_SERVER_IP>:5000/swagger
```
If the Swagger interactive documentation page loads, the backend is 100% operational!

---

## 📱 5. Android Mobile App Configuration

Once the test server is running:

1. Open [`UrlConfig.java`](file:///d:/passenger/passenger/Passenger_uni/app/src/main/java/com/sensel/passengerpro/UrlConfig.java) in Android Studio.
2. Update the base URL:
   ```java
   // Replace localhost/emulator IP with your Test Server IP:
   public static final String BASE_URL = "http://<TEST_SERVER_IP>:5000";
   ```
3. Build and run the app. All mobile requests will now route through the test server!

---

## 🩺 6. Troubleshooting Checklist

| Issue | Root Cause | Fix |
|:---|:---|:---|
| **Cannot connect to database (`172.16.15.30`)** | Firewall blocking port 3306 on laptop | Run the `netsh advfirewall` command on laptop (Section 3). |
| **App cannot connect to Test Server (`5000`)** | Firewall blocking port 5000 on server | Run the firewall rule on the server (Section 4, Step 5). |
| **"dotnet is not recognized" error** | .NET 8 Runtime missing | Install the [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0). |
