// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeConstraint.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// A constraint that compares an attribute of the subject of a requirement with a limit, with an optional factor on each
    /// side that carries a margin, as <c>subj.mass * 1.2 &lt;= 150</c> does. It is the form of constraint that the
    /// <c>SetConstraint</c> change builds and that <c>check_requirements</c> evaluates.
    /// </summary>
    /// <param name="Subject">The name of the subject of the requirement, for example <c>subj</c>, or <c>null</c> when it has none.</param>
    /// <param name="Attribute">The name of the constrained attribute of the subject, for example <c>mass</c>.</param>
    /// <param name="ValueFactor">The factor that multiplies the value of the attribute, or <c>null</c> when there is none.</param>
    /// <param name="Operator">The comparison operator, one of <see cref="Operators"/>.</param>
    /// <param name="Limit">The limit, in the unit of the attribute.</param>
    /// <param name="LimitFactor">The factor that multiplies the limit, or <c>null</c> when there is none.</param>
    public sealed record AttributeConstraint(string Subject, string Attribute, double? ValueFactor, string Operator, double Limit, double? LimitFactor)
    {
        /// <summary>
        /// The number of decimals of the compared values and of the gap, as for the totals of the budget tools.
        /// </summary>
        private const int DecimalCount = 3;

        /// <summary>
        /// The number of decimals of a factor computed from a margin, so that it is written as <c>1.15</c> rather than with
        /// the rounding noise of a binary floating-point number.
        /// </summary>
        private const int FactorDecimalCount = 6;

        /// <summary>
        /// Gets the comparison operators that a constraint can use.
        /// </summary>
        public static IReadOnlyList<string> Operators { get; } = ["<", "<=", ">", ">=", "=="];

        /// <summary>
        /// Creates the constraint of a requirement from its limit and a margin that always makes it harder to meet: the margin
        /// is added to the value for an upper limit (<c>subj.mass * 1.2 &lt;= 150</c>) and to the limit for a lower limit
        /// (<c>subj.capacity &gt;= 300 * 1.2</c>).
        /// </summary>
        /// <param name="subject">The name of the subject of the requirement.</param>
        /// <param name="attribute">The name of the constrained attribute.</param>
        /// <param name="comparisonOperator">The comparison operator, one of <see cref="Operators"/>.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="margin">The margin in percent, 0 when there is none. It must be 0 for <c>==</c>.</param>
        /// <returns>The new <see cref="AttributeConstraint"/>.</returns>
        public static AttributeConstraint WithMargin(string subject, string attribute, string comparisonOperator, double limit, double margin)
        {
            double? factor = margin > 0 ? Math.Round(1 + margin / 100, FactorDecimalCount) : null;

            return comparisonOperator is "<" or "<="
                ? new AttributeConstraint(subject, attribute, factor, comparisonOperator, limit, null)
                : new AttributeConstraint(subject, attribute, null, comparisonOperator, limit, factor);
        }

        /// <summary>
        /// Evaluates the constraint for a value of the attribute.
        /// </summary>
        /// <param name="value">The value of the attribute.</param>
        /// <returns>The <see cref="ConstraintEvaluation"/> that tells whether the constraint holds.</returns>
        public ConstraintEvaluation Evaluate(double value)
        {
            var comparedValue = Math.Round(value * (this.ValueFactor ?? 1), DecimalCount);
            var comparedLimit = Math.Round(this.Limit * (this.LimitFactor ?? 1), DecimalCount);

            // The gap is positive when the constraint holds with room to spare, and negative when it does not hold.
            var gap = Math.Round(this.Operator switch
            {
                "<" or "<=" => comparedLimit - comparedValue,
                ">" or ">=" => comparedValue - comparedLimit,
                _ => -Math.Abs(comparedValue - comparedLimit)
            }, DecimalCount);

            var isSatisfied = this.Operator is "<" or ">" ? gap > 0 : gap >= 0;
            var shownOperator = isSatisfied ? this.Operator : Negate(this.Operator);

            return new ConstraintEvaluation(isSatisfied, gap, $"{Format(comparedValue)} {shownOperator} {Format(comparedLimit)}");
        }

        /// <summary>
        /// Writes the constraint as its expression in the SysML v2 textual notation, for example <c>subj.mass * 1.2 &lt;= 150</c>.
        /// </summary>
        /// <returns>The expression of the constraint.</returns>
        public override string ToString()
        {
            var attribute = this.Subject == null ? this.Attribute : $"{this.Subject}.{this.Attribute}";

            return $"{attribute}{FormatFactor(this.ValueFactor)} {this.Operator} {Format(this.Limit)}{FormatFactor(this.LimitFactor)}";
        }

        /// <summary>
        /// Gets the operator that holds when the given one does not, for example <c>&gt;</c> for <c>&lt;=</c>.
        /// </summary>
        /// <param name="comparisonOperator">The comparison operator.</param>
        /// <returns>The negated operator.</returns>
        private static string Negate(string comparisonOperator)
        {
            return comparisonOperator switch
            {
                "<" => ">=",
                "<=" => ">",
                ">" => "<=",
                ">=" => "<",
                _ => "!="
            };
        }

        /// <summary>
        /// Writes a number with a dot as decimal separator, whatever the culture of the server.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The number as text, for example <c>150.96</c>.</returns>
        private static string Format(double number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes a factor as the multiplication it stands for in the expression.
        /// </summary>
        /// <param name="factor">The factor, or <c>null</c>.</param>
        /// <returns>The multiplication, for example <c> * 1.2</c>, or an empty text when there is no factor.</returns>
        private static string FormatFactor(double? factor)
        {
            return factor == null ? string.Empty : $" * {Format(factor.Value)}";
        }
    }
}
