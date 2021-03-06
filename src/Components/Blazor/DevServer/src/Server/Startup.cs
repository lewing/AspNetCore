// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Blazor.DevServer.Server
{
    internal class Startup
    {
        public static string ApplicationAssembly { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();

            services.AddResponseCompression(options =>
            {
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    MediaTypeNames.Application.Octet,
                    WasmMediaTypeNames.Application.Wasm
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment environment, IConfiguration configuration)
        {
            var applicationAssemblyFullPath = ResolveApplicationAssemblyFullPath(environment);

            app.UseDeveloperExceptionPage();
            app.UseResponseCompression();
            EnableConfiguredPathbase(app, configuration);

            app.UseBlazorDebugging();

            app.UseStaticFiles();
            app.UseClientSideBlazorFiles(applicationAssemblyFullPath);

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapFallbackToClientSideBlazor(applicationAssemblyFullPath, "index.html");
            });
        }

        private static string ResolveApplicationAssemblyFullPath(IWebHostEnvironment environment)
        {
            var applicationAssemblyFullPath = Path.Combine(environment.ContentRootPath, ApplicationAssembly);
            if (!File.Exists(applicationAssemblyFullPath))
            {
                throw new InvalidOperationException($"Application assembly not found at {applicationAssemblyFullPath}.");
            }

            return applicationAssemblyFullPath;
        }

        private static void EnableConfiguredPathbase(IApplicationBuilder app, IConfiguration configuration)
        {
            var pathBase = configuration.GetValue<string>("pathbase");
            if (!string.IsNullOrEmpty(pathBase))
            {
                app.UsePathBase(pathBase);

                // To ensure consistency with a production environment, only handle requests
                // that match the specified pathbase.
                app.Use((context, next) =>
                {
                    if (context.Request.PathBase == pathBase)
                    {
                        return next();
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        return context.Response.WriteAsync($"The server is configured only to " +
                            $"handle request URIs within the PathBase '{pathBase}'.");
                    }
                });
            }
        }
    }
}
