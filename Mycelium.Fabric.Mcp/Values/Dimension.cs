// ------------------------------------------------------------------------------------------------
//  <copyright file="Dimension.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    /// <summary>
    /// The dimension of a quantity, as the exponents of the base quantities of the SI, plus the plane angle and the amount of
    /// information, kept apart so that an angle or a data volume is never compared with a plain number by mistake. For
    /// example, a power (W = kg·m²·s⁻³) has <c>Mass = 1</c>, <c>Length = 2</c> and <c>Time = -3</c>.
    /// </summary>
    /// <param name="Length">The exponent of the length (m).</param>
    /// <param name="Mass">The exponent of the mass (kg).</param>
    /// <param name="Time">The exponent of the time (s).</param>
    /// <param name="Current">The exponent of the electric current (A).</param>
    /// <param name="Temperature">The exponent of the thermodynamic temperature (K).</param>
    /// <param name="Amount">The exponent of the amount of substance (mol).</param>
    /// <param name="Luminosity">The exponent of the luminous intensity (cd).</param>
    /// <param name="Angle">The exponent of the plane angle (rad).</param>
    /// <param name="Information">The exponent of the amount of information (bit).</param>
    public readonly record struct Dimension(int Length, int Mass, int Time, int Current, int Temperature, int Amount, int Luminosity, int Angle, int Information)
    {
        /// <summary>
        /// Gets the dimension of a number without unit.
        /// </summary>
        public static Dimension None => default;

        /// <summary>
        /// Gets the dimension of the product of two quantities.
        /// </summary>
        /// <param name="left">The dimension of the left quantity.</param>
        /// <param name="right">The dimension of the right quantity.</param>
        /// <returns>The sum of the exponents.</returns>
        public static Dimension operator *(Dimension left, Dimension right)
        {
            return new Dimension(left.Length + right.Length, left.Mass + right.Mass, left.Time + right.Time, left.Current + right.Current, left.Temperature + right.Temperature,
                left.Amount + right.Amount, left.Luminosity + right.Luminosity, left.Angle + right.Angle, left.Information + right.Information);
        }

        /// <summary>
        /// Gets the dimension of a quantity raised to a power.
        /// </summary>
        /// <param name="exponent">The exponent, for example 2 for an area from a length, or -1 for a frequency from a time.</param>
        /// <returns>The exponents multiplied by <paramref name="exponent"/>.</returns>
        public Dimension Power(int exponent)
        {
            return new Dimension(this.Length * exponent, this.Mass * exponent, this.Time * exponent, this.Current * exponent, this.Temperature * exponent,
                this.Amount * exponent, this.Luminosity * exponent, this.Angle * exponent, this.Information * exponent);
        }
    }
}
