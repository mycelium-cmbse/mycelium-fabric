// ------------------------------------------------------------------------------------------------
//  <copyright file="TermReaderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Expressions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.Packages;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Suite of tests for the <see cref="TermReader"/> class.
    /// </summary>
    [TestFixture]
    public class TermReaderTestFixture
    {
        [Test]
        public void VerifyRead()
        {
            var dtos = new List<DtoElement>();
            var package = ModelDtoHelper.AddPackage(dtos, "Expressions");
            var builder = new ConstraintDtoBuilder(dtos);
            var subject = builder.Attribute(package.Id, "subj");
            var camera = builder.Attribute(package.Id, "camera");
            var mass = builder.Attribute(package.Id, "mass");
            var unnamed = builder.Attribute(package.Id, null);
            var kilogram = builder.Attribute(package.Id, "kilogram");
            kilogram.DeclaredShortName = "kg";
            var parser = new ConstraintTextParser(builder, name => name == "subj" ? subject.Id : null);

            // Expressions written in the textual notation, read and written back the same way.
            string[] texts =
            [
                "true", "150", "1.5", "\"S-band\"", "null", "subj", "subj.camera.mass", "150 [kg]", "9.81 [m/s^2]", "-20 [°C]", "max(subj.mass, 100)",
                "subj.mass * 1.2 <= 150 [kg] and not subj.isRedundant", "if subj.isRedundant ? 1 else 2", "(1, 2, 3)#(2)"
            ];

            var textExpressions = texts.ToDictionary(text => text, parser.Parse);

            var readExpressions = new Dictionary<string, DtoElement>
            {
                ["infinity"] = builder.Infinity(),
                ["chain with a target"] = builder.Chain(builder.Reference(subject.Id), mass.Id),
                ["chain with a feature chain"] = builder.Chain(builder.Reference(subject.Id), builder.ChainedFeature(camera.Id, mass.Id).Id),
                ["unit of the model"] = builder.Quantity(builder.Integer(150), builder.Reference(kilogram.Id)),
                ["quotient of units"] = builder.Quantity(builder.Integer(10), builder.Operation("/", builder.NamedReference("km"), builder.NamedReference("h"))),
                ["power of a unit"] = builder.Quantity(builder.Integer(10), builder.Operation("^", builder.NamedReference("m"), builder.Integer(2))),

                // Expressions that cannot be read.
                ["unknown unit"] = builder.Quantity(builder.Integer(3), builder.NamedReference("furlong")),
                ["unknown unit in a product"] = builder.Quantity(builder.Integer(3), builder.Operation("*", builder.NamedReference("furlong"), builder.NamedReference("s"))),
                ["unit with a decimal exponent"] = builder.Quantity(builder.Integer(3), builder.Operation("^", builder.NamedReference("m"), builder.Literal(2.5))),
                ["unit that is a number"] = builder.Quantity(builder.Integer(3), builder.Integer(2)),
                ["number without unit"] = builder.Operation("[", builder.Integer(3)),
                ["number that cannot be read"] = builder.Quantity(builder.NamedReference("x"), builder.NamedReference("kg")),
                ["chain without source"] = builder.NamedChain(null, "mass"),
                ["chain without target name"] = builder.Chain(builder.Reference(subject.Id), unnamed.Id),
                ["chain whose source cannot be read"] = builder.NamedChain(builder.NamedReference("vehicle"), "mass"),
                ["call of a function that is not in the model"] = builder.Invocation(null, builder.Integer(1)),
                ["call with an argument that cannot be read"] = builder.Invocation("max", builder.NamedReference("x")),
                ["operation with an operand without value"] = builder.Operation("+", builder.Integer(1), null),
                ["unresolved reference without name"] = builder.Reference(Guid.NewGuid())
            };

            var elements = ModelDtoHelper.Assemble(dtos).ToDictionary(element => element.Id);

            using (Assert.EnterMultipleScope())
            {
                foreach (var (text, expression) in textExpressions)
                {
                    Assert.That(Read(expression), Is.EqualTo(text));
                }

                Assert.That(Read(readExpressions["infinity"]), Is.EqualTo("Infinity"));
                Assert.That(Read(readExpressions["chain with a target"]), Is.EqualTo("subj.mass"));
                Assert.That(Read(readExpressions["chain with a feature chain"]), Is.EqualTo("subj.camera.mass"));
                Assert.That(Read(readExpressions["unit of the model"]), Is.EqualTo("150 [kg]"));
                Assert.That(Read(readExpressions["quotient of units"]), Is.EqualTo("10 [km/h]"));
                Assert.That(Read(readExpressions["power of a unit"]), Is.EqualTo("10 [m^2]"));

                Assert.That(Read(readExpressions["unknown unit"]), Is.EqualTo("error: The unit 'furlong' is not known."));
                Assert.That(Read(readExpressions["unknown unit in a product"]), Is.EqualTo("error: The unit 'furlong' is not known."));
                Assert.That(Read(readExpressions["unit with a decimal exponent"]), Is.EqualTo("error: The exponent of a unit must be an integer."));
                Assert.That(Read(readExpressions["unit that is a number"]), Is.EqualTo("error: The unit of the number is not a reference to a unit."));
                Assert.That(Read(readExpressions["number without unit"]), Is.EqualTo("error: The number with a unit has no unit."));
                Assert.That(Read(readExpressions["number that cannot be read"]), Is.EqualTo("error: The reference to 'x' does not resolve to an element of the model."));
                Assert.That(Read(readExpressions["chain without source"]), Is.EqualTo("error: The feature chain has no source."));
                Assert.That(Read(readExpressions["chain without target name"]), Is.EqualTo("error: The target of the feature chain has no name."));
                Assert.That(Read(readExpressions["chain whose source cannot be read"]), Is.EqualTo("error: The reference to 'vehicle' does not resolve to an element of the model."));
                Assert.That(Read(readExpressions["call of a function that is not in the model"]), Is.EqualTo("error: The function that the expression calls is not in the model."));
                Assert.That(Read(readExpressions["call with an argument that cannot be read"]), Is.EqualTo("error: The reference to 'x' does not resolve to an element of the model."));
                Assert.That(Read(readExpressions["operation with an operand without value"]), Is.EqualTo("error: An operand of the expression has no value."));
                Assert.That(Read(readExpressions["unresolved reference without name"]), Is.EqualTo("error: The reference to 'an element' does not resolve to an element of the model."));
                Assert.That(TermReader.Read(new Package()).FirstError.Description, Is.EqualTo("The expression 'Package' is not supported."));
            }

            // Reads an expression of the model and writes it back, or gives its error.
            string Read(DtoElement expression)
            {
                var term = TermReader.Read(elements[expression.Id]);

                return term.IsError ? $"error: {term.FirstError.Description}" : term.Value.ToString();
            }
        }

        [Test]
        public void VerifyGetValueExpression()
        {
            var dtos = new List<DtoElement>();
            var package = ModelDtoHelper.AddPackage(dtos, "Values");
            var builder = new ConstraintDtoBuilder(dtos);
            var withValue = builder.Attribute(package.Id, "withValue", builder.Integer(38));
            var withoutValue = builder.Attribute(package.Id, "withoutValue");

            var elements = ModelDtoHelper.Assemble(dtos).ToDictionary(element => element.Id);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => TermReader.GetValueExpression(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(TermReader.Read(TermReader.GetValueExpression((IFeature)elements[withValue.Id])).Value.ToString(), Is.EqualTo("38"));
                Assert.That(TermReader.GetValueExpression((IFeature)elements[withoutValue.Id]), Is.Null);
            }
        }
    }
}
