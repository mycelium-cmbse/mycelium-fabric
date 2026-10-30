// ------------------------------------------------------------------------------------------------
//  <copyright file="UnitCatalogTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Values
{
    using System;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// Suite of tests for the <see cref="UnitCatalog"/> class.
    /// </summary>
    [TestFixture]
    public class UnitCatalogTestFixture
    {
        [Test]
        public void VerifyTryParse()
        {
            using (Assert.EnterMultipleScope())
            {
                // Base units, prefixes, accepted units, information units, imperial units, names.
                Assert.That(Factor("kg"), Is.EqualTo(1).Within(1e-12));
                Assert.That(Factor("g"), Is.EqualTo(1e-3).Within(1e-15));
                Assert.That(Factor("mm"), Is.EqualTo(1e-3).Within(1e-15));
                Assert.That(Factor("µs"), Is.EqualTo(1e-6).Within(1e-18));
                Assert.That(Factor("us"), Is.EqualTo(1e-6).Within(1e-18));
                Assert.That(Factor("dam"), Is.EqualTo(10).Within(1e-12));
                Assert.That(Factor("min"), Is.EqualTo(60));
                Assert.That(Factor("h"), Is.EqualTo(3600));
                Assert.That(Factor("hPa"), Is.EqualTo(100).Within(1e-9));
                Assert.That(Factor("kWh"), Is.EqualTo(3.6e6).Within(1e-3));
                Assert.That(Factor("mAh"), Is.EqualTo(3.6).Within(1e-12));
                Assert.That(Factor("arcsec"), Is.EqualTo(Math.PI / 648000).Within(1e-15));
                Assert.That(Factor("mas"), Is.EqualTo(Math.PI / 648000 / 1000).Within(1e-18));
                Assert.That(Factor("deg"), Is.EqualTo(Math.PI / 180).Within(1e-15));
                Assert.That(Factor("MB"), Is.EqualTo(8e6).Within(1e-6));
                Assert.That(Factor("Mbit"), Is.EqualTo(1e6).Within(1e-6));
                Assert.That(Factor("lb"), Is.EqualTo(0.45359237));
                Assert.That(Factor("kilogram"), Is.EqualTo(1).Within(1e-12));
                Assert.That(Factor("Kilometre"), Is.EqualTo(1000).Within(1e-9));
                Assert.That(Factor("watt hour"), Is.EqualTo(3600));

                // Compound units.
                Assert.That(Factor("km/h"), Is.EqualTo(1000.0 / 3600).Within(1e-12));
                Assert.That(Factor("m/s^2"), Is.EqualTo(1));
                Assert.That(Factor("m/s²"), Is.EqualTo(1));
                Assert.That(Factor("s⁻¹"), Is.EqualTo(1));
                Assert.That(Factor("kg*m^2"), Is.EqualTo(1).Within(1e-12));
                Assert.That(Factor("kg·m/(s^2)"), Is.EqualTo(1).Within(1e-12));
                Assert.That(Factor("1/s"), Is.EqualTo(1));
                Assert.That(Factor("Mbit/s"), Is.EqualTo(1e6).Within(1e-6));

                Assert.That(Parse("km/h").Dimension, Is.EqualTo(new Dimension { Length = 1, Time = -1 }));
                Assert.That(Parse("N").Dimension, Is.EqualTo(Parse("kg*m/s^2").Dimension));
                Assert.That(Parse("°C").Offset, Is.EqualTo(273.15));
                Assert.That(Parse(" kg ").Symbol, Is.EqualTo("kg"));

                // Unknown units: a decibel is not a decibyte, and a malformed compound unit.
                Assert.That(UnitCatalog.TryParse("dB", out _), Is.False);
                Assert.That(UnitCatalog.TryParse("furlong", out _), Is.False);
                Assert.That(UnitCatalog.TryParse(null, out _), Is.False);
                Assert.That(UnitCatalog.TryParse(" ", out _), Is.False);
                Assert.That(UnitCatalog.TryParse("m/", out _), Is.False);
                Assert.That(UnitCatalog.TryParse("m^", out _), Is.False);
                Assert.That(UnitCatalog.TryParse("(m/s", out _), Is.False);
                Assert.That(UnitCatalog.TryParse("m)", out _), Is.False);
            }
        }

        [Test]
        public void VerifyTryParseWithUnitsOfTheCaller()
        {
            var slug = new Unit("slug", new Dimension { Mass = 1 }, 14.5939);

            Unit FindUnit(string text) => text == "slug" ? slug : null;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(UnitCatalog.TryParse("slug", FindUnit, out var unit), Is.True);
                Assert.That(unit, Is.EqualTo(slug));
                Assert.That(UnitCatalog.TryParse("slug/s", FindUnit, out unit), Is.True);
                Assert.That(unit.Dimension, Is.EqualTo(new Dimension { Mass = 1, Time = -1 }));
                Assert.That(UnitCatalog.TryParse("(slug*m)^2", FindUnit, out unit), Is.True);
                Assert.That(unit.Factor, Is.EqualTo(14.5939 * 14.5939).Within(1e-9));
                Assert.That(UnitCatalog.TryParse("kg", FindUnit, out unit), Is.True);
                Assert.That(unit.Factor, Is.EqualTo(1));
                Assert.That(UnitCatalog.TryParse("slugs", FindUnit, out _), Is.False);
                Assert.That(UnitCatalog.TryParse("slug", null, out _), Is.False);
            }
        }

        [Test]
        public void VerifyTryGetPrefixFactor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(UnitCatalog.TryGetPrefixFactor("kilo", out var factor), Is.True);
                Assert.That(factor, Is.EqualTo(1e3));
                Assert.That(UnitCatalog.TryGetPrefixFactor("Milli", out factor), Is.True);
                Assert.That(factor, Is.EqualTo(1e-3));
                Assert.That(UnitCatalog.TryGetPrefixFactor("k", out _), Is.False);
                Assert.That(UnitCatalog.TryGetPrefixFactor(null, out _), Is.False);
            }
        }

        /// <summary>
        /// Parses a unit that must be known.
        /// </summary>
        /// <param name="text">The symbol or name of the unit.</param>
        /// <returns>The unit.</returns>
        private static Unit Parse(string text)
        {
            Assert.That(UnitCatalog.TryParse(text, out var unit), Is.True, text);

            return unit;
        }

        /// <summary>
        /// Gets the factor that converts a value in a unit to the coherent SI unit.
        /// </summary>
        /// <param name="text">The symbol or name of the unit.</param>
        /// <returns>The factor.</returns>
        private static double Factor(string text)
        {
            return Parse(text).Factor;
        }
    }
}
