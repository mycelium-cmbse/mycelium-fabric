// ------------------------------------------------------------------------------------------------
//  <copyright file="ProgramTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Tests
{
    using System;
    using System.Linq;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;
    using Mycelium.SDK.DTO;

    using Npgsql;

    using NUnit.Framework;

    [TestFixture]
    public class ProgramTestFixture
    {
        private const string Secret = "sensitive-test-value";

        private const string ConnectionStringArgument = $"--ConnectionStrings:{FunctionalDataInitializer.ConnectionStringName}=";

        [Test]
        public void VerifyCreateHost()
        {
            Assert.That(() => Program.CreateHost(null), Throws.ArgumentNullException);

            string[] blankConnectionStrings = [string.Empty, " "];

            foreach (var blankConnectionString in blankConnectionStrings)
            {
                Assert.That(() => Program.CreateHost([ConnectionStringArgument + blankConnectionString]),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains($"'{FunctionalDataInitializer.ConnectionStringName}' connection string"));
            }

            Assert.That(() => Program.CreateHost([$"{ConnectionStringArgument}Host=localhost;Password={Secret};UnknownOption=value"]),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains($"'{FunctionalDataInitializer.ConnectionStringName}' connection string is malformed")
                    .And.InnerException.InstanceOf<ArgumentException>().And.InnerException.Message.Not.Contains(Secret));

            NpgsqlDataSource dataSource;

            using (var host = Program.CreateHost([$"{ConnectionStringArgument}Host=localhost;Port=1;Database=mycelium_functional_data;Password={Secret}"]))
            {
                dataSource = host.Services.GetService<NpgsqlDataSource>();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(dataSource, Is.Not.Null);
                    Assert.That(host.Services.GetService<IFunctionalDataInitializer>(), Is.TypeOf<FunctionalDataInitializer>());
                    Assert.That(host.Services.GetServices<IHostedService>().OfType<FunctionalDataInitializationService>().ToList(), Has.Count.EqualTo(1));
                    Assert.That(host.Services.GetService<IDao<IUser>>(), Is.TypeOf<UserDao>());
                }
            }

            Assert.That(dataSource.OpenConnection, Throws.TypeOf<ObjectDisposedException>());
        }
    }
}
