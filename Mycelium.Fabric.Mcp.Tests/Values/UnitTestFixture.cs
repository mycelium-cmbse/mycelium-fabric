// ------------------------------------------------------------------------------------------------
//  <copyright file="UnitTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="Unit"/> class.
    /// </summary>
    [TestFixture]
    public class UnitTestFixture
    {
        [Test]
        public void VerifyIsCompatibleWith()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Parse("kg").IsCompatibleWith(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Parse("kg").IsCompatibleWith(Parse("lb")), Is.True);
                Assert.That(Parse("kWh").IsCompatibleWith(Parse("J")), Is.True);
                Assert.That(Parse("kg").IsCompatibleWith(Parse("W")), Is.False);
                Assert.That(Parse("deg").IsCompatibleWith(Parse("%")), Is.False);
            }
        }

        [Test]
        public void VerifyConvertTo()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Parse("kg").ConvertTo(1, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Parse("kg").ConvertTo(38, Parse("g")), Is.EqualTo(38000).Within(1e-9));
                Assert.That(Parse("km/h").ConvertTo(36, Parse("m/s")), Is.EqualTo(10).Within(1e-12));
                Assert.That(Parse("°C").ConvertTo(20, Parse("K")), Is.EqualTo(293.15).Within(1e-9));
                Assert.That(Parse("°F").ConvertTo(212, Parse("°C")), Is.EqualTo(100).Within(1e-9));
                Assert.That(Parse("kWh").ConvertTo(1, Parse("J")), Is.EqualTo(3.6e6).Within(1e-3));
            }
        }

        [Test]
        public void VerifyMultiply()
        {
            var energy = Parse("W").Multiply(Parse("h"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Parse("W").Multiply(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(energy.Symbol, Is.EqualTo("W*h"));
                Assert.That(energy.IsCompatibleWith(Parse("J")), Is.True);
                Assert.That(energy.Factor, Is.EqualTo(3600));
            }
        }

        [Test]
        public void VerifyDivide()
        {
            var speed = Parse("km").Divide(Parse("h"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Parse("km").Divide(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(speed.Symbol, Is.EqualTo("km/h"));
                Assert.That(speed.Factor, Is.EqualTo(1000.0 / 3600).Within(1e-12));
                Assert.That(Parse("kg").Divide(speed).Symbol, Is.EqualTo("kg/(km/h)"));
            }
        }

        [Test]
        public void VerifyPower()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Parse("km").Power(2).Symbol, Is.EqualTo("km^2"));
                Assert.That(Parse("km").Power(2).Factor, Is.EqualTo(1e6).Within(1e-6));
                Assert.That(Parse("km/h").Power(-1).Symbol, Is.EqualTo("(km/h)^-1"));
                Assert.That(Parse("s").Power(-1).IsCompatibleWith(Parse("Hz")), Is.True);
            }
        }

        [Test]
        public void VerifyToString()
        {
            Assert.That(Parse("m/s²").ToString(), Is.EqualTo("m/s^2"));
        }

        /// <summary>
        /// Parses a known unit.
        /// </summary>
        /// <param name="text">The symbol of the unit.</param>
        /// <returns>The unit.</returns>
        private static Unit Parse(string text)
        {
            Assert.That(UnitCatalog.TryParse(text, out var unit), Is.True, text);

            return unit;
        }
    }
}
