// ------------------------------------------------------------------------------------------------
//  <copyright file="DependencyEndRuleTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Services.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Services;
    using Mycelium.Fabric.ConcurrentServer.Services.Rules;

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM.DTO;

    [TestFixture]
    public class DependencyEndRuleTestFixture
    {
        private static readonly Guid ClientId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private static readonly Guid SupplierId = Guid.Parse("55555555-5555-5555-5555-000000000002");

        private static readonly Guid UsageId = Guid.Parse("55555555-5555-5555-5555-000000000003");

        private static readonly Guid MissingId = Guid.Parse("55555555-5555-5555-5555-000000000004");

        private Mock<IChangeContext> context;

        private DependencyEndRule rule;

        [SetUp]
        public void SetUp()
        {
            var data = new Dictionary<Guid, IData>
            {
                [ClientId] = new PartUsage { Id = ClientId },
                [SupplierId] = new PartUsage { Id = SupplierId },
                [UsageId] = new ProjectUsage { Id = UsageId }
            };

            this.context = new Mock<IChangeContext>(MockBehavior.Strict);

            this.context.Setup(context => context.GetDataAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> identityIds, CancellationToken _) =>
                    ErrorOrFactory.From<IReadOnlyDictionary<Guid, IData>>(identityIds.Where(data.ContainsKey).Distinct().ToDictionary(identityId => identityId, identityId => data[identityId])));

            this.rule = new DependencyEndRule();
        }

        [Test]
        public async Task VerifyValidateAsync()
        {
            var dependency = new Dependency { Id = Guid.NewGuid(), Client = [ClientId], Supplier = [SupplierId] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.rule.ValidateAsync(null, dependency, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.rule.ValidateAsync(this.context.Object, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            }

            Assert.That((await this.rule.ValidateAsync(this.context.Object, dependency, CancellationToken.None)).IsError, Is.False);

            this.context.Verify(context => context.GetDataAsync(It.Is<IReadOnlyCollection<Guid>>(identityIds => identityIds.SequenceEqual(new[] { ClientId, SupplierId })),
                It.IsAny<CancellationToken>()), Times.Once);

            dependency.Client = [ClientId, MissingId];
            dependency.Supplier = [UsageId];

            var dangling = await this.rule.ValidateAsync(this.context.Object, dependency, CancellationToken.None);

            Assert.That(dangling.Errors.ConvertAll(error => error.Code), Is.EqualTo(["dependency.client.dangling", "dependency.supplier.dangling"]));

            this.context.Setup(context => context.GetDataAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Unexpected("store.failed"));

            Assert.That((await this.rule.ValidateAsync(this.context.Object, dependency, CancellationToken.None)).FirstError.Code, Is.EqualTo("store.failed"));
        }
    }
}
