// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementCoverage.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// How many of the requirement identifiers found in a document, or in one of its sections, are already modelled.
    /// </summary>
    /// <param name="Found">The number of requirement identifiers found.</param>
    /// <param name="Modelled">The number of them that a requirement of the model has as <c>reqId</c>.</param>
    /// <param name="Missing">
    /// The identifiers found that no requirement of the model has, in the order of the document, at most 50.
    /// </param>
    public sealed record RequirementCoverage(int Found, int Modelled, IReadOnlyList<string> Missing);
}
