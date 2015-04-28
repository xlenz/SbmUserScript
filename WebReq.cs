using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace userScript
{
    public static class WebReq
    {
        private static readonly int[] StatusCodes = {400, 499, 500, 404};

        public static string Make(string url, int timeout = 60, string method = "POST", string data = null, string contentType = null,
            string ssoBase64 = null, bool keepAlive = false, string accept = "*/*", int contentLength = -1)
        {
            var request = (HttpWebRequest) WebRequest.Create(url);

            request.Timeout = (int) TimeSpan.FromSeconds(timeout).TotalMilliseconds;
            request.KeepAlive = keepAlive;
            request.Accept = accept;
            request.Method = method.ToUpper();
            request.Proxy = null; //speed-up WebReq, do not autodetect Proxy

            if (ssoBase64 != null)
                request.Headers.Add("ALFSSOAuthNToken", ssoBase64);
            if (contentType != null)
                request.ContentType = contentType;
            if (contentLength >= 0)
                request.ContentLength = contentLength;

            var response = MakeWebRequest(request, data);
            return response;
        }

        private static string MakeWebRequest(WebRequest request, string data)
        {
            if (data != null)
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                request.ContentLength = bytes.Length;
                using (var ps = request.GetRequestStream())
                {
                    ps.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    var responseStream = response.GetResponseStream();
                    if (responseStream == null)
                        throw new Exception("Response stream is null.");

                    using (var reader = new StreamReader(responseStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (WebException ex)
            {
                var exResp = (HttpWebResponse) ex.Response;
                var statusCode = (int) exResp.StatusCode;

                if (!StatusCodes.Contains(statusCode))
                    throw;

                using (var stream = ex.Response.GetResponseStream())
                {
                    if (stream == null) throw;
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
    }
}