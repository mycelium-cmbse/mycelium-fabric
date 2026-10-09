// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelValueTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Values
{
    using System;
    using System.Text.Json;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the values that are not numbers (<see cref="BooleanValue"/>, <see cref="TextValue"/>,
    /// <see cref="EnumValue"/>, <see cref="PartValue"/>, <see cref="SequenceValue"/>), for the <see cref="Dimension"/> of the
    /// units and for the <see cref="UnitJsonConverter"/>, which are small types written and read the same way.
    /// </summary>
    [TestFixture]
    public class ModelValueTestFixture
    {
        [Test]
        public void VerifyToString()
        {
            var part = new PartUsage { DeclaredName = "camera" };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(new BooleanValue(true).ToString(), Is.EqualTo("true"));
                Assert.That(new BooleanValue(false).ToString(), Is.EqualTo("false"));
                Assert.That(new TextValue("S-band").ToString(), Is.EqualTo("\"S-band\""));
                Assert.That(new EnumValue("sunSynchronous").ToString(), Is.EqualTo("sunSynchronous"));
                Assert.That(new PartValue(part).ToString(), Is.EqualTo("camera"));
                Assert.That(new SequenceValue([new NumberValue(1), new BooleanValue(true)]).ToString(), Is.EqualTo("(1, true)"));
                Assert.That(SequenceValue.Empty.ToString(), Is.EqualTo("null"));
            }
        }

        [Test]
        public void VerifyKindName()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new BooleanValue(true).KindName, Is.EqualTo("a Boolean"));
                Assert.That(new TextValue("S-band").KindName, Is.EqualTo("a text"));
                Assert.That(new EnumValue("polar").KindName, Is.EqualTo("an enumeration value"));
                Assert.That(new PartValue(new PartUsage()).KindName, Is.EqualTo("a part"));
                Assert.That(SequenceValue.Empty.KindName, Is.EqualTo("a sequence"));
            }
        }

        [Test]
        public void VerifyDimension()
        {
            var speed = new Dimension { Length = 1, Time = -1 };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Dimension.None, Is.EqualTo(default(Dimension)));
                Assert.That(speed * new Dimension { Time = 1 }, Is.EqualTo(new Dimension { Length = 1 }));
                Assert.That(speed.Power(2), Is.EqualTo(new Dimension { Length = 2, Time = -2 }));
                Assert.That(new Dimension(1, 2, 3, 4, 5, 6, 7, 8, 9).Power(-1), Is.EqualTo(new Dimension(-1, -2, -3, -4, -5, -6, -7, -8, -9)));
                Assert.That(new Dimension(1, 2, 3, 4, 5, 6, 7, 8, 9) * new Dimension(1, 1, 1, 1, 1, 1, 1, 1, 1), Is.EqualTo(new Dimension(2, 3, 4, 5, 6, 7, 8, 9, 10)));
            }
        }

        [Test]
        public void VerifyUnitJsonConverter()
        {
            UnitCatalog.TryParse("km/h", out var speed);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(JsonSerializer.Serialize(speed), Is.EqualTo("\"km/h\""));
                Assert.That(JsonSerializer.Deserialize<Unit>("\"kg\"").Factor, Is.EqualTo(1).Within(1e-12));
                Assert.That(() => JsonSerializer.Deserialize<Unit>("\"furlong\""), Throws.TypeOf<JsonException>().With.Message.EqualTo("The unit 'furlong' is not known."));
                Assert.That(() => new UnitJsonConverter().Write(null, speed, null), Throws.TypeOf<ArgumentNullException>());
            }
        }
    }
}
