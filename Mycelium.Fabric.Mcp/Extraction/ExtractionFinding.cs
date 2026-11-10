// ------------------------------------------------------------------------------------------------
//  <copyright file="ExtractionFinding.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    /// <summary>
    /// A finding on a proposed requirement: what is wrong with it, found by the server or given by the AI assistant.
    /// </summary>
    /// <param name="Kind">The <see cref="ExtractionFindingKind"/>.</param>
    /// <param name="Message">The explanation for the user and the AI assistant.</param>
    public sealed record ExtractionFinding(ExtractionFindingKind Kind, string Message);
}
