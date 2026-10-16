// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffLinearForm.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    /// <summary>
    /// A <see cref="TradeOffLimit"/> written as one linear inequality on integers:
    /// <c>TotalCoefficient * total + LimitCoefficient * limitTotal &lt;= Bound</c>, where the totals are in units of
    /// <see cref="TradeOffScale"/>.
    /// </summary>
    /// <param name="TotalCoefficient">The coefficient of the total of the constrained attribute.</param>
    /// <param name="LimitCoefficient">The coefficient of the total of the limit attribute, 0 when the limit is a number.</param>
    /// <param name="Bound">The bound of the inequality.</param>
    public sealed record TradeOffLinearForm(long TotalCoefficient, long LimitCoefficient, long Bound)
    {
        /// <summary>
        /// Tells whether the given totals meet the inequality.
        /// </summary>
        /// <param name="total">The total of the constrained attribute, in units.</param>
        /// <param name="limitTotal">The total of the limit attribute, in units, ignored when the limit is a number.</param>
        /// <returns><c>true</c> when the inequality holds.</returns>
        public bool IsSatisfiedBy(long total, long limitTotal)
        {
            return this.TotalCoefficient * total + this.LimitCoefficient * limitTotal <= this.Bound;
        }
    }
}
