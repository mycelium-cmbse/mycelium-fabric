// ------------------------------------------------------------------------------------------------
//  <copyright file="OperationTermTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="OperationTerm"/> class.
    /// </summary>
    [TestFixture]
    public class OperationTermTestFixture
    {
        /// <summary>
        /// The context of the evaluations, which gives an error for the value of any feature.
        /// </summary>
        private Mock<IEvaluationContext> context;

        /// <summary>
        /// A term that cannot be evaluated: the reference to a feature without value.
        /// </summary>
        private ReferenceTerm unknown;

        [SetUp]
        public void SetUp()
        {
            this.context = new Mock<IEvaluationContext>();
            this.context.Setup(context => context.GetValue(It.IsAny<IFeature>())).Returns((ErrorOr<ModelValue>)Error.Validation(description: "'unknown' has no value."));
            this.unknown = new ReferenceTerm("unknown", new PartUsage { DeclaredName = "unknown" });
        }

        [Test]
        public void VerifyEvaluate()
        {
            var camera = new PartUsage { DeclaredName = "camera" };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => Operation("+", Number(1), Number(2)).Evaluate(null), Throws.TypeOf<ArgumentNullException>());

                // Arithmetic.
                Assert.That(this.Value(Operation("+", Number(1), Number(2))), Is.EqualTo(new NumberValue(3)));
                Assert.That(this.Value(Operation("-", Number(5), Number(2))), Is.EqualTo(new NumberValue(3)));
                Assert.That(this.Value(Operation("*", Number(2), Number(3))), Is.EqualTo(new NumberValue(6)));
                Assert.That(this.Value(Operation("/", Number(6), Number(3))), Is.EqualTo(new NumberValue(2)));
                Assert.That(this.Value(Operation("%", Number(7), Number(3))), Is.EqualTo(new NumberValue(1)));
                Assert.That(this.Value(Operation("**", Number(2), Number(3))), Is.EqualTo(new NumberValue(8)));
                Assert.That(this.Value(Operation("^", Number(2), Number(3))), Is.EqualTo(new NumberValue(8)));
                Assert.That(this.Value(Operation("-", Number(5))), Is.EqualTo(new NumberValue(-5)));
                Assert.That(this.Value(Operation("+", Number(5))), Is.EqualTo(new NumberValue(5)));
                Assert.That(this.Value(Operation("+", Text("S"), Text("-band"))), Is.EqualTo(new TextValue("S-band")));
                Assert.That(this.Value(Operation("+", Number(2, "km"), Number(500, "m"))).ToString(), Is.EqualTo("2.5 [km]"));
                Assert.That(this.ErrorOf(Operation("+", Number(2, "kg"), Number(3, "W"))), Does.StartWith("Cannot add 2 [kg] and 3 [W]"));
                Assert.That(this.ErrorOf(Operation("+", Number(1), this.unknown)), Is.EqualTo("'unknown' has no value."));

                // Comparisons of numbers, Booleans, texts, enumeration values, parts and null.
                Assert.That(this.Boolean(Operation("<=", Number(38000, "g"), Number(40, "kg"))), Is.EqualTo((true, "38000 [g] <= 40000 [g]")));
                Assert.That(this.Boolean(Operation("===", Number(1), Number(1))), Is.EqualTo((true, "1 == 1")));
                Assert.That(this.Boolean(Operation("!==", Number(1), Number(1))), Is.EqualTo((false, "1 == 1")));
                Assert.That(this.Boolean(Operation("==", Boolean(true), Boolean(false))), Is.EqualTo((false, "true != false")));
                Assert.That(this.Boolean(Operation("!=", Text("S-band"), Text("X-band"))), Is.EqualTo((true, "\"S-band\" != \"X-band\"")));
                Assert.That(this.Boolean(Operation("==", Enum("polar"), Enum("polar"))), Is.EqualTo((true, "polar == polar")));
                Assert.That(this.Boolean(Operation("==", Enum("polar"), Text("polar"))), Is.EqualTo((true, "polar == \"polar\"")));
                Assert.That(this.Boolean(Operation("==", Text("polar"), Enum("sunSynchronous"))), Is.EqualTo((false, "\"polar\" != sunSynchronous")));
                Assert.That(this.Boolean(Operation("==", Part(camera), Part(camera))), Is.EqualTo((true, "camera == camera")));
                Assert.That(this.Boolean(Operation("==", Null(), Null())), Is.EqualTo((true, "null == null")));
                Assert.That(this.Boolean(Operation("!=", Number(1), Null())), Is.EqualTo((true, "1 != null")));
                Assert.That(this.ErrorOf(Operation("<", Boolean(true), Boolean(false))), Is.EqualTo("a Boolean (true) and a Boolean (false) cannot be ordered: only numbers can."));
                Assert.That(this.ErrorOf(Operation("==", Boolean(true), Number(1))), Is.EqualTo("a Boolean (true) cannot be compared with a number (1)."));

                // Logic with three values: false and x is false, true or x is true, even when x cannot be evaluated.
                Assert.That(this.Boolean(Operation("and", Compare(1, "<", 2), Compare(2, "<", 3))), Is.EqualTo((true, "1 < 2 and 2 < 3")));
                Assert.That(this.Boolean(Operation("&", Compare(1, ">", 2), this.unknown)), Is.EqualTo((false, "1 <= 2")));
                Assert.That(this.ErrorOf(Operation("and", Compare(1, "<", 2), this.unknown)), Is.EqualTo("'unknown' has no value."));
                Assert.That(this.Boolean(Operation("or", this.unknown, Compare(1, "<", 2))), Is.EqualTo((true, "1 < 2")));
                Assert.That(this.Boolean(Operation("|", Compare(1, ">", 2), Compare(2, ">", 3))), Is.EqualTo((false, "1 <= 2 | 2 <= 3")));
                Assert.That(this.ErrorOf(Operation("or", this.unknown, Compare(1, ">", 2))), Is.EqualTo("'unknown' has no value."));
                Assert.That(this.ErrorOf(Operation("and", Number(1), Boolean(true))), Is.EqualTo("'1' gives a number (1), not a Boolean."));
                Assert.That(this.Boolean(Operation("xor", Boolean(true), Compare(1, "<", 2))), Is.EqualTo((false, "true is true xor 1 < 2")));
                Assert.That(this.Boolean(Operation("not", Compare(1, ">", 2))), Is.EqualTo((true, "1 <= 2")));
                Assert.That(this.Boolean(Operation("~", Boolean(true))), Is.EqualTo((false, "true is true")));
                Assert.That(this.ErrorOf(Operation("not", Number(1))), Is.EqualTo("The operator 'not' does not apply to a number (1)."));
                Assert.That(this.Boolean(Operation("implies", Compare(1, ">", 2), this.unknown)), Is.EqualTo((true, "1 <= 2")));
                Assert.That(this.Boolean(Operation("implies", this.unknown, Compare(1, "<", 2))), Is.EqualTo((true, "1 < 2")));
                Assert.That(this.Boolean(Operation("implies", Compare(1, "<", 2), Compare(2, ">", 3))), Is.EqualTo((false, "1 < 2 but 2 <= 3")));
                Assert.That(this.ErrorOf(Operation("implies", this.unknown, Compare(2, ">", 3))), Is.EqualTo("'unknown' has no value."));
                Assert.That(this.ErrorOf(Operation("implies", Compare(1, "<", 2), this.unknown)), Is.EqualTo("'unknown' has no value."));

                // Condition, null coalescing, sequence and index.
                Assert.That(this.Value(Operation("if", Boolean(true), Number(1), Number(2))), Is.EqualTo(new NumberValue(1)));
                Assert.That(this.Value(Operation("if", Compare(1, ">", 2), Number(1), Number(2))), Is.EqualTo(new NumberValue(2)));
                Assert.That(this.ErrorOf(Operation("if", this.unknown, Number(1), Number(2))), Is.EqualTo("'unknown' has no value."));
                Assert.That(this.ErrorOf(Operation("if", Number(0), Number(1), Number(2))), Is.EqualTo("'0' gives a number (0), not a Boolean."));
                Assert.That(this.Value(Operation("??", Null(), Number(5))), Is.EqualTo(new NumberValue(5)));
                Assert.That(this.Value(Operation("??", Number(3), Number(5))), Is.EqualTo(new NumberValue(3)));
                Assert.That(this.ErrorOf(Operation("??", this.unknown, Number(5))), Is.EqualTo("'unknown' has no value."));
                Assert.That(this.Value(Operation(",", Number(1), Operation(",", Number(2), Number(3)))).ToString(), Is.EqualTo("(1, 2, 3)"));
                Assert.That(this.Value(Operation("#", Operation(",", Number(10), Number(20)), Number(2))), Is.EqualTo(new NumberValue(20)));
                Assert.That(this.Value(Operation("#", Number(10), Number(1))), Is.EqualTo(new NumberValue(10)));
                Assert.That(this.ErrorOf(Operation("#", Number(10), Number(2))), Is.EqualTo("The index 2 is out of the sequence 10."));
                Assert.That(this.ErrorOf(Operation("#", Number(10), Number(1.5))), Is.EqualTo("The index 1.5 is out of the sequence 10."));

                // An operator that does not apply.
                Assert.That(this.ErrorOf(Operation("istype", Part(camera), Text("Camera"))), Is.EqualTo("The operator 'istype' does not apply to a part (camera) and a text (\"Camera\")."));
                Assert.That(this.ErrorOf(Operation("and", Boolean(true))), Is.EqualTo("The operator 'and' does not apply to a Boolean (true)."));
            }
        }

        [Test]
        public void VerifyToString()
        {
            var mass = new NavigationTerm(new ReferenceTerm("subj", new PartUsage()), "mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Operation("<=", Operation("*", mass, Number(1.2)), Number(150)).ToString(), Is.EqualTo("subj.mass * 1.2 <= 150"));
                Assert.That(Operation("*", Operation("+", mass, Number(5)), Number(2)).ToString(), Is.EqualTo("(subj.mass + 5) * 2"));
                Assert.That(Operation("+", Operation("*", mass, Number(5)), Number(2)).ToString(), Is.EqualTo("subj.mass * 5 + 2"));
                Assert.That(Operation("-", Number(20)).ToString(), Is.EqualTo("-20"));
                Assert.That(Operation("-", Operation("+", mass, Number(5))).ToString(), Is.EqualTo("-(subj.mass + 5)"));
                Assert.That(Operation("not", Operation("and", Boolean(true), Boolean(false))).ToString(), Is.EqualTo("not (true and false)"));
                Assert.That(Operation("if", Boolean(true), Number(1), Number(2)).ToString(), Is.EqualTo("if true ? 1 else 2"));
                Assert.That(Operation("#", Operation(",", Number(10), Number(20)), Number(2)).ToString(), Is.EqualTo("(10, 20)#(2)"));
                Assert.That(Operation("@@", Number(1), Number(2), Number(3)).ToString(), Is.EqualTo("@@(1, 2, 3)"));
                Assert.That(Operation("implies", Operation("or", Boolean(true), Boolean(false)), Boolean(true)).ToString(), Is.EqualTo("true or false implies true"));
                Assert.That(Operation("??", Null(), Operation("xor", Boolean(true), Boolean(false))).ToString(), Is.EqualTo("null ?? true xor false"));
                Assert.That(Operation("==", Operation("<", Number(1), Number(2)), Boolean(true)).ToString(), Is.EqualTo("1 < 2 == true"));
                Assert.That(Operation("**", Operation("%", Number(7), Number(3)), Number(2)).ToString(), Is.EqualTo("(7 % 3) ** 2"));
                Assert.That(Operation("|", Operation("&", Boolean(true), Boolean(false)), Boolean(true)).ToString(), Is.EqualTo("true & false | true"));
                Assert.That(Operation("istype", mass, Text("Mass")).ToString(), Is.EqualTo("subj.mass istype \"Mass\""));
            }
        }

        /// <summary>
        /// Creates an operation.
        /// </summary>
        /// <param name="operatorSymbol">The operator.</param>
        /// <param name="operands">The operands.</param>
        /// <returns>The new <see cref="OperationTerm"/>.</returns>
        private static OperationTerm Operation(string operatorSymbol, params Term[] operands)
        {
            return new OperationTerm(operatorSymbol, operands);
        }

        /// <summary>
        /// Creates the comparison of two numbers.
        /// </summary>
        /// <param name="left">The left number.</param>
        /// <param name="comparisonOperator">The comparison operator.</param>
        /// <param name="right">The right number.</param>
        /// <returns>The new <see cref="OperationTerm"/>.</returns>
        private static OperationTerm Compare(double left, string comparisonOperator, double right)
        {
            return Operation(comparisonOperator, Number(left), Number(right));
        }

        /// <summary>
        /// Creates a literal number, with an optional unit.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <param name="unit">The symbol of the unit, or <c>null</c>.</param>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Number(double number, string unit = null)
        {
            if (unit == null)
            {
                return new LiteralTerm(new NumberValue(number));
            }

            Assert.That(UnitCatalog.TryParse(unit, out var parsedUnit), Is.True, unit);

            return new LiteralTerm(new NumberValue(number, parsedUnit));
        }

        /// <summary>
        /// Creates a literal Boolean.
        /// </summary>
        /// <param name="value">The Boolean.</param>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Boolean(bool value)
        {
            return new LiteralTerm(new BooleanValue(value));
        }

        /// <summary>
        /// Creates a literal text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Text(string text)
        {
            return new LiteralTerm(new TextValue(text));
        }

        /// <summary>
        /// Creates a literal enumeration value.
        /// </summary>
        /// <param name="name">The name of the enumeration value.</param>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Enum(string name)
        {
            return new LiteralTerm(new EnumValue(name));
        }

        /// <summary>
        /// Creates a term whose value is a part.
        /// </summary>
        /// <param name="feature">The part.</param>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Part(IFeature feature)
        {
            return new LiteralTerm(new PartValue(feature));
        }

        /// <summary>
        /// Creates the literal <c>null</c>.
        /// </summary>
        /// <returns>The new <see cref="LiteralTerm"/>.</returns>
        private static LiteralTerm Null()
        {
            return new LiteralTerm(SequenceValue.Empty);
        }

        /// <summary>
        /// Evaluates a term that must give a value.
        /// </summary>
        /// <param name="term">The term.</param>
        /// <returns>The value.</returns>
        private ModelValue Value(Term term)
        {
            var value = term.Evaluate(this.context.Object);

            return value.IsError ? throw new AssertionException($"{term}: {value.FirstError.Description}") : value.Value;
        }

        /// <summary>
        /// Evaluates a term that must give a Boolean.
        /// </summary>
        /// <param name="term">The term.</param>
        /// <returns>The Boolean and its explanation.</returns>
        private (bool Value, string Explanation) Boolean(Term term)
        {
            var value = (BooleanValue)this.Value(term);

            return (value.Value, value.Explanation);
        }

        /// <summary>
        /// Evaluates a term that must give an error.
        /// </summary>
        /// <param name="term">The term.</param>
        /// <returns>The description of the error.</returns>
        private string ErrorOf(OperationTerm term)
        {
            var value = term.Evaluate(this.context.Object);

            return value.IsError ? value.FirstError.Description : $"no error but {value.Value}";
        }
    }
}
