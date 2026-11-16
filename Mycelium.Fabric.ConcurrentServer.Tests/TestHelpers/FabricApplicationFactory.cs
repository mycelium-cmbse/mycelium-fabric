// ------------------------------------------------------------------------------------------------
//  <copyright file="FabricApplicationFactory.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers
{
    using System.Linq;
    using System.Threading;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Services;

    public class FabricApplicationFactory : WebApplicationFactory<ApiBase>
    {
        public FabricApplicationFactory()
        {
            this.Migrator.Setup(migrator => migrator.MigrateAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }

        public Mock<ISqlSchemaMigrator> Migrator { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // The connection string is never opened, since the migrator that would use it is mocked.
            builder.UseSetting($"ConnectionStrings:{SqlSchemaMigrator.ConnectionStringName}", "Host=localhost;Database=unused")
                .ConfigureTestServices(services =>
                {
                    services.AddSingleton(this.Migrator.Object);
                    RemoveServicesWithoutPersistence(services);
                });
        }

        // No repository or transaction factory is registered yet, so the PIM services and the database handler cannot be built;
        // the endpoint tests register the service mocks they need.
        private static void RemoveServicesWithoutPersistence(IServiceCollection services)
        {
            var unresolvable = services.Where(descriptor => typeof(IBaseService).IsAssignableFrom(descriptor.ServiceType) || descriptor.ServiceType == typeof(IDatabaseHandler))
                .ToList();

            foreach (var descriptor in unresolvable)
            {
                services.Remove(descriptor);
            }
        }
    }
}
