// ------------------------------------------------------------------------------------------------
//  <copyright file="BodyEvaluationContextTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Expressions
{
    using System;

    using Moq;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="BodyEvaluationContext"/> class, around a mocked <see cref="IEvaluationContext"/>.
    /// </summary>
    [TestFixture]
    public class BodyEvaluationContextTestFixture
    {
        /// <summary>
        /// The parameter <c>p</c> of the body.
        /// </summary>
        private readonly ReferenceUsage parameter = new() { Id = Guid.NewGuid(), DeclaredName = "p" };

        /// <summary>
        /// The camera part, the item that the parameter stands for.
        /// </summary>
        private readonly PartUsage camera = new() { Id = Guid.NewGuid(), DeclaredName = "camera" };

        /// <summary>
        /// The satellite part, another feature of the model.
        /// </summary>
        private readonly PartUsage satellite = new() { Id = Guid.NewGuid(), DeclaredName = "eosat1" };

        /// <summary>
        /// The context of the whole expression.
        /// </summary>
        private Mock<IEvaluationContext> outerContext;

        /// <summary>
        /// The context under test.
        /// </summary>
        private BodyEvaluationContext context;

        [SetUp]
        public void SetUp()
        {
            this.outerContext = new Mock<IEvaluationContext>();
            this.outerContext.Setup(context => context.GetValue(this.satellite)).Returns(new PartValue(this.satellite));
            this.outerContext.Setup(context => context.Navigate(new PartValue(this.camera), "mass")).Returns(new NumberValue(38));
            this.context = new BodyEvaluationContext(this.outerContext.Object, this.parameter, new PartValue(this.camera));
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new BodyEvaluationContext(null, this.parameter, new NumberValue(1)), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new BodyEvaluationContext(this.outerContext.Object, null, new NumberValue(1)), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new BodyEvaluationContext(this.outerContext.Object, this.parameter, null), Is.Not.Null);
            }
        }

        [Test]
        public void VerifyGetValue()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.context.GetValue(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.context.GetValue(this.parameter).Value, Is.EqualTo(new PartValue(this.camera)));
                Assert.That(this.context.GetValue(this.satellite).Value, Is.EqualTo(new PartValue(this.satellite)));
                this.outerContext.Verify(context => context.GetValue(this.satellite), Times.Once);
                this.outerContext.Verify(context => context.GetValue(this.parameter), Times.Never);
            }
        }

        [Test]
        public void VerifyNavigate()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.context.Navigate(new PartValue(this.camera), "mass").Value, Is.EqualTo(new NumberValue(38)));
                this.outerContext.Verify(context => context.Navigate(new PartValue(this.camera), "mass"), Times.Once);
            }
        }
    }
}
