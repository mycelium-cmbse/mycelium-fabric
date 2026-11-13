// ------------------------------------------------------------------------------------------------
//  <copyright file="FunctionalDataInitializerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Tests.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;
    using Mycelium.Fabric.FunctionalDataServer.Tests.TestHelpers;

    using Npgsql;

    using NUnit.Framework;

    [TestFixture]
    public class FunctionalDataInitializerTestFixture
    {
        private const string UnusedConnectionString = "Host=localhost;Database=mycelium_functional_data";

        private NpgsqlDataSource dataSource;

        [SetUp]
        public void SetUp()
        {
            this.dataSource = NpgsqlDataSource.Create(UnusedConnectionString);
        }

        [TearDown]
        public async Task TearDown()
        {
            await this.dataSource.DisposeAsync();
        }

        [Test]
        public void VerifyConstructor()
        {
            var configuration = CreateConfiguration(null);
            var logger = new Mock<ILogger<FunctionalDataInitializer>>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new FunctionalDataInitializer(null, configuration, logger.Object), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(this.dataSource, null, logger.Object), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(this.dataSource, configuration, null), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(this.dataSource, configuration, logger.Object), Throws.Nothing);
            }

            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task VerifyInitializeAsync()
        {
            var logger = new Mock<ILogger<FunctionalDataInitializer>>();

            logger.Setup(log => log.IsEnabled(LogLevel.Debug)).Returns(true);

            string[] invalidTimeouts = ["0", "-1", "invalid", "4294967295"];

            foreach (var timeout in invalidTimeouts)
            {
                var misconfiguredInitializer = new FunctionalDataInitializer(this.dataSource, CreateConfiguration(timeout), logger.Object);

                Assert.That(() => misconfiguredInitializer.InitializeAsync(CancellationToken.None),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("InitializationTimeoutSeconds"));
            }

            var initializer = new FunctionalDataInitializer(this.dataSource, CreateConfiguration("60"), logger.Object);

            using var cancellation = new CancellationTokenSource();

            await cancellation.CancelAsync();

            var canceledToken = cancellation.Token;

            Assert.That(() => initializer.InitializeAsync(canceledToken), Throws.TypeOf<OperationCanceledException>());

            await this.dataSource.DisposeAsync();

            var failingInitializer = new FunctionalDataInitializer(this.dataSource, CreateConfiguration(null), logger.Object);

            Assert.That(() => failingInitializer.InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.InnerException.InstanceOf<ObjectDisposedException>());

            logger.VerifyLogged(LogLevel.Debug, "opening the Functional Data database", Times.Once());
            logger.VerifyLogged(LogLevel.Error, "Functional Data initialization failed.", Times.Once());
        }

        private static IConfiguration CreateConfiguration(string timeout)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["FunctionalData:InitializationTimeoutSeconds"] = timeout
                })
                .Build();
        }
    }
}
