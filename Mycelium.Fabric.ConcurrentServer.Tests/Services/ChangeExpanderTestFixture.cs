// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeExpanderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Testing;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;

    [TestFixture]
    public class ChangeExpanderTestFixture
    {
        private static readonly Guid CreatedId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private static readonly Guid UpdatedId = Guid.Parse("55555555-5555-5555-5555-000000000002");

        private static readonly Guid DeletedId = Guid.Parse("55555555-5555-5555-5555-000000000003");

        private static readonly Guid UsageId = Guid.Parse("55555555-5555-5555-5555-000000000004");

        private Mock<ICoreDataService> partUsageService;

        private Mock<ICoreDataService> dependencyService;

        private Mock<ICoreDataServiceProvider> serviceProvider;

        private Mock<IChangeContext> context;

        private Dependency deleted;

        private FakeLogger<ChangeExpander> logger;

        [SetUp]
        public void SetUp()
        {
            this.partUsageService = new Mock<ICoreDataService>(MockBehavior.Strict);

            this.partUsageService.Setup(service => service.ComputeEffectsAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>(Array.Empty<DataVersion>()));

            this.dependencyService = new Mock<ICoreDataService>(MockBehavior.Strict);

            this.dependencyService.Setup(service => service.ComputeEffectsAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>(Array.Empty<DataVersion>()));

            this.serviceProvider = new Mock<ICoreDataServiceProvider>(MockBehavior.Strict);
            this.serviceProvider.Setup(provider => provider.Resolve(typeof(PartUsage))).Returns(this.partUsageService.Object);
            this.serviceProvider.Setup(provider => provider.Resolve(typeof(Dependency))).Returns(this.dependencyService.Object);

            this.deleted = new Dependency { Id = DeletedId };
            this.logger = new FakeLogger<ChangeExpander>();

            this.context = new Mock<IChangeContext>();

            this.context.Setup(context => context.PreviousState).Returns(new Dictionary<Guid, DataVersion>
            {
                [UpdatedId] = new() { Identity = new DataIdentity { Id = UpdatedId }, Payload = new PartUsage { Id = UpdatedId } },
                [DeletedId] = new() { Identity = new DataIdentity { Id = DeletedId }, Payload = this.deleted },
                [UsageId] = new() { Identity = new DataIdentity { Id = UsageId }, Payload = new ProjectUsage { Id = UsageId } }
            });
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ChangeExpander(null, this.logger), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ChangeExpander(this.serviceProvider.Object, null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public async Task VerifyComputeEffectsAsync()
        {
            var changeExpander = new ChangeExpander(this.serviceProvider.Object, this.logger);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => changeExpander.ComputeEffectsAsync(null, [], CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => changeExpander.ComputeEffectsAsync(this.context.Object, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            }

            Assert.That((await changeExpander.ComputeEffectsAsync(this.context.Object, [], CancellationToken.None)).Value, Is.Empty);

            var created = new PartUsage { Id = CreatedId };
            var updated = new PartUsage { Id = UpdatedId };

            List<DataVersion> versions =
            [
                new() { Identity = new DataIdentity { Id = CreatedId }, Payload = created },
                new() { Identity = new DataIdentity { Id = UpdatedId }, Payload = updated },
                new() { Identity = new DataIdentity { Id = DeletedId } },
                new() { Identity = new DataIdentity { Id = UsageId } }
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await changeExpander.ComputeEffectsAsync(this.context.Object, versions, CancellationToken.None)).Value, Is.Empty);
                Assert.That(this.logger.Collector.Count, Is.Zero);
            }

            this.partUsageService.Verify(service => service.ComputeEffectsAsync(this.context.Object, created, ChangeType.CREATED, It.IsAny<CancellationToken>()), Times.Once);
            this.partUsageService.Verify(service => service.ComputeEffectsAsync(this.context.Object, updated, ChangeType.UPDATED, It.IsAny<CancellationToken>()), Times.Once);
            this.dependencyService.Verify(service => service.ComputeEffectsAsync(this.context.Object, this.deleted, ChangeType.DELETED, It.IsAny<CancellationToken>()), Times.Once);
            this.serviceProvider.Verify(provider => provider.Resolve(It.IsAny<Type>()), Times.Exactly(3));

            var partUsageEffect = new DataVersion { Id = Guid.NewGuid() };
            var dependencyEffect = new DataVersion { Id = Guid.NewGuid() };

            this.partUsageService.Setup(service => service.ComputeEffectsAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>([partUsageEffect]));

            this.dependencyService.Setup(service => service.ComputeEffectsAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>([dependencyEffect]));

            var expanded = await changeExpander.ComputeEffectsAsync(this.context.Object, versions, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(expanded.Value, Is.EqualTo([partUsageEffect, partUsageEffect, dependencyEffect]));
                Assert.That(this.logger.LatestRecord.Level, Is.EqualTo(LogLevel.Debug));
                Assert.That(this.logger.LatestRecord.Message, Is.EqualTo("The effects of 4 versions add 3 versions to the change, with 0 errors"));
            }

            this.partUsageService.Setup(service => service.ComputeEffectsAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("partUsage.effect"));

            this.dependencyService.Setup(service => service.ComputeEffectsAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("dependency.effect"));

            var failed = await changeExpander.ComputeEffectsAsync(this.context.Object, versions, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failed.Errors, Is.EqualTo([Error.Validation("partUsage.effect"), Error.Validation("partUsage.effect"), Error.Validation("dependency.effect")]));
                Assert.That(this.logger.LatestRecord.Message, Is.EqualTo("The effects of 4 versions add 0 versions to the change, with 3 errors"));
                Assert.That(this.logger.Collector.Count, Is.EqualTo(2));
            }
        }
    }
}
