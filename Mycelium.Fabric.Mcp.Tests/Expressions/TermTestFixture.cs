// ------------------------------------------------------------------------------------------------
//  <copyright file="TermTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Expressions
{
    using System;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the small terms: <see cref="LiteralTerm"/>, <see cref="ReferenceTerm"/>, <see cref="NavigationTerm"/>,
    /// <see cref="QuantityTerm"/> and <see cref="InvocationTerm"/>, whose evaluation reads the model through a mocked
    /// <see cref="IEvaluationContext"/>.
    /// </summary>
    [TestFixture]
    public class TermTestFixture
    {
        /// <summary>
        /// The satellite part that the subject stands for.
        /// </summary>
        private readonly PartUsage satellite = new() { DeclaredName = "eosat1" };

        /// <summary>
        /// The context of the evaluations.
        /// </summary>
        private Mock<IEvaluationContext> context;

        [SetUp]
        public void SetUp()
        {
            this.context = new Mock<IEvaluationContext>();
            this.context.Setup(context => context.GetValue(this.satellite)).Returns(new PartValue(this.satellite));
            this.context.Setup(context => context.Navigate(new PartValue(this.satellite), "mass")).Returns(new NumberValue(125.8, Unit("kg")));
            this.context.Setup(context => context.Navigate(new PartValue(this.satellite), "temperature")).Returns((ErrorOr<ModelValue>)Error.Validation(description: "no temperature"));
        }

        [Test]
        public void VerifyLiteralTerm()
        {
            var literal = new LiteralTerm(new NumberValue(150));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(literal.Evaluate(null).Value, Is.EqualTo(new NumberValue(150)));
                Assert.That(literal.ToString(), Is.EqualTo("150"));
                Assert.That(literal.Precedence, Is.EqualTo(Term.HighestPrecedence));
            }
        }

        [Test]
        public void VerifyReferenceTerm()
        {
            var subject = new ReferenceTerm("subj", this.satellite);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => subject.Evaluate(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(subject.Evaluate(this.context.Object).Value, Is.EqualTo(new PartValue(this.satellite)));
                Assert.That(subject.ToString(), Is.EqualTo("subj"));
            }

            this.context.Verify(context => context.GetValue(this.satellite), Times.Once);
        }

        [Test]
        public void VerifyNavigationTerm()
        {
            var subject = new ReferenceTerm("subj", this.satellite);
            var number = new LiteralTerm(new NumberValue(3));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NavigationTerm(subject, "mass").Evaluate(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new NavigationTerm(subject, "mass").Evaluate(this.context.Object).Value.ToString(), Is.EqualTo("125.8 [kg]"));
                Assert.That(new NavigationTerm(subject, "temperature").Evaluate(this.context.Object).FirstError.Description, Is.EqualTo("no temperature"));
                Assert.That(new NavigationTerm(number, "mass").Evaluate(this.context.Object).FirstError.Description, Is.EqualTo("'3' gives a number (3), which has no feature 'mass'."));
                Assert.That(new NavigationTerm(new NavigationTerm(subject, "temperature"), "max").Evaluate(this.context.Object).FirstError.Description, Is.EqualTo("no temperature"));
                Assert.That(new NavigationTerm(new NavigationTerm(subject, "camera"), "mass").ToString(), Is.EqualTo("subj.camera.mass"));
                Assert.That(new NavigationTerm(new OperationTerm("??", [subject, subject]), "mass").ToString(), Is.EqualTo("(subj ?? subj).mass"));
            }
        }

        [Test]
        public void VerifyQuantityTerm()
        {
            var kilograms = Unit("kg");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new QuantityTerm(new LiteralTerm(new NumberValue(150)), kilograms).Evaluate(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new QuantityTerm(new LiteralTerm(new NumberValue(150)), kilograms).Evaluate(this.context.Object).Value.ToString(), Is.EqualTo("150 [kg]"));
                Assert.That(new QuantityTerm(new LiteralTerm(new NumberValue(1500, Unit("g"))), kilograms).Evaluate(this.context.Object).Value.ToString(), Is.EqualTo("1.5 [kg]"));
                Assert.That(new QuantityTerm(new LiteralTerm(new NumberValue(1, Unit("W"))), kilograms).Evaluate(this.context.Object).FirstError.Description, Is.EqualTo("1 [W] cannot be converted to kg."));
                Assert.That(new QuantityTerm(new LiteralTerm(new BooleanValue(true)), kilograms).Evaluate(this.context.Object).FirstError.Description,
                    Is.EqualTo("'true' gives a Boolean (true), which cannot have the unit kg."));
                Assert.That(new QuantityTerm(new OperationTerm("-", [new LiteralTerm(new NumberValue(20))]), Unit("°C")).ToString(), Is.EqualTo("(-20) [°C]"));
                Assert.That(new QuantityTerm(new LiteralTerm(new NumberValue(150)), kilograms).ToString(), Is.EqualTo("150 [kg]"));
            }
        }

        [Test]
        public void VerifyInvocationTerm()
        {
            var mass = new NavigationTerm(new ReferenceTerm("subj", this.satellite), "mass");
            var temperature = new NavigationTerm(new ReferenceTerm("subj", this.satellite), "temperature");
            var numbers = new OperationTerm(",", [Number(-2.5), Number(4), Number(1)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Call("abs", Number(1)).Evaluate(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(InvocationTerm.Functions, Has.Count.EqualTo(12));
                Assert.That(this.Value(Call("abs", Number(-2.5))), Is.EqualTo("2.5"));
                Assert.That(this.Value(Call("sqrt", Number(16))), Is.EqualTo("4"));
                Assert.That(this.Value(Call("floor", Number(2.7))), Is.EqualTo("2"));
                Assert.That(this.Value(Call("ceil", Number(2.1))), Is.EqualTo("3"));
                Assert.That(this.Value(Call("round", Number(2.5))), Is.EqualTo("3"));
                Assert.That(this.Value(Call("min", numbers)), Is.EqualTo("-2.5"));
                Assert.That(this.Value(Call("max", mass, new QuantityTerm(Number(100), Unit("kg")))), Is.EqualTo("125.8 [kg]"));
                Assert.That(this.Value(Call("max", new QuantityTerm(Number(130000), Unit("g")), mass)), Is.EqualTo("130000 [g]"));
                Assert.That(this.Value(Call("min", mass, new QuantityTerm(Number(130000), Unit("g")))), Is.EqualTo("125.8 [kg]"));
                Assert.That(this.Value(Call("sum", numbers)), Is.EqualTo("2.5"));
                Assert.That(this.Value(Call("product", numbers)), Is.EqualTo("-10"));
                Assert.That(this.Value(Call("size", numbers)), Is.EqualTo("3"));
                Assert.That(this.Value(Call("isEmpty", new LiteralTerm(SequenceValue.Empty))), Is.EqualTo("true"));
                Assert.That(this.Value(Call("notEmpty", numbers)), Is.EqualTo("true"));

                // Errors: an argument without value, a function that is not supported, arguments that are not numbers, units.
                Assert.That(this.ErrorOf(Call("max", temperature, Number(1))), Is.EqualTo("no temperature"));
                Assert.That(this.ErrorOf(Call("average", numbers)), Does.StartWith("The function 'average' is not supported. Supported functions: abs, sqrt"));
                Assert.That(this.ErrorOf(Call("max", Number(1), new LiteralTerm(new BooleanValue(true)))), Is.EqualTo("The function 'max' needs numbers, not (1, true)."));
                Assert.That(this.ErrorOf(Call("max")), Is.EqualTo("The function 'max' needs numbers, not ()."));
                Assert.That(this.ErrorOf(Call("sqrt", Number(-1))), Is.EqualTo("sqrt does not apply to -1."));
                Assert.That(this.ErrorOf(Call("sqrt", mass)), Is.EqualTo("sqrt does not apply to 125.8 [kg]."));
                Assert.That(this.ErrorOf(Call("max", mass, new QuantityTerm(Number(1), Unit("W")))), Is.EqualTo("1 [W] cannot be converted to kg."));
                Assert.That(Call("max", mass, numbers).ToString(), Is.EqualTo("max(subj.mass, -2.5, 4, 1)"));
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

        /// <summary>
        /// Creates a literal number.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Number(double number)
        {
            return new LiteralTerm(new NumberValue(number));
        }

        /// <summary>
        /// Creates a call of a function.
        /// </summary>
        /// <param name="function">The name of the function.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The new <see cref="InvocationTerm"/>.</returns>
        private static InvocationTerm Call(string function, params Term[] arguments)
        {
            return new InvocationTerm(function, arguments);
        }

        /// <summary>
        /// Evaluates a term that must give a value.
        /// </summary>
        /// <param name="term">The term.</param>
        /// <returns>The value as text.</returns>
        private string Value(Term term)
        {
            var value = term.Evaluate(this.context.Object);

            return value.IsError ? $"error: {value.FirstError.Description}" : value.Value.ToString();
        }

        /// <summary>
        /// Evaluates a term that must give an error.
        /// </summary>
        /// <param name="term">The term.</param>
        /// <returns>The description of the error.</returns>
        private string ErrorOf(Term term)
        {
            var value = term.Evaluate(this.context.Object);

            return value.IsError ? value.FirstError.Description : $"no error but {value.Value}";
        }
    }
}
