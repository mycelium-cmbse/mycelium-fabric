// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonElementExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.Text.Json;

    using Mycelium.Fabric.Mcp.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="JsonElementExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class JsonElementExtensionsTestFixture
    {
        /// <summary>
        /// The names of the types of a property that can be <c>null</c>.
        /// </summary>
        private static readonly string[] TextOrNullTypeNames = ["string", "null"];

        /// <summary>
        /// The name of the type of a text.
        /// </summary>
        private static readonly string[] TextTypeNames = ["string"];

        [Test]
        public void VerifyEqualsIgnoringCase()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Parse("\"Inout\"").EqualsIgnoringCase(Parse("\"inOUT\"")), Is.True);
                Assert.That(Parse("\"Inout\"").EqualsIgnoringCase(Parse("\"In\"")), Is.False);
                Assert.That(Parse("4").EqualsIgnoringCase(Parse("4.0")), Is.True);
                Assert.That(Parse("4").EqualsIgnoringCase(Parse("\"4\"")), Is.False);
                Assert.That(Parse("null").EqualsIgnoringCase(Parse("null")), Is.True);
            }
        }

        [Test]
        public void VerifyGetTypeNames()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Parse("\"string\"").GetTypeNames(), Is.EqualTo(TextTypeNames));
                Assert.That(Parse("[\"string\", \"null\"]").GetTypeNames(), Is.EqualTo(TextOrNullTypeNames));
                Assert.That(Parse("[]").GetTypeNames(), Is.Empty);
            }
        }

        [Test]
        public void VerifyHasSchemaType()
        {
            var textOrNull = Parse("[\"string\", \"null\"]");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Parse("\"text\"").HasSchemaType(textOrNull), Is.True);
                Assert.That(Parse("null").HasSchemaType(textOrNull), Is.True);
                Assert.That(Parse("4").HasSchemaType(textOrNull), Is.False);
                Assert.That(Parse("1.5").HasSchemaType(Parse("\"number\"")), Is.True);
                Assert.That(Parse("2").HasSchemaType(Parse("\"integer\"")), Is.True);
                Assert.That(Parse("1.5").HasSchemaType(Parse("\"integer\"")), Is.False);
                Assert.That(Parse("\"2\"").HasSchemaType(Parse("\"integer\"")), Is.False);
                Assert.That(Parse("false").HasSchemaType(Parse("\"boolean\"")), Is.True);
                Assert.That(Parse("[]").HasSchemaType(Parse("\"array\"")), Is.True);
                Assert.That(Parse("{}").HasSchemaType(Parse("\"object\"")), Is.True);
                Assert.That(Parse("{}").HasSchemaType(Parse("\"array\"")), Is.False);
                Assert.That(Parse("{}").HasSchemaType(Parse("\"unknown\"")), Is.True);
                Assert.That(Parse("{}").HasSchemaType(Parse("[]")), Is.False);
            }
        }

        [Test]
        public void VerifyIsStrictSchema()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Parse("{ \"type\": \"object\", \"additionalProperties\": false }").IsStrictSchema(), Is.True);
                Assert.That(Parse("{ \"type\": \"object\", \"additionalProperties\": true }").IsStrictSchema(), Is.False);
                Assert.That(Parse("{ \"type\": \"object\" }").IsStrictSchema(), Is.False);
                Assert.That(Parse("false").IsStrictSchema(), Is.False);
                Assert.That(default(JsonElement).IsStrictSchema(), Is.False);
            }
        }

        [Test]
        public void VerifyToShortText()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Parse("4").ToShortText(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(Parse("\"input\"").ToShortText(60), Is.EqualTo("\"input\""));
                Assert.That(Parse("4").ToShortText(60), Is.EqualTo("4"));
                Assert.That(Parse("{\"type\":\"PartUsage\"}").ToShortText(60), Is.EqualTo("{\"type\":\"PartUsage\"}"));
                Assert.That(Parse("\"a long text\"").ToShortText(7), Is.EqualTo("\"a long…"));
                Assert.That(Parse("\"input\"").ToShortText(7), Is.EqualTo("\"input\""));
            }
        }

        private static JsonElement Parse(string json)
        {
            return JsonDocument.Parse(json).RootElement;
        }
    }
}
