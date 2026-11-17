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

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    using NUnit.Framework;

    [TestFixture]
    public class ProgramTestFixture
    {
        private const string Secret = "sensitive-test-value";

        private const string ConnectionStringArgument = $"--ConnectionStrings:{FunctionalDataInitializer.ConnectionStringName}=";

        [Test]
        public void VerifyMain()
        {
            Assert.That(() => Program.Main(null), Throws.ArgumentNullException);

            string[] blankConnectionStrings = [string.Empty, " "];

            foreach (var blankConnectionString in blankConnectionStrings)
            {
                Assert.That(() => Program.Main([ConnectionStringArgument + blankConnectionString]),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains($"'{FunctionalDataInitializer.ConnectionStringName}' connection string"));
            }

            Assert.That(() => Program.Main([$"{ConnectionStringArgument}Host=localhost;Password={Secret};UnknownOption=value"]),
                Throws.ArgumentException.With.Message.Not.Contains(Secret).And.InnerException.Message.Not.Contains(Secret));

            string[] configuredArguments =
            [
                $"{ConnectionStringArgument}Host=localhost;Database=mycelium_functional_data;Password={Secret}",
                "--FunctionalData:InitializationTimeoutSeconds=0"
            ];

            Assert.That(() => Program.Main(configuredArguments),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("InitializationTimeoutSeconds").And.Message.Not.Contains(Secret));
        }
    }
}
