-- ============================================================
-- PathlabDB Setup Script
-- Run this in SSMS against DESKTOP-KU06BHL (sa / bws)
-- ============================================================

-- 1. Create database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PathlabDB')
BEGIN
  CREATE DATABASE PathlabDB;
END
GO

USE PathlabDB;
GO

-- 2. Tables

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Patients' AND xtype='U')
CREATE TABLE Patients (
  PatientId     INT IDENTITY(1,1) PRIMARY KEY,
  FullName      NVARCHAR(150) NOT NULL,
  Phone         NVARCHAR(15)  NOT NULL UNIQUE,
  Email         NVARCHAR(150),
  Gender        NVARCHAR(10),
  DateOfBirth   DATETIME,
  Address       NVARCHAR(300),
  City          NVARCHAR(100),
  Pincode       NVARCHAR(10),
  PasswordHash  NVARCHAR(255) NOT NULL DEFAULT '',
  IsActive      BIT           NOT NULL DEFAULT 1,
  CreatedDate   DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LabTests' AND xtype='U')
CREATE TABLE LabTests (
  TestId        INT IDENTITY(1,1) PRIMARY KEY,
  TestSuiteID   NVARCHAR(50)  NOT NULL,
  TestSuiteName NVARCHAR(200) NOT NULL,
  ShortName     NVARCHAR(100),
  Price         DECIMAL(18,2) NOT NULL DEFAULT 0,
  SampleType    NVARCHAR(100),
  TestCount     INT           NOT NULL DEFAULT 1,
  ReportTime    NVARCHAR(50),
  Fasting       NVARCHAR(50),
  Category      NVARCHAR(100),
  Description   NVARCHAR(500),
  TestType      NVARCHAR(20)  NOT NULL DEFAULT 'Test',  -- 'Test' | 'Package' (landing-page grouping; not in LIMS)
  IsActive      BIT           NOT NULL DEFAULT 1
);
GO

-- Migration for databases created before TestType existed
IF COL_LENGTH('LabTests','TestType') IS NULL
BEGIN
  ALTER TABLE LabTests ADD TestType NVARCHAR(20) NOT NULL DEFAULT 'Test';
END
GO
-- EF stores a model hash here when it created the DB itself; after adding
-- TestType the hashes no longer match and EF throws on startup. Dropping the
-- history table makes EF skip the compatibility check (we don't use migrations).
IF OBJECT_ID('dbo.__MigrationHistory') IS NOT NULL DROP TABLE dbo.__MigrationHistory;
GO
UPDATE LabTests SET TestType = 'Package' WHERE Category = 'Packages' AND TestType <> 'Package';
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='HealthPackages' AND xtype='U')
CREATE TABLE HealthPackages (
  PackageId     INT IDENTITY(1,1) PRIMARY KEY,
  PackageCode   NVARCHAR(50)  NOT NULL,
  PackageName   NVARCHAR(200) NOT NULL,
  Description   NVARCHAR(500),
  Price         DECIMAL(18,2) NOT NULL DEFAULT 0,
  OriginalPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
  TestCount     INT           NOT NULL DEFAULT 1,
  SampleType    NVARCHAR(100),
  ReportTime    NVARCHAR(50),
  Fasting       NVARCHAR(50),
  Badge         NVARCHAR(100),
  Includes      NVARCHAR(MAX),
  IsActive      BIT           NOT NULL DEFAULT 1
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Bookings' AND xtype='U')
CREATE TABLE Bookings (
  BookingId     INT IDENTITY(1,1) PRIMARY KEY,
  BookingRef    NVARCHAR(20)  NOT NULL UNIQUE,
  PatientId     INT           NOT NULL,
  CollectionType NVARCHAR(20),
  BranchName    NVARCHAR(200),
  Address       NVARCHAR(400),
  CollectionDate DATETIME,
  TimeSlot      NVARCHAR(50),
  Subtotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
  GstAmount     DECIMAL(18,2) NOT NULL DEFAULT 0,
  TotalAmount   DECIMAL(18,2) NOT NULL DEFAULT 0,
  PaymentMethod NVARCHAR(50),
  PaymentStatus NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
  SampleStatus  INT           NOT NULL DEFAULT 0,
  BookingStatus NVARCHAR(30)  NOT NULL DEFAULT 'Booked',
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (PatientId) REFERENCES Patients(PatientId)
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BookingTests' AND xtype='U')
CREATE TABLE BookingTests (
  BookingTestId INT IDENTITY(1,1) PRIMARY KEY,
  BookingId     INT           NOT NULL,
  TestSuiteID   NVARCHAR(50),
  TestSuiteName NVARCHAR(200),
  Price         DECIMAL(18,2) NOT NULL DEFAULT 0,
  SampleType    NVARCHAR(100),
  TestCount     INT           NOT NULL DEFAULT 1,
  FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Reports' AND xtype='U')
CREATE TABLE Reports (
  ReportId       INT IDENTITY(1,1) PRIMARY KEY,
  BookingId      INT           NOT NULL DEFAULT 0,
  BookingRef     NVARCHAR(20),
  PatientId      INT           NOT NULL,
  TestNames      NVARCHAR(500),
  ReportFilePath NVARCHAR(500),
  Status         NVARCHAR(30)  NOT NULL DEFAULT 'Pending',
  ReportDate     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- 3. Seed LabTests (skip if already seeded)
IF NOT EXISTS (SELECT 1 FROM LabTests)
BEGIN
  INSERT INTO LabTests (TestSuiteID, TestSuiteName, ShortName, Price, SampleType, TestCount, ReportTime, Fasting, Category, Description, TestType, IsActive) VALUES
  ('TST001','Complete Blood Count (CBC)',              'CBC',     350,  'Blood',        24, '6 Hours',  'No',  'Haematology',   'Measures red blood cells, white blood cells, and platelets.',    'Test',    1),
  ('TST002','Lipid Profile',                           'Lipid',   650,  'Blood',         8, '12 Hours', 'Yes', 'Biochemistry',  'Measures cholesterol levels and triglycerides.',                 'Test',    1),
  ('TST003','Liver Function Test (LFT)',               'LFT',     750,  'Blood',        12, '12 Hours', 'Yes', 'Biochemistry',  'Assesses liver health and function.',                            'Test',    1),
  ('TST004','Kidney Function Test (KFT)',              'KFT',     700,  'Blood',        10, '6 Hours',  'No',  'Biochemistry',  'Evaluates kidney function.',                                     'Test',    1),
  ('TST005','Thyroid Profile (T3, T4, TSH)',           'Thyroid', 550,  'Blood',         3, '24 Hours', 'No',  'Endocrinology', 'Checks thyroid hormone levels.',                                 'Test',    1),
  ('TST006','HbA1c (Glycated Haemoglobin)',            'HbA1c',   450,  'Blood',         1, '6 Hours',  'No',  'Diabetes',      'Measures average blood glucose over 3 months.',                  'Test',    1),
  ('TST007','Urine Routine & Microscopy',             'Urine',   200,  'Urine',        18, '6 Hours',  'No',  'Urine',         'Complete urine examination.',                                    'Test',    1),
  ('TST008','Dengue NS1 Antigen',                     'Dengue',  600,  'Blood',         1, '6 Hours',  'No',  'Infection',     'Early dengue detection test.',                                   'Test',    1),
  ('TST009','Vitamin D (25-OH)',                       'Vit D',   1200, 'Blood',         1, '24 Hours', 'No',  'Vitamins',      'Measures Vitamin D levels in blood.',                            'Test',    1),
  ('TST010','Vitamin B12',                            'Vit B12', 850,  'Blood',         1, '24 Hours', 'No',  'Vitamins',      'Measures Vitamin B12 levels.',                                   'Test',    1),
  ('TST011','Iron Studies (Serum Iron, TIBC, Ferritin)','Iron',   900,  'Blood',         3, '12 Hours', 'No',  'Haematology',   'Evaluates iron storage and transport.',                          'Test',    1),
  ('TST012','Blood Glucose Fasting (FBS)',             'FBS',     150,  'Blood',         1, '6 Hours',  'Yes', 'Diabetes',      'Measures fasting blood glucose.',                                'Test',    1),
  ('TST013','C-Reactive Protein (CRP)',               'CRP',     350,  'Blood',         1, '6 Hours',  'No',  'Infection',     'Detects inflammation in the body.',                              'Test',    1),
  ('TST014','Allergy Screen Panel (20 Allergens)',     'Allergy', 2500, 'Blood',        20, '48 Hours', 'No',  'Allergy',       'Identifies common allergen sensitivities.',                      'Test',    1),
  ('TST015','Comprehensive Health Package',            'CHP',     3500, 'Blood, Urine', 65, '24 Hours', 'Yes', 'Packages',      'Full body health checkup with 65 parameters.',                   'Package', 1),
  ('TST016','Blood Glucose Post Prandial (PPBS)',      'PPBS',    150,  'Blood',         1, '6 Hours',  'No',  'Diabetes',      'Measures blood glucose 2 hours after meal.',                     'Test',    1),
  ('TST017','Serum Calcium',                          'Calcium', 280,  'Blood',         1, '6 Hours',  'No',  'Biochemistry',  'Measures calcium levels in blood.',                              'Test',    1),
  ('TST018','PSA (Prostate Specific Antigen)',         'PSA',     800,  'Blood',         1, '24 Hours', 'No',  'Oncology',      'Prostate cancer screening test.',                                'Test',    1),
  ('TST019','Dengue IgG & IgM Antibody',              'DengueAb',900,  'Blood',         2, '6 Hours',  'No',  'Infection',     'Detects dengue antibodies.',                                     'Test',    1),
  ('TST020','COVID-19 RT-PCR',                        'COVID',   500,  'Nasal Swab',    1, '24 Hours', 'No',  'Infection',     'Detects SARS-CoV-2 virus.',                                      'Test',    1);
END
GO

-- 4. Seed HealthPackages (skip if already seeded)
IF NOT EXISTS (SELECT 1 FROM HealthPackages)
BEGIN
  INSERT INTO HealthPackages (PackageCode, PackageName, Description, Price, OriginalPrice, TestCount, SampleType, ReportTime, Fasting, Badge, Includes, IsActive) VALUES
  ('PKG001','Basic Health Checkup','Essential screening for everyday wellness.',999,1500,15,'Blood, Urine','24 Hours','Yes','Popular','Complete Blood Count,Blood Glucose Fasting,Urine Routine,Lipid Profile,Liver Function Test',1),
  ('PKG002','Full Body Checkup','Comprehensive checkup covering major organ systems.',2499,3500,65,'Blood, Urine','24 Hours','Yes','Best Value','Complete Blood Count,Lipid Profile,Liver Function Test,Kidney Function Test,Thyroid Profile,HbA1c,Urine Routine,Vitamin D,Vitamin B12',1),
  ('PKG003','Diabetes Care Package','Focused monitoring for diabetes management.',799,1100,8,'Blood','12 Hours','Yes','','HbA1c,Blood Glucose Fasting,Blood Glucose Post Prandial,Lipid Profile,Kidney Function Test',1),
  ('PKG004','Women''s Wellness Package','Health screening tailored for women.',1899,2600,32,'Blood, Urine','24 Hours','Yes','New','Complete Blood Count,Thyroid Profile,Vitamin D,Vitamin B12,Iron Studies,Urine Routine',1),
  ('PKG005','Senior Citizen Package','Extensive checkup for ages 55 and above.',2999,4200,48,'Blood, Urine','24 Hours','Yes','Recommended','Complete Blood Count,Lipid Profile,Liver Function Test,Kidney Function Test,Thyroid Profile,HbA1c,PSA,Vitamin D,Vitamin B12,Urine Routine',1),
  ('PKG006','Fever Panel','Quick screening for common fever-causing infections.',1299,1800,5,'Blood','12 Hours','No','','CRP,Dengue NS1 Antigen,Dengue IgG & IgM Antibody,Complete Blood Count',1);
END
GO

-- ============================================================
-- 5. LIMS integration — SetupTestsuite -> LabTests mapping
-- ============================================================
-- The LIMS test master lives in table SetupTestsuite (one row per test
-- parameter). Field mapping agreed for the website catalogue:
--
--   Website field            LIMS source
--   ---------------------    ------------------------------------------
--   TestSuiteName            SetupTestsuite.TestSuitename (test group name)
--   TestCount                COUNT(TestName) per TestSuitename (parameters)
--   Price                    NOT in LIMS -> maintained in LabTests.Price
--   Description              NOT in LIMS -> maintained in LabTests.Description
--   TestType                 NOT in LIMS -> 'Test' | 'Package', set per row
--
-- When the LIMS database is linked (linked server or same instance),
-- uncomment and adjust the MERGE below to sync names + parameter counts
-- while preserving website-maintained Price/Description/TestType:
--
-- MERGE LabTests AS w
-- USING (
--   SELECT TestSuitename          AS TestSuiteName,
--          COUNT(TestName)        AS TestCount
--   FROM   LIMSDB.dbo.SetupTestsuite          -- adjust linked-server/db name
--   GROUP  BY TestSuitename
-- ) AS l
-- ON w.TestSuiteName = l.TestSuiteName
-- WHEN MATCHED THEN
--   UPDATE SET w.TestCount = l.TestCount
-- WHEN NOT MATCHED BY TARGET THEN
--   INSERT (TestSuiteID, TestSuiteName, TestCount, Price, TestType, IsActive)
--   VALUES ('LIMS-' + LEFT(l.TestSuiteName,40), l.TestSuiteName, l.TestCount, 0, 'Test', 0);
--   -- new LIMS suites arrive inactive (IsActive=0) with Price=0 so staff can
--   -- fill in price/description/type before they appear on the website.

PRINT 'PathlabDB setup complete. Tables created and LabTests + HealthPackages seeded.';
GO

-- ============================================================
-- Sample tracking upgrade (safe to re-run)
-- Adds portal Sample ID / barcode / LIMS job fields to Bookings
-- and the SampleStatusHistory timeline table.
-- ============================================================

IF COL_LENGTH('Bookings','SampleId') IS NULL
  ALTER TABLE Bookings ADD SampleId NVARCHAR(30) NULL;
IF COL_LENGTH('Bookings','Barcode') IS NULL
  ALTER TABLE Bookings ADD Barcode NVARCHAR(30) NULL;
IF COL_LENGTH('Bookings','LimsJobId') IS NULL
  ALTER TABLE Bookings ADD LimsJobId NVARCHAR(50) NULL;
IF COL_LENGTH('Bookings','LimsSyncStatus') IS NULL
  ALTER TABLE Bookings ADD LimsSyncStatus NVARCHAR(20) NULL;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SampleStatusHistory' AND xtype='U')
CREATE TABLE SampleStatusHistory (
  SampleEventId INT IDENTITY(1,1) PRIMARY KEY,
  BookingId     INT           NOT NULL,
  Status        INT           NOT NULL DEFAULT 0,
  StatusLabel   NVARCHAR(50),
  Source        NVARCHAR(30),
  Notes         NVARCHAR(300),
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);
GO

-- Backfill: give pre-upgrade bookings a Sample ID/barcode (derived from the
-- booking ref) and a "Booked" timeline entry so the portal timeline renders.
UPDATE Bookings
SET SampleId = 'SMP' + SUBSTRING(BookingRef, 3, 20),
    Barcode  = SUBSTRING(BookingRef, 3, 20),
    LimsSyncStatus = 'Pending'
WHERE SampleId IS NULL;
GO

INSERT INTO SampleStatusHistory (BookingId, Status, StatusLabel, Source, Notes, CreatedAt)
SELECT b.BookingId, 0, 'Booked', 'Portal', 'Backfilled for existing booking', b.CreatedAt
FROM Bookings b
WHERE NOT EXISTS (SELECT 1 FROM SampleStatusHistory h WHERE h.BookingId = b.BookingId);
GO

PRINT 'Sample tracking upgrade complete (Bookings columns + SampleStatusHistory).';
GO

-- ============================================================
-- Notification logging upgrade (safe to re-run)
-- Every SmsHelper.Send call (booking confirm, status update,
-- cancellation, reminder) writes one row here.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='NotificationLogs' AND xtype='U')
CREATE TABLE NotificationLogs (
  NotificationLogId INT IDENTITY(1,1) PRIMARY KEY,
  Phone         NVARCHAR(15),
  Channel       NVARCHAR(30)  NOT NULL DEFAULT 'SMS',
  Type          NVARCHAR(30),
  BookingRef    NVARCHAR(20),
  Message       NVARCHAR(500),
  Success       BIT           NOT NULL DEFAULT 0,
  ErrorDetail   NVARCHAR(300),
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

PRINT 'Notification logging upgrade complete (NotificationLogs table).';
GO

-- ============================================================
-- Audit trail upgrade (safe to re-run)
-- Append-only, hash-chained log. AuditHelper.Log() is the only
-- writer (INSERT only) — there is no UPDATE/DELETE path for this
-- table anywhere in the app, and PrevHash/Hash make tampering
-- detectable via VerifyAuditChain / AuditHelper.VerifyChain().
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLogs' AND xtype='U')
CREATE TABLE AuditLogs (
  AuditLogId     INT IDENTITY(1,1) PRIMARY KEY,
  Actor          NVARCHAR(100),
  ActorPatientId INT NULL,
  Action         NVARCHAR(50),
  EntityType     NVARCHAR(50),
  EntityRef      NVARCHAR(50),
  Detail         NVARCHAR(500),
  IPAddress      NVARCHAR(50),
  Success        BIT           NOT NULL DEFAULT 1,
  CreatedAt      DATETIME      NOT NULL DEFAULT GETDATE(),
  PrevHash       NVARCHAR(100),
  Hash           NVARCHAR(100)
);
GO

PRINT 'Audit trail upgrade complete (AuditLogs table).';
GO

-- ============================================================
-- Payment reconciliation upgrade (safe to re-run)
-- PaymentRef holds the gateway transaction id (Razorpay payment
-- id, or DEMO-* / front-desk receipts) so every online payment is
-- reconcilable; PaidAt is when the payment was confirmed. Written
-- by CreateBooking (gateway-paid bookings) and UpdatePaymentStatus
-- (counter settlement / gateway webhook).
-- ============================================================

IF COL_LENGTH('Bookings','PaymentRef') IS NULL
  ALTER TABLE Bookings ADD PaymentRef NVARCHAR(60) NULL;
IF COL_LENGTH('Bookings','PaidAt') IS NULL
  ALTER TABLE Bookings ADD PaidAt DATETIME NULL;
GO

-- Backfill: bookings already marked Paid predate PaymentRef capture —
-- stamp PaidAt from CreatedAt so finance reports have a paid date.
UPDATE Bookings SET PaidAt = CreatedAt
WHERE PaymentStatus = 'Paid' AND PaidAt IS NULL;
GO

PRINT 'Payment reconciliation upgrade complete (PaymentRef + PaidAt columns).';
GO

-- ============================================================
-- LIMS Phase 2 upgrade (safe to re-run)
-- Patient/catalogue sync tracking + server-generated GST invoices.
-- ============================================================

IF COL_LENGTH('Patients','LimsPatientId') IS NULL
  ALTER TABLE Patients ADD LimsPatientId NVARCHAR(50) NULL;
IF COL_LENGTH('Patients','LimsSyncStatus') IS NULL
  ALTER TABLE Patients ADD LimsSyncStatus NVARCHAR(20) NULL;
GO

IF COL_LENGTH('LabTests','LimsSyncedAt') IS NULL
  ALTER TABLE LabTests ADD LimsSyncedAt DATETIME NULL;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Invoices' AND xtype='U')
CREATE TABLE Invoices (
  InvoiceId     INT IDENTITY(1,1) PRIMARY KEY,
  BookingId     INT           NOT NULL,
  InvoiceNumber NVARCHAR(30)  NULL,
  Gstin         NVARCHAR(20),
  PlaceOfSupply NVARCHAR(100),
  HsnCode       NVARCHAR(10),
  Subtotal      DECIMAL(18,2) NOT NULL DEFAULT 0,
  GstAmount     DECIMAL(18,2) NOT NULL DEFAULT 0,
  TotalAmount   DECIMAL(18,2) NOT NULL DEFAULT 0,
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);
GO

-- Backfill: bookings already Paid before this upgrade get an invoice too,
-- numbered by financial year (Apr–Mar) same as MaybeGenerateInvoice does.
INSERT INTO Invoices (BookingId, Subtotal, GstAmount, TotalAmount, CreatedAt)
SELECT b.BookingId, b.Subtotal, b.GstAmount, b.TotalAmount, ISNULL(b.PaidAt, b.CreatedAt)
FROM Bookings b
WHERE b.PaymentStatus = 'Paid'
  AND NOT EXISTS (SELECT 1 FROM Invoices i WHERE i.BookingId = b.BookingId);
GO

UPDATE i
SET i.InvoiceNumber = 'INV/' +
  CASE WHEN MONTH(i.CreatedAt) >= 4
       THEN CAST(YEAR(i.CreatedAt) AS NVARCHAR) + '-' + RIGHT('0' + CAST((YEAR(i.CreatedAt)+1)%100 AS NVARCHAR),2)
       ELSE CAST(YEAR(i.CreatedAt)-1 AS NVARCHAR) + '-' + RIGHT('0' + CAST(YEAR(i.CreatedAt)%100 AS NVARCHAR),2)
  END + '/' + RIGHT('000000' + CAST(i.InvoiceId AS NVARCHAR), 6)
FROM Invoices i
WHERE i.InvoiceNumber IS NULL;
GO

PRINT 'LIMS Phase 2 upgrade complete (patient/catalogue sync columns + Invoices table).';
GO

-- ============================================================
-- Phase 3 upgrade (safe to re-run)
-- Family profiles, promo codes, refunds/partial payments.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FamilyMembers' AND xtype='U')
CREATE TABLE FamilyMembers (
  FamilyMemberId INT IDENTITY(1,1) PRIMARY KEY,
  PatientId     INT           NOT NULL,
  Name          NVARCHAR(150) NOT NULL,
  Relation      NVARCHAR(30),
  Gender        NVARCHAR(10),
  DateOfBirth   DATETIME,
  Phone         NVARCHAR(15),
  IsActive      BIT           NOT NULL DEFAULT 1,
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (PatientId) REFERENCES Patients(PatientId)
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PromoCodes' AND xtype='U')
CREATE TABLE PromoCodes (
  PromoCodeId   INT IDENTITY(1,1) PRIMARY KEY,
  Code          NVARCHAR(30)  NOT NULL UNIQUE,
  DiscountType  NVARCHAR(20)  NOT NULL DEFAULT 'Percent',  -- 'Percent' | 'Flat'
  DiscountValue DECIMAL(18,2) NOT NULL DEFAULT 0,
  MaxDiscount   DECIMAL(18,2) NULL,
  MinOrderValue DECIMAL(18,2) NOT NULL DEFAULT 0,
  ExpiryDate    DATETIME NULL,
  UsageLimit    INT NULL,
  UsedCount     INT           NOT NULL DEFAULT 0,
  IsActive      BIT           NOT NULL DEFAULT 1,
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- Seed a couple of starter codes (skip if any already exist)
IF NOT EXISTS (SELECT 1 FROM PromoCodes)
BEGIN
  INSERT INTO PromoCodes (Code, DiscountType, DiscountValue, MaxDiscount, MinOrderValue, ExpiryDate, UsageLimit, IsActive) VALUES
  ('WELCOME10', 'Percent', 10, 200, 300, NULL, NULL, 1),
  ('FLAT50',    'Flat',    50, NULL, 200, NULL, NULL, 1);
END
GO

IF COL_LENGTH('Bookings','FamilyMemberId') IS NULL
  ALTER TABLE Bookings ADD FamilyMemberId INT NULL;
IF COL_LENGTH('Bookings','FamilyMemberName') IS NULL
  ALTER TABLE Bookings ADD FamilyMemberName NVARCHAR(150) NULL;
IF COL_LENGTH('Bookings','PromoCode') IS NULL
  ALTER TABLE Bookings ADD PromoCode NVARCHAR(30) NULL;
IF COL_LENGTH('Bookings','DiscountAmount') IS NULL
  ALTER TABLE Bookings ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
IF COL_LENGTH('Bookings','AmountPaid') IS NULL
  ALTER TABLE Bookings ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0;
IF COL_LENGTH('Bookings','RefundAmount') IS NULL
  ALTER TABLE Bookings ADD RefundAmount DECIMAL(18,2) NULL;
IF COL_LENGTH('Bookings','RefundReason') IS NULL
  ALTER TABLE Bookings ADD RefundReason NVARCHAR(300) NULL;
IF COL_LENGTH('Bookings','RefundedAt') IS NULL
  ALTER TABLE Bookings ADD RefundedAt DATETIME NULL;
GO

-- Backfill: bookings already Paid before AmountPaid existed should read as
-- fully paid, not as owing their whole total.
UPDATE Bookings SET AmountPaid = TotalAmount WHERE PaymentStatus = 'Paid' AND AmountPaid = 0;
GO

PRINT 'Phase 3 upgrade complete (FamilyMembers + PromoCodes tables, Bookings discount/refund/partial-payment columns).';
GO

-- ============================================================
-- Phase 4 upgrade (safe to re-run)
-- Saved carts, test subscriptions, chain of custody, notification prefs.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SavedCarts' AND xtype='U')
CREATE TABLE SavedCarts (
  SavedCartId   INT IDENTITY(1,1) PRIMARY KEY,
  PatientId     INT           NOT NULL,
  CartJson      NVARCHAR(MAX),
  UpdatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (PatientId) REFERENCES Patients(PatientId)
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TestSubscriptions' AND xtype='U')
CREATE TABLE TestSubscriptions (
  TestSubscriptionId INT IDENTITY(1,1) PRIMARY KEY,
  PatientId     INT           NOT NULL,
  TestSuiteID   NVARCHAR(50),
  TestSuiteName NVARCHAR(200),
  FrequencyDays INT           NOT NULL DEFAULT 90,
  NextDueDate   DATETIME      NOT NULL,
  IsActive      BIT           NOT NULL DEFAULT 1,
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (PatientId) REFERENCES Patients(PatientId)
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CustodyEvents' AND xtype='U')
CREATE TABLE CustodyEvents (
  CustodyEventId INT IDENTITY(1,1) PRIMARY KEY,
  BookingId     INT           NOT NULL,
  HandlerName   NVARCHAR(100),
  HandlerRole   NVARCHAR(50),
  Action        NVARCHAR(30),
  Location      NVARCHAR(150),
  Notes         NVARCHAR(300),
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
  FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='NotificationPreferences' AND xtype='U')
CREATE TABLE NotificationPreferences (
  NotificationPreferenceId INT IDENTITY(1,1) PRIMARY KEY,
  PatientId     INT           NOT NULL,
  Channel       NVARCHAR(30),
  Type          NVARCHAR(30),
  Enabled       BIT           NOT NULL DEFAULT 1,
  FOREIGN KEY (PatientId) REFERENCES Patients(PatientId)
);
GO

PRINT 'Phase 4 upgrade complete (SavedCarts, TestSubscriptions, CustodyEvents, NotificationPreferences tables).';
GO

-- ============================================================
-- Feedback upgrade (safe to re-run)
-- Real, self-hosted complaint/feedback channel for the Portal
-- Help section — replaces linking out to the legacy Feedback.cshtml
-- page, whose form actually POSTs to a third party's API.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Feedbacks' AND xtype='U')
CREATE TABLE Feedbacks (
  FeedbackId    INT IDENTITY(1,1) PRIMARY KEY,
  PatientId     INT NULL,
  Name          NVARCHAR(150),
  Phone         NVARCHAR(15),
  BookingRef    NVARCHAR(20),
  Message       NVARCHAR(1000),
  Status        NVARCHAR(20)  NOT NULL DEFAULT 'Open',
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

PRINT 'Feedback upgrade complete (Feedbacks table).';
GO

-- ============================================================
-- Home Collection popup fix — the homepage lead-capture popup used to only
-- write to the browser's localStorage, so a lead was lost the moment the
-- visitor closed the tab and no one on staff ever saw it. Real server-side
-- record staff can follow up on (see Admin > Home Collection Leads).
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='HomeCollectionLeads' AND xtype='U')
CREATE TABLE HomeCollectionLeads (
  HomeCollectionLeadId INT IDENTITY(1,1) PRIMARY KEY,
  Name          NVARCHAR(150) NOT NULL,
  Mobile        NVARCHAR(15)  NOT NULL,
  City          NVARCHAR(100),
  Status        NVARCHAR(20)  NOT NULL DEFAULT 'New',
  CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

PRINT 'Home Collection popup upgrade complete (HomeCollectionLeads table).';
GO

-- ============================================================
-- Doctor share link fix — the "Share with doctor" link used to be built from
-- the plain, sequential BookingRef, so anyone who could guess a booking
-- reference could open someone else's report. ShareToken is a random opaque
-- value minted per-booking (GetOrCreateShareToken) that the shared link is
-- built from instead.
-- ============================================================

IF COL_LENGTH('Bookings','ShareToken') IS NULL
  ALTER TABLE Bookings ADD ShareToken NVARCHAR(64) NULL;
GO

PRINT 'Doctor share link upgrade complete (Bookings.ShareToken).';
GO
