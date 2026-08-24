// ------------------------------------------------------------------------------------------------
//  <copyright file="ApiBase.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Base class of every Carter module in this assembly, carrying the behaviour shared by the request
    /// handlers.
    /// </summary>
    /// <remarks>
    /// Each module declares its base class on its hand-coded partial; the generated partial only declares
    /// the <c>ICarterModule</c> interface, and the two halves combine into a single type deriving from
    /// this class.
    /// <para>
    /// The modules deliberately carry no constructor dependencies — Carter's own analyzer reports
    /// <c>CARTER1 'x' should not have dependencies</c> for those, because modules are registered as
    /// singletons and a scoped dependency would be captive. Services a handler needs are injected into the
    /// handler method instead, which minimal APIs resolve from the request's service provider.
    /// </para>
    /// </remarks>
    public abstract class ApiBase
    {
        /// <summary>
        /// The <see cref="ProblemDetails.Title"/> carried by every not-yet-implemented response.
        /// </summary>
        protected const string NotYetImplementedTitle = "Not yet implemented";

        /// <summary>
        /// Writes the placeholder response served by a route that is registered but whose handler has not
        /// been implemented yet.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="operationId">
        /// The operation id the route is registered under — the value passed to <c>WithName</c> by the
        /// generated route registration, which is itself taken from the specification.
        /// </param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="context"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// The routes registered under <c>Modules/AutoGenModules/</c> are generated from the OMG Systems
        /// Modeling API and Services 1.0 REST/HTTP PSM, so the whole surface exists before any of it is
        /// backed by a service. Until a handler is implemented it answers <c>500 Internal Server Error</c>
        /// with an RFC 7807 <see cref="ProblemDetails"/> payload rather than throwing, so that a client
        /// exercising the surface gets a well-formed, machine-readable answer instead of an unhandled
        /// exception.
        /// </remarks>
        protected static Task NotYetImplemented(HttpContext context, string operationId)
        {
            ArgumentNullException.ThrowIfNull(context);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = NotYetImplementedTitle,
                Detail = $"The '{operationId}' operation is registered but is not yet implemented by this server.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["operationId"] = operationId
                }
            };

            return Results.Problem(problemDetails).ExecuteAsync(context);
        }
    }
}
