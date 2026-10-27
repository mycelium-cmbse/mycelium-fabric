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
    using System;
    using System.Globalization;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// A checked <see cref="TradeOffConstraint"/> of a <see cref="TradeOffProblem"/>: the total of an attribute compared with a
    /// number or with the total of another attribute, with a margin that always makes the constraint harder to meet.
    /// </summary>
    /// <param name="Attribute">The name of the constrained attribute.</param>
    /// <param name="Operator">The <see cref="TradeOffOperator"/> of the comparison.</param>
    /// <param name="Limit">The limit, or <c>null</c> when the limit is the total of <paramref name="LimitAttribute"/>.</param>
    /// <param name="LimitAttribute">The name of the attribute whose total is the limit, or <c>null</c> for a number.</param>
    /// <param name="Margin">The margin in percent, 0 for none.</param>
    /// <param name="Unit">The unit of the totals and of the limit, or <c>null</c> when the attribute has none.</param>
    /// <param name="Name">The short name of the constraint, for example <c>REQ-SYS-001</c>, or <c>null</c>.</param>
    public sealed record TradeOffLimit(string Attribute, TradeOffOperator Operator, double? Limit, string LimitAttribute, double Margin, Unit Unit = null, string Name = null)
    {
        /// <summary>
        /// Gets a value indicating whether the total must stay under the limit, rather than over it.
        /// </summary>
        public bool IsUpperBound => this.Operator is TradeOffOperator.LessThan or TradeOffOperator.LessThanOrEqual;

        /// <summary>
        /// Gets the operator of the comparison, as written in a constraint: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> or <c>&gt;=</c>.
        /// </summary>
        public string Symbol => this.Operator switch
        {
            TradeOffOperator.LessThan => "<",
            TradeOffOperator.LessThanOrEqual => "<=",
            TradeOffOperator.GreaterThan => ">",
            _ => ">="
        };

        /// <summary>
        /// Gets the total of the attribute at which a numeric limit is reached, margin included (<c>150 / 1.2 = 125</c> for
        /// <c>mass * 1.2 &lt;= 150</c>), or <c>null</c> when the limit is the total of another attribute.
        /// </summary>
        public double? Threshold => this.Limit is { } limit && this.LimitAttribute == null
            ? Math.Round(this.IsUpperBound ? limit / this.Factor : limit * this.Factor, 3)
            : null;

        /// <summary>
        /// Gets the factor of the margin, 1 without margin.
        /// </summary>
        private double Factor => 1 + this.Margin / 100;

        /// <summary>
        /// Gets the text of the margin factor in a description (<c> * 1.2</c>), empty without margin.
        /// </summary>
        private string FactorText => this.Margin > 0 ? $" * {this.Factor.ToString("0.####", CultureInfo.InvariantCulture)}" : string.Empty;

        /// <summary>
        /// Gets the text of the constraint, written like a SysML v2 constraint expression (for example
        /// <c>mass * 1.2 &lt;= 150</c> or <c>capacity &gt;= eclipseEnergy * 1.2</c>).
        /// </summary>
        public string Description
        {
            get
            {
                var limit = this.LimitAttribute ?? this.FormatValue(this.Limit ?? 0);

                return this.IsUpperBound ? $"{this.Attribute}{this.FactorText} {this.Symbol} {limit}" : $"{this.Attribute} {this.Symbol} {limit}{this.FactorText}";
            }
        }

        /// <summary>
        /// Gets the two sides of the comparison for a combination, as the constraint compares them: the total times the margin
        /// factor and the limit for an upper bound, the total and the limit times the margin factor for a lower bound.
        /// </summary>
        /// <param name="total">The total of the attribute.</param>
        /// <param name="limitTotal">The total of the limit attribute, ignored for a numeric limit.</param>
        /// <returns>The compared value and the limit it is compared with, rounded to 3 decimals.</returns>
        public (double Value, double Limit) GetComparedValues(double total, double limitTotal)
        {
            var limit = this.LimitAttribute == null ? this.Limit ?? 0 : limitTotal;

            return this.IsUpperBound
                ? (Math.Round(total * this.Factor, 3), Math.Round(limit, 3))
                : (Math.Round(total, 3), Math.Round(limit * this.Factor, 3));
        }

        /// <summary>
        /// Describes why a combination breaks the constraint, with its values: <c>mass * 1.2 = 150.96 kg &gt; 150 kg</c> or
        /// <c>power = 260 W &gt; powerOutput = 250 W</c>.
        /// </summary>
        /// <param name="value">The compared value, from <see cref="GetComparedValues"/>.</param>
        /// <param name="limit">The limit, from <see cref="GetComparedValues"/>.</param>
        /// <returns>The text of the breach, without the name of the constraint.</returns>
        public string DescribeBreach(double value, double limit)
        {
            var opposite = this.Operator switch
            {
                TradeOffOperator.LessThan => ">=",
                TradeOffOperator.LessThanOrEqual => ">",
                TradeOffOperator.GreaterThan => "<=",
                _ => "<"
            };

            var left = this.IsUpperBound ? $"{this.Attribute}{this.FactorText}" : this.Attribute;
            var right = this.IsUpperBound || (this.LimitAttribute == null && this.Margin == 0)
                ? this.LimitAttribute
                : $"{this.LimitAttribute ?? this.FormatValue(this.Limit ?? 0)}{this.FactorText}";
            var limitText = string.IsNullOrEmpty(right) ? this.FormatValue(limit) : $"{right} = {this.FormatValue(limit)}";

            return $"{left} = {this.FormatValue(value)} {opposite} {limitText}";
        }

        /// <summary>
        /// Writes a value of the attribute with its unit, for example <c>150 kg</c>.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>The text of the value.</returns>
        private string FormatValue(double value)
        {
            var unit = this.Unit == null ? string.Empty : $" {this.Unit.Symbol}";

            return value.ToString("0.###", CultureInfo.InvariantCulture) + unit;
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
