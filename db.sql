-- Run this setup script on your sql-server db.

USE master
GO

CREATE DATABASE Catland
GO

USE Catland
GO

EXEC sys.sp_cdc_enable_db
GO

CREATE TABLE dbo.Cats (
	[Id] BIGINT PRIMARY KEY IDENTITY(1, 1)
	,[Name] VARCHAR(30) NOT NULL
	,[LastName] VARCHAR(30) NULL
	,[Age] INT NULL
	,[CreationDate] DATETIME2(3) NOT NULL
	)
GO

EXEC sys.sp_cdc_enable_table @source_schema = N'dbo'
	,@source_name = N'Cats'
	,@role_name = NULL
	,@supports_net_changes = 1
GO

USE master
GO

SELECT name
	,is_cdc_enabled
FROM sys.databases
WHERE name = 'Catland'
GO


