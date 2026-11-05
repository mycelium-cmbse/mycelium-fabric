// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonArrayExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.Text.Json.Nodes;

    using Mycelium.Fabric.Mcp.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="JsonArrayExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class JsonArrayExtensionsTestFixture
    {
        [Test]
        public void VerifyGetTypesWithoutNull()
        {
            var textOrNull = new JsonArray("string", "null");
            var numberTextOrNull = new JsonArray("number", "string", "null");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((JsonArray)null).GetTypesWithoutNull(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(textOrNull.GetTypesWithoutNull().ToJsonString(), Is.EqualTo("\"string\""));
                Assert.That(numberTextOrNull.GetTypesWithoutNull().ToJsonString(), Is.EqualTo("[\"number\",\"string\"]"));
                Assert.That(new JsonArray("string").GetTypesWithoutNull().ToJsonString(), Is.EqualTo("\"string\""));
                Assert.That(textOrNull.ToJsonString(), Is.EqualTo("[\"string\",\"null\"]"));
            }
        }
    }
}
