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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;
    using Mycelium.SDK.Dal;

    using NUnit.Framework;

    [TestFixture]
    public class FunctionalDataInitializerTestFixture
    {
        [Test]
        public void VerifyConstructor()
        {
            var configuration = new Mock<IConfiguration>();
            var logger = new Mock<ILogger<FunctionalDataInitializer>>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new FunctionalDataInitializer(null, logger.Object), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(configuration.Object, null), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(configuration.Object, logger.Object), Throws.Nothing);
            }

            configuration.VerifyNoOtherCalls();
            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task VerifyInitializeAsync()
        {
            var logger = new Mock<ILogger<FunctionalDataInitializer>>();

            logger.Setup(log => log.IsEnabled(LogLevel.Debug)).Returns(true);

            var configuration = CreateConfiguration(null);
            var initializer = new FunctionalDataInitializer(configuration.Object, logger.Object);

            Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.InnerException.TypeOf<InvalidOperationException>()
                    .And.InnerException.Message.Contains("'functionaldata' connection string"));

            logger.Verify(log => log.Log(LogLevel.Debug, It.IsAny<EventId>(), It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("reading the connection configuration")),
                null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            string[] invalidTimeouts = ["0", "-1", "invalid", "4294967295"];

            foreach (var timeout in invalidTimeouts)
            {
                configuration.Setup(config => config["FunctionalData:InitializationTimeoutSeconds"]).Returns(timeout);

                Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("InitializationTimeoutSeconds"));
            }

            configuration.Setup(config => config["FunctionalData:InitializationTimeoutSeconds"]).Returns("60");

            using var cancellation = new CancellationTokenSource();

            await cancellation.CancelAsync();

            Assert.That(() => initializer.InitializeAsync(cancellation.Token), Throws.TypeOf<OperationCanceledException>());

            const string secret = "sensitive-test-value";

            configuration = CreateConfiguration($"Password={secret};UnknownOption=value");
            initializer = new FunctionalDataInitializer(configuration.Object, logger.Object);

            Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.Message.Not.Contains(secret)
                    .And.InnerException.Not.Null.And.InnerException.Message.Not.Contains(secret));

            logger.Verify(log => log.Log(LogLevel.Debug, It.IsAny<EventId>(), It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("opening the Functional Data database")),
                null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            foreach (var invocation in logger.Invocations.Where(invocation => invocation.Method.Name == nameof(ILogger.Log)))
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(invocation.Arguments[2].ToString(), Does.Not.Contain(secret));
                    Assert.That(invocation.Arguments[3]?.ToString() ?? string.Empty, Does.Not.Contain(secret));
                }
            }
        }

        [Test]
        public void VerifySelectInitialScript()
        {
            const string sql = "CREATE SCHEMA IF NOT EXISTS fabric;\n";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => FunctionalDataInitializer.SelectInitialScript(null), Throws.ArgumentNullException);
                Assert.That(() => FunctionalDataInitializer.SelectInitialScript([]), Throws.TypeOf<InvalidOperationException>().With.Message.Contains("0001_initial.sql"));
                Assert.That(() => FunctionalDataInitializer.SelectInitialScript([("schema.sql", sql)]), Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => FunctionalDataInitializer.SelectInitialScript([("0001_INITIAL.sql", sql)]), Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => FunctionalDataInitializer.SelectInitialScript([("0001_initial.sql", sql), ("0002_upgrade.sql", sql)]),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(FunctionalDataInitializer.SelectInitialScript([("0001_initial.sql", sql)]), Is.SameAs(sql));
            }

            var packaged = FunctionalDataInitializer.SelectInitialScript(SqlSchemaScripts.GetAll());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(packaged, Does.Contain("-- Mycelium.Model.Fabric 0.3.0\n"));
                Assert.That(packaged, Does.Contain("-- Mycelium.Model.CommonPrimitives 0.4.0\n"));
                Assert.That(packaged, Does.Not.Contain("\r"));
                Assert.That(packaged, Does.EndWith("\n"));
            }
        }

        private static Mock<IConfiguration> CreateConfiguration(string connectionString)
        {
            var section = new Mock<IConfigurationSection>();

            section.Setup(connection => connection["functionaldata"]).Returns(connectionString);

            var configuration = new Mock<IConfiguration>();

            configuration.Setup(config => config.GetSection("ConnectionStrings")).Returns(section.Object);

            return configuration;
        }
    }
}
