// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementDaoProviderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence
{
    using System;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Systems.Parts;

    [TestFixture]
    public class ElementDaoProviderTestFixture
    {
        [Test]
        public void VerifyResolve()
        {
            var commentDao = new Mock<IElementDao<Comment>>(MockBehavior.Strict);

            using var serviceProvider = new ServiceCollection()
                .AddScoped(_ => commentDao.Object)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            using var scope = serviceProvider.CreateScope();

            var provider = new ElementDaoProvider(scope.ServiceProvider);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ElementDaoProvider(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(provider.Resolve(ClassKind.Comment), Is.SameAs(commentDao.Object));
                Assert.That(provider.Resolve(typeof(Comment)), Is.SameAs(commentDao.Object));
                Assert.That(provider.Resolve<Comment>(), Is.SameAs(commentDao.Object));
                Assert.That(() => provider.Resolve(ClassKind.Element), Throws.TypeOf<ArgumentException>());
                Assert.That(() => provider.Resolve(typeof(string)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => provider.Resolve((Type)null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => provider.Resolve(typeof(PartUsage)), Throws.TypeOf<InvalidOperationException>());
                Assert.That(provider.Resolve<PartUsage>, Throws.TypeOf<ArgumentException>());
            }
        }
    }
}
