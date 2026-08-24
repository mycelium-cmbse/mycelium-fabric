// ------------------------------------------------------------------------------------------------
//  <copyright file="MetaApi.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Hand-coded request handlers for the Meta routes registered by the generated companion partial.
    /// </summary>
    /// <remarks>
    /// <b>Neither route below is defined by the specification.</b> The PIM-to-REST mapping table in
    /// Systems Modeling API and Services 1.0 §8.1.3 (pp. 50–56) contains no <c>/meta</c> endpoint, and no
    /// service in §7.2 (pp. 39–47) declares a datatype-introspection operation — the six services are
    /// <c>ProjectService</c>, <c>ElementNavigationService</c>, <c>ProjectDataVersioningService</c>,
    /// <c>QueryService</c>, <c>ExternalRelationshipService</c> and <c>ProjectUsageService</c>. These routes
    /// come from the SysML v2 pilot implementation's API surface rather than from the adopted
    /// specification, so they are outside the conformance criteria of §2 (pp. 17–19).
    /// <para>
    /// The intended semantics are therefore a local design decision: the natural reading is that they
    /// expose the KerML/SysML v2 metaclasses this server understands — the same set the SysML2.NET SDK
    /// generates DTOs and POCOs for. Confirm the contract before implementing, and prefer grounding the
    /// metaclass names themselves in the metamodel rather than in this API specification.
    /// </para>
    /// </remarks>
    public partial class MetaApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /meta/datatypes</c>, returning the datatypes — that is, the metaclasses — that
        /// this server exposes.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Not a specification endpoint; see the remarks on <see cref="MetaApi"/> for provenance. Not yet
        /// implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetDatatypes(HttpContext context, ILogger<MetaApi> logger)
        {
            logger.LogInformation("getDatatypes invoked");

            return NotYetImplemented(context, "getDatatypes");
        }

        /// <summary>
        /// Handles <c>GET /meta/datatypes/{datatypeId}</c>, returning the single datatype — that is,
        /// metaclass — identified by <c>datatypeId</c>.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// The <c>datatypeId</c> route segment carries no <c>:guid</c> constraint, unlike the identifiers
        /// on the specification-derived routes — a metaclass is identified by name rather than by a record
        /// id. It is therefore <b>not</b> bound as a method parameter here, since only guid-constrained
        /// segments are. Binding it as a <c>string</c> is an open decision.
        /// <para>
        /// Not a specification endpoint; see the remarks on <see cref="MetaApi"/> for provenance. Not yet
        /// implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </para>
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetDatatypeById(HttpContext context, ILogger<MetaApi> logger)
        {
            logger.LogInformation("getDatatypeById invoked");

            return NotYetImplemented(context, "getDatatypeById");
        }
    }
}
