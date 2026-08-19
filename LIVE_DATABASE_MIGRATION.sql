-- ==============================================================================
-- 🚀 UNIFIED PASSENGER APPLICATION - LIVE DATABASE MIGRATION SCRIPT
-- ==============================================================================
-- Execute this script on the target MySQL database (rds2_psngr / production DB).
-- All statements use IF NOT EXISTS / INSERT IGNORE / ON DUPLICATE KEY UPDATE.
-- ==============================================================================

USE `rds2_psngr`; -- Adjust database name if needed

-- ------------------------------------------------------------------------------
-- 1. CREATE NEW TABLE: mobile_app_configurable (Account-Level Feature Flags & Policies)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mobile_app_configurable` (
  `Id` INT(11) NOT NULL AUTO_INCREMENT,
  `AccountId` INT(11) NOT NULL,
  `Username` VARCHAR(100) DEFAULT NULL,
  `AutoLogoutEnabled` TINYINT(4) DEFAULT '1',
  `AutoLogoutTimeoutMinutes` INT(11) DEFAULT '15',
  `TwoFactorAuthEnabled` TINYINT(4) DEFAULT '0',
  `ActivityLogEnabled` TINYINT(4) DEFAULT '1',
  `ForceUpdateEnabled` TINYINT(4) DEFAULT '0',
  `MinRequiredVersion` VARCHAR(20) DEFAULT '1.0.0',
  `PrivacyPolicyEnabled` TINYINT(4) DEFAULT '1',
  `PrivacyPolicyText` LONGTEXT,
  `PanicPressEmail` TINYINT(1) DEFAULT '1',
  `PanicPressSMS` TINYINT(1) DEFAULT '0',
  `UpdatedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `AccountId` (`AccountId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ------------------------------------------------------------------------------
-- 2. CREATE NEW TABLE: mobileapp_activitylog (Audit Trail & Activity Logs)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mobileapp_activitylog` (
  `Id` INT(11) NOT NULL AUTO_INCREMENT,
  `MobileNo` VARCHAR(50) DEFAULT NULL,
  `AccountId` INT(11) DEFAULT NULL,
  `PackageName` VARCHAR(100) DEFAULT '',
  `AppVersion` VARCHAR(50) DEFAULT '',
  `Activity` VARCHAR(150) DEFAULT NULL,
  `Latitude` VARCHAR(50) DEFAULT '',
  `Longitude` VARCHAR(50) DEFAULT '',
  `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ------------------------------------------------------------------------------
-- 3. INSERT MENUS (Auto-increment IDs, no hardcoded IDs)
-- ------------------------------------------------------------------------------
INSERT IGNORE INTO `mobileappmenu` (`menukey`, `menuvalue`) VALUES
('dashboard', 'Grid Icon Dashboard'),
('checklist', 'Pre-Trip Safety Checklist'),
('assigned_veh_tracking', 'Assigned Vehicle Tracking'),
('school_bus_tracking', 'School Bus Tracking'),
('proximity_check', 'Proximity GPS Check'),
('vehicle_change', 'Allow Vehicle Change'),
('qr_scanner', 'Vehicle QR Scanner'),
('tag_in_otp', 'Tag-In with OTP'),
('tag_out', 'Tag-Out Flow'),
('home_location', 'Set Home Location'),
('notifications', 'Notifications Inbox'),
('panic_sos', 'Emergency Panic SOS'),
('live_tracking', 'Live Vehicle Map Tracking');

-- ------------------------------------------------------------------------------
-- 4. INSERT ROLES (Auto-increment IDs, no hardcoded IDs)
-- ------------------------------------------------------------------------------
INSERT IGNORE INTO `roles` (`RoleName`, `RoleType`) VALUES
('passenger_default', 'MobileAppMenu'),
('passenger_school', 'MobileAppMenu'),
('passenger_checklist_only', 'MobileAppMenu');

-- ------------------------------------------------------------------------------
-- 5. SEED DATA: Account Configuration & Terms (mobile_app_configurable)
-- ------------------------------------------------------------------------------
INSERT INTO `mobile_app_configurable` 
(`AccountId`, `Username`, `AutoLogoutEnabled`, `AutoLogoutTimeoutMinutes`, `TwoFactorAuthEnabled`, `ActivityLogEnabled`, `ForceUpdateEnabled`, `MinRequiredVersion`, `PrivacyPolicyEnabled`, `PrivacyPolicyText`, `PanicPressEmail`, `PanicPressSMS`)
VALUES 
(2100, 'Nokia', 1, 15, 0, 1, 0, '1.0.0', 1, 
'<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to the following conditions:</p><ul><li><strong>Location Telemetry:</strong> Vehicle location is tracked for safety, dispatch, and routing.</li><li><strong>Data Protection:</strong> Employee data is kept secure and confidential under organization guidelines.</li><li><strong>Safety Compliance:</strong> Mandatory pre-trip safety checklists must be completed prior to vehicle departure.</li></ul><p>Please review and accept these terms to continue using the application.</p>', 1, 0),
(4315, 'Amazon', 1, 15, 0, 1, 0, '1.0.0', 1, 
'<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to the following conditions:</p><ul><li><strong>Location Telemetry:</strong> Vehicle location is tracked for safety, dispatch, and routing.</li><li><strong>Data Protection:</strong> Employee data is kept secure and confidential under organization guidelines.</li><li><strong>Safety Compliance:</strong> Mandatory pre-trip safety checklists must be completed prior to vehicle departure.</li></ul><p>Please review and accept these terms to continue using the application.</p>', 1, 0)
ON DUPLICATE KEY UPDATE 
`Username` = VALUES(`Username`),
`PrivacyPolicyText` = VALUES(`PrivacyPolicyText`),
`PanicPressEmail` = VALUES(`PanicPressEmail`),
`PanicPressSMS` = VALUES(`PanicPressSMS`);

-- ==============================================================================
-- ✅ MIGRATION COMPLETE!
-- ==============================================================================
