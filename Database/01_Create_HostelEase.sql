/* =====================================================================
   HostelEase - database creation script (SQL Server 2019+ / Express)
   Run in SSMS while connected to  .\SQLEXPRESS
   Safe to run more than once: nothing is dropped, existing objects are kept.
   ===================================================================== */
IF DB_ID(N'HostelEase') IS NULL
    CREATE DATABASE HostelEase;
GO
USE HostelEase;
GO

/* ---------- Upgrade from the earlier starter version ----------
   The first starter used a different table layout (no Users/Blocks tables, Rooms.Occupied column...).
   If it is detected, those old tables are RENAMED to *_Legacy (nothing is deleted) and the new
   structure is created below. */
IF OBJECT_ID(N'dbo.Students', N'U') IS NOT NULL AND COL_LENGTH('dbo.Students', 'UserID') IS NULL
   AND OBJECT_ID(N'dbo.Students_Legacy', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.Complaints', N'U') IS NOT NULL          EXEC sp_rename 'dbo.Complaints', 'Complaints_Legacy';
    IF OBJECT_ID(N'dbo.Allocations', N'U') IS NOT NULL         EXEC sp_rename 'dbo.Allocations', 'Allocations_Legacy';
    IF OBJECT_ID(N'dbo.AllocationRequests', N'U') IS NOT NULL  EXEC sp_rename 'dbo.AllocationRequests', 'AllocationRequests_Legacy';
    IF OBJECT_ID(N'dbo.Rooms', N'U') IS NOT NULL               EXEC sp_rename 'dbo.Rooms', 'Rooms_Legacy';
    IF OBJECT_ID(N'dbo.Hostels', N'U') IS NOT NULL             EXEC sp_rename 'dbo.Hostels', 'Hostels_Legacy';
    EXEC sp_rename 'dbo.Students', 'Students_Legacy';
END
GO

/* ---------- Users: login + role ---------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserID        INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    Email         NVARCHAR(150) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
    PasswordHash  NVARCHAR(300) NOT NULL,
    Role          NVARCHAR(20)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin','Student')),
    IsActive      BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CreatedAt     DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIME()
);
GO

/* ---------- Students ---------- */
IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
CREATE TABLE dbo.Students (
    StudentID      INT IDENTITY(1,1) CONSTRAINT PK_Students PRIMARY KEY,
    UserID         INT NOT NULL,
    FullName       NVARCHAR(100) NOT NULL,
    StudentNumber  NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Students_Number UNIQUE,
    Course         NVARCHAR(100) NOT NULL,
    Semester       INT NOT NULL CONSTRAINT CK_Students_Semester CHECK (Semester BETWEEN 1 AND 12),
    Contact        NVARCHAR(20)  NULL,
    IsActive       BIT NOT NULL CONSTRAINT DF_Students_IsActive DEFAULT 1,
    CreatedAt      DATETIME2 NOT NULL CONSTRAINT DF_Students_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Students_User UNIQUE (UserID),
    CONSTRAINT FK_Students_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID)
);
GO

/* ---------- Hostels ---------- */
IF OBJECT_ID(N'dbo.Hostels', N'U') IS NULL
CREATE TABLE dbo.Hostels (
    HostelID   INT IDENTITY(1,1) CONSTRAINT PK_Hostels PRIMARY KEY,
    Name       NVARCHAR(100) NOT NULL CONSTRAINT UQ_Hostels_Name UNIQUE,
    Location   NVARCHAR(200) NOT NULL,
    Type       NVARCHAR(20)  NOT NULL CONSTRAINT CK_Hostels_Type CHECK (Type IN ('Boys','Girls','Co-ed')),
    IsActive   BIT NOT NULL CONSTRAINT DF_Hostels_IsActive DEFAULT 1
);
GO

/* ---------- Blocks ---------- */
IF OBJECT_ID(N'dbo.Blocks', N'U') IS NULL
CREATE TABLE dbo.Blocks (
    BlockID    INT IDENTITY(1,1) CONSTRAINT PK_Blocks PRIMARY KEY,
    HostelID   INT NOT NULL CONSTRAINT FK_Blocks_Hostels REFERENCES dbo.Hostels(HostelID),
    BlockName  NVARCHAR(50) NOT NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Blocks_IsActive DEFAULT 1,
    CONSTRAINT UQ_Blocks_Hostel_Name UNIQUE (HostelID, BlockName)
);
GO

/* ---------- Rooms ---------- */
IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
CREATE TABLE dbo.Rooms (
    RoomID      INT IDENTITY(1,1) CONSTRAINT PK_Rooms PRIMARY KEY,
    BlockID     INT NOT NULL CONSTRAINT FK_Rooms_Blocks REFERENCES dbo.Blocks(BlockID),
    RoomNumber  NVARCHAR(20) NOT NULL,
    Capacity    INT NOT NULL CONSTRAINT CK_Rooms_Capacity CHECK (Capacity BETWEEN 1 AND 20),
    Status      NVARCHAR(20) NOT NULL CONSTRAINT DF_Rooms_Status DEFAULT 'Available'
                    CONSTRAINT CK_Rooms_Status CHECK (Status IN ('Available','Maintenance','Inactive')),
    CONSTRAINT UQ_Rooms_Block_Number UNIQUE (BlockID, RoomNumber)
);
GO

