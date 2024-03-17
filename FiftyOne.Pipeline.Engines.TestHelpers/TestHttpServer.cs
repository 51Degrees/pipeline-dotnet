/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    /// <summary>
    /// An HTTP server to use during testing.
    /// </summary>
    public class TestHttpServer
    {
        /// <summary>
        /// The HttpListener used to monitor for requests.
        /// </summary>
        public HttpListener Listener { get; private set; }

        /// <summary>
        /// The content returned by the HttpServer.
        /// </summary>
        public string PageData { get; private set; } =
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            "    <title>Test Page</title>" +
            "  </head>" +
            "  <body>" +
            "  </body>" +
            "</html>";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="listener">The HttpListener used to monitor for requests.</param>
        public TestHttpServer(HttpListener listener)
        {
            Listener = listener;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="listener">The HttpListener used to monitor for requests.</param>
        /// <param name="pageData">The content returned by the HttpServer.</param>
        public TestHttpServer(HttpListener listener, string pageData)
        {
            Listener = listener;
            PageData = pageData;
        }

        /// <summary>
        /// Handle connections, either server the page data as content or
        /// shutdown the server if cancellation has been requested.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task HandleIncomingConnections(CancellationToken token)
        {
            bool runServer = true;

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await Listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // If cancellation requested then shutdown server.
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("Shutdown requested");
                    runServer = false;
                }

                // Write the response info
                byte[] data = Encoding.UTF8.GetBytes(PageData);
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.AddHeader("Access-Control-Allow-Origin", "*");

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }
    }
}
