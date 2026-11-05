// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffOperatorExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Extension methods that write a <see cref="TradeOffOperator"/> as in a constraint.
    /// </summary>
    public static class TradeOffOperatorExtensions
    {
        /// <param name="comparison">The <see cref="TradeOffOperator"/> to write.</param>
        extension(TradeOffOperator comparison)
        {
            /// <summary>
            /// Writes the operator as in a constraint: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c> or <c>==</c>.
            /// </summary>
            /// <returns>The symbol of the operator.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown when <paramref name="comparison"/> is not a known operator.
            /// </exception>
            public string ToSymbol()
            {
                return comparison switch
                {
                    TradeOffOperator.LessThan => "<",
                    TradeOffOperator.LessThanOrEqual => "<=",
                    TradeOffOperator.GreaterThan => ">",
                    TradeOffOperator.GreaterThanOrEqual => ">=",
                    TradeOffOperator.Equal => "==",
                    _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown operator.")
                };
            }

            /// <summary>
            /// Writes the opposite of the operator, the one that holds when the constraint is broken: <c>&gt;=</c> for
            /// <c>&lt;</c>, <c>!=</c> for <c>==</c>.
            /// </summary>
            /// <returns>The symbol of the opposite operator.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown when <paramref name="comparison"/> is not a known operator.
            /// </exception>
            public string ToOppositeSymbol()
            {
                return comparison switch
                {
                    TradeOffOperator.LessThan => ">=",
                    TradeOffOperator.LessThanOrEqual => ">",
                    TradeOffOperator.GreaterThan => "<=",
                    TradeOffOperator.GreaterThanOrEqual => "<",
                    TradeOffOperator.Equal => "!=",
                    _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown operator.")
                };
            }
        }
    }
}
