// ------------------------------------------------------------------------------------------------
//  <copyright file="QuantityTerm.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// A number with a unit, as <c>150 [kg]</c> writes it.
    /// </summary>
    /// <param name="Value">The term that gives the number, for example <c>150</c>.</param>
    /// <param name="Unit">The unit, for example <c>kg</c>.</param>
    public sealed record QuantityTerm(Term Value, Unit Unit) : Term
    {
        /// <summary>
        /// Gets the number in the unit: a number without unit gets the unit, a number with a unit is converted to it.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The number with the unit, or an error when the term does not give a number of a compatible unit.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return this.Value.Evaluate(context).Then(value => value is NumberValue number
                ? number.ConvertTo(this.Unit).Then(ModelValue (converted) => converted)
                : Error.Validation(description: $"'{this.Value}' gives {value.Describe()}, which cannot have the unit {this.Unit}."));
        }

        /// <summary>
        /// Writes the number and its unit as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The number and its unit, for example <c>150 [kg]</c>.</returns>
        public override string ToString()
        {
            return $"{this.Enclose(this.Value)} [{this.Unit}]";
        }
    }
}
