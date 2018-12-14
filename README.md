# sqlclr-http-request

Make HTTP Requests/Query Web APIs from T-SQL via SQLCLR

SQLCLR is a feature in Microsoft SQL Server that allows the creation of objects (stored procdures, functions, etc.) from compiled code written in one of the .NET languages, such as C#. This project uses the SQLCLR feature to create a versatile function that can make HTTP requests utilizing the .NET framework's HttpWebRequest class. Now from SQL one can connect to and pull data from web APIs without bringing in additional technologies such as SSIS or projects written in other programming languages. There are definitely instances where a tool such as SSIS is a much better option, but for many use cases this function can simplify architecture and make integrating data a much more rapid proecess.

I'm going to initially link to the article initially posted with this and complete more documentation later:
http://www.sqlservercentral.com/articles/SQLCLR/177834/

If you're waiting for me or have any questions for me, bug me!

## Deployment

### Ensure the CLR integration is enabled on the SQL Server instance
```
USE [master]
GO
EXECUTE [dbo].[sp_configure] 'clr enabled', 1;
GO
RECONFIGURE;
GO
```

### *Note:* The rest of these steps are all included in Deployment.sql

### In the [master] database...

Create an asymmetric key from the compiled dll
```
CREATE ASYMMETRIC KEY [key_clr_http_request] FROM EXECUTABLE FILE = 'C:\ClrHttpRequest.dll';
```

Create a login from the assymetic key and grant it UNSAFE assembly
```
CREATE LOGIN [lgn_clr_http_request] FROM ASYMMETRIC KEY [key_clr_http_request];
GRANT UNSAFE ASSEMBLY TO [lgn_clr_http_request];
```

### In the desired user database...
Create a user for the login just created
```
CREATE USER [usr_clr_http_request] FOR LOGIN [lgn_clr_http_request];
```

Create the assembly from the dll
```
CREATE ASSEMBLY [ClrHttpRequest] FROM 'C:\ClrHttpRequest.dll' WITH PERMISSION_SET=EXTERNAL_ACCESS;
```

Create the clr_http_request function
```
CREATE FUNCTION [dbo].[clr_http_request] (@requestMethod NVARCHAR(MAX), @url NVARCHAR(MAX), @parameters NVARCHAR(MAX), @headers NVARCHAR(MAX), @optionsXml NVARCHAR(MAX))
RETURNS XML AS EXTERNAL NAME [ClrHttpRequest].[UserDefinedFunctions].[clr_http_request];
```

### A quick test to confirm it works
```
SELECT [dbo].[clr_http_request]('GET', 'https://github.com/eilerth/sqlclr-http-request/', NULL, NULL, '<security_protocol>Tls12</security_protocol>');
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
