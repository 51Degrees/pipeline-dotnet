/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    /// <summary>
    /// Test HTTP Listener for serving static content, used in testing to mock
    /// a client website or provide a static response for a service.
    /// </summary>
    public static class TestHttpListener
    {
        /// <summary>
        /// Creates a simple HttpListener.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        public static HttpListener SimpleListener(string url, CancellationToken token)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            // Handle requests
            Task listenTask = new TestHttpServer(listener).HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return listener;
        }

        /// <summary>
        /// Creates a simple HttpListener.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="pageData"></param>
        /// <param name="token"></param>
        public static HttpListener SimpleListener(string url, string pageData, CancellationToken token)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            // Handle requests
            Task listenTask = new TestHttpServer(listener, pageData).HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return listener;
        }

        /// <summary>
        /// Start a new TcpListener with the port as 0 so that the OS assigns an
        /// available port. Record this then close the listener. This ensures 
        /// that a 'randomly' generated port number is free.
        /// </summary>
        /// <returns></returns>
        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
