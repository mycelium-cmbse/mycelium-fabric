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
    using System.Collections.Generic;

    using Moq;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="ModelProviderExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ModelProviderExtensionsTestFixture
    {
        private static readonly string[] ModelledIds = ["REQ-SYS-001", "REQ-SYS-003"];

        [Test]
        public void VerifyGetRequirementIds()
        {
            // REQ-SYS-001 is a requirement usage, REQ-SYS-003 a requirement definition; the others have no identifier, or are not requirements.
            IReadOnlyList<IElement> elements =
            [
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == "REQ-SYS-001"),
                Mock.Of<IRequirementDefinition>(requirement => requirement.ReqId == "REQ-SYS-003"),
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == " "),
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == null),
                new Mock<IElement>().Object
            ];

            var modelProvider = new Mock<IModelProvider>();
            modelProvider.Setup(provider => provider.Elements).Returns(elements);

            var identifiers = modelProvider.Object.GetRequirementIds();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(identifiers, Is.EquivalentTo(ModelledIds));
                Assert.That(identifiers.Contains("req-sys-001"), Is.False);
                Assert.That(() => ((IModelProvider)null).GetRequirementIds(), Throws.TypeOf<ArgumentNullException>());
                modelProvider.Verify(provider => provider.Elements, Times.Once);
            }
        }
    }
}
