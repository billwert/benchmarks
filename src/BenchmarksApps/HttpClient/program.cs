﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Proxy
{
    public class Program
    {
        private static HttpMessageInvoker _httpMessageInvoker;

        private static string _scheme;
        private static string _pathBase;
        private static QueryString _appendQuery;
        private static List<HostString> _hosts;

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
            _hosts = new List<HostString>();
            var baseUri = new Uri(baseUriArg);

            // Cache base URI values
            _scheme = baseUri.Scheme;
            _hosts.Add(new HostString(baseUri.Authority));
            _pathBase = baseUri.AbsolutePath;
            _appendQuery = new QueryString(baseUri.Query);
            // if there were additional hosts specified on the command line, store them.
            for(int i = 1; i < hosts.Length; i++)
            {
                var uri = new Uri(hosts[i]);
                _hosts.Add(new HostString(uri.Authority));
            }

        

            Console.WriteLine($"Base URI: {baseUriArg}");
            for (int i = 1; i < _hosts.Count; i++)
            {
                Console.WriteLine($"Additional host: {_hosts[i]}");
            }

            WriteStatistics();

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
                .UseKestrel()
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

            _httpMessageInvoker = new HttpMessageInvoker(httpHandler);
        }

        private static async Task ProxyRequest(HttpContext context)
        {
            var destinationUri = BuildDestinationUri(context);

            using var requestMessage = context.CreateProxyHttpRequest(destinationUri);
            using var responseMessage = await _httpMessageInvoker.SendAsync(requestMessage, context.RequestAborted);
            await context.CopyProxyHttpResponse(responseMessage);

        }

        private static int hostIndex = 0;
        private static Uri BuildDestinationUri(HttpContext context) 
        {
            var host = _hosts[hostIndex];
            hostIndex = (hostIndex + 1) % _hosts.Count;
            return new Uri(UriHelper.BuildAbsolute(_scheme, host, _pathBase, context.Request.Path, context.Request.QueryString.Add(_appendQuery)));
        }

        private static void WriteStatistics()
        {
            Console.WriteLine("#StartJobStatistics"
            + Environment.NewLine
            + System.Text.Json.JsonSerializer.Serialize(new
            {
                Metadata = new object[]
                {
                    new { Source= "Benchmarks", Name= "AspNetCoreVersion", ShortDescription = "ASP.NET Core Version", LongDescription = "ASP.NET Core Version" },
                    new { Source= "Benchmarks", Name= "NetCoreAppVersion", ShortDescription = ".NET Runtime Version", LongDescription = ".NET Runtime Version" },
                },
                Measurements = new object[]
                {
                    new { Timestamp = DateTime.UtcNow, Name = "AspNetCoreVersion", Value = typeof(IWebHostBuilder).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion },
                    new { Timestamp = DateTime.UtcNow, Name = "NetCoreAppVersion", Value = typeof(object).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion },
                }
            })
            + Environment.NewLine
            + "#EndJobStatistics"
            );
        }
    }
}
