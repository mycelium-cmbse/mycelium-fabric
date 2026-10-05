// ------------------------------------------------------------------------------------------------
//  <copyright file="Contribution.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The value that one part adds to the total computed by the <c>sum_attribute</c> tool.
    /// </summary>
    /// <param name="Id">The identifier of the part, to pass to <c>evaluate_what_if</c>.</param>
    /// <param name="QualifiedName">The qualified name of the part.</param>
    /// <param name="Types">The declared names of the types of the part, empty when it is not typed.</param>
    /// <param name="Value">The value of the attribute for the part.</param>
    public sealed record Contribution(Guid Id, string QualifiedName, IReadOnlyList<string> Types, double Value);
}
