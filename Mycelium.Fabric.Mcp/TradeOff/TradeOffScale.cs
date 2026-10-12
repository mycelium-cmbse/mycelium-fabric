// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffScale.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System;

    /// <summary>
    /// Turns the values of a <see cref="TradeOffProblem"/> into the integers that the CP-SAT solver works with, and back. The
    /// <see cref="TradeOffProblem"/> and the <see cref="CpSatTradeOffSolver"/> both use it, so that a total is computed the same
    /// way when the solver chooses a combination and when the tool checks it.
    /// </summary>
    public static class TradeOffScale
    {
        /// <summary>
        /// The number of units in a value: values are kept to 3 decimals (38.25 kg becomes 38 250).
        /// </summary>
        public const long UnitsPerValue = 1000;

        /// <summary>
        /// The number of units in a margin factor: margins are kept to 2 decimals of a percent (a margin of 20 % becomes the
        /// factor 12 000, that is 1.2).
        /// </summary>
        public const long UnitsPerFactor = 10000;

        /// <summary>
        /// The largest value, in absolute value, that a problem may contain, so that no computation overflows.
        /// </summary>
        public const double MaximumValue = 1e9;

        /// <summary>
        /// Turns a value into units.
        /// </summary>
        /// <param name="value">The value, at most <see cref="MaximumValue"/> in absolute value.</param>
        /// <returns>The value in units, rounded to the nearest unit.</returns>
        public static long ToUnits(double value)
        {
            return (long)Math.Round(value * UnitsPerValue, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Turns units back into a value.
        /// </summary>
        /// <param name="units">The value in units.</param>
        /// <returns>The value, with at most 3 decimals.</returns>
        public static double ToValue(long units)
        {
            return (double)units / UnitsPerValue;
        }

        /// <summary>
        /// Turns a margin in percent into the factor that multiplies the side of a constraint it makes harder to meet.
        /// </summary>
        /// <param name="margin">The margin in percent, for example 20.</param>
        /// <returns>The factor in units, for example 12 000 for a margin of 20 %.</returns>
        public static long ToFactorUnits(double margin)
        {
            return (long)Math.Round((1 + margin / 100) * UnitsPerFactor, MidpointRounding.AwayFromZero);
        }
    }
}
