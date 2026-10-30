// ------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer
{
    using System.Threading.Tasks;

    using Carter;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;

    /// <summary>
    /// Entry class of this application
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry method of this application
        /// </summary>
        /// <param name="args">The launch arguments</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddLogging();
            builder.Services.AddExceptionHandler<NegotiatedExceptionHandler>();
            builder.Services.AddRequiredServices();

            var app = builder.Build();

            app.UseExceptionHandler(_ => { });
            app.UseStatusCodePages(NegotiateStatusCodeAsync);
            app.MapCarter();

            app.Run();
        }

        /// <summary>
        /// Gives a bodiless error response raised outside the handlers, such as an unmatched route, the OpenAPI <c>Error</c> model.
        /// </summary>
        /// <param name="context">The <see cref="StatusCodeContext"/> of the response.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        private static Task NegotiateStatusCodeAsync(StatusCodeContext context)
        {
            var response = context.HttpContext.Response;
            var request = context.HttpContext.Request;

            return response.NegotiateErrorAsync(response.StatusCode, $"{ReasonPhrases.GetReasonPhrase(response.StatusCode)}: {request.Method} {request.Path}");
        }
    }
}
