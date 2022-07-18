using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

namespace VocalRecallService
{
    public static class WebDownloader
    {
        private const int MAX_DOWNLOADER_THREAD_COUNT = 16;

        private static int internalCounter = 0;
        public static int RunningThreadCount = 0;
        private static List<string> activeUrls = new List<string>();

        private enum TraceEventType { Critical, Error, Information, Resume, Start, Stop, Suspend, Transfer, Verbose, Warning };

        private static CultureInfo ci = new CultureInfo("en-US");
        private static DateTimeStyles dts = System.Globalization.DateTimeStyles.AllowLeadingWhite | System.Globalization.DateTimeStyles.AllowTrailingWhite | System.Globalization.DateTimeStyles.AllowWhiteSpaces;

        public delegate void DownloadedDelegate(byte[] data, object[] parameters);
        
        private static byte[] DownloadFile(CookieCollection cookies, string url)
        {
            byte[] result = new byte[0];

            HttpWebResponse response = null;

            response = GetResponse(url, cookies);
            if (response == null) return null;

            BinaryReader reader = new BinaryReader(response.GetResponseStream());

            byte[] buffer = new byte[65536];
            int readBytes = 0;

            while ((readBytes = reader.Read(buffer, 0, 65536)) > 0)
            {
                byte[] temp = result;
                result = new byte[temp.Length + readBytes];
                temp.CopyTo(result, 0);
                Array.Copy(buffer, 0, result, temp.Length, readBytes);
            }

            reader.Close();
            response.Close();

            return result;
        }

        private static HttpWebResponse GetResponse(string requestURI, CookieCollection cookies)
        {
            return GetResponse(requestURI, cookies, "");
        }

        private static HttpWebResponse GetResponse(string requestURI, CookieCollection cookies, string postData)
        {
            // Create a request for the URL
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(requestURI);
			request.Method = "GET";
			request.KeepAlive = false;
			request.Timeout = 90000; // set it to 90sec
            request.Accept = "image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-shockwave-flash, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-icq, */*";
            request.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-gb,hu;q=0.5");
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; Tablet PC 1.7; .NET CLR 1.0.3705; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
            // If required by the server, set the credentials.
            request.Credentials = CredentialCache.DefaultCredentials;

            // set cookies
            if (cookies != null)
            {
                lock (cookies)
                {
                    request.CookieContainer = new CookieContainer();
                    request.CookieContainer.Add(cookies);
                }
            }

            // check if it's a POST
            if (postData != "")
            {
                ASCIIEncoding encoding = new ASCIIEncoding();

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = postData.Length;

                byte[] data = encoding.GetBytes(postData);
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(data, 0, data.Length);
                requestStream.Close();
            }

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException)
            {
				// NOTE: If you get "No connection could be made because the target machine actively refused it 127.0.0.1:8118" error here, that mean that Tor is not running!!
                return null;
            }

            // Display the status.
            if ((response == null) || (response.StatusCode != HttpStatusCode.OK))
            {
                return null;
            }

            // set new cookies values
            lock (cookies)
            {
                cookies.Add(response.Cookies);
            }

            return response;
        }

        private static string GetStreamAsString(Stream stream, bool removeWhiteSpace)
        {
            // Open the stream using a StreamReader for easy access.
            StreamReader reader = new StreamReader(stream);
            // Read the content.
            string result = reader.ReadToEnd();

            // Cleanup the streams and the response.
            reader.Close();
            stream.Close();

            if (removeWhiteSpace)
            {
                result = result.Replace("\t", "").Replace("\n", "").Replace("\r", "").Replace("&#160;", "").Replace("&nbsp;", "");

                // I know, this is very unefficient but quick fix
                string spaces = "";
                for (int i = 1; i < 30; i++)
                {
                    spaces += " ";
                    result = result.Replace(">" + spaces + "<", "><");
                }
            }

            return result;
        }

        public static string GetPatternMatchedInformation(string document, string startPattern, string endPattern)
        {
            int index = document.IndexOf(startPattern);

            if (index < 0)
            {
                //// there is kaka in the pancake
                return null;
            }

            index += startPattern.Length;
            return document.Substring(index, document.IndexOf(endPattern, index) - index);
        }