/* ---------- AllocationRequests ---------- */
IF OBJECT_ID(N'dbo.AllocationRequests', N'U') IS NULL
CREATE TABLE dbo.AllocationRequests (
    RequestID          INT IDENTITY(1,1) CONSTRAINT PK_AllocationRequests PRIMARY KEY,
    StudentID          INT NOT NULL CONSTRAINT FK_Requests_Students REFERENCES dbo.Students(StudentID),
    PreferredHostelID  INT NULL CONSTRAINT FK_Requests_Hostels REFERENCES dbo.Hostels(HostelID),
    PreferredRoomID    INT NULL CONSTRAINT FK_Requests_Rooms REFERENCES dbo.Rooms(RoomID),
    RequestDate        DATETIME2 NOT NULL CONSTRAINT DF_Requests_Date DEFAULT SYSDATETIME(),
    Status             NVARCHAR(20) NOT NULL CONSTRAINT DF_Requests_Status DEFAULT 'Pending'
                           CONSTRAINT CK_Requests_Status CHECK (Status IN ('Pending','Approved','Rejected','Cancelled')),
    AdminRemarks       NVARCHAR(500) NULL,
    ReviewedDate       DATETIME2 NULL
);
GO

/* ---------- Allocations (approved room assignments) ---------- */
IF OBJECT_ID(N'dbo.Allocations', N'U') IS NULL
CREATE TABLE dbo.Allocations (
    AllocationID    INT IDENTITY(1,1) CONSTRAINT PK_Allocations PRIMARY KEY,
    StudentID       INT NOT NULL CONSTRAINT FK_Allocations_Students REFERENCES dbo.Students(StudentID),
    RoomID          INT NOT NULL CONSTRAINT FK_Allocations_Rooms REFERENCES dbo.Rooms(RoomID),
    RequestID       INT NULL CONSTRAINT FK_Allocations_Requests REFERENCES dbo.AllocationRequests(RequestID),
    AllocationDate  DATE NOT NULL CONSTRAINT DF_Allocations_Date DEFAULT CAST(GETDATE() AS DATE),
    EndDate         DATE NULL,
    Status          NVARCHAR(20) NOT NULL CONSTRAINT DF_Allocations_Status DEFAULT 'Active'
                        CONSTRAINT CK_Allocations_Status CHECK (Status IN ('Active','Ended'))
);
GO

/* ---------- Complaints ---------- */
IF OBJECT_ID(N'dbo.Complaints', N'U') IS NULL
CREATE TABLE dbo.Complaints (
    ComplaintID       INT IDENTITY(1,1) CONSTRAINT PK_Complaints PRIMARY KEY,
    StudentID         INT NOT NULL CONSTRAINT FK_Complaints_Students REFERENCES dbo.Students(StudentID),
    Category          NVARCHAR(50)  NOT NULL,
    Description       NVARCHAR(1000) NOT NULL,
    DateSubmitted     DATETIME2 NOT NULL CONSTRAINT DF_Complaints_Date DEFAULT SYSDATETIME(),
    ResolutionStatus  NVARCHAR(20) NOT NULL CONSTRAINT DF_Complaints_Status DEFAULT 'Open'
                          CONSTRAINT CK_Complaints_Status CHECK (ResolutionStatus IN ('Open','In Progress','Resolved')),
    AdminResponse     NVARCHAR(1000) NULL
);
GO

/* ---------- Indexes + business-rule guards ---------- */
-- A student can hold only ONE active room, and have only ONE pending request.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Allocations_ActiveStudent')
    CREATE UNIQUE INDEX UX_Allocations_ActiveStudent ON dbo.Allocations(StudentID) WHERE Status = 'Active';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Requests_PendingStudent')
    CREATE UNIQUE INDEX UX_Requests_PendingStudent ON dbo.AllocationRequests(StudentID) WHERE Status = 'Pending';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Allocations_Room_Status')
    CREATE INDEX IX_Allocations_Room_Status ON dbo.Allocations(RoomID, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Rooms_Block')
    CREATE INDEX IX_Rooms_Block ON dbo.Rooms(BlockID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Requests_Status')
    CREATE INDEX IX_Requests_Status ON dbo.AllocationRequests(Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Students_FullName')
    CREATE INDEX IX_Students_FullName ON dbo.Students(FullName);
GO

/* ---------- Sample data (only when Hostels is empty) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Hostels)
BEGIN
    INSERT INTO dbo.Hostels (Name, Location, Type) VALUES
        (N'Aryabhatta Boys Hostel', N'North Campus', 'Boys'),
        (N'Sarojini Girls Hostel',  N'South Campus', 'Girls'),
        (N'Global Residency',       N'Main Campus',  'Co-ed');

    INSERT INTO dbo.Blocks (HostelID, BlockName)
    SELECT h.HostelID, v.BlockName
    FROM dbo.Hostels h CROSS JOIN (VALUES (N'Block A'), (N'Block B')) v(BlockName);

    INSERT INTO dbo.Rooms (BlockID, RoomNumber, Capacity)
    SELECT b.BlockID, v.RoomNumber, v.Capacity
    FROM dbo.Blocks b
    CROSS JOIN (VALUES (N'101',2),(N'102',2),(N'103',3),(N'201',3),(N'202',4),(N'203',4)) v(RoomNumber, Capacity);
END
GO

/* NOTE: the default admin and demo student accounts are created automatically by the
   application the first time it starts (passwords are hashed in C#, not in SQL). */
PRINT 'HostelEase database is ready.';
