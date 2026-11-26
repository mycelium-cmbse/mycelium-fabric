// ------------------------------------------------------------------------------------------------
//  <copyright file="ManagedValueProviderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Tests.Persistence
{
    using System;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    using NUnit.Framework;

    [TestFixture]
    public class ManagedValueProviderTestFixture
    {
        private ManagedValueProvider provider;

        [SetUp]
        public void SetUp()
        {
            this.provider = new ManagedValueProvider();
        }

        [Test]
        public void VerifyQueryActingUserId()
        {
            Assert.That(this.provider.QueryActingUserId, Throws.InvalidOperationException);

            var actingUserId = Guid.NewGuid();

            this.provider.SetActingUserId(actingUserId);

            Assert.That(this.provider.QueryActingUserId(), Is.EqualTo(actingUserId));
        }

        [Test]
        public void VerifySetActingUserId()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.provider.SetActingUserId(Guid.Empty),
                    Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("actingUserId"));

                Assert.That(this.provider.QueryActingUserId, Throws.InvalidOperationException);
            }

            var firstActingUserId = Guid.NewGuid();
            var secondActingUserId = Guid.NewGuid();

            this.provider.SetActingUserId(firstActingUserId);
            this.provider.SetActingUserId(secondActingUserId);

            Assert.That(this.provider.QueryActingUserId(), Is.EqualTo(secondActingUserId));

            Assert.That(() => this.provider.SetActingUserId(Guid.Empty), Throws.ArgumentException);
            Assert.That(this.provider.QueryActingUserId(), Is.EqualTo(secondActingUserId));
        }
    }
}
