// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintUsageExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.IO;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.Behaviors;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Kernel.Functions;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Constraints;
    using SysML2.NET.Core.POCO.Systems.Requirements;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="ConstraintUsageExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ConstraintUsageExtensionsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>massBudget</c> requirement (REQ-SYS-001) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid MassBudgetId = Guid.Parse("6b93c533-0395-7e18-1bf6-4475deb47ba5");

        [Test]
        public void VerifyGetAttributeConstraint()
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier());
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            modelProvider.ApplyChanges([new ModelChange { Identity = MassBudgetId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 } } }]);

            var builtConstraint = ((IRequirementUsage)modelProvider.GetElementById(MassBudgetId)).requiredConstraint.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IConstraintUsage)null).GetAttributeConstraint(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new ConstraintUsage().GetAttributeConstraint(), Is.Null);
                Assert.That(builtConstraint.GetAttributeConstraint(), Is.EqualTo(new AttributeConstraint("subj", "mass", 1.2, "<=", 150, null)));

                // Expressions of other forms.
                Assert.That(CreateConstraint(CreateOperation("+", Literal(1), Literal(2))).GetAttributeConstraint(), Is.Null);
                Assert.That(CreateConstraint(CreateOperation("<=", Literal(1))).GetAttributeConstraint(), Is.Null);
                Assert.That(CreateConstraint(CreateOperation("<=", Literal(1), Literal(2))).GetAttributeConstraint(), Is.Null);
                Assert.That(CreateConstraint(CreateOperation("<=", CreateOperation("*", Literal(1), new FeatureReferenceExpression()), Literal(2))).GetAttributeConstraint(), Is.Null);
                Assert.That(CreateConstraint(CreateOperation("<=", CreateOperation("*", Literal(1)), Literal(2))).GetAttributeConstraint(), Is.Null);
            }
        }

        /// <summary>
        /// Creates a constraint whose expression is the given one.
        /// </summary>
        /// <param name="expression">The expression of the constraint.</param>
        /// <returns>The new <see cref="IConstraintUsage"/>.</returns>
        private static IConstraintUsage CreateConstraint(IElement expression)
        {
            var constraint = new ConstraintUsage();
            constraint.AssignOwnership(new ResultExpressionMembership(), expression);

            return constraint;
        }

        /// <summary>
        /// Creates an operation whose arguments are the values of its input parameters.
        /// </summary>
        /// <param name="operatorSymbol">The operator.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The new <see cref="OperatorExpression"/>.</returns>
        private static OperatorExpression CreateOperation(string operatorSymbol, params IElement[] arguments)
        {
            var operation = new OperatorExpression { Operator = operatorSymbol };

            foreach (var argument in arguments)
            {
                var parameter = new Feature { Direction = FeatureDirectionKind.In };
                operation.AssignOwnership(new ParameterMembership(), parameter);
                parameter.AssignOwnership(new FeatureValue(), argument);
            }

            return operation;
        }

        /// <summary>
        /// Creates a literal number.
        /// </summary>
        /// <param name="value">The value of the literal.</param>
        /// <returns>The new <see cref="LiteralRational"/>.</returns>
        private static LiteralRational Literal(double value)
        {
            return new LiteralRational { Value = value };
        }
    }
}
