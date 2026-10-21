-- ==============================================================================
-- 🚀 UNIFIED PASSENGER APPLICATION - LIVE DATABASE MIGRATION SCRIPT
-- ==============================================================================
-- Target Database: awsrds2 (Production RDS) or rds2_psngr (Local Development)
-- 
-- KEY DESIGN PRINCIPLES:
-- 1. NO HARDCODED IDs: All tables use MySQL AUTO_INCREMENT. The database
--    automatically assigns the next available ID after the current maximum.
-- 2. SAFE & IDEMPOTENT: Uses IF NOT EXISTS, INSERT IGNORE, and NOT EXISTS 
--    subqueries. Running this script multiple times is completely safe.
-- 3. CASE-COMPLIANT: Uses PascalCase for `Roles` and `UsersInRoles` to support
--    strict Linux MySQL case sensitivity (lower_case_table_names = 0).
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. CREATE TABLE: mobile_app_configurable (Account Settings & Policies)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mobile_app_configurable` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `AccountId` INT NOT NULL,
  `Username` VARCHAR(100) DEFAULT NULL,
  `AutoLogoutEnabled` TINYINT DEFAULT '1',
  `AutoLogoutTimeoutMinutes` INT DEFAULT '15',
  `TwoFactorAuthEnabled` TINYINT DEFAULT '0',
  `ActivityLogEnabled` TINYINT DEFAULT '1',
  `ForceUpdateEnabled` TINYINT DEFAULT '0',
  `MinRequiredVersion` VARCHAR(20) DEFAULT '2.0.0',
  `PrivacyPolicyEnabled` TINYINT DEFAULT '1',
  `PrivacyPolicyText` LONGTEXT,
  `PanicPressEmail` TINYINT(1) DEFAULT '1',
  `PanicPressSMS` TINYINT(1) DEFAULT '0',
  `UpdatedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `AccountId` (`AccountId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ------------------------------------------------------------------------------
-- 2. CREATE TABLE: mobileapp_activitylog (Audit Trail & Activity Logs)
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mobileapp_activitylog` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `MobileNo` VARCHAR(50) DEFAULT NULL,
  `AccountId` INT DEFAULT NULL,
  `PackageName` VARCHAR(100) DEFAULT 'com.sensel.passenger',
  `AppVersion` VARCHAR(50) DEFAULT '',
  `Activity` VARCHAR(150) DEFAULT NULL,
  `Latitude` VARCHAR(50) DEFAULT '',
  `Longitude` VARCHAR(50) DEFAULT '',
  `CreatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ------------------------------------------------------------------------------
-- 3. INSERT PASSENGER MENUS (No IDs specified - uses next auto-increment)
-- ------------------------------------------------------------------------------
INSERT INTO `mobileappmenu` (`menukey`, `menuvalue`)
SELECT m.menukey, m.menuvalue
FROM (
    SELECT 'psngr_dashboard' AS menukey, 'Grid Icon Dashboard' AS menuvalue UNION ALL
    SELECT 'psngr_checklist', 'Pre-Trip Safety Checklist' UNION ALL
    SELECT 'psngr_assigned_veh_tracking', 'Assigned Vehicle Tracking' UNION ALL
    SELECT 'psngr_school_bus_tracking', 'School Bus Tracking' UNION ALL
    SELECT 'psngr_proximity_check', 'Proximity GPS Check' UNION ALL
    SELECT 'psngr_vehicle_change', 'Allow Vehicle Change' UNION ALL
    SELECT 'psngr_qr_scanner', 'Vehicle QR Scanner' UNION ALL
    SELECT 'psngr_tag_in_otp', 'Tag-In with OTP' UNION ALL
    SELECT 'psngr_tag_out', 'Tag-Out Flow' UNION ALL
    SELECT 'psngr_home_location', 'Set Home Location' UNION ALL
    SELECT 'psngr_notifications', 'Notifications Inbox' UNION ALL
    SELECT 'psngr_panic_sos', 'Emergency Panic SOS' UNION ALL
    SELECT 'psngr_live_tracking', 'Live Vehicle Map Tracking'
) m
WHERE NOT EXISTS (
    SELECT 1 FROM `mobileappmenu` existing WHERE existing.`menukey` = m.menukey
);

-- ------------------------------------------------------------------------------
-- 4. INSERT PASSENGER ROLES (No IDs specified - uses next auto-increment)
-- ------------------------------------------------------------------------------
INSERT INTO `Roles` (`RoleName`, `RoleType`)
SELECT r.RoleName, r.RoleType
FROM (
    SELECT 'passenger_default' AS RoleName, 'MobileAppMenu' AS RoleType UNION ALL
    SELECT 'passenger_school', 'MobileAppMenu' UNION ALL
    SELECT 'passenger_checklist_only', 'MobileAppMenu'
) r
WHERE NOT EXISTS (
    SELECT 1 FROM `Roles` existing WHERE existing.`RoleName` = r.RoleName
);

-- ------------------------------------------------------------------------------
-- 5. MAP MENUS TO ROLES (Dynamic subqueries - zero hardcoded IDs)
-- ------------------------------------------------------------------------------

-- Role: passenger_default (Full Grid Dashboard)
INSERT INTO `mobileappmenuinroles` (`MobileAppMenuId`, `RoleId`)
SELECT m.Id, r.ID 
FROM `mobileappmenu` m, `Roles` r
WHERE r.RoleName = 'passenger_default'
  AND m.menukey IN (
      'psngr_dashboard',
      'psngr_qr_scanner',
      'psngr_tag_in_otp',
      'psngr_tag_out',
      'psngr_notifications',
      'psngr_panic_sos',
      'psngr_live_tracking'
  )
  AND NOT EXISTS (
      SELECT 1 FROM `mobileappmenuinroles` existing 
      WHERE existing.MobileAppMenuId = m.Id AND existing.RoleId = r.ID
  );

-- Role: passenger_school (School Bus Tracking Flow)
INSERT INTO `mobileappmenuinroles` (`MobileAppMenuId`, `RoleId`)
SELECT m.Id, r.ID 
FROM `mobileappmenu` m, `Roles` r
WHERE r.RoleName = 'passenger_school'
  AND m.menukey = 'psngr_school_bus_tracking'
  AND NOT EXISTS (
      SELECT 1 FROM `mobileappmenuinroles` existing 
      WHERE existing.MobileAppMenuId = m.Id AND existing.RoleId = r.ID
  );

-- Role: passenger_checklist_only (Pre-Trip Safety Checklist Flow)
INSERT INTO `mobileappmenuinroles` (`MobileAppMenuId`, `RoleId`)
SELECT m.Id, r.ID 
FROM `mobileappmenu` m, `Roles` r
WHERE r.RoleName = 'passenger_checklist_only'
  AND m.menukey = 'psngr_checklist'
  AND NOT EXISTS (
      SELECT 1 FROM `mobileappmenuinroles` existing 
      WHERE existing.MobileAppMenuId = m.Id AND existing.RoleId = r.ID
  );

-- ------------------------------------------------------------------------------
-- 6. MAP USERNAMES TO ROLES IN UsersInRoles (Dynamic subqueries - zero hardcoded IDs)
-- ------------------------------------------------------------------------------

-- User Amazon -> passenger_default
INSERT INTO `UsersInRoles` (`UserId`, `RoleId`, `CreatedAt`)
SELECT 'Amazon', r.ID, NOW()
FROM `Roles` r
WHERE r.RoleName = 'passenger_default'
  AND NOT EXISTS (
      SELECT 1 FROM `UsersInRoles` existing 
      WHERE existing.UserId = 'Amazon' AND existing.RoleId = r.ID
  );

-- User dpss -> passenger_school
INSERT INTO `UsersInRoles` (`UserId`, `RoleId`, `CreatedAt`)
SELECT 'dpss', r.ID, NOW()
FROM `Roles` r
WHERE r.RoleName = 'passenger_school'
  AND NOT EXISTS (
      SELECT 1 FROM `UsersInRoles` existing 
      WHERE existing.UserId = 'dpss' AND existing.RoleId = r.ID
  );

-- User Nokia -> passenger_checklist_only
INSERT INTO `UsersInRoles` (`UserId`, `RoleId`, `CreatedAt`)
SELECT 'Nokia', r.ID, NOW()
FROM `Roles` r
WHERE r.RoleName = 'passenger_checklist_only'
  AND NOT EXISTS (
      SELECT 1 FROM `UsersInRoles` existing 
      WHERE existing.UserId = 'Nokia' AND existing.RoleId = r.ID
  );

-- ------------------------------------------------------------------------------
-- 7. SEED ACCOUNT CONFIGURATION (mobile_app_configurable)
-- ------------------------------------------------------------------------------
INSERT INTO `mobile_app_configurable` 
(`AccountId`, `Username`, `AutoLogoutEnabled`, `AutoLogoutTimeoutMinutes`, `TwoFactorAuthEnabled`, `ActivityLogEnabled`, `ForceUpdateEnabled`, `MinRequiredVersion`, `PrivacyPolicyEnabled`, `PrivacyPolicyText`, `PanicPressEmail`, `PanicPressSMS`)
VALUES 
(2100, 'Nokia', 1, 15, 0, 1, 0, '2.0.0', 1, 
'<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to the following conditions:</p><ul><li><strong>Location Telemetry:</strong> Vehicle location is tracked for safety, dispatch, and routing.</li><li><strong>Data Protection:</strong> Employee data is kept secure and confidential under organization guidelines.</li><li><strong>Safety Compliance:</strong> Mandatory pre-trip safety checklists must be completed prior to vehicle departure.</li></ul><p>Please review and accept these terms to continue using the application.</p>', 1, 0),
(2757, 'dpss', 1, 15, 0, 1, 0, '2.0.0', 1, 
'<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to the following conditions:</p><ul><li><strong>Location Telemetry:</strong> Vehicle location is tracked for safety, dispatch, and routing.</li><li><strong>Data Protection:</strong> Employee data is kept secure and confidential under organization guidelines.</li><li><strong>Safety Compliance:</strong> Mandatory pre-trip safety checklists must be completed prior to vehicle departure.</li></ul><p>Please review and accept these terms to continue using the application.</p>', 1, 0),
(4315, 'Amazon', 1, 15, 0, 1, 0, '2.0.0', 1, 
'<h3>Passenger Pro Terms & Privacy Policy</h3><p>Welcome to the Passenger Application. By using this service, you agree to the following conditions:</p><ul><li><strong>Location Telemetry:</strong> Vehicle location is tracked for safety, dispatch, and routing.</li><li><strong>Data Protection:</strong> Employee data is kept secure and confidential under organization guidelines.</li><li><strong>Safety Compliance:</strong> Mandatory pre-trip safety checklists must be completed prior to vehicle departure.</li></ul><p>Please review and accept these terms to continue using the application.</p>', 1, 0)
ON DUPLICATE KEY UPDATE 
`Username` = VALUES(`Username`),
`PrivacyPolicyText` = VALUES(`PrivacyPolicyText`),
`PanicPressEmail` = VALUES(`PanicPressEmail`),
`PanicPressSMS` = VALUES(`PanicPressSMS`);

-- ==============================================================================
-- ✅ LIVE DATABASE MIGRATION SCRIPT READY
-- ==============================================================================
