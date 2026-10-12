// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffLimit.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Globalization;

    /// <summary>
    /// A checked <see cref="TradeOffConstraint"/> of a <see cref="TradeOffProblem"/>: the total of an attribute compared with a
    /// number or with the total of another attribute, with a margin that always makes the constraint harder to meet.
    /// </summary>
    /// <param name="Attribute">The name of the constrained attribute.</param>
    /// <param name="Operator">The <see cref="TradeOffOperator"/> of the comparison.</param>
    /// <param name="Limit">The limit, or <c>null</c> when the limit is the total of <paramref name="LimitAttribute"/>.</param>
    /// <param name="LimitAttribute">The name of the attribute whose total is the limit, or <c>null</c> for a number.</param>
    /// <param name="Margin">The margin in percent, 0 for none.</param>
    public sealed record TradeOffLimit(string Attribute, TradeOffOperator Operator, double? Limit, string LimitAttribute, double Margin)
    {
        /// <summary>
        /// Gets a value indicating whether the total must stay under the limit, rather than over it.
        /// </summary>
        public bool IsUpperBound => this.Operator is TradeOffOperator.LessThan or TradeOffOperator.LessThanOrEqual;

        /// <summary>
        /// Gets the text of the constraint, written like a SysML v2 constraint expression (for example
        /// <c>mass * 1.2 &lt;= 150</c> or <c>capacity &gt;= eclipseEnergy * 1.2</c>).
        /// </summary>
        public string Description
        {
            get
            {
                var factor = this.Margin > 0 ? $" * {(1 + this.Margin / 100).ToString("0.####", CultureInfo.InvariantCulture)}" : string.Empty;
                var limit = this.LimitAttribute ?? this.Limit?.ToString("0.###", CultureInfo.InvariantCulture);

                var symbol = this.Operator switch
                {
                    TradeOffOperator.LessThan => "<",
                    TradeOffOperator.LessThanOrEqual => "<=",
                    TradeOffOperator.GreaterThan => ">",
                    _ => ">="
                };

                return this.IsUpperBound ? $"{this.Attribute}{factor} {symbol} {limit}" : $"{this.Attribute} {symbol} {limit}{factor}";
            }
        }

        /// <summary>
        /// Writes the constraint as one linear inequality on totals in units: with the margin factor <c>f</c>,
        /// <c>total * f &lt;= limit</c> for an upper bound and <c>total &gt;= limit * f</c> for a lower bound, multiplied out
        /// so that every coefficient is an integer, and one unit less on the bound for a strict comparison.
        /// </summary>
        /// <returns>The <see cref="TradeOffLinearForm"/> of the constraint.</returns>
        public TradeOffLinearForm GetLinearForm()
        {
            var factor = TradeOffScale.ToFactorUnits(this.Margin);
            var strictness = this.Operator is TradeOffOperator.LessThan or TradeOffOperator.GreaterThan ? 1 : 0;

            if (this.LimitAttribute != null)
            {
                return this.IsUpperBound
                    ? new TradeOffLinearForm(factor, -TradeOffScale.UnitsPerFactor, -strictness)
                    : new TradeOffLinearForm(-TradeOffScale.UnitsPerFactor, factor, -strictness);
            }

            var limit = TradeOffScale.ToUnits(this.Limit ?? 0);

            return this.IsUpperBound
                ? new TradeOffLinearForm(factor, 0, TradeOffScale.UnitsPerFactor * limit - strictness)
                : new TradeOffLinearForm(-TradeOffScale.UnitsPerFactor, 0, -factor * limit - strictness);
        }
    }
}
