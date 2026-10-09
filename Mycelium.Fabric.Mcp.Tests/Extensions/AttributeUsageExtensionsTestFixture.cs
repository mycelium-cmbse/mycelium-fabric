// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeUsageExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Systems.Attributes;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Suite of tests for the <see cref="AttributeUsageExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class AttributeUsageExtensionsTestFixture
    {
        /// <summary>
        /// The attributes of the test model, by name.
        /// </summary>
        private Dictionary<string, IAttributeUsage> attributes;

        [SetUp]
        public void SetUp()
        {
            var dtos = new List<DtoElement>();
            var package = ModelDtoHelper.AddPackage(dtos, "Values");
            var builder = new ConstraintDtoBuilder(dtos);
            var orbitKinds = builder.Enumeration(package.Id, "OrbitKind", "sunSynchronous", "polar");
            var parser = new ConstraintTextParser(builder, name => name == "OrbitKind::polar" ? orbitKinds[1].Id : null);

            (string Name, string Value)[] values =
            [
                ("integer", "55"), ("rational", "1.5"), ("negative", "-20"), ("quantity", "38 [kg]"), ("compound", "7.5 [km/s]"), ("boolean", "true"),
                ("text", "\"S-band\""), ("enumeration", "OrbitKind::polar"), ("reference", "otherAttribute"), ("unknownUnit", "3 [furlong]"), ("infinite", "*")
            ];

            foreach (var (name, value) in values)
            {
                builder.Attribute(package.Id, name, parser.Parse(value));
            }

            builder.Attribute(package.Id, "noValue");

            this.attributes = ModelDtoHelper.Assemble(dtos)
                .OfType<IAttributeUsage>()
                .Where(attribute => attribute.DeclaredName != null)
                .ToDictionary(attribute => attribute.DeclaredName);
        }

        [Test]
        public void VerifyGetConstantValue()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IAttributeUsage)null).GetConstantValue(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.attributes["integer"].GetConstantValue(), Is.EqualTo(new NumberValue(55)));
                Assert.That(this.attributes["negative"].GetConstantValue(), Is.EqualTo(new NumberValue(-20)));
                Assert.That(this.attributes["quantity"].GetConstantValue().ToString(), Is.EqualTo("38 [kg]"));
                Assert.That(this.attributes["compound"].GetConstantValue().ToString(), Is.EqualTo("7.5 [km/s]"));
                Assert.That(this.attributes["boolean"].GetConstantValue(), Is.EqualTo(new BooleanValue(true)));
                Assert.That(this.attributes["text"].GetConstantValue(), Is.EqualTo(new TextValue("S-band")));
                Assert.That(this.attributes["enumeration"].GetConstantValue(), Is.EqualTo(new EnumValue("polar")));

                // No value, a value that reads another value, a value whose unit is not known.
                Assert.That(this.attributes["noValue"].GetConstantValue(), Is.Null);
                Assert.That(this.attributes["reference"].GetConstantValue(), Is.Null);
                Assert.That(this.attributes["unknownUnit"].GetConstantValue(), Is.Null);
            }
        }

        [Test]
        public void VerifyGetNumericValue()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IAttributeUsage)null).GetNumericValue(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.attributes["rational"].GetNumericValue().Number, Is.EqualTo(1.5));
                Assert.That(this.attributes["quantity"].GetNumericValue().Number, Is.EqualTo(38));
                Assert.That(this.attributes["quantity"].GetNumericValue().Unit.Symbol, Is.EqualTo("kg"));
                Assert.That(this.attributes["boolean"].GetNumericValue(), Is.Null);
                Assert.That(this.attributes["noValue"].GetNumericValue(), Is.Null);
                Assert.That(this.attributes["infinite"].GetConstantValue(), Is.EqualTo(new NumberValue(double.PositiveInfinity)));
                Assert.That(this.attributes["infinite"].GetNumericValue(), Is.Null);
            }
        }
    }
}
