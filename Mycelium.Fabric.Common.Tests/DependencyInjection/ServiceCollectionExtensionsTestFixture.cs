// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Common.Tests.DependencyInjection
{
    using System;

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.Common.DependencyInjection;

    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        private interface IMarker
        {
        }

        private interface IDerived : IMarker
        {
        }

        private interface IMostDerived : IDerived
        {
        }

        private interface ISibling : IMarker
        {
        }

        private interface IAmbiguousMarker
        {
        }

        private interface IAmbiguous : IAmbiguousMarker
        {
        }

        [Test]
        public void VerifyAddEnumerableImplementationsOf()
        {
            Assert.That(() => ((IServiceCollection)null).AddEnumerableImplementationsOf<IMarker>(), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ServiceCollection().AddEnumerableImplementationsOf<MarkerOnly>(), Throws.TypeOf<ArgumentException>());

            var services = new ServiceCollection();

            Assert.That(services.AddEnumerableImplementationsOf<IAmbiguousMarker>(), Is.SameAs(services));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(services, Has.Count.EqualTo(2));
                Assert.That(services, Has.All.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IAmbiguousMarker)
                                                                                       && descriptor.Lifetime == ServiceLifetime.Scoped));
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ImplementationType == typeof(FirstAmbiguous)));
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ImplementationType == typeof(SecondAmbiguous)));
            }

            services.AddEnumerableImplementationsOf<IAmbiguousMarker>(ServiceLifetime.Transient);

            Assert.That(services, Has.Count.EqualTo(2));

            var markers = new ServiceCollection().AddEnumerableImplementationsOf<IMarker>(ServiceLifetime.Transient, typeof(ServiceCollectionExtensionsTestFixture).Assembly);

            Assert.That(markers, Has.Count.EqualTo(2));
        }

        [Test]
        public void VerifyAddImplementationsOf()
        {
            Assert.That(() => ((IServiceCollection)null).AddImplementationsOf<IMarker>(), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ServiceCollection().AddImplementationsOf<MarkerOnly>(), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new ServiceCollection().AddImplementationsOf<IAmbiguousMarker>(), Throws.TypeOf<InvalidOperationException>().With.Message.Contains("IAmbiguous"));

            var services = new ServiceCollection();

            Assert.That(services.AddImplementationsOf<IMarker>(), Is.SameAs(services));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(services, Has.Count.EqualTo(3));
                Assert.That(services, Has.All.Matches<ServiceDescriptor>(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped));
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IMostDerived)
                                                                                       && descriptor.ImplementationType == typeof(MostDerivedAndSibling)));
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(ISibling)
                                                                                       && descriptor.ImplementationType == typeof(MostDerivedAndSibling)));
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IMarker)
                                                                                       && descriptor.ImplementationType == typeof(MarkerOnly)));
            }

            var preRegistered = new ServiceCollection();
            preRegistered.AddSingleton<ISibling, MostDerivedAndSibling>();

            preRegistered.AddImplementationsOf<IMarker>(ServiceLifetime.Transient, typeof(ServiceCollectionExtensionsTestFixture).Assembly);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(preRegistered, Has.Count.EqualTo(3));
                Assert.That(preRegistered, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(ISibling)
                                                                                            && descriptor.ImplementationType == typeof(MostDerivedAndSibling)
                                                                                            && descriptor.Lifetime == ServiceLifetime.Singleton));
                Assert.That(preRegistered, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IMostDerived)
                                                                                            && descriptor.Lifetime == ServiceLifetime.Transient));
            }
        }

        private sealed class MarkerOnly : IMarker
        {
        }

        private sealed class MostDerivedAndSibling : IMostDerived, ISibling
        {
        }

        private abstract class AbstractMarker : IMarker
        {
        }

        private sealed class OpenMarker<T> : IMarker
        {
        }

        private sealed class FirstAmbiguous : IAmbiguous
        {
        }

        private sealed class SecondAmbiguous : IAmbiguous
        {
        }
    }
}
