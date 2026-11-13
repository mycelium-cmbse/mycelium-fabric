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

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;
    using Mycelium.SDK.DTO;
    using Mycelium.SDK.Serializer.Json;

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
        public void VerifyAddFunctionalDataAccess()
        {
            Assert.That(() => ServiceCollectionExtensions.AddFunctionalDataAccess(null), Throws.ArgumentNullException);

            var services = new ServiceCollection();

            Assert.That(services.AddFunctionalDataAccess(), Is.SameAs(services));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(services.Select(descriptor => descriptor.ServiceType).Where(IsDaoInterface), Is.EquivalentTo(ExpectedDaos.Keys));
                Assert.That(services.Select(descriptor => descriptor.Lifetime), Is.All.EqualTo(ServiceLifetime.Singleton));
            }

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            foreach (var (serviceType, implementationType) in ExpectedDaos)
            {
                Assert.That(provider.GetRequiredService(serviceType), Is.TypeOf(implementationType));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.GetRequiredService<ISerializer>(), Is.TypeOf<Serializer>());
                Assert.That(provider.GetRequiredService<IDeSerializer>(), Is.TypeOf<DeSerializer>());
            }
        }

        private static bool IsDaoInterface(Type type)
        {
            return type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IDao<>);
        }
    }
}
