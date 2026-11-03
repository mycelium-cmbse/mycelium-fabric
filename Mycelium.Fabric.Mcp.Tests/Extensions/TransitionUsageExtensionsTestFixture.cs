// ------------------------------------------------------------------------------------------------
//  <copyright file="TransitionUsageExtensionsTestFixture.cs" company="Starion Group S.A.">
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
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Systems.States;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="TransitionUsageExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class TransitionUsageExtensionsTestFixture
    {
        [Test]
        public void VerifyGetEndPaths()
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            Assert.That(modelProvider.ApplyChanges(ConnectionChangesHelper.CreateChanges()).Problems, Is.Empty);
            Assert.That(modelProvider.ApplyChanges(BehaviourChangesHelper.CreateChanges()).Problems, Is.Empty);

            var transitions = modelProvider.Elements.OfType<ITransitionUsage>().ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((ITransitionUsage)null).GetEndPaths(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(transitions.Select(transition => string.Join(" to ", transition.GetEndPaths())), Is.EquivalentTo(["launch to safe", "safe to nominal", "nominal to safe"]));
            }
        }
    }
}
