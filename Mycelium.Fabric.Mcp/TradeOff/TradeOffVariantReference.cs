// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffVariantReference.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    /// <summary>
    /// A variant of a <see cref="TradeOffDecision"/>, as a rule designates it.
    /// </summary>
    /// <param name="DecisionIndex">The index of the decision in the <see cref="TradeOffProblem"/>.</param>
    /// <param name="Variant">The name of the variant.</param>
    public sealed record TradeOffVariantReference(int DecisionIndex, string Variant);
}
