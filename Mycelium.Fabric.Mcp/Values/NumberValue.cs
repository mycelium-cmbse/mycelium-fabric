// ------------------------------------------------------------------------------------------------
//  <copyright file="NumberValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using ErrorOr;

    /// <summary>
    /// A number, with or without unit, for example <c>38 [kg]</c> or <c>1.2</c>. A number without unit is taken in the unit
    /// of the number it is combined with, so that <c>subj.mass &lt;= 150</c> compares 150 with the mass in its own unit.
    /// </summary>
    /// <param name="Number">The number.</param>
    /// <param name="Unit">The unit, or <c>null</c> when the number has none.</param>
    public sealed record NumberValue(double Number, Unit Unit = null) : ModelValue
    {
        /// <summary>
        /// The number of decimals of the numbers written in the explanations and of the gaps.
        /// </summary>
        private const int DecimalCount = 3;

        /// <summary>
        /// The relative tolerance under which two numbers are equal, which hides the rounding noise of the conversions.
        /// </summary>
        private const double RelativeTolerance = 1e-9;

        /// <inheritdoc/>
        public override string KindName => "a number";

        /// <summary>
        /// Adds numbers, converted to the unit of the first one that has a unit.
        /// </summary>
        /// <param name="values">The numbers to add.</param>
        /// <returns>The sum, rounded to 3 decimals, or an error when two units have different dimensions.</returns>
        public static ErrorOr<NumberValue> Sum(IEnumerable<NumberValue> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            ErrorOr<NumberValue> sum = new NumberValue(0);

            foreach (var value in values)
            {
                sum = sum.Then(total => total.Add(value));
            }

            return sum.Then(total => total with { Number = Math.Round(total.Number, DecimalCount) });
        }

        /// <summary>
        /// Writes a number as in the explanations: rounded to 3 decimals, with a dot as decimal separator.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The number as text, for example <c>150.96</c>.</returns>
        public static string Format(double number)
        {
            return Math.Abs(number) >= 0.001 || IsZero(number)
                ? Math.Round(number, DecimalCount).ToString(CultureInfo.InvariantCulture)
                : number.ToString("G4", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Adds a number to this one.
        /// </summary>
        /// <param name="other">The number to add.</param>
        /// <returns>The sum, in the unit of this number, or an error when the units have different dimensions.</returns>
        public ErrorOr<NumberValue> Add(NumberValue other)
        {
            return this.Align(other, "add").Then(aligned => new NumberValue(aligned.Left + aligned.Right, aligned.Unit));
        }

        /// <summary>
        /// Subtracts a number from this one.
        /// </summary>
        /// <param name="other">The number to subtract.</param>
        /// <returns>The difference, in the unit of this number, or an error when the units have different dimensions.</returns>
        public ErrorOr<NumberValue> Subtract(NumberValue other)
        {
            return this.Align(other, "subtract").Then(aligned => new NumberValue(aligned.Left - aligned.Right, aligned.Unit));
        }

        /// <summary>
        /// Gets the remainder of the division of this number by another one.
        /// </summary>
        /// <param name="other">The divisor.</param>
        /// <returns>The remainder, or an error when the units have different dimensions or the divisor is zero.</returns>
        public ErrorOr<NumberValue> Remainder(NumberValue other)
        {
            var aligned = this.Align(other, "divide");

            if (aligned.IsError)
            {
                return aligned.Errors;
            }

            if (IsZero(aligned.Value.Right))
            {
                return Error.Validation(description: "The expression divides by zero.");
            }

            return new NumberValue(aligned.Value.Left % aligned.Value.Right, aligned.Value.Unit);
        }

        /// <summary>
        /// Multiplies this number by another one; their units are multiplied too.
        /// </summary>
        /// <param name="other">The other number.</param>
        /// <returns>The product.</returns>
        public NumberValue Multiply(NumberValue other)
        {
            ArgumentNullException.ThrowIfNull(other);

            var unit = (this.Unit, other.Unit) switch
            {
                (null, _) => other.Unit,
                (_, null) => this.Unit,
                _ => this.Unit.Multiply(other.Unit)
            };

            return new NumberValue(this.Number * other.Number, unit);
        }

        /// <summary>
        /// Divides this number by another one; their units are divided too.
        /// </summary>
        /// <param name="other">The divisor.</param>
        /// <returns>The quotient, or an error when the divisor is zero.</returns>
        public ErrorOr<NumberValue> Divide(NumberValue other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (IsZero(other.Number))
            {
                return Error.Validation(description: "The expression divides by zero.");
            }

            var unit = (this.Unit, other.Unit) switch
            {
                (_, null) => this.Unit,
                (null, _) => other.Unit.Power(-1),
                _ => this.Unit.Divide(other.Unit)
            };

            return new NumberValue(this.Number / other.Number, unit);
        }

        /// <summary>
        /// Raises this number to a power.
        /// </summary>
        /// <param name="exponent">The exponent, which must have no unit, and be an integer when this number has a unit.</param>
        /// <returns>The power, or an error when the exponent has a unit, or is not an integer for a number with a unit.</returns>
        public ErrorOr<NumberValue> Power(NumberValue exponent)
        {
            ArgumentNullException.ThrowIfNull(exponent);

            if (exponent.Unit != null)
            {
                return Error.Validation(description: $"The exponent {exponent} has a unit.");
            }

            if (this.Unit == null)
            {
                return new NumberValue(Math.Pow(this.Number, exponent.Number));
            }

            if (!double.IsInteger(exponent.Number))
            {
                return Error.Validation(description: $"{this} cannot be raised to the power {exponent}, which is not an integer.");
            }

            return new NumberValue(Math.Pow(this.Number, exponent.Number), this.Unit.Power((int)exponent.Number));
        }

        /// <summary>
        /// Gets the opposite of this number.
        /// </summary>
        /// <returns>The opposite, in the same unit.</returns>
        public NumberValue Negate()
        {
            return this with { Number = -this.Number };
        }

        /// <summary>
        /// Compares this number with another one, converted to the unit of this number.
        /// </summary>
        /// <param name="other">The other number.</param>
        /// <param name="comparisonOperator">The comparison operator: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>==</c> or <c>!=</c>.</param>
        /// <returns>
        /// The <see cref="BooleanValue"/> of the comparison, with the comparison that holds (for example <c>150.96 &gt; 150</c>)
        /// and the gap (how far the left number is from the right one, positive when the comparison holds), or an error when
        /// the units have different dimensions.
        /// </returns>
        public ErrorOr<BooleanValue> Compare(NumberValue other, string comparisonOperator)
        {
            return this.Align(other, "compare").Then(aligned => CompareAligned(aligned.Left, aligned.Right, aligned.Unit, comparisonOperator));
        }

        /// <summary>
        /// Converts this number to a unit.
        /// </summary>
        /// <param name="unit">The unit, or <c>null</c> to keep the number as it is.</param>
        /// <returns>The number in this unit, or an error when the units have different dimensions.</returns>
        public ErrorOr<NumberValue> ConvertTo(Unit unit)
        {
            if (unit == null || this.Unit == null)
            {
                return this with { Unit = unit ?? this.Unit };
            }

            if (!this.Unit.IsCompatibleWith(unit))
            {
                return Error.Validation(description: $"{this} cannot be converted to {unit}.");
            }

            return new NumberValue(this.Unit.ConvertTo(this.Number, unit), unit);
        }

        /// <summary>
        /// Tells whether two numbers are equal, apart from the rounding noise.
        /// </summary>
        /// <param name="left">The left number.</param>
        /// <param name="right">The right number.</param>
        /// <returns><c>true</c> when the numbers are equal.</returns>
        public static bool AreEqual(double left, double right)
        {
            if (double.IsInfinity(left) || double.IsInfinity(right))
            {
                return double.IsPositiveInfinity(left) ? double.IsPositiveInfinity(right) : double.IsNegativeInfinity(left) && double.IsNegativeInfinity(right);
            }

            return Math.Abs(left - right) <= RelativeTolerance * Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
        }

        /// <summary>
        /// Writes the number with its unit, as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The number, for example <c>38 [kg]</c>.</returns>
        public override string ToString()
        {
            return this.Unit == null ? Format(this.Number) : $"{Format(this.Number)} [{this.Unit}]";
        }

        /// <summary>
        /// Tells whether a number is zero, without comparing floating-point numbers for equality.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns><c>true</c> when the number is zero.</returns>
        private static bool IsZero(double number)
        {
            return Math.Abs(number) < double.Epsilon;
        }

        /// <summary>
        /// Compares two numbers in the same unit.
        /// </summary>
        /// <param name="left">The left number.</param>
        /// <param name="right">The right number.</param>
        /// <param name="unit">Their unit, or <c>null</c>.</param>
        /// <param name="comparisonOperator">The comparison operator.</param>
        /// <returns>The <see cref="BooleanValue"/> of the comparison, or an error when the operator is not a comparison.</returns>
        private static ErrorOr<BooleanValue> CompareAligned(double left, double right, Unit unit, string comparisonOperator)
        {
            var areEqual = AreEqual(left, right);

            // The gap is positive when the comparison holds with room to spare, and negative when it does not hold.
            double? gap = comparisonOperator switch
            {
                "<" or "<=" => areEqual ? 0 : right - left,
                ">" or ">=" => areEqual ? 0 : left - right,
                "==" => areEqual ? 0 : -Math.Abs(left - right),
                _ => null
            };

            bool? holds = comparisonOperator switch
            {
                "<" or ">" => gap > 0,
                "<=" or ">=" or "==" => gap >= 0,
                "!=" => !areEqual,
                _ => null
            };

            if (holds == null)
            {
                return Error.Validation(description: $"'{comparisonOperator}' is not a comparison operator.");
            }

            var shownOperator = holds.Value ? comparisonOperator : Negate(comparisonOperator);
            var comparison = $"{new NumberValue(left, unit)} {shownOperator} {new NumberValue(right, unit)}";

            // An infinite gap, against an infinite limit, is no gap: JSON cannot write it.
            return new BooleanValue(holds.Value) { Explanation = comparison, Gap = gap == null || double.IsInfinity(gap.Value) ? null : Math.Round(gap.Value, DecimalCount) };
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
                "==" => "!=",
                _ => "=="
            };
        }

        /// <summary>
        /// Converts another number to the unit of this one, so that they can be added or compared. A number without unit
        /// takes the unit of the other one.
        /// </summary>
        /// <param name="other">The other number.</param>
        /// <param name="action">The action, for the message, for example <c>add</c>.</param>
        /// <returns>Both numbers in the same unit, or an error when the units have different dimensions.</returns>
        private ErrorOr<(double Left, double Right, Unit Unit)> Align(NumberValue other, string action)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (this.Unit == null || other.Unit == null)
            {
                return (this.Number, other.Number, this.Unit ?? other.Unit);
            }

            if (!this.Unit.IsCompatibleWith(other.Unit))
            {
                return Error.Validation(description: $"Cannot {action} {this} and {other}: their units have different dimensions.");
            }

            return (this.Number, other.Unit.ConvertTo(other.Number, this.Unit), this.Unit);
        }
    }
}
