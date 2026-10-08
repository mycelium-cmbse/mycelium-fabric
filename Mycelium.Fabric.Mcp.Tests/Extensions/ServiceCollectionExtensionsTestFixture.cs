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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    using ModelContextProtocol.Server;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Serializer.Json;

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

            var serializerRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(ISerializer)).ToList();
            var deSerializerRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IDeSerializer)).ToList();
            var changeApplierRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelChangeApplier)).ToList();
            var modelProviderRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelProvider)).ToList();
            var exportOptionsRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<ModelExportOptions>)).ToList();
            var exporterRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelExporter)).ToList();
            var toolRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(McpServerTool)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(serverBuilder, Is.Not.Null);
                Assert.That(serializerRegistrations, Has.Count.EqualTo(1));
                Assert.That(serializerRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(serializerRegistrations[0].ImplementationType, Is.EqualTo(typeof(Serializer)));
                Assert.That(deSerializerRegistrations, Has.Count.EqualTo(1));
                Assert.That(deSerializerRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(deSerializerRegistrations[0].ImplementationType, Is.EqualTo(typeof(DeSerializer)));
                Assert.That(changeApplierRegistrations, Has.Count.EqualTo(1));
                Assert.That(changeApplierRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(changeApplierRegistrations[0].ImplementationType, Is.EqualTo(typeof(ModelChangeApplier)));
                Assert.That(modelProviderRegistrations, Has.Count.EqualTo(1));
                Assert.That(modelProviderRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(exportOptionsRegistrations, Has.Count.EqualTo(1));
                Assert.That(exporterRegistrations, Has.Count.EqualTo(1));
                Assert.That(exporterRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(toolRegistrations, Has.Count.EqualTo(9));
            }

            var dataDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
            var absoluteExportDirectory = Path.Combine(Path.GetTempPath(), "mycelium-exports");

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(provider => provider.GetService(typeof(IModelChangeApplier))).Returns(new Mock<IModelChangeApplier>().Object);
            serviceProvider.Setup(provider => provider.GetService(typeof(IDeSerializer))).Returns(new DeSerializer());
            serviceProvider.Setup(provider => provider.GetService(typeof(ISerializer))).Returns(new Mock<ISerializer>().Object);

            serviceProvider.SetupSequence(provider => provider.GetService(typeof(IOptions<ModelExportOptions>)))
                .Returns(Options.Create(new ModelExportOptions()))
                .Returns(Options.Create(new ModelExportOptions { ExportDirectory = "Exports" }))
                .Returns(Options.Create(new ModelExportOptions { ExportDirectory = absoluteExportDirectory }));

            var modelProvider = modelProviderRegistrations[0].ImplementationFactory(serviceProvider.Object);
            var defaultExporter = exporterRegistrations[0].ImplementationFactory(serviceProvider.Object);
            var relativeExporter = (JsonModelExporter)exporterRegistrations[0].ImplementationFactory(serviceProvider.Object);
            var absoluteExporter = (JsonModelExporter)exporterRegistrations[0].ImplementationFactory(serviceProvider.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(modelProvider, Is.InstanceOf<InMemoryModelProvider>());
                Assert.That(((IModelProvider)modelProvider).Elements, Has.Count.EqualTo(548));
                Assert.That(defaultExporter, Is.InstanceOf<JsonModelExporter>());
                Assert.That(((JsonModelExporter)defaultExporter).ExportDirectory.LocalPath, Is.EqualTo(Path.Combine(dataDirectory, "exports")));
                Assert.That(relativeExporter.ExportDirectory.LocalPath, Is.EqualTo(Path.Combine(dataDirectory, "Exports")));
                Assert.That(absoluteExporter.ExportDirectory.LocalPath, Is.EqualTo(absoluteExportDirectory));
            }

            serviceProvider.Verify(provider => provider.GetService(typeof(IModelChangeApplier)), Times.Once);
            serviceProvider.Verify(provider => provider.GetService(typeof(IDeSerializer)), Times.Once);
            serviceProvider.Verify(provider => provider.GetService(typeof(ISerializer)), Times.Exactly(3));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["FabricMcp:ExportDirectory"] = absoluteExportDirectory })
                .Build();

            serviceProvider.Setup(provider => provider.GetService(typeof(IConfiguration))).Returns(configuration);

            var exportOptions = new ModelExportOptions();
            ((IConfigureOptions<ModelExportOptions>)exportOptionsRegistrations[0].ImplementationFactory(serviceProvider.Object)).Configure(exportOptions);

            Assert.That(exportOptions.ExportDirectory, Is.EqualTo(absoluteExportDirectory));
        }
    }
}
