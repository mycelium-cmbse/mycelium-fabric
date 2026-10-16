// ------------------------------------------------------------------------------------------------
//  <copyright file="ConnectionSummary.cs" company="Starion Group S.A.">
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
    /// A connector of the model, such as a connection or an interface, as listed in the details of the elements it connects.
    /// </summary>
    /// <param name="Id">The identifier of the connector.</param>
    /// <param name="Name">The declared name of the connector, or <c>null</c> when it has none.</param>
    /// <param name="Type">The metaclass of the connector, followed by its definition when it is typed (for example <c>InterfaceUsage : DataInterface</c>).</param>
    /// <param name="QualifiedName">The qualified name of the owner of the connector, where the paths of its ends start.</param>
    /// <param name="Ends">The path of each end, from the owner of the connector (for example <c>payloadSubsystem.camera.dataOut</c>).</param>
    public sealed record ConnectionSummary(Guid Id, string Name, string Type, string QualifiedName, IReadOnlyList<string> Ends);
}
