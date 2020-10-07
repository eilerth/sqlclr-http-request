using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Text;
using System.Xml.Linq;

public partial class UserDefinedFunctions
{
    /// <summary>
    /// Performs HTTP request tailored to provided variables and returns response
    /// </summary>
    /// <param name="requestMethod">GET/POST/etc.</param>
    /// <param name="url">URL endpoint</param>
    /// <param name="parameters">Provided in query string format as a=b&c=d&e=f&...</param>
    /// <param name="headersXml">Provided as XML in format <Headers><Header Name="Header Name">Header Value</Header></Headers></param>
    /// <param name="optionsXml">Provided as XML in format <Options><timeout>300000</timeout><security_protocol>Tls12,Tls11,Tls</security_protocol></Options></param>
    /// <returns>Response from HttpWebResponse object represented as XML</returns>
    [Microsoft.SqlServer.Server.SqlFunction]
    public static SqlXml clr_http_request(string requestMethod, string url, string parameters, string headersXml, string optionsXml)
    {
        var debugXml = new XElement("Debug");
        debugXml.Add(GetDebugStepXElement("Starting",
            new XElement(
                "InputParameters",
                new XElement("requestMethod", requestMethod),
                new XElement("url", url),
                new XElement("parameters", parameters),
                new XElement("headersXml", headersXml),
                new XElement("optionsXml", optionsXml)
            )
        ));
        try
        {
            // Parse options, if any, into dictionary
            Dictionary<string, string> options = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(optionsXml))
            {
                foreach (XElement element in XDocument.Parse(optionsXml).Descendants())
                {
                    options.Add(element.Name.LocalName, element.Value);
                }
            }
            debugXml.Add(GetDebugStepXElement("Parsed Options"));

            // Attempt to set SecurityProtocol if passed as option (i.e., Tls12,Tls11,Tls)
            if (options.ContainsKey("security_protocol"))
            {
                SecurityProtocolType? securityProtocol = null;
                foreach (var protocol in options["security_protocol"].Split(','))
                {
                    if (securityProtocol == null)
                    {
                        securityProtocol = (SecurityProtocolType)Enum.Parse(typeof(SecurityProtocolType), protocol);
                    }
                    else
                    {
                        securityProtocol = securityProtocol | (SecurityProtocolType)Enum.Parse(typeof(SecurityProtocolType), protocol);
                    }

                }
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)securityProtocol;
            }
            debugXml.Add(GetDebugStepXElement("Handled Option 'security_protocol'"));

            // If GET request, and there are parameters, build into url
            if (requestMethod.ToUpper() == "GET" && !string.IsNullOrWhiteSpace(parameters))
            {
                url += (url.IndexOf('?') > 0 ? "&" : "?") + parameters;
            }
            debugXml.Add(GetDebugStepXElement("Handled GET parameters"));

            // Create an HttpWebRequest with the url and set the method
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = requestMethod.ToUpper();
            debugXml.Add(GetDebugStepXElement("Created HttpWebRequest object and set method"));

            // Add in any headers provided
            bool contentLengthSetFromHeaders = false;
            bool contentTypeSetFromHeaders = false;
            if (!string.IsNullOrWhiteSpace(headersXml))
            {
                // Parse provided headers as XML and loop through header elements
                foreach (XElement headerElement in XElement.Parse(headersXml).Descendants())
                {
                    // Retrieve header's name and value
                    var headerName = headerElement.Attribute("Name").Value;
                    var headerValue = headerElement.Value;

                    // Some headers cannot be set by request.Headers.Add() and need to set the HttpWebRequest property directly
                    switch (headerName)
                    {
                        case "Accept":
                            request.Accept = headerValue;
                            break;
                        case "Connection":
                            request.Connection = headerValue;
                            break;
                        case "Content-Length":
                            request.ContentLength = long.Parse(headerValue);
                            contentLengthSetFromHeaders = true;
                            break;
                        case "Content-Type":
                            request.ContentType = headerValue;
                            contentTypeSetFromHeaders = true;
                            break;
                        case "Date":
                            request.Date = DateTime.Parse(headerValue);
                            break;
                        case "Expect":
                            request.Expect = headerValue;
                            break;
                        case "Host":
                            request.Host = headerValue;
                            break;
                        case "If-Modified-Since":
                            request.IfModifiedSince = DateTime.Parse(headerValue);
                            break;
                        case "Range":
                            var parts = headerValue.Split('-');
                            request.AddRange(int.Parse(parts[0]), int.Parse(parts[1]));
                            break;
                        case "Referer":
                            request.Referer = headerValue;
                            break;
                        case "Transfer-Encoding":
                            request.TransferEncoding = headerValue;
                            break;
                        case "User-Agent":
                            request.UserAgent = headerValue;
                            break;
                        default: // other headers
                            request.Headers.Add(headerName, headerValue);
                            break;
                    }
                }
            }
            debugXml.Add(GetDebugStepXElement("Processed Headers"));

