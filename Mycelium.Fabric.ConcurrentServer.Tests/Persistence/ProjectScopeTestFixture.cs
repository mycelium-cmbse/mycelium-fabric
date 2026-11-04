// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectScopeTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence
{
    using System;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    [TestFixture]
    public class ProjectScopeTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid OtherProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        [Test]
        public void VerifyProjectId()
        {
            var scope = new ProjectScope();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(scope, Is.InstanceOf<IProjectScope>());
                Assert.That(() => scope.ProjectId, Throws.TypeOf<InvalidOperationException>());
            }

            using (scope.Enter(ProjectId))
            {
                Assert.That(scope.ProjectId, Is.EqualTo(ProjectId));
            }

            Assert.That(() => scope.ProjectId, Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void VerifyEnter()
        {
            var scope = new ProjectScope();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => scope.Enter(Guid.Empty), Throws.TypeOf<ArgumentException>());
                Assert.That(() => scope.ProjectId, Throws.TypeOf<InvalidOperationException>());
            }

            var exit = scope.Enter(ProjectId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => scope.Enter(OtherProjectId), Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => scope.Enter(ProjectId), Throws.TypeOf<InvalidOperationException>());
                Assert.That(scope.ProjectId, Is.EqualTo(ProjectId));
            }

            exit.Dispose();

            Assert.That(() => scope.ProjectId, Throws.TypeOf<InvalidOperationException>());

            // A scope that has been left can be entered again, and a stale handle must not leave the new one.
            using (scope.Enter(OtherProjectId))
            {
                Assert.That(exit.Dispose, Throws.Nothing);
                Assert.That(scope.ProjectId, Is.EqualTo(OtherProjectId));
            }

            Assert.That(() => scope.ProjectId, Throws.TypeOf<InvalidOperationException>());
        }
    }
}
