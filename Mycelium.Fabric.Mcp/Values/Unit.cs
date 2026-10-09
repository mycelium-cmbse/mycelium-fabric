// ------------------------------------------------------------------------------------------------
//  <copyright file="Unit.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A unit of measurement, such as <c>kg</c>, <c>km/h</c> or <c>°C</c>: a value <c>v</c> in this unit is
    /// <c>v × Factor + Offset</c> in the coherent SI unit of its dimension (kg, m/s, K). It is written as its symbol in JSON.
    /// </summary>
    /// <param name="Symbol">The symbol of the unit, for example <c>kg</c>.</param>
    /// <param name="Dimension">The dimension of the unit.</param>
    /// <param name="Factor">The factor that converts a value in this unit to the coherent SI unit, for example 0.001 for <c>g</c>.</param>
    /// <param name="Offset">The offset of the conversion, which only an absolute temperature unit such as <c>°C</c> has.</param>
    [JsonConverter(typeof(UnitJsonConverter))]
    public sealed record Unit(string Symbol, Dimension Dimension, double Factor, double Offset = 0)
    {
        /// <summary>
        /// Tells whether a value in this unit can be converted to another unit: both have the same dimension.
        /// </summary>
        /// <param name="other">The other unit.</param>
        /// <returns><c>true</c> when the units have the same dimension.</returns>
        public bool IsCompatibleWith(Unit other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return this.Dimension == other.Dimension;
        }

        /// <summary>
        /// Converts a value in this unit to another unit of the same dimension.
        /// </summary>
        /// <param name="value">The value in this unit.</param>
        /// <param name="target">The unit to convert to, which must be compatible with this one.</param>
        /// <returns>The value in the <paramref name="target"/> unit, for example 38000 for 38 kg in g.</returns>
        public double ConvertTo(double value, Unit target)
        {
            ArgumentNullException.ThrowIfNull(target);

            return (value * this.Factor + this.Offset - target.Offset) / target.Factor;
        }

        /// <summary>
        /// Gets the unit of the product of two quantities, for example <c>W*h</c>.
        /// </summary>
        /// <param name="other">The unit of the right quantity.</param>
        /// <returns>The product of the units.</returns>
        public Unit Multiply(Unit other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return new Unit($"{this.Symbol}*{other.Symbol}", this.Dimension * other.Dimension, this.Factor * other.Factor);
        }

        /// <summary>
        /// Gets the unit of the quotient of two quantities, for example <c>km/h</c>.
        /// </summary>
        /// <param name="other">The unit of the right quantity.</param>
        /// <returns>The quotient of the units.</returns>
        public Unit Divide(Unit other)
        {
            ArgumentNullException.ThrowIfNull(other);

            var otherSymbol = other.Symbol.Contains('*') || other.Symbol.Contains('/') ? $"({other.Symbol})" : other.Symbol;

            return new Unit($"{this.Symbol}/{otherSymbol}", this.Dimension * other.Dimension.Power(-1), this.Factor / other.Factor);
        }

        /// <summary>
        /// Gets the unit of a quantity raised to a power, for example <c>m^2</c>.
        /// </summary>
        /// <param name="exponent">The exponent.</param>
        /// <returns>The unit raised to the power.</returns>
        public Unit Power(int exponent)
        {
            var symbol = this.Symbol.Contains('*') || this.Symbol.Contains('/') ? $"({this.Symbol})" : this.Symbol;

            return new Unit($"{symbol}^{exponent}", this.Dimension.Power(exponent), Math.Pow(this.Factor, exponent));
        }

        /// <summary>
        /// Writes the symbol of the unit.
        /// </summary>
        /// <returns>The symbol, for example <c>kg</c>.</returns>
        public override string ToString()
        {
            return this.Symbol;
        }
    }
}
