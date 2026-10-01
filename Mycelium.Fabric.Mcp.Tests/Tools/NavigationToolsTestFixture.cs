// ------------------------------------------------------------------------------------------------
//  <copyright file="NavigationToolsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;

    using Moq;

    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="NavigationTools"/> class.
    /// </summary>
    [TestFixture]
    public class NavigationToolsTestFixture
    {
        private Mock<IModelProvider> modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NavigationTools(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new NavigationTools(this.modelProvider.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyGetModelOverview()
        {
            this.modelProvider.Setup(provider => provider.Elements).Returns([]);
            this.modelProvider.Setup(provider => provider.RootElements).Returns([]);

            var tools = new NavigationTools(this.modelProvider.Object);
            var overview = tools.GetModelOverview();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(overview.TopLevelElements, Has.Count.EqualTo(0));
                Assert.That(overview.ElementCount, Is.EqualTo(0));
                Assert.That(overview.NamedElementCount, Is.EqualTo(0));
                Assert.That(overview.MostFrequentTypes, Has.Count.EqualTo(0));
            }

            // A root namespace owns the "EOSat1" package through a membership; an unnamed part completes the model.
            var rootNamespace = new Namespace();
            var membership = new OwningMembership();
            var package = new Package { DeclaredName = "EOSat1" };
            var part = new PartUsage();

            rootNamespace.AssignOwnership(membership, package);

            this.modelProvider.Setup(provider => provider.Elements).Returns([rootNamespace, membership, package, part]);
            this.modelProvider.Setup(provider => provider.RootElements).Returns([rootNamespace]);

            overview = tools.GetModelOverview();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(overview.TopLevelElements, Is.EqualTo(["EOSat1"]));
                Assert.That(overview.ElementCount, Is.EqualTo(4));
                Assert.That(overview.NamedElementCount, Is.EqualTo(1));
                Assert.That(overview.MostFrequentTypes, Has.Count.EqualTo(4));
                Assert.That(overview.MostFrequentTypes["PartUsage"], Is.EqualTo(1));
                this.modelProvider.VerifyGet(provider => provider.Elements, Times.Exactly(2));
                this.modelProvider.VerifyGet(provider => provider.RootElements, Times.Exactly(2));
            }
        }
    }
}