-- ============================================================================
-- PRODUCTION MIGRATION SCRIPT FOR PASSENGER MOBILE APP
-- Tables: mobile_app_configurable & mobileapp_activitylog
-- Database: rds2_psngr
-- ============================================================================

-- 1. Create mobile_app_configurable Table (Account-Level Feature Flags)
CREATE TABLE IF NOT EXISTS `mobile_app_configurable` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `AccountId` INT UNIQUE NOT NULL,
    `AutoLogoutEnabled` TINYINT DEFAULT 1 COMMENT '1 = Enabled, 0 = Disabled',
    `AutoLogoutTimeoutMinutes` INT DEFAULT 15 COMMENT 'Minutes of inactivity before auto logout',
    `TwoFactorAuthEnabled` TINYINT DEFAULT 0 COMMENT 'Placeholder for future 2FA',
    `ActivityLogEnabled` TINYINT DEFAULT 1 COMMENT '1 = Log activities, 0 = Disable logging',
    `ForceUpdateEnabled` TINYINT DEFAULT 0 COMMENT '1 = Force app update required',
    `MinRequiredVersion` VARCHAR(20) DEFAULT '2.0.0' COMMENT 'Min required APK version string',
    `PrivacyPolicyEnabled` TINYINT DEFAULT 1 COMMENT '1 = Prompt privacy policy popup',
    `PrivacyPolicyText` LONGTEXT COMMENT 'Dynamic HTML/Markdown terms text',
    `UpdatedAt` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 2. Create mobileapp_activitylog Table (Granular User Activity Audit Trail)
CREATE TABLE IF NOT EXISTS `mobileapp_activitylog` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `MobileNo` VARCHAR(50) COMMENT 'Passenger mobile number',
    `AccountId` INT COMMENT 'Organization account ID',
    `PackageName` VARCHAR(100) DEFAULT 'com.sensel.passenger' COMMENT 'App package name',
    `Activity` VARCHAR(150) COMMENT 'LOGIN, TAG_IN, TAG_OUT, PANIC_ALERT, PRIVACY_POLICY_ACCEPTED, etc.',
    `Latitude` VARCHAR(50) DEFAULT '' COMMENT 'GPS Latitude',
    `Longitude` VARCHAR(50) DEFAULT '' COMMENT 'GPS Longitude',
    `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT 'Log timestamp'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Sample Seed Configuration for Account 2100 & 4315
INSERT INTO `mobile_app_configurable` 
(`AccountId`, `AutoLogoutEnabled`, `AutoLogoutTimeoutMinutes`, `TwoFactorAuthEnabled`, `ActivityLogEnabled`, `ForceUpdateEnabled`, `MinRequiredVersion`, `PrivacyPolicyEnabled`, `PrivacyPolicyText`)
VALUES 
(2100, 1, 15, 0, 1, 0, '2.0.0', 1, '<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to organization safety telemetry guidelines.</p>'),
(4315, 1, 15, 0, 1, 0, '2.0.0', 1, '<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to organization safety telemetry guidelines.</p>')
ON DUPLICATE KEY UPDATE 
`AutoLogoutEnabled` = 1, `AutoLogoutTimeoutMinutes` = 15, `ActivityLogEnabled` = 1, `PrivacyPolicyEnabled` = 1;
