-- ============================================================
-- Portfolio Website (Meghana Chandran) - MS SQL Server Schema & Stored Procedures
-- Compatible with SQL Server 2016+ & Azure SQL Database
-- ============================================================

-- 1. Admin Users
IF OBJECT_ID(N'dbo.AdminUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers (
        AdminUserId   INT IDENTITY(1,1) PRIMARY KEY,
        FullName      NVARCHAR(100)  NOT NULL,
        Email         NVARCHAR(150)  NOT NULL CONSTRAINT UQ_AdminUsers_Email UNIQUE,
        PasswordHash  NVARCHAR(255)  NOT NULL,
        Role          NVARCHAR(50)   NOT NULL CONSTRAINT DF_AdminUsers_Role DEFAULT N'Admin',
        IsActive      BIT            NOT NULL CONSTRAINT DF_AdminUsers_IsActive DEFAULT 1,
        LastLoginAt   DATETIME2      NULL,
        CreatedAt     DATETIME2      NOT NULL CONSTRAINT DF_AdminUsers_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 2. Site Profile / About Content
IF OBJECT_ID(N'dbo.SiteProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SiteProfile (
        ProfileId        INT IDENTITY(1,1) PRIMARY KEY,
        FullName         NVARCHAR(100)  NOT NULL,
        Headline         NVARCHAR(200)  NOT NULL,
        AboutSummary     NVARCHAR(500)  NOT NULL,
        AboutBio         NVARCHAR(MAX)  NOT NULL,
        ProfileImageUrl  NVARCHAR(500)  NULL,
        ResumeUrl        NVARCHAR(500)  NULL,
        Email            NVARCHAR(150)  NOT NULL,
        Phone            NVARCHAR(30)   NULL,
        Location         NVARCHAR(100)  NULL,
        GitHubUrl        NVARCHAR(300)  NULL,
        LinkedInUrl      NVARCHAR(300)  NULL,
        UpdatedAt        DATETIME2      NOT NULL CONSTRAINT DF_SiteProfile_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 3. Services
IF OBJECT_ID(N'dbo.Services', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Services (
        ServiceId     INT IDENTITY(1,1) PRIMARY KEY,
        Title         NVARCHAR(100)  NOT NULL,
        Description   NVARCHAR(500)  NOT NULL,
        IconClass     NVARCHAR(100)  NULL,
        DisplayOrder  INT            NOT NULL CONSTRAINT DF_Services_DisplayOrder DEFAULT 0,
        IsActive      BIT            NOT NULL CONSTRAINT DF_Services_IsActive DEFAULT 1,
        CreatedAt     DATETIME2      NOT NULL CONSTRAINT DF_Services_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 4. Portfolio Categories
IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        CategoryId    INT IDENTITY(1,1) PRIMARY KEY,
        Name          NVARCHAR(80)   NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE,
        Slug          NVARCHAR(100)  NOT NULL CONSTRAINT UQ_Categories_Slug UNIQUE,
        DisplayOrder  INT            NOT NULL CONSTRAINT DF_Categories_DisplayOrder DEFAULT 0,
        CreatedAt     DATETIME2      NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 5. Portfolio Projects & Case Studies
IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Projects (
        ProjectId          INT IDENTITY(1,1) PRIMARY KEY,
        CategoryId         INT            NOT NULL,
        Title              NVARCHAR(150)  NOT NULL,
        Slug               NVARCHAR(180)  NOT NULL CONSTRAINT UQ_Projects_Slug UNIQUE,
        ShortDescription   NVARCHAR(400)  NOT NULL,
        ThumbnailUrl       NVARCHAR(500)  NOT NULL,
        ClientName         NVARCHAR(120)  NULL,
        ProjectDate        DATE           NULL,
        TechStack          NVARCHAR(300)  NULL,
        LiveUrl            NVARCHAR(500)  NULL,
        RepoUrl            NVARCHAR(500)  NULL,
        CaseStudyChallenge NVARCHAR(MAX)  NULL,
        CaseStudySolution  NVARCHAR(MAX)  NULL,
        CaseStudyOutcome   NVARCHAR(MAX)  NULL,
        IsPublished        BIT            NOT NULL CONSTRAINT DF_Projects_IsPublished DEFAULT 0,
        IsFeatured         BIT            NOT NULL CONSTRAINT DF_Projects_IsFeatured DEFAULT 0,
        DisplayOrder       INT            NOT NULL CONSTRAINT DF_Projects_DisplayOrder DEFAULT 0,
        CreatedBy          INT            NULL,
        CreatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Projects_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Projects_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_Projects_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
        CONSTRAINT FK_Projects_AdminUsers FOREIGN KEY (CreatedBy) REFERENCES dbo.AdminUsers(AdminUserId)
    );
END
GO

-- 6. Case Study Gallery Images
IF OBJECT_ID(N'dbo.ProjectImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProjectImages (
        ImageId       INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId     INT            NOT NULL,
        ImageUrl      NVARCHAR(500)  NOT NULL,
        Caption       NVARCHAR(200)  NULL,
        DisplayOrder  INT            NOT NULL CONSTRAINT DF_ProjectImages_DisplayOrder DEFAULT 0,
        UploadedAt    DATETIME2      NOT NULL CONSTRAINT DF_ProjectImages_UploadedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_ProjectImages_Projects FOREIGN KEY (ProjectId)
            REFERENCES dbo.Projects(ProjectId) ON DELETE CASCADE
    );
END
GO

-- 7. Contact Form Submissions
IF OBJECT_ID(N'dbo.ContactMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContactMessages (
        MessageId     INT IDENTITY(1,1) PRIMARY KEY,
        SenderName    NVARCHAR(100)  NOT NULL,
        SenderEmail   NVARCHAR(150)  NOT NULL,
        Subject       NVARCHAR(200)  NOT NULL,
        MessageBody   NVARCHAR(MAX)  NOT NULL,
        IsRead        BIT            NOT NULL CONSTRAINT DF_ContactMessages_IsRead DEFAULT 0,
        SubmittedAt   DATETIME2      NOT NULL CONSTRAINT DF_ContactMessages_SubmittedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Published_Order' AND object_id = OBJECT_ID(N'dbo.Projects'))
    CREATE INDEX IX_Projects_Published_Order ON dbo.Projects (IsPublished, IsFeatured DESC, DisplayOrder ASC, CreatedAt DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_CategoryId' AND object_id = OBJECT_ID(N'dbo.Projects'))
    CREATE INDEX IX_Projects_CategoryId ON dbo.Projects (CategoryId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectImages_ProjectId' AND object_id = OBJECT_ID(N'dbo.ProjectImages'))
    CREATE INDEX IX_ProjectImages_ProjectId ON dbo.ProjectImages (ProjectId, DisplayOrder ASC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContactMessages_Read_Date' AND object_id = OBJECT_ID(N'dbo.ContactMessages'))
    CREATE INDEX IX_ContactMessages_Read_Date ON dbo.ContactMessages (IsRead, SubmittedAt DESC);
GO

-- ============================================================
-- Stored Procedures
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.sp_GetLandingPageData
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        ProfileId, FullName, Headline, AboutSummary, AboutBio,
        ProfileImageUrl, ResumeUrl, Email, Phone, Location, GitHubUrl, LinkedInUrl
    FROM dbo.SiteProfile
    ORDER BY ProfileId ASC;

    SELECT ServiceId, Title, Description, IconClass, DisplayOrder
    FROM dbo.Services
    WHERE IsActive = 1
    ORDER BY DisplayOrder ASC, ServiceId ASC;

    SELECT CategoryId, Name, Slug, DisplayOrder
    FROM dbo.Categories
    ORDER BY DisplayOrder ASC, Name ASC;

    SELECT
        p.ProjectId, p.Title, p.Slug, p.ShortDescription, p.ThumbnailUrl,
        p.TechStack, p.IsFeatured, p.ProjectDate,
        c.CategoryId, c.Name AS CategoryName, c.Slug AS CategorySlug
    FROM dbo.Projects p
    INNER JOIN dbo.Categories c ON p.CategoryId = c.CategoryId
    WHERE p.IsPublished = 1
    ORDER BY p.IsFeatured DESC, p.DisplayOrder ASC, p.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetPublishedProjects
    @CategorySlug NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.ProjectId, p.Title, p.Slug, p.ShortDescription, p.ThumbnailUrl,
        p.TechStack, p.IsFeatured, p.ProjectDate,
        c.CategoryId, c.Name AS CategoryName, c.Slug AS CategorySlug
    FROM dbo.Projects p
    INNER JOIN dbo.Categories c ON p.CategoryId = c.CategoryId
    WHERE p.IsPublished = 1
      AND (@CategorySlug IS NULL OR c.Slug = @CategorySlug)
    ORDER BY p.IsFeatured DESC, p.DisplayOrder ASC, p.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetProjectCaseStudyBySlug
    @Slug NVARCHAR(180),
    @IncludeUnpublished BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProjectId INT;

    SELECT @ProjectId = p.ProjectId
    FROM dbo.Projects p
    WHERE p.Slug = @Slug
      AND (@IncludeUnpublished = 1 OR p.IsPublished = 1);

    SELECT
        p.ProjectId, p.CategoryId, c.Name AS CategoryName, c.Slug AS CategorySlug,
        p.Title, p.Slug, p.ShortDescription, p.ThumbnailUrl,
        p.ClientName, p.ProjectDate, p.TechStack, p.LiveUrl, p.RepoUrl,
        p.CaseStudyChallenge, p.CaseStudySolution, p.CaseStudyOutcome,
        p.IsPublished, p.IsFeatured, p.CreatedAt, p.UpdatedAt
    FROM dbo.Projects p
    INNER JOIN dbo.Categories c ON p.CategoryId = c.CategoryId
    WHERE p.ProjectId = @ProjectId;

    SELECT ImageId, ImageUrl, Caption, DisplayOrder
    FROM dbo.ProjectImages
    WHERE ProjectId = @ProjectId
    ORDER BY DisplayOrder ASC, ImageId ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_UpsertProject
    @ProjectId          INT = NULL,
    @CategoryId         INT,
    @Title              NVARCHAR(150),
    @Slug               NVARCHAR(180),
    @ShortDescription   NVARCHAR(400),
    @ThumbnailUrl       NVARCHAR(500),
    @ClientName         NVARCHAR(120) = NULL,
    @ProjectDate        DATE = NULL,
    @TechStack          NVARCHAR(300) = NULL,
    @LiveUrl            NVARCHAR(500) = NULL,
    @RepoUrl            NVARCHAR(500) = NULL,
    @CaseStudyChallenge NVARCHAR(MAX) = NULL,
    @CaseStudySolution  NVARCHAR(MAX) = NULL,
    @CaseStudyOutcome   NVARCHAR(MAX) = NULL,
    @IsPublished        BIT = 0,
    @IsFeatured         BIT = 0,
    @DisplayOrder       INT = 0,
    @CreatedBy          INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @ProjectId IS NULL OR @ProjectId = 0
    BEGIN
        INSERT INTO dbo.Projects (
            CategoryId, Title, Slug, ShortDescription, ThumbnailUrl,
            ClientName, ProjectDate, TechStack, LiveUrl, RepoUrl,
            CaseStudyChallenge, CaseStudySolution, CaseStudyOutcome,
            IsPublished, IsFeatured, DisplayOrder, CreatedBy
        )
        VALUES (
            @CategoryId, @Title, @Slug, @ShortDescription, @ThumbnailUrl,
            @ClientName, @ProjectDate, @TechStack, @LiveUrl, @RepoUrl,
            @CaseStudyChallenge, @CaseStudySolution, @CaseStudyOutcome,
            @IsPublished, @IsFeatured, @DisplayOrder, @CreatedBy
        );

        SET @ProjectId = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.Projects
        SET
            CategoryId         = @CategoryId,
            Title              = @Title,
            Slug               = @Slug,
            ShortDescription   = @ShortDescription,
            ThumbnailUrl       = @ThumbnailUrl,
            ClientName         = @ClientName,
            ProjectDate        = @ProjectDate,
            TechStack          = @TechStack,
            LiveUrl            = @LiveUrl,
            RepoUrl            = @RepoUrl,
            CaseStudyChallenge = @CaseStudyChallenge,
            CaseStudySolution  = @CaseStudySolution,
            CaseStudyOutcome   = @CaseStudyOutcome,
            IsPublished        = @IsPublished,
            IsFeatured         = @IsFeatured,
            DisplayOrder       = @DisplayOrder,
            UpdatedAt          = SYSUTCDATETIME()
        WHERE ProjectId = @ProjectId;
    END

    SELECT @ProjectId AS ProjectId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ToggleProjectPublish
    @ProjectId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Projects
    SET IsPublished = CASE WHEN IsPublished = 1 THEN 0 ELSE 1 END,
        UpdatedAt   = SYSUTCDATETIME()
    OUTPUT INSERTED.ProjectId, INSERTED.IsPublished
    WHERE ProjectId = @ProjectId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_DeleteProject
    @ProjectId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FilesToDelete TABLE (FileUrl NVARCHAR(500));

    INSERT INTO @FilesToDelete (FileUrl)
    SELECT ThumbnailUrl FROM dbo.Projects WHERE ProjectId = @ProjectId
    UNION ALL
    SELECT ImageUrl FROM dbo.ProjectImages WHERE ProjectId = @ProjectId;

    DELETE FROM dbo.Projects WHERE ProjectId = @ProjectId;

    SELECT FileUrl FROM @FilesToDelete WHERE FileUrl IS NOT NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetAdminDashboardStats
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COUNT(*) AS TotalProjects,
        ISNULL(SUM(CASE WHEN IsPublished = 1 THEN 1 ELSE 0 END), 0) AS PublishedProjects,
        ISNULL(SUM(CASE WHEN IsPublished = 0 THEN 1 ELSE 0 END), 0) AS DraftProjects,
        (SELECT COUNT(*) FROM dbo.Categories) AS TotalCategories,
        (SELECT COUNT(*) FROM dbo.Services WHERE IsActive = 1) AS ActiveServices,
        (SELECT COUNT(*) FROM dbo.ContactMessages WHERE IsRead = 0) AS UnreadMessages
    FROM dbo.Projects;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_SubmitContactMessage
    @SenderName  NVARCHAR(100),
    @SenderEmail NVARCHAR(150),
    @Subject     NVARCHAR(200),
    @MessageBody NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ContactMessages (SenderName, SenderEmail, Subject, MessageBody)
    VALUES (@SenderName, @SenderEmail, @Subject, @MessageBody);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS MessageId;
END;
GO
