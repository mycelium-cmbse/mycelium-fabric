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

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="AttributeUsageExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class AttributeUsageExtensionsTestFixture
    {
        [Test]
        public void VerifyGetNumericValue()
        {
            var integerAttribute = new AttributeUsage();
            integerAttribute.AssignOwnership(new FeatureValue(), new LiteralInteger { Value = 55 });

            var rationalAttribute = new AttributeUsage();
            rationalAttribute.AssignOwnership(new FeatureValue(), new LiteralRational { Value = 1.5 });

            var expressionAttribute = new AttributeUsage();
            expressionAttribute.AssignOwnership(new FeatureValue(), new OperatorExpression());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IAttributeUsage)null).GetNumericValue(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new AttributeUsage().GetNumericValue(), Is.Null);
                Assert.That(expressionAttribute.GetNumericValue(), Is.Null);
                Assert.That(integerAttribute.GetNumericValue(), Is.EqualTo(55));
                Assert.That(rationalAttribute.GetNumericValue(), Is.EqualTo(1.5));
            }
        }
    }
}
