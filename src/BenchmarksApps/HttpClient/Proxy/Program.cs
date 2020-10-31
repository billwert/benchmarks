// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Crank.EventSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Proxy
{
    public class HostEntry
    {
        public HostString Host { get; set; }
        public long Requests { get; set; }
        public long Successes { get; set; }
    }
    public class Program
    {
        private static HttpMessageInvoker _httpMessageInvoker;

        private static string _scheme;
        private static string _pathBase;
        private static QueryString _appendQuery;
        private static List<HostEntry> _hosts;

        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .AddCommandLine(args)
                .Build();

            // The url all requests will be forwarded to
            var baseUriArg = config["baseUri"];

            if (String.IsNullOrWhiteSpace(baseUriArg))
            {
                throw new ArgumentException("--baseUri is required");
            }
            
            
            string[] hosts = baseUriArg.Split(',');
            baseUriArg = hosts[0];
            _hosts = new List<HostEntry>();
            var baseUri = new Uri(baseUriArg);

            // Cache base URI values
            _scheme = baseUri.Scheme;
            _hosts.Add(new HostEntry() { Host = new HostString(baseUri.Authority) });
            _pathBase = baseUri.AbsolutePath;
            _appendQuery = new QueryString(baseUri.Query);
            // if there were additional hosts specified on the command line, store them.
            for(int i = 1; i < hosts.Length; i++)
            {
                var uri = new Uri(hosts[i]);
                _hosts.Add(new HostEntry() { Host = new HostString(uri.Authority) } );
            }

        

            Console.WriteLine($"Base URI: {baseUriArg}");
            for (int i = 1; i < _hosts.Count; i++)
            {
                Console.WriteLine($"Additional host: {_hosts[i].Host}");
            }

            BenchmarksEventSource.MeasureAspNetVersion();
            BenchmarksEventSource.MeasureNetCoreAppVersion();

            
            for (int i = 0; i < _hosts.Count; i++)
            {
                BenchmarksEventSource.Log.Metadata("Host Total: " + _hosts[i].Host.ToString(), "count", "count", "Total requests", "Total requests", "n0");
                BenchmarksEventSource.Log.Metadata("Host Success: " + _hosts[i].Host.ToString(), "count", "count", "Total success", "Total success", "n0");
            }
            var builder = new WebHostBuilder()
                .ConfigureLogging(loggerFactory =>
                {
                    // Don't enable console logging if no specific level is defined (perf)

                    if (Enum.TryParse(config["LogLevel"], out LogLevel logLevel))
                    {
                        Console.WriteLine($"Console Logging enabled with level '{logLevel}'");
                        loggerFactory.AddConsole().SetMinimumLevel(logLevel);
                    }
                })
                .UseKestrel((context, kestrelOptions) =>
                {
                    kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(context.HostingEnvironment.ContentRootPath, "testCert.pfx"), "testPassword");
                    });
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(config)
                ;

            InitializeHttpClient();

            builder = builder.Configure(app => app.Run(ProxyRequest));


            builder
                .Build()
                .Run();
        }

        private static void InitializeHttpClient()
        {
            var httpHandler = new SocketsHttpHandler();

            httpHandler.AllowAutoRedirect = false;
            httpHandler.UseProxy = false;
            httpHandler.AutomaticDecompression = System.Net.DecompressionMethods.None;
            // Accept any SSL certificate
            httpHandler.SslOptions.RemoteCertificateValidationCallback += (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;

            _httpMessageInvoker = new HttpMessageInvoker(httpHandler);
        }

        private static async Task ProxyRequest(HttpContext context)
        {
            var ( destinationUri, host)  = BuildDestinationUri(context);

            using var requestMessage = context.CreateProxyHttpRequest(destinationUri);
            requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            requestMessage.Version = new Version(2, 0);

            using var responseMessage = await _httpMessageInvoker.SendAsync(requestMessage, context.RequestAborted);
            host.Requests++;
            if(responseMessage.IsSuccessStatusCode)
            {
                host.Successes++;
            }
            await context.CopyProxyHttpResponse(responseMessage);
            if((host.Requests % 10000) == 0)
            {
                for(int i =  0; i < _hosts.Count; i++)
                {

                    BenchmarksEventSource.Measure("Host Total: " + _hosts[i].Host.ToString(), _hosts[i].Requests);
                    BenchmarksEventSource.Measure("Host Success: " + _hosts[i].Host.ToString(), _hosts[i].Successes);
                }
            }
        }
        
        private static int hostIndex = 0;
        private static object lockobj = new object();
        private static (Uri, HostEntry) BuildDestinationUri(HttpContext context) 
        {
            HostEntry host = null;
            lock(lockobj)
            {
                host = _hosts[hostIndex];
                hostIndex = (hostIndex+1) % _hosts.Count;
            }
            
            return (new Uri(UriHelper.BuildAbsolute(_scheme, host.Host, _pathBase, context.Request.Path, context.Request.QueryString.Add(_appendQuery))), host);
        }
    }
}
