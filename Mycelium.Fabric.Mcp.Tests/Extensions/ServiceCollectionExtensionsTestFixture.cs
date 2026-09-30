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

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="ServiceCollectionExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        private string satelliteModelFilePath;

        [SetUp]
        public void SetUp()
        {
            this.satelliteModelFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json");
        }

        [Test]
        public void VerifyAddFabricMcpServer()
        {
            var services = new ServiceCollection();

            Assert.That(() => ((IServiceCollection)null).AddFabricMcpServer(this.satelliteModelFilePath), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => services.AddFabricMcpServer(" "), Throws.TypeOf<ArgumentException>());
            Assert.That(services, Has.Count.EqualTo(0));

            var serverBuilder = services.AddFabricMcpServer(this.satelliteModelFilePath);

            var modelProviderRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(IModelProvider)).ToList();
            var toolRegistrations = services.Where(descriptor => descriptor.ServiceType == typeof(McpServerTool)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(serverBuilder, Is.Not.Null);
                Assert.That(modelProviderRegistrations, Has.Count.EqualTo(1));
                Assert.That(modelProviderRegistrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
                Assert.That(toolRegistrations, Has.Count.EqualTo(1));
            }

            var serviceProvider = new Mock<IServiceProvider>();
            var modelProvider = modelProviderRegistrations[0].ImplementationFactory(serviceProvider.Object);

            Assert.That(modelProvider, Is.InstanceOf<InMemoryModelProvider>());
        }
    }
}