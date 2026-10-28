// ------------------------------------------------------------------------------------------------
//  <copyright file="RollUp.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// A value of an attribute of a part that an evaluation computed as <c>sum_attribute</c> does: the own value of the
    /// part, or else the sum of the values of its sub-parts.
    /// </summary>
    /// <param name="Part">The part.</param>
    /// <param name="Attribute">The name of the attribute, for example <c>mass</c>.</param>
    /// <param name="Total">The value, for example <c>125.8 [kg]</c>, with the margins that the model gives for the values of the parts.</param>
    /// <param name="Contributions">The parts that give the value, each with its own value.</param>
    /// <param name="TotalWithoutMargins">The value without the margins of the model, or <c>null</c> when the model gives no margin.</param>
    public sealed record RollUp(IFeature Part, string Attribute, NumberValue Total, IReadOnlyList<Contribution> Contributions, NumberValue TotalWithoutMargins = null);
}
