// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensionsTestFixture.cs" company="Starion Group S.A.">
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
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;
    using Mycelium.SDK.DTO;
    using Mycelium.SDK.Serializer.Json;

    using Npgsql;

    using NUnit.Framework;

    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        private static readonly Dictionary<Type, Type> ExpectedDaos = new()
        {
            [typeof(IDao<IBranchProtectionRule>)] = typeof(BranchProtectionRuleDao),
            [typeof(IDao<IComment>)] = typeof(CommentDao),
            [typeof(IDao<IFunctionalProject>)] = typeof(FunctionalProjectDao),
            [typeof(IDao<IFunctionalProjectPolicy>)] = typeof(FunctionalProjectPolicyDao),
            [typeof(IDao<IOrganization>)] = typeof(OrganizationDao),
            [typeof(IDao<IOrganizationMember>)] = typeof(OrganizationMemberDao),
            [typeof(IDao<IOrganizationPolicy>)] = typeof(OrganizationPolicyDao),
            [typeof(IDao<IOwnership>)] = typeof(OwnershipDao),
            [typeof(IDao<IProjectMember>)] = typeof(ProjectMemberDao),
            [typeof(IDao<IReply>)] = typeof(ReplyDao),
            [typeof(IDao<IReview>)] = typeof(ReviewDao),
            [typeof(IDao<IUser>)] = typeof(UserDao)
        };

        [Test]
        public async Task VerifyAddFunctionalDataAccess()
        {
            Assert.That(() => ServiceCollectionExtensions.AddFunctionalDataAccess(null), Throws.ArgumentNullException);

            var services = new ServiceCollection();

            services.AddSingleton(CreateConfiguration("Host=localhost;Database=mycelium_functional_data"));

            Assert.That(services.AddFunctionalDataAccess(), Is.SameAs(services));

            var accessDescriptors = services.Where(descriptor => descriptor.ServiceType != typeof(IConfiguration)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(accessDescriptors.Select(descriptor => descriptor.ServiceType).Where(IsDaoInterface), Is.EquivalentTo(ExpectedDaos.Keys));
                Assert.That(accessDescriptors.Select(descriptor => descriptor.Lifetime), Is.All.EqualTo(ServiceLifetime.Singleton));
            }

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            foreach (var (serviceType, implementationType) in ExpectedDaos)
            {
                Assert.That(provider.GetRequiredService(serviceType), Is.TypeOf(implementationType));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.GetRequiredService<ISerializer>(), Is.TypeOf<Serializer>());
                Assert.That(provider.GetRequiredService<IDeSerializer>(), Is.TypeOf<DeSerializer>());
                Assert.That(provider.GetRequiredService<NpgsqlDataSource>, Throws.Nothing);
            }

            string[] invalidConnectionStrings = [null, " "];

            foreach (var connectionString in invalidConnectionStrings)
            {
                var unconfiguredServices = new ServiceCollection();

                unconfiguredServices.AddSingleton(CreateConfiguration(connectionString));
                unconfiguredServices.AddFunctionalDataAccess();

                await using var unconfiguredProvider = unconfiguredServices.BuildServiceProvider();

                Assert.That(unconfiguredProvider.GetRequiredService<NpgsqlDataSource>,
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("'functionaldata' connection string"));
            }
        }

        private static bool IsDaoInterface(Type type)
        {
            return type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IDao<>);
        }

        private static IConfiguration CreateConfiguration(string connectionString)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:functionaldata"] = connectionString
                })
                .Build();
        }
    }
}
