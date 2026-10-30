// ------------------------------------------------------------------------------------------------
//  <copyright file="NegotiatedExceptionHandler.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Http
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Answers an exception no handler caught with the OpenAPI <c>Error</c> model, through content negotiation.
    /// </summary>
    public sealed class NegotiatedExceptionHandler : IExceptionHandler
    {
        /// <summary>
        /// The (injected) logger the exception is recorded with.
        /// </summary>
        private readonly ILogger<NegotiatedExceptionHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NegotiatedExceptionHandler"/> class.
        /// </summary>
        /// <param name="logger">The (injected) logger the exception is recorded with.</param>
        public NegotiatedExceptionHandler(ILogger<NegotiatedExceptionHandler> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
        }

        /// <summary>
        /// Writes the response reporting an exception: the status of a <see cref="BadHttpRequestException"/>, otherwise a logged <c>500</c>.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="exception">The exception no handler caught.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> of the request.</param>
        /// <returns><c>true</c>, since every exception is answered, or needs no answer once the client has gone.</returns>
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            switch (exception)
            {
                case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                    return true;

                case BadHttpRequestException badRequest:
                    await httpContext.Response.NegotiateErrorAsync(badRequest.StatusCode,
                        $"{ReasonPhrases.GetReasonPhrase(badRequest.StatusCode)}: {httpContext.Request.Method} {httpContext.Request.Path}");

                    return true;

                default:
                    this.logger.LogError(exception, "{Method} {Path} failed unexpectedly", httpContext.Request.Method, httpContext.Request.Path);

                    await httpContext.Response.NegotiateErrorAsync(StatusCodes.Status500InternalServerError, "The server did not complete the request.");

                    return true;
            }
        }
    }
}
