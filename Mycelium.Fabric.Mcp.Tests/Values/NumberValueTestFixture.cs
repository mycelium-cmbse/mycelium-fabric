// ------------------------------------------------------------------------------------------------
//  <copyright file="NumberValueTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="NumberValue"/> class.
    /// </summary>
    [TestFixture]
    public class NumberValueTestFixture
    {
        [Test]
        public void VerifySum()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => NumberValue.Sum(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(NumberValue.Sum([]).Value, Is.EqualTo(new NumberValue(0)));
                Assert.That(NumberValue.Sum([new NumberValue(38), new NumberValue(1.5)]).Value, Is.EqualTo(new NumberValue(39.5)));
                Assert.That(NumberValue.Sum([new NumberValue(38, Unit("kg")), new NumberValue(1500, Unit("g")), new NumberValue(1)]).Value.ToString(), Is.EqualTo("40.5 [kg]"));
                Assert.That(NumberValue.Sum([new NumberValue(1), new NumberValue(500, Unit("g"))]).Value.ToString(), Is.EqualTo("501 [g]"));
                Assert.That(NumberValue.Sum([new NumberValue(0.1), new NumberValue(0.2)]).Value.Number, Is.EqualTo(0.3));
                Assert.That(NumberValue.Sum([new NumberValue(38, Unit("kg")), new NumberValue(55, Unit("W"))]).FirstError.Description,
                    Is.EqualTo("Cannot add 38 [kg] and 55 [W]: their units have different dimensions."));
            }
        }

        [Test]
        public void VerifyFormat()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(NumberValue.Format(150.95999999999998), Is.EqualTo("150.96"));
                Assert.That(NumberValue.Format(0), Is.EqualTo("0"));
                Assert.That(NumberValue.Format(-20), Is.EqualTo("-20"));
                Assert.That(NumberValue.Format(1234567.8912), Is.EqualTo("1234567.891"));
                Assert.That(NumberValue.Format(0.0000123456), Is.EqualTo("1.235E-05"));
            }
        }

        [Test]
        public void VerifyAdd()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NumberValue(1).Add(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new NumberValue(2, Unit("km")).Add(new NumberValue(500, Unit("m"))).Value.ToString(), Is.EqualTo("2.5 [km]"));
                Assert.That(new NumberValue(2, Unit("km")).Add(new NumberValue(500, Unit("s"))).IsError, Is.True);
            }
        }

        [Test]
        public void VerifySubtract()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new NumberValue(50, Unit("kg")).Subtract(new NumberValue(40)).Value.ToString(), Is.EqualTo("10 [kg]"));
                Assert.That(new NumberValue(50, Unit("kg")).Subtract(new NumberValue(4, Unit("W"))).FirstError.Description, Does.StartWith("Cannot subtract 50 [kg] and 4 [W]"));
            }
        }

        [Test]
        public void VerifyRemainder()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new NumberValue(7).Remainder(new NumberValue(3)).Value, Is.EqualTo(new NumberValue(1)));
                Assert.That(new NumberValue(7).Remainder(new NumberValue(0)).FirstError.Description, Is.EqualTo("The expression divides by zero."));
                Assert.That(new NumberValue(7, Unit("m")).Remainder(new NumberValue(3, Unit("s"))).IsError, Is.True);
            }
        }

        [Test]
        public void VerifyMultiply()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NumberValue(1).Multiply(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new NumberValue(125.8).Multiply(new NumberValue(1.2)).Number, Is.EqualTo(150.96).Within(1e-9));
                Assert.That(new NumberValue(2).Multiply(new NumberValue(3, Unit("kg"))).ToString(), Is.EqualTo("6 [kg]"));
                Assert.That(new NumberValue(3, Unit("kg")).Multiply(new NumberValue(2)).ToString(), Is.EqualTo("6 [kg]"));
                Assert.That(new NumberValue(100, Unit("W")).Multiply(new NumberValue(2, Unit("h"))).ToString(), Is.EqualTo("200 [W*h]"));
            }
        }

        [Test]
        public void VerifyDivide()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NumberValue(1).Divide(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new NumberValue(100, Unit("km")).Divide(new NumberValue(2, Unit("h"))).Value.ToString(), Is.EqualTo("50 [km/h]"));
                Assert.That(new NumberValue(100, Unit("km")).Divide(new NumberValue(2)).Value.ToString(), Is.EqualTo("50 [km]"));
                Assert.That(new NumberValue(1).Divide(new NumberValue(2, Unit("s"))).Value.ToString(), Is.EqualTo("0.5 [s^-1]"));
                Assert.That(new NumberValue(1).Divide(new NumberValue(0)).FirstError.Description, Is.EqualTo("The expression divides by zero."));
            }
        }

        [Test]
        public void VerifyPower()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NumberValue(1).Power(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new NumberValue(2).Power(new NumberValue(0.5)).Value.Number, Is.EqualTo(Math.Sqrt(2)));
                Assert.That(new NumberValue(3, Unit("m")).Power(new NumberValue(2)).Value.ToString(), Is.EqualTo("9 [m^2]"));
                Assert.That(new NumberValue(3, Unit("m")).Power(new NumberValue(0.5)).FirstError.Description, Is.EqualTo("3 [m] cannot be raised to the power 0.5, which is not an integer."));
                Assert.That(new NumberValue(3).Power(new NumberValue(2, Unit("m"))).FirstError.Description, Is.EqualTo("The exponent 2 [m] has a unit."));
            }
        }

        [Test]
        public void VerifyNegate()
        {
            Assert.That(new NumberValue(20, Unit("°C")).Negate().ToString(), Is.EqualTo("-20 [°C]"));
        }

        [Test]
        public void VerifyCompare()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NumberValue(1).Compare(null, "<"), Throws.TypeOf<ArgumentNullException>());

                var massBudget = new NumberValue(150.96).Compare(new NumberValue(150), "<=").Value;
                Assert.That(massBudget.Value, Is.False);
                Assert.That(massBudget.Gap, Is.EqualTo(-0.96));
                Assert.That(massBudget.Explanation, Is.EqualTo("150.96 > 150"));

                var converted = new NumberValue(38000, Unit("g")).Compare(new NumberValue(40, Unit("kg")), "<").Value;
                Assert.That(converted.Value, Is.True);
                Assert.That(converted.Gap, Is.EqualTo(2000));
                Assert.That(converted.Explanation, Is.EqualTo("38000 [g] < 40000 [g]"));

                Assert.That(new NumberValue(70).Compare(new NumberValue(70), "<").Value.Explanation, Is.EqualTo("70 >= 70"));
                Assert.That(new NumberValue(420).Compare(new NumberValue(360), ">=").Value.Gap, Is.EqualTo(60));
                Assert.That(new NumberValue(150).Compare(new NumberValue(150), ">").Value.Value, Is.False);
                Assert.That(new NumberValue(4).Compare(new NumberValue(4), "==").Value.Explanation, Is.EqualTo("4 == 4"));
                Assert.That(new NumberValue(3).Compare(new NumberValue(4), "==").Value.Gap, Is.EqualTo(-1));
                Assert.That(new NumberValue(3).Compare(new NumberValue(4), "!=").Value.Explanation, Is.EqualTo("3 != 4"));
                Assert.That(new NumberValue(4).Compare(new NumberValue(4), "!=").Value.Explanation, Is.EqualTo("4 == 4"));
                Assert.That(new NumberValue(4).Compare(new NumberValue(4), "!=").Value.Gap, Is.Null);
                Assert.That(new NumberValue(0.1 + 0.2).Compare(new NumberValue(0.3), "==").Value.Value, Is.True);
                Assert.That(new NumberValue(1, Unit("kg")).Compare(new NumberValue(1, Unit("W")), "<").FirstError.Description, Does.StartWith("Cannot compare 1 [kg] and 1 [W]"));
                Assert.That(new NumberValue(1).Compare(new NumberValue(1), "<>").FirstError.Description, Is.EqualTo("'<>' is not a comparison operator."));
                Assert.That(new NumberValue(125.8).Compare(new NumberValue(double.PositiveInfinity), "<").Value, Is.EqualTo(new BooleanValue(true) { Explanation = "125.8 < Infinity" }));
            }
        }

        [Test]
        public void VerifyConvertTo()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new NumberValue(38).ConvertTo(Unit("kg")).Value.ToString(), Is.EqualTo("38 [kg]"));
                Assert.That(new NumberValue(38, Unit("kg")).ConvertTo(null).Value.ToString(), Is.EqualTo("38 [kg]"));
                Assert.That(new NumberValue(38, Unit("kg")).ConvertTo(Unit("g")).Value.ToString(), Is.EqualTo("38000 [g]"));
                Assert.That(new NumberValue(38, Unit("kg")).ConvertTo(Unit("W")).FirstError.Description, Is.EqualTo("38 [kg] cannot be converted to W."));
            }
        }

        [Test]
        public void VerifyAreEqual()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(NumberValue.AreEqual(150, 150.0000000001), Is.True);
                Assert.That(NumberValue.AreEqual(1e-12, 0), Is.True);
                Assert.That(NumberValue.AreEqual(150, 150.001), Is.False);
                Assert.That(NumberValue.AreEqual(125.8, double.PositiveInfinity), Is.False);
                Assert.That(NumberValue.AreEqual(double.PositiveInfinity, double.PositiveInfinity), Is.True);
            }
        }

        [Test]
        public void VerifyToString()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new NumberValue(38).ToString(), Is.EqualTo("38"));
                Assert.That(new NumberValue(38, Unit("kg")).ToString(), Is.EqualTo("38 [kg]"));
                Assert.That(new NumberValue(38).KindName, Is.EqualTo("a number"));
            }
        }

        /// <summary>
        /// Parses a known unit.
        /// </summary>
        /// <param name="symbol">The symbol of the unit.</param>
        /// <returns>The unit.</returns>
        private static Unit Unit(string symbol)
        {
            UnitCatalog.TryParse(symbol, out var unit);

            return unit;
        }
    }
}
