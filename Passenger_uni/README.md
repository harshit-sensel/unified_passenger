# Passenger Pro (Android)

# Passenger Pro release signing (keystore in app folder)
PASSENGERPRO_RELEASE_STORE_FILE=passengerpro-release.keystore
PASSENGERPRO_RELEASE_STORE_PASSWORD=passengerpro123
PASSENGERPRO_RELEASE_KEY_ALIAS=passengerpro
PASSENGERPRO_RELEASE_KEY_PASSWORD=passengerpro123

Native Android app for **Sensel Passenger Pro**: OTP login, QR / OTP tag-in, vehicle tracking on a map, panic, tag-out, notifications, and home location. It talks to Sensel backend services over SOAP and a small REST endpoint for OTP.

## Application identity

| Item | Value |
|------|--------|
| **Application ID** | `com.sensel.passengerpro` |
| **Namespace** | `com.sensel.passengerpro` |
| **Launcher activity** | `LoginActivity` |

Current **version** (see `app/build.gradle`): `versionName` / `versionCode` in `defaultConfig`.

## Requirements

- **Android Studio** (recent stable) or Android Gradle build environment  
- **JDK 11** (project `compileOptions` / `targetCompatibility`: 11)  
- **Android SDK 35** (`compileSdkVersion` / `targetSdkVersion`: 35)  
- **Min SDK**: 21  

## Open and build

1. Open the folder `PassengerPro` in Android Studio (this repo root contains `app/` and `settings.gradle`).
2. Sync Gradle.
3. **Debug**: Run the `app` configuration on a device or emulator.
4. **Release APK / bundle**: `Build → Generate Signed Bundle / APK…` or from terminal:

```text
gradlew.bat assembleRelease
```

(Use `./gradlew assembleRelease` on macOS/Linux.)

## Release signing

Setting	Value in repo
Keystore password (PASSENGERPRO_RELEASE_STORE_PASSWORD)
passengerpro123
Key password (PASSENGERPRO_RELEASE_KEY_PASSWORD)
passengerpro123
Key alias (PASSENGERPRO_RELEASE_KEY_ALIAS)

Release builds use a keystore configured via **Gradle project properties** (see `gradle.properties` in the project root):

| Property | Purpose |
|----------|---------|
| `PASSENGERPRO_RELEASE_STORE_FILE` | Keystore file path (relative to the **`app`** module unless absolute) |
| `PASSENGERPRO_RELEASE_STORE_PASSWORD` | Keystore password |
| `PASSENGERPRO_RELEASE_KEY_ALIAS` | Key alias |
| `PASSENGERPRO_RELEASE_KEY_PASSWORD` | Key password |

`app/build.gradle` wires `signingConfigs.release` when `PASSENGERPRO_RELEASE_STORE_FILE` is set. For CI or tighter security, you can define the same properties in `~/.gradle/gradle.properties` or environment-specific files **without** committing secrets.

## Main features (high level)

- **Login**: mobile number → validate → OTP → session stored as `passengerinfo` preferences.
- **Main menu**: Tag In (QR), Tag In (OTP), Track vehicle (WebView map), Panic (if enabled by `PanicFlag`), Tagout, Logout.
- **Location**: Fine location used for tag flows, panic, and activity logging; fused / cached coordinates where applicable.
- **Force update**: On main menu entry, `ForceUpdateChecker` compares server `GetAppCurrentVersion` metadata (`VersionCode`, `StableVersion`, `Priority`) with `BuildConfig.VERSION_CODE` and can prompt for Play Store update (aligned with legacy FleetSmart-style rules).
- **Activity logging**: `PassengerActivityLogger` / `KeepPassengerProuseractivitylog` for screen and menu events (with live **Validate**-first vehicle resolution where configured).

## Permissions (summary)

Declared in `app/src/main/AndroidManifest.xml`, including among others: `INTERNET`, `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION`, `READ_PHONE_STATE`, network state, storage (where applicable), and `CAMERA` for QR / photo flows.

## Project layout

| Path | Role |
|------|------|
| `app/src/main/java/com/sensel/passengerpro/` | Java sources (activities, services, SOAP helpers) |
| `app/src/main/res/` | Layouts, drawables, strings, themes |
| `app/libs/` | Bundled JARs (e.g. kSOAP) |
| `app/build.gradle` | App module config, signing, dependencies |
| `gradle.properties` | Project-wide Gradle flags and optional signing props |

## Backend / config

SOAP base URLs and REST endpoints are centralized (e.g. `UrlConfig`, `WebServices`). Domain sync from `GetAppCurrentVersion` may apply before some calls (e.g. tracking). Adjust only with backend coordination.

## Related

A **Flutter** Passenger Pro client also exists in the workspace; behavior is kept conceptually aligned where features overlap, but this README applies **only** to this Android project.

## License / ownership

Proprietary — Sensel / project owner. Do not redistribute the keystore or credentials.
