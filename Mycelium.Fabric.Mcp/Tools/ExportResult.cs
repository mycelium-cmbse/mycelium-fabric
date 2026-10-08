// ------------------------------------------------------------------------------------------------
//  <copyright file="ExportResult.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// The result of the <c>export_model</c> tool: the file in which the model has been written.
    /// </summary>
    /// <param name="Path">The full path of the written JSON file.</param>
    /// <param name="ElementCount">The number of exported elements.</param>
    public sealed record ExportResult(string Path, int ElementCount);
}
