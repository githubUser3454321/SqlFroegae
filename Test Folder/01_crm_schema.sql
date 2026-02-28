/*
Step 1: CRM Test-DB Schema (SQL Server)
- Creates schemas, tables, constraints, indexes, views and useful helper objects.
- Intentionally contains NO seed data.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'CrmTestDb') IS NULL
BEGIN
    CREATE DATABASE CrmTestDb;
END;
GO

USE CrmTestDb;
GO

-- 1) Schemas
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'crm') EXEC('CREATE SCHEMA crm AUTHORIZATION dbo;');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'sales') EXEC('CREATE SCHEMA sales AUTHORIZATION dbo;');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'support') EXEC('CREATE SCHEMA support AUTHORIZATION dbo;');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'marketing') EXEC('CREATE SCHEMA marketing AUTHORIZATION dbo;');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'audit') EXEC('CREATE SCHEMA audit AUTHORIZATION dbo;');
GO

-- 2) Core reference tables
CREATE TABLE crm.Country
(
    CountryCode CHAR(2) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    IsEu BIT NOT NULL CONSTRAINT DF_Country_IsEu DEFAULT (0),
    CONSTRAINT PK_Country PRIMARY KEY CLUSTERED (CountryCode)
);
GO

CREATE TABLE crm.Address
(
    AddressId BIGINT IDENTITY(1,1) NOT NULL,
    Street NVARCHAR(250) NOT NULL,
    PostalCode NVARCHAR(20) NOT NULL,
    City NVARCHAR(120) NOT NULL,
    StateProvince NVARCHAR(120) NULL,
    CountryCode CHAR(2) NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Address_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Address PRIMARY KEY CLUSTERED (AddressId),
    CONSTRAINT FK_Address_Country FOREIGN KEY (CountryCode) REFERENCES crm.Country(CountryCode)
);
GO

CREATE TABLE crm.Team
(
    TeamId INT IDENTITY(1,1) NOT NULL,
    TeamName NVARCHAR(120) NOT NULL,
    RegionCode NVARCHAR(20) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Team_IsActive DEFAULT (1),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Team_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Team PRIMARY KEY CLUSTERED (TeamId),
    CONSTRAINT UQ_Team_TeamName UNIQUE (TeamName)
);
GO

CREATE TABLE crm.AppUser
(
    UserId INT IDENTITY(1,1) NOT NULL,
    TeamId INT NULL,
    ManagerUserId INT NULL,
    ExternalEmployeeNo NVARCHAR(40) NULL,
    UserName NVARCHAR(80) NOT NULL,
    DisplayName NVARCHAR(120) NOT NULL,
    Email NVARCHAR(320) NOT NULL,
    RoleCode NVARCHAR(50) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT (1),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastLoginAt DATETIME2(3) NULL,
    CONSTRAINT PK_AppUser PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT UQ_AppUser_UserName UNIQUE (UserName),
    CONSTRAINT UQ_AppUser_Email UNIQUE (Email),
    CONSTRAINT CK_AppUser_Email CHECK (Email LIKE '%@%.%'),
    CONSTRAINT FK_AppUser_Team FOREIGN KEY (TeamId) REFERENCES crm.Team(TeamId),
    CONSTRAINT FK_AppUser_Manager FOREIGN KEY (ManagerUserId) REFERENCES crm.AppUser(UserId)
);
GO

-- 3) CRM entities
CREATE TABLE crm.Account
(
    AccountId BIGINT IDENTITY(1,1) NOT NULL,
    ParentAccountId BIGINT NULL,
    OwnerUserId INT NOT NULL,
    BillingAddressId BIGINT NULL,
    ShippingAddressId BIGINT NULL,
    AccountNumber NVARCHAR(40) NOT NULL,
    AccountName NVARCHAR(250) NOT NULL,
    IndustryCode NVARCHAR(80) NULL,
    AccountTier TINYINT NOT NULL CONSTRAINT DF_Account_AccountTier DEFAULT (3),
    AnnualRevenue DECIMAL(18,2) NULL,
    EmployeeCount INT NULL,
    IsKeyAccount BIT NOT NULL CONSTRAINT DF_Account_IsKeyAccount DEFAULT (0),
    StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Account_StatusCode DEFAULT (N'Active'),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Account_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Account_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    RowVersion ROWVERSION,
    CONSTRAINT PK_Account PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT UQ_Account_AccountNumber UNIQUE (AccountNumber),
    CONSTRAINT FK_Account_Parent FOREIGN KEY (ParentAccountId) REFERENCES crm.Account(AccountId),
    CONSTRAINT FK_Account_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT FK_Account_BillingAddress FOREIGN KEY (BillingAddressId) REFERENCES crm.Address(AddressId),
    CONSTRAINT FK_Account_ShippingAddress FOREIGN KEY (ShippingAddressId) REFERENCES crm.Address(AddressId),
    CONSTRAINT CK_Account_Status CHECK (StatusCode IN (N'Active', N'Prospect', N'Inactive', N'Blocked')),
    CONSTRAINT CK_Account_AccountTier CHECK (AccountTier BETWEEN 1 AND 5),
    CONSTRAINT CK_Account_Revenue CHECK (AnnualRevenue IS NULL OR AnnualRevenue >= 0)
);
GO

CREATE TABLE crm.Contact
(
    ContactId BIGINT IDENTITY(1,1) NOT NULL,
    AccountId BIGINT NOT NULL,
    OwnerUserId INT NOT NULL,
    AddressId BIGINT NULL,
    FirstName NVARCHAR(120) NOT NULL,
    LastName NVARCHAR(120) NOT NULL,
    FullName AS (CONCAT(FirstName, N' ', LastName)) PERSISTED,
    Email NVARCHAR(320) NULL,
    Phone NVARCHAR(40) NULL,
    JobTitle NVARCHAR(120) NULL,
    IsPrimary BIT NOT NULL CONSTRAINT DF_Contact_IsPrimary DEFAULT (0),
    ConsentEmail BIT NOT NULL CONSTRAINT DF_Contact_ConsentEmail DEFAULT (0),
    ConsentPhone BIT NOT NULL CONSTRAINT DF_Contact_ConsentPhone DEFAULT (0),
    BirthDate DATE NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Contact_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Contact_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Contact PRIMARY KEY CLUSTERED (ContactId),
    CONSTRAINT FK_Contact_Account FOREIGN KEY (AccountId) REFERENCES crm.Account(AccountId),
    CONSTRAINT FK_Contact_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT FK_Contact_Address FOREIGN KEY (AddressId) REFERENCES crm.Address(AddressId),
    CONSTRAINT CK_Contact_Email CHECK (Email IS NULL OR Email LIKE '%@%.%')
);
GO

CREATE TABLE sales.Lead
(
    LeadId BIGINT IDENTITY(1,1) NOT NULL,
    OwnerUserId INT NOT NULL,
    SourceCode NVARCHAR(50) NOT NULL,
    StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Lead_StatusCode DEFAULT (N'New'),
    CompanyName NVARCHAR(250) NOT NULL,
    FirstName NVARCHAR(120) NULL,
    LastName NVARCHAR(120) NULL,
    Email NVARCHAR(320) NULL,
    Phone NVARCHAR(40) NULL,
    Score INT NOT NULL CONSTRAINT DF_Lead_Score DEFAULT (0),
    EstimatedDealValue DECIMAL(18,2) NULL,
    QualifiedAt DATETIME2(3) NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Lead_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Lead PRIMARY KEY CLUSTERED (LeadId),
    CONSTRAINT FK_Lead_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT CK_Lead_Status CHECK (StatusCode IN (N'New', N'Working', N'Qualified', N'Unqualified', N'Converted')),
    CONSTRAINT CK_Lead_Score CHECK (Score BETWEEN 0 AND 100)
);
GO

CREATE TABLE sales.Opportunity
(
    OpportunityId BIGINT IDENTITY(1,1) NOT NULL,
    AccountId BIGINT NOT NULL,
    PrimaryContactId BIGINT NULL,
    OwnerUserId INT NOT NULL,
    CampaignId BIGINT NULL,
    Name NVARCHAR(250) NOT NULL,
    StageCode NVARCHAR(50) NOT NULL,
    ProbabilityPct TINYINT NOT NULL CONSTRAINT DF_Opportunity_Probability DEFAULT (10),
    EstimatedValue DECIMAL(18,2) NOT NULL,
    ForecastCategory NVARCHAR(30) NOT NULL CONSTRAINT DF_Opportunity_Forecast DEFAULT (N'Pipeline'),
    ExpectedCloseDate DATE NULL,
    ActualCloseDate DATE NULL,
    LostReason NVARCHAR(250) NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Opportunity_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Opportunity_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    RowVersion ROWVERSION,
    CONSTRAINT PK_Opportunity PRIMARY KEY CLUSTERED (OpportunityId),
    CONSTRAINT FK_Opportunity_Account FOREIGN KEY (AccountId) REFERENCES crm.Account(AccountId),
    CONSTRAINT FK_Opportunity_Contact FOREIGN KEY (PrimaryContactId) REFERENCES crm.Contact(ContactId),
    CONSTRAINT FK_Opportunity_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT CK_Opportunity_Stage CHECK (StageCode IN (N'Prospecting', N'Qualification', N'Proposal', N'Negotiation', N'ClosedWon', N'ClosedLost')),
    CONSTRAINT CK_Opportunity_Probability CHECK (ProbabilityPct BETWEEN 0 AND 100),
    CONSTRAINT CK_Opportunity_Value CHECK (EstimatedValue >= 0)
);
GO

CREATE TABLE sales.Product
(
    ProductId BIGINT IDENTITY(1,1) NOT NULL,
    ProductSku NVARCHAR(60) NOT NULL,
    ProductName NVARCHAR(250) NOT NULL,
    ProductFamily NVARCHAR(100) NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    IsSubscription BIT NOT NULL CONSTRAINT DF_Product_IsSubscription DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Product_IsActive DEFAULT (1),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Product_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Product PRIMARY KEY CLUSTERED (ProductId),
    CONSTRAINT UQ_Product_Sku UNIQUE (ProductSku),
    CONSTRAINT CK_Product_UnitPrice CHECK (UnitPrice >= 0)
);
GO

CREATE TABLE sales.OpportunityLine
(
    OpportunityLineId BIGINT IDENTITY(1,1) NOT NULL,
    OpportunityId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    Quantity DECIMAL(18,4) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    DiscountPct DECIMAL(5,2) NOT NULL CONSTRAINT DF_OpportunityLine_DiscountPct DEFAULT (0),
    LineAmount AS (ROUND(Quantity * UnitPrice * (1 - (DiscountPct / 100.0)), 2)) PERSISTED,
    CONSTRAINT PK_OpportunityLine PRIMARY KEY CLUSTERED (OpportunityLineId),
    CONSTRAINT FK_OpportunityLine_Opportunity FOREIGN KEY (OpportunityId) REFERENCES sales.Opportunity(OpportunityId),
    CONSTRAINT FK_OpportunityLine_Product FOREIGN KEY (ProductId) REFERENCES sales.Product(ProductId),
    CONSTRAINT CK_OpportunityLine_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_OpportunityLine_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT CK_OpportunityLine_Discount CHECK (DiscountPct BETWEEN 0 AND 100)
);
GO

CREATE TABLE marketing.Campaign
(
    CampaignId BIGINT IDENTITY(1,1) NOT NULL,
    OwnerUserId INT NOT NULL,
    CampaignCode NVARCHAR(40) NOT NULL,
    CampaignName NVARCHAR(250) NOT NULL,
    ChannelCode NVARCHAR(40) NOT NULL,
    Budget DECIMAL(18,2) NULL,
    StartDate DATE NULL,
    EndDate DATE NULL,
    StatusCode NVARCHAR(30) NOT NULL CONSTRAINT DF_Campaign_Status DEFAULT (N'Planned'),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Campaign_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Campaign PRIMARY KEY CLUSTERED (CampaignId),
    CONSTRAINT UQ_Campaign_CampaignCode UNIQUE (CampaignCode),
    CONSTRAINT FK_Campaign_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT CK_Campaign_Budget CHECK (Budget IS NULL OR Budget >= 0),
    CONSTRAINT CK_Campaign_DateRange CHECK (EndDate IS NULL OR StartDate IS NULL OR EndDate >= StartDate)
);
GO

ALTER TABLE sales.Opportunity
ADD CONSTRAINT FK_Opportunity_Campaign FOREIGN KEY (CampaignId) REFERENCES marketing.Campaign(CampaignId);
GO

CREATE TABLE support.Ticket
(
    TicketId BIGINT IDENTITY(1,1) NOT NULL,
    AccountId BIGINT NOT NULL,
    ContactId BIGINT NULL,
    OwnerUserId INT NOT NULL,
    SeverityCode NVARCHAR(20) NOT NULL,
    StatusCode NVARCHAR(20) NOT NULL,
    Subject NVARCHAR(250) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    OpenedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Ticket_OpenedAt DEFAULT (SYSUTCDATETIME()),
    ClosedAt DATETIME2(3) NULL,
    FirstResponseAt DATETIME2(3) NULL,
    SLAHours INT NULL,
    CONSTRAINT PK_Ticket PRIMARY KEY CLUSTERED (TicketId),
    CONSTRAINT FK_Ticket_Account FOREIGN KEY (AccountId) REFERENCES crm.Account(AccountId),
    CONSTRAINT FK_Ticket_Contact FOREIGN KEY (ContactId) REFERENCES crm.Contact(ContactId),
    CONSTRAINT FK_Ticket_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT CK_Ticket_Severity CHECK (SeverityCode IN (N'Low', N'Medium', N'High', N'Critical')),
    CONSTRAINT CK_Ticket_Status CHECK (StatusCode IN (N'Open', N'InProgress', N'Pending', N'Resolved', N'Closed'))
);
GO

CREATE TABLE crm.Activity
(
    ActivityId BIGINT IDENTITY(1,1) NOT NULL,
    AccountId BIGINT NULL,
    ContactId BIGINT NULL,
    OpportunityId BIGINT NULL,
    TicketId BIGINT NULL,
    OwnerUserId INT NOT NULL,
    ActivityType NVARCHAR(40) NOT NULL,
    Subject NVARCHAR(250) NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    DueAt DATETIME2(3) NULL,
    CompletedAt DATETIME2(3) NULL,
    PriorityCode NVARCHAR(20) NOT NULL CONSTRAINT DF_Activity_Priority DEFAULT (N'Medium'),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Activity_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Activity PRIMARY KEY CLUSTERED (ActivityId),
    CONSTRAINT FK_Activity_Account FOREIGN KEY (AccountId) REFERENCES crm.Account(AccountId),
    CONSTRAINT FK_Activity_Contact FOREIGN KEY (ContactId) REFERENCES crm.Contact(ContactId),
    CONSTRAINT FK_Activity_Opportunity FOREIGN KEY (OpportunityId) REFERENCES sales.Opportunity(OpportunityId),
    CONSTRAINT FK_Activity_Ticket FOREIGN KEY (TicketId) REFERENCES support.Ticket(TicketId),
    CONSTRAINT FK_Activity_Owner FOREIGN KEY (OwnerUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT CK_Activity_Type CHECK (ActivityType IN (N'Call', N'Email', N'Meeting', N'Task', N'Note')),
    CONSTRAINT CK_Activity_Priority CHECK (PriorityCode IN (N'Low', N'Medium', N'High'))
);
GO

CREATE TABLE sales.Quote
(
    QuoteId BIGINT IDENTITY(1,1) NOT NULL,
    OpportunityId BIGINT NOT NULL,
    QuoteNumber NVARCHAR(50) NOT NULL,
    ValidUntil DATE NULL,
    QuoteStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_Quote_Status DEFAULT (N'Draft'),
    CurrencyCode CHAR(3) NOT NULL CONSTRAINT DF_Quote_Currency DEFAULT ('EUR'),
    CreatedByUserId INT NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Quote_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Quote PRIMARY KEY CLUSTERED (QuoteId),
    CONSTRAINT UQ_Quote_QuoteNumber UNIQUE (QuoteNumber),
    CONSTRAINT FK_Quote_Opportunity FOREIGN KEY (OpportunityId) REFERENCES sales.Opportunity(OpportunityId),
    CONSTRAINT FK_Quote_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES crm.AppUser(UserId)
);
GO

CREATE TABLE sales.QuoteLine
(
    QuoteLineId BIGINT IDENTITY(1,1) NOT NULL,
    QuoteId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    Quantity DECIMAL(18,4) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    DiscountPct DECIMAL(5,2) NOT NULL CONSTRAINT DF_QuoteLine_Discount DEFAULT (0),
    LineAmount AS (ROUND(Quantity * UnitPrice * (1 - (DiscountPct / 100.0)), 2)) PERSISTED,
    CONSTRAINT PK_QuoteLine PRIMARY KEY CLUSTERED (QuoteLineId),
    CONSTRAINT FK_QuoteLine_Quote FOREIGN KEY (QuoteId) REFERENCES sales.Quote(QuoteId),
    CONSTRAINT FK_QuoteLine_Product FOREIGN KEY (ProductId) REFERENCES sales.Product(ProductId),
    CONSTRAINT CK_QuoteLine_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_QuoteLine_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT CK_QuoteLine_Discount CHECK (DiscountPct BETWEEN 0 AND 100)
);
GO

CREATE TABLE audit.ChangeLog
(
    ChangeLogId BIGINT IDENTITY(1,1) NOT NULL,
    EntityName NVARCHAR(128) NOT NULL,
    EntityId NVARCHAR(128) NOT NULL,
    ChangeType NVARCHAR(20) NOT NULL,
    ChangedByUserId INT NULL,
    ChangedAt DATETIME2(3) NOT NULL CONSTRAINT DF_ChangeLog_ChangedAt DEFAULT (SYSUTCDATETIME()),
    PayloadJson NVARCHAR(MAX) NULL,
    CONSTRAINT PK_ChangeLog PRIMARY KEY CLUSTERED (ChangeLogId),
    CONSTRAINT FK_ChangeLog_User FOREIGN KEY (ChangedByUserId) REFERENCES crm.AppUser(UserId),
    CONSTRAINT CK_ChangeLog_ChangeType CHECK (ChangeType IN (N'INSERT', N'UPDATE', N'DELETE'))
);
GO

-- 4) Performance-focused indexes
CREATE INDEX IX_Account_Owner_Status ON crm.Account (OwnerUserId, StatusCode) INCLUDE (AccountName, IndustryCode, AnnualRevenue);
CREATE INDEX IX_Contact_Account_Primary ON crm.Contact (AccountId, IsPrimary) INCLUDE (FullName, Email, Phone);
CREATE INDEX IX_Lead_Status_Score ON sales.Lead (StatusCode, Score DESC) INCLUDE (OwnerUserId, CompanyName, EstimatedDealValue);
CREATE INDEX IX_Opportunity_Stage_CloseDate ON sales.Opportunity (StageCode, ExpectedCloseDate) INCLUDE (OwnerUserId, EstimatedValue, ProbabilityPct);
CREATE INDEX IX_Opportunity_Account_Owner ON sales.Opportunity (AccountId, OwnerUserId) INCLUDE (StageCode, EstimatedValue);
CREATE INDEX IX_OpportunityLine_Opportunity ON sales.OpportunityLine (OpportunityId) INCLUDE (LineAmount, Quantity, UnitPrice);
CREATE INDEX IX_Campaign_Status_Date ON marketing.Campaign (StatusCode, StartDate, EndDate) INCLUDE (CampaignName, ChannelCode, Budget);
CREATE INDEX IX_Ticket_Status_Severity ON support.Ticket (StatusCode, SeverityCode) INCLUDE (AccountId, OwnerUserId, OpenedAt, ClosedAt);
CREATE INDEX IX_Activity_Owner_Due ON crm.Activity (OwnerUserId, DueAt) INCLUDE (ActivityType, PriorityCode, AccountId, OpportunityId);
GO

-- 5) Analytics views
CREATE OR ALTER VIEW sales.vwOpportunityPipeline
AS
SELECT
    o.OpportunityId,
    o.Name AS OpportunityName,
    o.StageCode,
    o.ProbabilityPct,
    o.EstimatedValue,
    ExpectedWeightedValue = (o.EstimatedValue * o.ProbabilityPct / 100.0),
    o.ExpectedCloseDate,
    a.AccountId,
    a.AccountName,
    ownerUser.DisplayName AS OwnerName,
    c.CampaignName,
    RevenueBucket = CASE
        WHEN o.EstimatedValue < 5000 THEN N'Small'
        WHEN o.EstimatedValue < 50000 THEN N'Mid'
        ELSE N'Enterprise'
    END
FROM sales.Opportunity o
INNER JOIN crm.Account a ON a.AccountId = o.AccountId
INNER JOIN crm.AppUser ownerUser ON ownerUser.UserId = o.OwnerUserId
LEFT JOIN marketing.Campaign c ON c.CampaignId = o.CampaignId;
GO

CREATE OR ALTER VIEW support.vwTicketSlaSnapshot
AS
SELECT
    t.TicketId,
    t.Subject,
    t.StatusCode,
    t.SeverityCode,
    t.OpenedAt,
    t.ClosedAt,
    t.SLAHours,
    ElapsedHours = DATEDIFF(HOUR, t.OpenedAt, COALESCE(t.ClosedAt, SYSUTCDATETIME())),
    IsSlaBreached = CASE
        WHEN t.SLAHours IS NULL THEN NULL
        WHEN DATEDIFF(HOUR, t.OpenedAt, COALESCE(t.ClosedAt, SYSUTCDATETIME())) > t.SLAHours THEN 1
        ELSE 0
    END,
    a.AccountName,
    ownerUser.DisplayName AS OwnerName
FROM support.Ticket t
INNER JOIN crm.Account a ON a.AccountId = t.AccountId
INNER JOIN crm.AppUser ownerUser ON ownerUser.UserId = t.OwnerUserId;
GO

PRINT 'CRM schema creation finished successfully.';
