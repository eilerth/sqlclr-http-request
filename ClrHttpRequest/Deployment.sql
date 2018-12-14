-- if there is an existing database, remove this AND make sure to change the "USE [db_clr_http_request]" below to direct things to the desired database
CREATE DATABASE [db_clr_http_request];
GO

-- take care of stuff in master database
USE [master]
GO
-- create asymmetric key
IF NOT EXISTS (SELECT * FROM [master].sys.asymmetric_keys WHERE [name] = 'key_clr_http_request')
BEGIN
	CREATE ASYMMETRIC KEY [key_clr_http_request] FROM EXECUTABLE FILE = 'C:\ClrHttpRequest.dll';
END;
GO
-- create login from key and grant UNSAFE ASSEMBLY
IF NOT EXISTS (SELECT * FROM [master].dbo.syslogins WHERE [name] = 'lgn_clr_http_request')
BEGIN
	CREATE LOGIN [lgn_clr_http_request] FROM ASYMMETRIC KEY [key_clr_http_request];
	GRANT UNSAFE ASSEMBLY TO [lgn_clr_http_request];
END;
GO

-- take care of stuff in target database
USE [db_clr_http_request]
GO
-- create user for login
IF NOT EXISTS (SELECT * FROM dbo.sysusers WHERE [name] = 'usr_clr_http_request')
BEGIN
	CREATE USER [usr_clr_http_request] FOR LOGIN [lgn_clr_http_request];
END;
GO
-- drop function if it exists
DROP FUNCTION IF EXISTS [dbo].[clr_http_request];
GO
-- drop assembly if it exists
DROP ASSEMBLY IF EXISTS [ClrHttpRequest]
GO
-- create assembly
CREATE ASSEMBLY [ClrHttpRequest] FROM 'C:\ClrHttpRequest.dll' WITH PERMISSION_SET=EXTERNAL_ACCESS;
GO
-- create function
CREATE FUNCTION [dbo].[clr_http_request] (@requestMethod NVARCHAR(MAX), @url NVARCHAR(MAX), @parameters NVARCHAR(MAX), @headers NVARCHAR(MAX), @optionsXml NVARCHAR(MAX))
RETURNS XML AS EXTERNAL NAME [ClrHttpRequest].[UserDefinedFunctions].[clr_http_request];
GO

---- drop created database if desired
--USE [master]
--GO
--DROP DATABASE [db_clr_http_request];
--GO