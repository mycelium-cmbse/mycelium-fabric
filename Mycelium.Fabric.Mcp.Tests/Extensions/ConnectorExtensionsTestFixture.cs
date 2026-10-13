// ------------------------------------------------------------------------------------------------
//  <copyright file="ConnectorExtensionsTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Kernel.Connectors;
    using SysML2.NET.Core.POCO.Systems.Connections;
    using SysML2.NET.Core.POCO.Systems.Interfaces;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="ConnectorExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ConnectorExtensionsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the <c>massMemory</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid MassMemoryId = Guid.Parse("8f7fcbf6-8c6a-5546-1eb6-2e5adfe7a3d4");

        private InMemoryModelProvider modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            this.modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            Assert.That(this.modelProvider.ApplyChanges(ConnectionChangesHelper.CreateChanges()).Problems, Is.Empty);
        }

        [Test]
        public void VerifyGetEndPaths()
        {
            var imageLink = this.modelProvider.Elements.OfType<IInterfaceUsage>().Single();
            var connection = this.modelProvider.Elements.OfType<IConnectionUsage>().Single(connectionUsage => connectionUsage is not IInterfaceUsage);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IConnector)null).GetEndPaths(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new ConnectionUsage().GetEndPaths(), Is.Empty);
                Assert.That(imageLink.GetEndPaths(), Is.EqualTo([ConnectionChangesHelper.CameraDataOut, ConnectionChangesHelper.ComputerDataIn]));
                Assert.That(connection.GetEndPaths(), Is.EqualTo(["camera", "massMemory"]));
            }
        }

        [Test]
        public void VerifyConnects()
        {
            var imageLink = this.modelProvider.Elements.OfType<IInterfaceUsage>().Single();
            var binding = this.modelProvider.Elements.OfType<IBindingConnectorAsUsage>().Single();
            var payloadSubsystem = this.modelProvider.GetElementById(Guid.Parse(ConnectionChangesHelper.PayloadSubsystemId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IConnector)null).Connects(payloadSubsystem), Throws.TypeOf<ArgumentNullException>());
                Assert.That(imageLink.Connects(this.modelProvider.GetElementById(CameraId)), Is.True);
                Assert.That(imageLink.Connects(this.modelProvider.GetElementById(Guid.Parse(ConnectionChangesHelper.OnBoardComputerId))), Is.True);
                Assert.That(imageLink.Connects(this.modelProvider.GetElementById(MassMemoryId)), Is.False);
                Assert.That(imageLink.Connects(imageLink), Is.False);
                Assert.That(binding.Connects(payloadSubsystem), Is.True);
                Assert.That(binding.Connects(this.modelProvider.GetElementById(MassMemoryId)), Is.False);
            }
        }
    }
}
