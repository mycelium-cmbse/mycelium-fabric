// ------------------------------------------------------------------------------------------------
//  <copyright file="ProgramTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests
{
    using System;
    using System.Linq;
    using System.Threading;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Hosting;
    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    using Npgsql;

    [TestFixture]
    public class ProgramTestFixture
    {
        [Test]
        public void VerifyMain()
        {
            using (var unconfiguredApplication = new WebApplicationFactory<ApiBase>())
            {
                Assert.That(() => unconfiguredApplication.Services, Throws.InstanceOf<InvalidOperationException>().With.Message.Contains(SqlSchemaMigrator.ConnectionStringName));
            }

            using var application = new FabricApplicationFactory();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(application.Services.GetService<NpgsqlDataSource>(), Is.Not.Null);
                Assert.That(application.Services.GetServices<IHostedService>().OfType<SqlSchemaInitializer>().ToList(), Has.Count.EqualTo(1));
            }

            application.Migrator.Verify(migrator => migrator.MigrateAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
