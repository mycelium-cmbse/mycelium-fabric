// ------------------------------------------------------------------------------------------------
//  <copyright file="ThingRepositoryTestFixture.cs" company="Starion Group S.A.">
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

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    using NUnit.Framework;

    [TestFixture]
    public class ThingRepositoryTestFixture
    {
        private Mock<IDaoProvider> daoProvider;

        private ThingRepository repository;

        [SetUp]
        public void SetUp()
        {
            this.daoProvider = new Mock<IDaoProvider>(MockBehavior.Strict);
            this.repository = new ThingRepository(this.daoProvider.Object);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ThingRepository(null), Throws.ArgumentNullException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("daoProvider"));
                Assert.That(() => new ThingRepository(this.daoProvider.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyReadByIdAsync()
        {
            Assert.That(() => this.repository.ReadByIdAsync(null, Guid.NewGuid(), CancellationToken.None),
                Throws.ArgumentNullException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("transaction"));
        }

        [Test]
        public void VerifyReadByIdsAsync()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.repository.ReadByIdsAsync(null, [Guid.NewGuid()], CancellationToken.None),
                    Throws.ArgumentNullException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("transaction"));

                Assert.That(() => this.repository.ReadByIdsAsync(null, null, CancellationToken.None),
                    Throws.ArgumentNullException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("ids"));
            }
        }
    }
}
