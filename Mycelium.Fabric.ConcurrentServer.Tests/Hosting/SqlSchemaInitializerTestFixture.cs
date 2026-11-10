// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlSchemaInitializerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Hosting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Hosting;
    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    [TestFixture]
    public class SqlSchemaInitializerTestFixture
    {
        private Mock<ISqlSchemaMigrator> migrator;

        private Mock<ILogger<SqlSchemaInitializer>> logger;

        private SqlSchemaInitializer initializer;

        [SetUp]
        public void SetUp()
        {
            this.migrator = new Mock<ISqlSchemaMigrator>();
            this.logger = new Mock<ILogger<SqlSchemaInitializer>>();
            this.initializer = new SqlSchemaInitializer(this.migrator.Object, this.logger.Object);
        }

        [Test]
        public async Task VerifyStartAsync()
        {
            using var cancellation = new CancellationTokenSource();

            this.migrator.Setup(s => s.MigrateAsync(cancellation.Token)).ReturnsAsync(["0001_initial.sql"]);
            this.logger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);

            await this.initializer.StartAsync(cancellation.Token);

            this.migrator.Verify(s => s.MigrateAsync(cancellation.Token), Times.Once);
            this.logger.VerifyLogged(LogLevel.Information, "1 script(s) applied", Times.Once());

            // The outcome is not logged when the Information level is disabled.
            this.logger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(false);
            this.migrator.Setup(m => m.MigrateAsync(cancellation.Token)).ReturnsAsync([]);

            await this.initializer.StartAsync(cancellation.Token);

            this.logger.VerifyLogged(LogLevel.Information, "0 script(s) applied", Times.Never());

            // A failed migration must stop the host, so it is not swallowed.
            this.migrator.Setup(m => m.MigrateAsync(cancellation.Token)).ThrowsAsync(new InvalidOperationException("schema"));

            Assert.That(() => this.initializer.StartAsync(cancellation.Token), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task VerifyStopAsync()
        {
            await this.initializer.StopAsync(CancellationToken.None);

            this.migrator.Verify(m => m.MigrateAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
