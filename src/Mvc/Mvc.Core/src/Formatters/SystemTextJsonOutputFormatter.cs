// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters.Json;

namespace Microsoft.AspNetCore.Mvc.Formatters
{
    /// <summary>
    /// A <see cref="TextOutputFormatter"/> for JSON content that uses <see cref="JsonSerializer"/>.
    /// </summary>
    public class SystemTextJsonOutputFormatter : TextOutputFormatter
    {

        /// <summary>
        /// Initializes a new <see cref="SystemTextJsonOutputFormatter"/> instance.
        /// </summary>
        /// <param name="options">The <see cref="JsonOptions"/>.</param>
        public SystemTextJsonOutputFormatter(JsonOptions options)
        {
            SerializerOptions = options.JsonSerializerOptions;

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationJson);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.TextJson);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationAnyJsonSyntax);
        }

        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions"/> used to configure the <see cref="JsonSerializer"/>.
        /// </summary>
        /// <remarks>
        /// A single instance of <see cref="SystemTextJsonOutputFormatter"/> is used for all JSON formatting. Any
        /// changes to the options will affect all output formatting.
        /// </remarks>
        public JsonSerializerOptions SerializerOptions { get; }

        /// <inheritdoc />
        public sealed override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (selectedEncoding == null)
            {
                throw new ArgumentNullException(nameof(selectedEncoding));
            }

            var httpContext = context.HttpContext;

            var writeStream = GetWriteStream(httpContext, selectedEncoding);
            try
            {
                // context.ObjectType reflects the declared model type when specified.
                // For polymorphic scenarios where the user declares a return type, but returns a derived type,
                // we want to serialize all the properties on the derived type. This keeps parity with
                // the behavior you get when the user does not declare the return type and with Json.Net at least at the top level.
                var objectType = context.Object?.GetType() ?? context.ObjectType;
                await JsonSerializer.WriteAsync(writeStream, context.Object, objectType, SerializerOptions);
                await writeStream.FlushAsync();
            }
            finally
            {
                if (writeStream is TranscodingWriteStream transcoding)
                {
                    await transcoding.DisposeAsync();
                }
            }
        }

        private Stream GetWriteStream(HttpContext httpContext, Encoding selectedEncoding)
        {
            if (selectedEncoding.CodePage == Encoding.UTF8.CodePage)
            {
                // JsonSerializer does not write a BOM. Therefore we do not have to handle it
                // in any special way.
                return httpContext.Response.Body;
            }

            return new TranscodingWriteStream(httpContext.Response.Body, selectedEncoding);
        }
    }
}