        private static object GetValueOfPattern(Type valueType, string document, string startPattern, string endPattern, TraceEventType traceEventType)
        {
            string parseString = GetPatternMatchedInformation(document, startPattern, endPattern);

            if (!String.IsNullOrEmpty(parseString))
            {
                parseString = parseString.Trim();

                if (parseString == "[nem látható]") return null;

                if (valueType.FullName == typeof(string).FullName)
                {
                    return parseString;
                }

                if (valueType.FullName == typeof(Int32).FullName)
                {
                    int parsedInt32 = -1;
                    if (Int32.TryParse(parseString, out parsedInt32))
                    {
                        return parsedInt32;
                    }
                    else
                    {
                        Trace.TraceWarning("[Int32] Can not parse the following: '" + parseString + "'", "PageProcess", TraceEventType.Warning);
                    }
                }

                if (valueType.FullName == typeof(DateTime).FullName)
                {
                    DateTime parsedDateTime;
                    if (DateTime.TryParseExact(parseString.Trim(), new string[] { "yyyy. MMMM d.", "MMMM d.", "yyyy. MMM d.", "yyyy. MMMM d., HH:mm" }, ci, dts, out parsedDateTime))
                    {
                        return parsedDateTime;
                    }
                    else
                    {
                        Trace.TraceWarning("[DateTime] Can not parse the following: '" + parseString + "'", "PageProcess", TraceEventType.Warning);
                    }
                }

                if (valueType.FullName == typeof(Boolean).FullName)
                {
                    if ((parseString.ToLower() == "false") || (parseString.ToLower() == "férfi"))
                    {
                        return false;
                    }
                    else if ((parseString.ToLower() == "true") || (parseString.ToLower() == "nő"))
                    {
                        return true;
                    }

                }
            }
            else
            {
                if (traceEventType == TraceEventType.Error)
                {
                    Trace.TraceError("Pattern was not recognized. (start: '" + startPattern + "')", "PageProcess", traceEventType);
                }
                else
                {
                    //Trace.TraceWarning("Pattern was not recognized. (start: '" + startPattern + "')", "PageProcess", traceEventType);
                }
            }

            return null;
        }

        public static int QueueDownload(CookieCollection cookies, string url, DownloadedDelegate downloadedCallback, object[] parameters)
        {
            if (cookies == null) cookies = new CookieCollection();

            lock (ci)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(
                    delegate(object s, DoWorkEventArgs args) 
                    {
                        if (String.IsNullOrEmpty((string)args.Argument)) return;

                        lock (ci)
                        {
                            if (!activeUrls.Contains(url))
                            {
                                activeUrls.Add(url);
								//Debug.WriteLineIf(activeUrls.Count % 100 == 50, "URLs in download queue: " + activeUrls.Count.ToString("N0"));
                            }
                            else
                            {
                                return;
                            }
                        }

                        //DateTime waitingStarted = DateTime.Now; // set a timeout for the waiting procedure to avoid deadlock
                        bool waitMore = true;
                        while (waitMore)// && (DateTime.Now - waitingStarted < new TimeSpan(0, 0, 2)))
                        {
                            lock (ci)
                            {
                                waitMore = (WebDownloader.RunningThreadCount > MAX_DOWNLOADER_THREAD_COUNT);
                            }

                            if (waitMore) Thread.Sleep(200);
                        }

                        lock (ci) {
                            WebDownloader.RunningThreadCount++;
                        }
                        args.Result = DownloadFile(cookies, args.Argument as string);
                        lock (ci)
                        {
                            WebDownloader.RunningThreadCount--;

                            if (activeUrls.Contains(url))
                            {
                                activeUrls.Remove(url);
								//Debug.WriteLineIf(activeUrls.Count % 100 == 0, "URLs in download queue: " + activeUrls.Count.ToString("N0"));
                            }
                        }
                    });

                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    delegate(object s, RunWorkerCompletedEventArgs args)
                    {
                        if (args.Error == null)
                        {
                            downloadedCallback(args.Result as byte[], parameters);
                        }

						internalCounter--;
                    }
                    );
                
                bw.RunWorkerAsync(url); // NOTE: no cookies are passed here!
                bw.Dispose();

                internalCounter++;
            }

            return internalCounter;
        }
    }
}