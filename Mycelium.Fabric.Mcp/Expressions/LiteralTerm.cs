// ------------------------------------------------------------------------------------------------
//  <copyright file="LiteralTerm.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// A value written in an expression: a number (<c>150</c>), a Boolean (<c>true</c>), a text (<c>"S-band"</c>) or
    /// <c>null</c>.
    /// </summary>
    /// <param name="Value">The value.</param>
    public sealed record LiteralTerm(ModelValue Value) : Term
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="context">Not used: a literal does not depend on the model.</param>
        /// <returns>The value.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            return this.Value;
        }

        /// <summary>
        /// Writes the value as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The value, for example <c>150</c> or <c>true</c>.</returns>
        public override string ToString()
        {
            return this.Value.ToString();
        }
    }
}
