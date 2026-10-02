// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelProviderExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="ModelProviderExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ModelProviderExtensionsTestFixture
    {
        private Mock<IModelProvider> modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
        }

        [Test]
        public void VerifyGetRequiredElementById()
        {
            var part = new PartUsage { Id = Guid.NewGuid() };
            var unknownId = Guid.NewGuid();

            this.modelProvider.Setup(provider => provider.GetElementById(part.Id)).Returns(part);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IModelProvider)null).GetRequiredElementById(part.Id), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.modelProvider.Object.GetRequiredElementById(unknownId), Throws.TypeOf<McpException>().With.Message.Contains(unknownId.ToString()));
                Assert.That(this.modelProvider.Object.GetRequiredElementById(part.Id), Is.SameAs(part));
            }

            this.modelProvider.Verify(provider => provider.GetElementById(part.Id), Times.Once);
        }
    }
}
