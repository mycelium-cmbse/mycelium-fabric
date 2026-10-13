// ------------------------------------------------------------------------------------------------
//  <copyright file="PayloadValueTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Changes
{
    using System;
    using System.Text.Json;

    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// Suite of tests for the <see cref="PayloadValue"/> structure and its <see cref="PayloadValueJsonConverter"/>.
    /// </summary>
    [TestFixture]
    public class PayloadValueTestFixture
    {
        [Test]
        public void VerifyImplicitConversions()
        {
            PayloadValue number = 38.5;
            PayloadValue boolean = true;
            PayloadValue text = "S-band";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(number, Is.EqualTo(new PayloadValue { Number = 38.5 }));
                Assert.That(boolean, Is.EqualTo(new PayloadValue { Boolean = true }));
                Assert.That(text, Is.EqualTo(new PayloadValue { Text = "S-band" }));
            }
        }

        [Test]
        public void VerifyToString()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(((PayloadValue)38.5).ToString(), Is.EqualTo("38.5"));
                Assert.That(((PayloadValue)false).ToString(), Is.EqualTo("false"));
                Assert.That(((PayloadValue)"S-band").ToString(), Is.EqualTo("\"S-band\""));
            }
        }

        [Test]
        public void VerifyJsonConverter()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(JsonSerializer.Deserialize<ElementPayload>("{\"Value\":38}").Value, Is.EqualTo((PayloadValue)38));
                Assert.That(JsonSerializer.Deserialize<ElementPayload>("{\"Value\":true}").Value, Is.EqualTo((PayloadValue)true));
                Assert.That(JsonSerializer.Deserialize<ElementPayload>("{\"Value\":\"S-band\"}").Value, Is.EqualTo((PayloadValue)"S-band"));
                Assert.That(JsonSerializer.Deserialize<ElementPayload>("{\"Value\":null}").Value, Is.Null);
                Assert.That(() => JsonSerializer.Deserialize<ElementPayload>("{\"Value\":[1]}"), Throws.TypeOf<JsonException>());
                Assert.That(JsonSerializer.Serialize((PayloadValue)38), Is.EqualTo("38"));
                Assert.That(JsonSerializer.Serialize((PayloadValue)true), Is.EqualTo("true"));
                Assert.That(JsonSerializer.Serialize((PayloadValue)"S-band"), Is.EqualTo("\"S-band\""));
                Assert.That(JsonSerializer.Deserialize<ConstraintPayload>("{\"Kind\":\"Assumption\"}").Kind.ToString(), Is.EqualTo("Assumption"));
                Assert.That(() => new PayloadValueJsonConverter().Write(null, 38, null), Throws.TypeOf<ArgumentNullException>());
            }
        }
    }
}
