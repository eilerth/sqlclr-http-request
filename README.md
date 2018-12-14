# sqlclr-http-request

Make HTTP Requests/Query Web APIs from T-SQL via SQLCLR

SQLCLR is a feature in Microsoft SQL Server that allows the creation of objects (stored procdures, functions, etc.) from compiled code written in one of the .NET languages, such as C#. This project uses the SQLCLR feature to create a versatile function that can make HTTP requests utilizing the .NET framework's [HttpWebRequest Class](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest). Now from SQL one can connect to and pull data from web APIs without bringing in additional technologies such as SSIS or projects written in other programming languages. There are definitely instances where a tool such as SSIS is a much better option, but for many use cases this function can simplify architecture and make integrating data a much more rapid proecess.

Also, more information can be found in the article initially posted with this function:
http://www.sqlservercentral.com/articles/SQLCLR/177834/

If you're waiting for me or have any questions for me, bug me!

## Usage/Examples

### Input parameters

- requestMethod (string) - Most often "GET" or "POST", but there are several others used for various purposes.

- url (string) - The URL attempting to connect to, such as an API endpoint

- parameters (string)

  If a GET request, these will just get added into the query string. In that case you could just include them in the url parameter and pass NULL for parameters.
  
  Otherwise, these parameters will be converted to a byte array and added to the content of the HTTP request.
  
  Format of this parameter matches that of a URL query string where you have key=value pairs separated by "&":
        param1=A&param2=B

- headers (string, in XML format) - This allows you to set headers for the HTTP request. They are passed as XML following this format:
```
  <Headers>
    <Header Name="MyHeader">My Header's Value</Header>
    <Header Name="…">…</Header>
    <Header Name="…">…</Header>
  </Headers>
```

- options (string, in XML format) - This allows you to specify several options to fine-tune the HTTP Request. They are passed as XML following this format:
```
  <Options>
    <*option_name*>*option value*</*option_name*>
  </Options>
```

#### Available options:
- security_protocol

  Pass a CSV of protocols from the [SecurityProtocolType Enum](https://docs.microsoft.com/en-us/dotnet/api/system.net.securityprotocoltype)
      
  Example: `<security_protocol>Tls12,Tls11,Tls</security_protocol>`
        
- timeout

  Sets the [HttpWebRequest.Timeout Property](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest.timeout) as the number of milliseconds until the request times out
  
  Example: `<timeout>60000</timeout>`
  
- auto_decompress

  Sets the [HttpWebRequest.AutomaticDecompression Property](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest.automaticdecompression) to automatically decompress the response

  Example: `<auto_decompress>true</auto_decompress>`

- convert_response_to_base64

  Base64 encodes response. This is particularly useful if the response is a file rather than just text.

  Example: `<convert_response_to_base64>true</convert_response_to_base64>`
  
  Note, in SQL Server you're able to then decode using something like 'CAST(@string AS XML).value(\'.\', \'VARBINARY(MAX)\')'
  
- debug
  
  Includes an element in the Response XML with info for each step of the execution
  
  Example: `<debug>true</debug>`

### Returned XML

The result from this function is an XML document generated from the properties available in the [HttpWebResponse Class](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse). This is the structure of that XML.

- Response - this is the root element
  - [CharacterSet](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.CharacterSet)
  - [ContentEncoding](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.ContentEncoding)
  - [ContentLength](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.ContentLength)
  - [ContentType](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.ContentType)
  - HeadersCount - Count of [Headers](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.Headers)
  - [IsFromCache](https://docs.microsoft.com/en-us/dotnet/api/system.net.webresponse.isfromcache)
  - [IsMutuallyAuthenticated](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.IsMutuallyAuthenticated)
  - [LastModified](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.LastModified)
  - [Method](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.Method)
  - [ProtocolVersion](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.ProtocolVersion)
  - [ResponseUri](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.ResponseUri)
  - [StatusCode](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.StatusCode)
  - [Server](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.Server)
  - StatusNumber - Number derived from [StatusCode](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.StatusCode)
  - [StatusDescription](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.StatusDescription)
  - [SupportsHeaders](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.SupportsHeaders)
  - [Headers](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebresponse.Headers)
    * Header - each header will get its own node here
      - Name
      - Values - a header can have multiple values in C#'s HttpWebResponse
        - Value
  - Body - Content from the response
  - Debug - Log and info for each step

### Examples

Query stackoverflow API
```
SELECT 
    B.*
FROM OPENJSON
    (
        [dbo].[clr_http_request]
            (
                'GET', 'http://api.stackexchange.com/2.2/questions?site=stackoverflow', 
                NULL /* parameters */, NULL /* headers */, NULL /* options */
            ).value('Response[1]/Body[1]', 'NVARCHAR(MAX)')
    ) WITH ([items] NVARCHAR(MAX) AS JSON) A
CROSS APPLY OPENJSON(A.[items]) WITH 
    (
        [question_id] INT,
        [title] NVARCHAR(MAX),
        [tags] NVARCHAR(MAX) AS JSON,
        [is_answered] BIT,
        [view_count] INT,
        [answer_count] INT,
        [score] INT
    ) B;
```

This section will be updated with more examples eventually. For now, please also refer to the original article for this function: http://www.sqlservercentral.com/articles/SQLCLR/177834/

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

### Copy ClrHttpRequest.dll to C:\ (or any preferred location, but update the following steps to reference it)

#### *Note:* The rest of these steps are all included in Deployment.sql

### In the [master] database...

Create an asymmetric key from the dll
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
SELECT [dbo].[clr_http_request]('GET', 'https://github.com/eilerth/sqlclr-http-request/', NULL, NULL, '<Options><security_protocol>Tls12</security_protocol></Options>');
```

## Should this be a feature shipped with SQL Server?

If you think so, you should vote for it here: https://feedback.azure.com/forums/908035-sql-server/suggestions/34429699-http-request-function

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
