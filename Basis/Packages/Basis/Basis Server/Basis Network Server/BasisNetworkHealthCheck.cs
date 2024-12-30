﻿using Basis.Network.Core;
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace Basis.Network.Server
{
    public class BasisNetworkHealthCheck
    {

        /// <summary>
        ///     Static empty array to reduce GC.
        /// </summary>
        private static readonly byte[] emptyArray = new byte[0];

        /// <summary>
        ///     The HTTP listener in use.
        /// </summary>
        private readonly HttpListener httpListener = new HttpListener();

        /// <summary>
        ///     The HTTP host we are listening on.
        /// </summary>
        private readonly string host;

        /// <summary>
        ///     The HTTP port we are listening on.
        /// </summary>
        private readonly ushort port;

        /// <summary>
        ///     The HTTP path we are listening on.
        /// </summary>
        private readonly string path;

        /// <summary>
        ///     The background thread listening for health check requests.
        /// </summary>
        private Thread listenThread;

        /// <summary>
        ///     If the server is still running or not.
        /// </summary>
        private volatile bool running = true;
        DateTime startTime = DateTime.Now;

        public BasisNetworkHealthCheck(string Host = "localhost",ushort Port = 10666, string HealthPath = "/health")
        {
            host = Host;

            port = Port;

            path = HealthPath;

            httpListener.Prefixes.Add($"http://{host}:{port}/");

            httpListener.Start();

            listenThread = new Thread(Listen);
            listenThread.Start();

            BNL.Log($"HTTP health check started at 'http://{host}:{port}{path}'");
            startTime = DateTime.Now;
        }
        private void Listen()
        {
            while (running)
            {
                HttpListenerContext context;
                try
                {
                    context = httpListener.GetContext();
                }
                catch (HttpListenerException e)
                {
                    if (e.ErrorCode != 500)
                    {
                        BNL.LogWarning("HTTP health check has exited prematurely as the HTTP server has reported an error. " + e.StackTrace);
                    }
                    return;
                }

                if (context.Request.HttpMethod != "GET")
                {
                    context.Response.StatusCode = 405;
                    context.Response.Close(emptyArray, false);
                }
                else if (context.Request.Url.AbsolutePath != path)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close(emptyArray, false);
                }
                else
                {
                    context.Response.ContentType = "application/json";

                    using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                    {
                        writer.WriteLine($"{{\"listening\": true, \"startTime\": \"{startTime:yyyy-MM-ddTHH:mm:ss.fffZ}\", \"version\": \"{BasisNetworkVersion.ServerVersion}\"}}");
                    }
                }
            }
        }

        public void Stop()
        {
            running = false;
            httpListener.Close();
        }
    }
}