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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;
    using Mycelium.Fabric.FunctionalDataServer.Tests.TestHelpers;

    using NUnit.Framework;

    [TestFixture]
    public class FunctionalDataInitializerTestFixture
    {
        [Test]
        public void VerifyConstructor()
        {
            var configuration = CreateConfiguration(null);
            var logger = new Mock<ILogger<FunctionalDataInitializer>>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new FunctionalDataInitializer(null, logger.Object), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(configuration, null), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializer(configuration, logger.Object), Throws.Nothing);
            }

            logger.VerifyNoOtherCalls();
        }

        [Test]
        public async Task VerifyInitializeAsync()
        {
            var logger = new Mock<ILogger<FunctionalDataInitializer>>();

            logger.Setup(log => log.IsEnabled(LogLevel.Debug)).Returns(true);

            var initializer = new FunctionalDataInitializer(CreateConfiguration(null), logger.Object);

            Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.InnerException.TypeOf<InvalidOperationException>()
                    .And.InnerException.Message.Contains("'functionaldata' connection string"));

            logger.VerifyLogged(LogLevel.Debug, "reading the connection configuration", Times.Once());
            logger.VerifyLogged(LogLevel.Error, "Functional Data initialization failed.", Times.Once());

            string[] invalidTimeouts = ["0", "-1", "invalid", "4294967295"];

            foreach (var timeout in invalidTimeouts)
            {
                initializer = new FunctionalDataInitializer(CreateConfiguration(null, timeout), logger.Object);

                Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("InitializationTimeoutSeconds"));
            }

            initializer = new FunctionalDataInitializer(CreateConfiguration(null, "60"), logger.Object);

            using var cancellation = new CancellationTokenSource();

            await cancellation.CancelAsync();

            Assert.That(() => initializer.InitializeAsync(cancellation.Token), Throws.TypeOf<OperationCanceledException>());

            const string secret = "sensitive-test-value";

            initializer = new FunctionalDataInitializer(CreateConfiguration($"Password={secret};UnknownOption=value"), logger.Object);

            Assert.That(() => initializer.InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.Message.Not.Contains(secret)
                    .And.InnerException.Not.Null.And.InnerException.Message.Not.Contains(secret));

            logger.VerifyLogged(LogLevel.Debug, "opening the Functional Data database", Times.Once());

            foreach (var invocation in logger.Invocations.Where(invocation => invocation.Method.Name == nameof(ILogger.Log)))
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(invocation.Arguments[2].ToString(), Does.Not.Contain(secret));
                    Assert.That(invocation.Arguments[3]?.ToString() ?? string.Empty, Does.Not.Contain(secret));
                }
            }
        }

        private static IConfiguration CreateConfiguration(string connectionString, string timeout = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:functionaldata"] = connectionString,
                    ["FunctionalData:InitializationTimeoutSeconds"] = timeout
                })
                .Build();
        }
    }
}
