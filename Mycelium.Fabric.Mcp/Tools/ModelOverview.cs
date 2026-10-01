// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelOverview.cs" company="Starion Group S.A.">
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
    /// The result of the <c>get_model_overview</c> tool: a short summary of the loaded model.
    /// </summary>
    /// <param name="TopLevelElements">The names of the elements directly owned by the root elements.</param>
    /// <param name="ElementCount">The total number of elements in the model.</param>
    /// <param name="NamedElementCount">The number of elements that have a declared name.</param>
    /// <param name="MostFrequentTypes">The most frequent element types, with the number of elements of each type.</param>
    public sealed record ModelOverview(IReadOnlyList<string> TopLevelElements, int ElementCount, int NamedElementCount, IReadOnlyDictionary<string, int> MostFrequentTypes);
}