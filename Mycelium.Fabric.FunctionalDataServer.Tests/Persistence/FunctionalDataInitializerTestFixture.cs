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
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

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
            var configuration = CreateConfiguration(null);
            var initializer = new FunctionalDataInitializer(configuration.Object, logger.Object);

            Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("reading connection configuration"));

            logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null,
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
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("opening the Functional Data database").And.Message.Not.Contains(secret));

            foreach (var invocation in logger.Invocations)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(invocation.Arguments[2].ToString(), Does.Not.Contain(secret));
                    Assert.That(invocation.Arguments[3], Is.Null);
                }
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
