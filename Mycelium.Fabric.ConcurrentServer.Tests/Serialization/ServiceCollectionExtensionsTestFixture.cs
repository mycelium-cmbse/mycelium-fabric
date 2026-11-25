// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Carter;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Testing;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Serialization;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using Npgsql;

    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        [Test]
        public void VerifyAddCoreDataAccess()
        {
            Assert.That(() => ((IServiceCollection)null).AddCoreDataAccess(), Throws.TypeOf<ArgumentNullException>());

            var services = new ServiceCollection();

            Assert.That(services.AddCoreDataAccess(), Is.SameAs(services));

            using var dataSource = NpgsqlDataSource.Create("Host=localhost");

            services.AddSingleton<DbDataSource>(dataSource);
            services.AddSingleton(new Mock<ISerializer>().Object);
            services.AddSingleton(new Mock<IDeSerializer>().Object);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            using var scope = provider.CreateScope();

            var elementDaoProvider = scope.ServiceProvider.GetRequiredService<IElementDaoProvider>();

            var elementDataTypes = typeof(IElement).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IElement).IsAssignableFrom(type))
                .ToList();

            var daoRegistrations = services
                .Where(descriptor => descriptor.ServiceType.IsGenericType && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IElementDao<>))
                .ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.GetService<IDataStoreTransactionFactory>(), Is.TypeOf<SqlTransactionFactory>());
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IDatabaseHandler)
                                                                                       && descriptor.ImplementationType == typeof(DatabaseHandler)
                                                                                       && descriptor.Lifetime == ServiceLifetime.Scoped));
                Assert.That(daoRegistrations.Select(descriptor => descriptor.ServiceType.GenericTypeArguments[0]), Is.EquivalentTo(elementDataTypes));
                Assert.That(daoRegistrations, Has.All.Matches<ServiceDescriptor>(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped));
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IElementDaoProvider)
                                                                                       && descriptor.Lifetime == ServiceLifetime.Scoped));
                Assert.That(() => provider.GetRequiredService<IElementDaoProvider>(), Throws.TypeOf<InvalidOperationException>());
                Assert.That(elementDataTypes, Has.All.Matches<Type>(type => elementDaoProvider.Resolve(type).DataType == type
                                                                            && elementDaoProvider.Resolve(type).ClassKind.ToString() == type.Name));
                Assert.That(elementDaoProvider.Resolve(typeof(PartUsage)), Is.TypeOf<PartUsageDao>());
                Assert.That(elementDaoProvider.Resolve<PartUsage>(), Is.SameAs(elementDaoProvider.Resolve(typeof(PartUsage))));
            }
        }

        [Test]
        public async Task VerifyAddCoreDataServices()
        {
            Assert.That(() => ((IServiceCollection)null).AddCoreDataServices(), Throws.TypeOf<ArgumentNullException>());

            var services = new ServiceCollection();

            Assert.That(services.AddCoreDataServices(), Is.SameAs(services));

            services.AddSingleton<ILogger<ChangeExpander>>(new FakeLogger<ChangeExpander>());
            services.AddSingleton<ILogger<ChangeValidator>>(new FakeLogger<ChangeValidator>());

            using var provider = services.BuildServiceProvider();

            var coreDataServiceProvider = provider.GetRequiredService<ICoreDataServiceProvider>();

            var elementDataTypes = typeof(IElement).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IElement).IsAssignableFrom(type))
                .ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.GetService<IChangeValidator>(), Is.TypeOf<ChangeValidator>());
                Assert.That(provider.GetService<IChangeExpander>(), Is.TypeOf<ChangeExpander>());
                Assert.That(services, Has.All.Matches<ServiceDescriptor>(descriptor => descriptor.Lifetime == ServiceLifetime.Singleton));
                Assert.That(elementDataTypes, Has.Count.EqualTo(167));
                Assert.That(elementDataTypes.Select(coreDataServiceProvider.Resolve), Is.Unique.And.All.Not.Null);
                Assert.That(coreDataServiceProvider.Resolve(typeof(PartUsage)), Is.SameAs(provider.GetService<ICoreDataService<IPartUsage>>()));
                Assert.That(provider.GetService<ICoreDataService<IPartUsage>>(), Is.TypeOf<CoreDataService<IPartUsage>>());
                Assert.That(() => coreDataServiceProvider.Resolve(typeof(ProjectUsage)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => coreDataServiceProvider.Resolve(null), Throws.TypeOf<ArgumentNullException>());
            }

            var part = new PartUsage { Id = Guid.NewGuid() };
            var dependency = new Dependency { Id = Guid.NewGuid(), Client = [part.Id], Supplier = [Guid.NewGuid()] };

            List<DataVersion> change =
            [
                new() { Identity = new DataIdentity { Id = part.Id }, Payload = part },
                new() { Identity = new DataIdentity { Id = dependency.Id }, Payload = dependency }
            ];

            var outcome = await provider.GetRequiredService<IChangeValidator>()
                .ValidateAsync(new ChangeContext(new Mock<IDataStoreTransaction>().Object, change, new Dictionary<Guid, DataVersion>(),
                    new Mock<IDataRepository>(MockBehavior.Strict).Object), CancellationToken.None);

            Assert.That(outcome.Errors.ConvertAll(error => error.Code), Is.EqualTo(["dependency.supplier.dangling"]));
        }

        [Test]
        public void VerifyAddRequiredServices()
        {
            Assert.That(() => ((IServiceCollection)null).AddRequiredServices(), Throws.TypeOf<ArgumentNullException>());

            var services = new ServiceCollection();

            Assert.That(services.AddRequiredServices(), Is.SameAs(services));

            using var provider = services.AddLogging().BuildServiceProvider();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.GetService<ISerializer>(), Is.TypeOf<Serializer>());
                Assert.That(provider.GetService<IDeSerializer>(), Is.TypeOf<DeSerializer>());
                Assert.That(provider.GetServices<IRequestBodyDeserializer>(), Has.One.TypeOf<JsonRequestBodyDeserializer>());
                Assert.That(provider.GetService<IRequestBodyReader>(), Is.TypeOf<RequestBodyReader>());
                Assert.That(services, Has.One.Matches<ServiceDescriptor>(descriptor => descriptor.ServiceType == typeof(IProjectScope)
                                                                                       && descriptor.ImplementationType == typeof(ProjectScope)
                                                                                       && descriptor.Lifetime == ServiceLifetime.Scoped));

                // Carter's own default negotiator writes any *json media type without the SysML2.NET serializers, so none of its negotiators survives.
                Assert.That(provider.GetServices<IResponseNegotiator>(), Has.Exactly(1).Items.And.All.TypeOf<JsonResponseNegotiator>());
                Assert.That(provider.GetServices<ICarterModule>(), Is.Not.Empty);
            }
        }
    }
}