            // Set the timeout if provided as an option
            if (options.ContainsKey("timeout"))
            {
                request.Timeout = int.Parse(options["timeout"]);
            }
            debugXml.Add(GetDebugStepXElement("Handled Option 'timeout'"));

            // Set the automatic decompression if provided as an option (default to Deflate | GZip)
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            if (options.ContainsKey("auto_decompress") && bool.Parse(options["auto_decompress"]) == false)
            {
                request.AutomaticDecompression = DecompressionMethods.None;
            }
            debugXml.Add(GetDebugStepXElement("Handled Option 'auto_decompress'"));

            // Add in non-GET parameters provided
            if (requestMethod.ToUpper() != "GET" && !string.IsNullOrWhiteSpace(parameters))
            {
                // Convert to byte array
                var parameterData = Encoding.UTF8.GetBytes(parameters);

                // Set content info
                if (!contentLengthSetFromHeaders)
                {
                    request.ContentLength = parameterData.Length;
                }
                if (!contentTypeSetFromHeaders)
                {
                    request.ContentType = "application/x-www-form-urlencoded";
                }

                // Add data to request stream
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(parameterData, 0, parameterData.Length);
                }
            }
            debugXml.Add(GetDebugStepXElement("Handled non-GET Parameters"));

            // Retrieve results from response
            XElement returnXml = null;
            try
            {
                debugXml.Add(GetDebugStepXElement("About to Send Request"));
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    debugXml.Add(GetDebugStepXElement("Retrieved Response"));
                    // Get headers (loop through response's headers)
                    var responseHeadersXml = new XElement("Headers");
                    var responseHeaders = response.Headers;
                    for (int i = 0; i < responseHeaders.Count; ++i)
                    {
                        // Get values for this header
                        var valuesXml = new XElement("Values");
                        foreach (string value in responseHeaders.GetValues(i))
                        {
                            valuesXml.Add(new XElement("Value", value));
                        }

                        // Add this header with its values to the headers xml
                        responseHeadersXml.Add(
                            new XElement("Header",
                                new XElement("Name", responseHeaders.GetKey(i)),
                                valuesXml
                            )
                        );
                    }
                    debugXml.Add(GetDebugStepXElement("Processed Response Headers"));

                    // Get the response body
                    var responseString = String.Empty;
                    using (var stream = response.GetResponseStream())
                    {
                        // If requested to convert to base 64 string, use memory stream, otherwise stream reader
                        if (options.ContainsKey("convert_response_to_base64") && bool.Parse(options["convert_response_to_base64"]) == true)
                        {
                            using (var memoryStream = new MemoryStream())
                            {
                                // Copy response stream to memory stream
                                stream.CopyTo(memoryStream);

                                // Convert memory stream to a byte array
                                var bytes = memoryStream.ToArray();

                                // Convert to base 64 string
                                responseString = Convert.ToBase64String(bytes);
                            }
                        }
                        else
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                // Retrieve response string
                                responseString = reader.ReadToEnd();
                            }
                        }
                        debugXml.Add(GetDebugStepXElement("Handled Option 'convert_response_to_base64' and Retrieved Response Stream"));
                    }

                    // Assemble reponse XML from details of HttpWebResponse
                    returnXml =
                        new XElement("Response",
                            new XElement("CharacterSet", response.CharacterSet),
                            new XElement("ContentEncoding", response.ContentEncoding),
                            new XElement("ContentLength", response.ContentLength),
                            new XElement("ContentType", response.ContentType),
                            new XElement("CookiesCount", response.Cookies.Count),
                            new XElement("HeadersCount", response.Headers.Count),
                            responseHeadersXml,
                            new XElement("IsFromCache", response.IsFromCache),
                            new XElement("IsMutuallyAuthenticated", response.IsMutuallyAuthenticated),
                            new XElement("LastModified", response.LastModified),
                            new XElement("Method", response.Method),
                            new XElement("ProtocolVersion", response.ProtocolVersion),
                            new XElement("ResponseUri", response.ResponseUri),
                            new XElement("Server", response.Server),
                            new XElement("StatusCode", response.StatusCode),
                            new XElement("StatusNumber", ((int)response.StatusCode)),
                            new XElement("StatusDescription", response.StatusDescription),
                            new XElement("SupportsHeaders", response.SupportsHeaders),
                            new XElement("Body", responseString)
                        );
                    debugXml.Add(GetDebugStepXElement("Assembled Return Xml"));
                    if (options.ContainsKey("debug") && bool.Parse(options["debug"]) == true)
                    {
                        returnXml.Add(debugXml);
                    }
                }
            }
            catch (WebException we)
            {
                debugXml.Add(GetDebugStepXElement("WebException Encountered. See Exception for more detail"));
                // Check to see if we got a response
                if (we.Response != null)
                {
                    // If we got a response, generate return XML with the HTTP status code 
                    HttpWebResponse errorResponse = we.Response as HttpWebResponse;
                    returnXml =
                        new XElement("Response",
                            new XElement("Server", errorResponse.Server),
                            new XElement("StatusCode", errorResponse.StatusCode),
                            new XElement("StatusNumber", ((int)errorResponse.StatusCode)),
                            new XElement("StatusDescription", errorResponse.StatusDescription),
                            debugXml
                        );
                }
                else
                {
                    // If there wasn't even a response then re-throw the exception since we didn't really want to catch it here
                    throw;
                }
            }

            // Return data
            return new SqlXml(returnXml.CreateReader());
        }
        catch (Exception ex)
        {
            debugXml.Add(GetDebugStepXElement("General Exception Encountered. See Exception for more detail"));

            // Return data
            return new SqlXml((new XElement("Response", debugXml, GetXElementFromException(ex))).CreateReader());
        }
    }

    /// <summary>
    /// Generates an XML element with a standard format for debug steps
    /// </summary>
    /// <param name="stepName">Name for this step in the process</param>
    /// <returns>XElement representing a debug step</returns>
    private static XElement GetDebugStepXElement(string stepName)
    {
        return new XElement(
            "Step",
            new XAttribute("Name", stepName),
            new XAttribute("LoggedUtc", DateTime.UtcNow)
        );
    }

    /// <summary>
    /// Generates an XML element with a standard format for debug steps
    /// </summary>
    /// <param name="stepName">Name for this step in the process</param>
    /// <param name="innerXElement">Additional data for the debug step provided as an XElement</param>
    /// <returns></returns>
    private static XElement GetDebugStepXElement(string stepName, XElement innerXElement)
    {
        var stepXElement = GetDebugStepXElement(stepName);
        stepXElement.Add(innerXElement);
        return stepXElement;
    }

    /// <summary>
    /// Converts an exception to an XML element, recursively appending inner exceptions.
    /// </summary>
    /// <param name="ex">Exception</param>
    /// <returns>XElement representing provided Exception</returns>
    public static XElement GetXElementFromException(Exception ex)
    {
        var returnXml =
            new XElement("Exception",
                new XElement("Message", ex.Message),
                new XElement("StackTrace", ex.StackTrace),
                new XElement("Source", ex.Source),
                new XElement("ToString", ex.ToString())
            );

        if (ex.InnerException != null)
        {
            returnXml.Add(new XElement("InnerException", GetXElementFromException(ex.InnerException)));
        }

        return returnXml;
    }
}
