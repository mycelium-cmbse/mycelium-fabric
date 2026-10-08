// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensionsTestFixture.cs" company="Starion Group S.A.">
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

    using Microsoft.Extensions.DependencyInjection;

    using ModelContextProtocol.Server;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="ServiceCollectionExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        private Uri satelliteModelPath;

        [SetUp]
        public void SetUp()
        {
            this.satelliteModelPath = new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json"));
        }

        [Test]
        public void VerifyAddFabricMcpServer()
        {
            var services = new ServiceCollection();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IServiceCollection)null).AddFabricMcpServer(this.satelliteModelPath), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => services.AddFabricMcpServer(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(services, Has.Count.EqualTo(0));
            }

            var serverBuilder = services.AddFabricMcpServer(this.satelliteModelPath);

            var changeApplierRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelChangeApplier)).ToList();
            var modelProviderRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelProvider)).ToList();
            var exporterRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelExporter)).ToList();
            var toolRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(McpServerTool)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(serverBuilder, Is.Not.Null);
                Assert.That(changeApplierRegistrations, Has.Count.EqualTo(1));
                Assert.That(changeApplierRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(changeApplierRegistrations[0].ImplementationType, Is.EqualTo(typeof(ModelChangeApplier)));
                Assert.That(modelProviderRegistrations, Has.Count.EqualTo(1));
                Assert.That(modelProviderRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(exporterRegistrations, Has.Count.EqualTo(1));
                Assert.That(exporterRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(toolRegistrations, Has.Count.EqualTo(9));
            }

            var changeApplier = new Mock<IModelChangeApplier>();
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(provider => provider.GetService(typeof(IModelChangeApplier))).Returns(changeApplier.Object);

            var modelProvider = modelProviderRegistrations[0].ImplementationFactory(serviceProvider.Object);
            var exporter = exporterRegistrations[0].ImplementationFactory(serviceProvider.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(modelProvider, Is.InstanceOf<InMemoryModelProvider>());
                Assert.That(((IModelProvider)modelProvider).Elements, Has.Count.EqualTo(548));
                Assert.That(exporter, Is.InstanceOf<JsonModelExporter>());
                Assert.That(((JsonModelExporter)exporter).ExportDirectory.LocalPath, Is.EqualTo(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "exports") + Path.DirectorySeparatorChar));
            }

            serviceProvider.Verify(provider => provider.GetService(typeof(IModelChangeApplier)), Times.Once);

            var exportDirectory = new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Exports"));
            var otherServices = new ServiceCollection();
            otherServices.AddFabricMcpServer(this.satelliteModelPath, exportDirectory);

            var otherExporter = otherServices.Single(descriptor => descriptor.ServiceType == typeof(IModelExporter)).ImplementationFactory(serviceProvider.Object);

            Assert.That(((JsonModelExporter)otherExporter).ExportDirectory, Is.EqualTo(exportDirectory));
        }
    }
}