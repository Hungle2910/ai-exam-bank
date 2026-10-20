-- SQL Server mapping draft, NOT an executed migration.
-- Merge this definition into the shared EF Core migration stream.
-- Do not run independently against an existing team database.
CREATE TABLE dbo.DeploymentRecord
(
    Id uniqueidentifier NOT NULL,
    Environment nvarchar(32) NOT NULL,
    DeploymentId nvarchar(128) NOT NULL,
    SourceVersion nvarchar(128) NOT NULL,
    ArtifactVersion nvarchar(128) NOT NULL,
    DeployedBy nvarchar(256) NOT NULL,
    DeployedAt datetime2(3) NOT NULL, -- UTC supplied by the deployment command.
    DeploymentStatus nvarchar(32) NOT NULL,
    Notes nvarchar(2000) NULL,
    CONSTRAINT PK_DeploymentRecord PRIMARY KEY (Id),
    CONSTRAINT UQ_DeploymentRecord_Environment_DeploymentId UNIQUE (Environment, DeploymentId),
    CONSTRAINT CK_DeploymentRecord_Status CHECK (DeploymentStatus IN (N'SUCCEEDED', N'FAILED', N'ROLLED_BACK'))
);
