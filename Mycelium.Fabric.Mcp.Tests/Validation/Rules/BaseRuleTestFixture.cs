// ------------------------------------------------------------------------------------------------
//  <copyright file="BaseRuleTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Validation.Rules
{
    using System;
    using System.Reflection;

    using Moq;
    using Moq.Protected;

    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;

    /// <summary>
    /// Suite of tests for the <see cref="BaseRule"/> class.
    /// </summary>
    [TestFixture]
    public class BaseRuleTestFixture
    {
        [Test]
        public void VerifyConstructor()
        {
            var rule = new Mock<BaseRule>("a-rule", ValidationSeverity.Warning, "The rule.").Object;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new Mock<BaseRule>(null, ValidationSeverity.Error, "The rule.").Object, Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<ArgumentException>());
                Assert.That(() => new Mock<BaseRule>(" ", ValidationSeverity.Error, "The rule.").Object, Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<ArgumentException>());
                Assert.That(() => new Mock<BaseRule>("a-rule", ValidationSeverity.Error, "").Object, Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<ArgumentException>());
                Assert.That(rule.Name, Is.EqualTo("a-rule"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(rule.Description, Is.EqualTo("The rule."));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            var element = new Namespace();
            var context = new ValidationContext([element]);

            var rule = new Mock<BaseRule>("a-rule", ValidationSeverity.Warning, "The rule.");
            rule.Protected().Setup<string>("FindProblemCore", element, context).Returns("Problem.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.Object.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.Object.FindProblem(element, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(rule.Object.FindProblem(element, context), Is.EqualTo("Problem."));
                rule.Protected().Verify<string>("FindProblemCore", Times.Once(), ItExpr.IsAny<IElement>(), ItExpr.IsAny<ValidationContext>());
            }
        }
    }
}
