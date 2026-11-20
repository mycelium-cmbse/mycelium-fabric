// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeContextTestFixture.cs" company="Starion Group S.A.">
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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM.DTO;

    [TestFixture]
    public class ChangeContextTestFixture
    {
        private static readonly Guid UsedCommitId = Guid.Parse("44444444-4444-4444-4444-111111111111");

        private static readonly Guid UnchangedId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private static readonly Guid UpdatedId = Guid.Parse("55555555-5555-5555-5555-000000000002");

        private static readonly Guid DeletedId = Guid.Parse("55555555-5555-5555-5555-000000000003");

        private static readonly Guid CreatedId = Guid.Parse("55555555-5555-5555-5555-000000000004");

        private static readonly Guid UsageId = Guid.Parse("55555555-5555-5555-5555-000000000005");

        private static readonly Guid LibraryId = Guid.Parse("66666666-6666-6666-6666-000000000001");

        private static readonly Guid MissingId = Guid.Parse("66666666-6666-6666-6666-000000000002");

        private static readonly Guid[] UnresolvedIds = [LibraryId, MissingId];

        private IDataStoreTransaction transaction;

        private Mock<IDataRepository> dataRepository;

        private Dictionary<Guid, DataVersion> previousState;

        private PartUsage library;

        [SetUp]
        public void SetUp()
        {
            this.transaction = new Mock<IDataStoreTransaction>().Object;
            this.library = new PartUsage { Id = LibraryId };

            this.dataRepository = new Mock<IDataRepository>(MockBehavior.Strict);

            this.dataRepository.Setup(repository => repository.ReadByIdsAsync<IData>(this.transaction, UsedCommitId,
                    It.Is<IReadOnlyCollection<Guid>>(identityIds => identityIds.SequenceEqual(UnresolvedIds)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<IData>>([this.library]));

            this.previousState = new Dictionary<Guid, DataVersion>
            {
                [UnchangedId] = Version(UnchangedId, new PartUsage { Id = UnchangedId }),
                [UpdatedId] = Version(UpdatedId, new PartUsage { Id = UpdatedId, DeclaredName = "before" }),
                [DeletedId] = Version(DeletedId, new PartUsage { Id = DeletedId }),
                [UsageId] = Version(UsageId, new ProjectUsage { Id = UsageId, UsedProjectCommit = UsedCommitId })
            };
        }

        [Test]
        public void VerifyConstructor()
        {
            List<DataVersion> change = [Version(CreatedId, new PartUsage { Id = CreatedId })];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ChangeContext(null, change, this.previousState, this.dataRepository.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ChangeContext(this.transaction, null, this.previousState, this.dataRepository.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ChangeContext(this.transaction, change, null, this.dataRepository.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ChangeContext(this.transaction, change, this.previousState, null), Throws.TypeOf<ArgumentNullException>());
            }

            var context = new ChangeContext(this.transaction, change, this.previousState, this.dataRepository.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Change, Is.SameAs(change));
                Assert.That(context.PreviousState, Is.SameAs(this.previousState));
            }
        }

        [Test]
        public async Task VerifyGetDataAsync()
        {
            var unchanged = this.previousState[UnchangedId].Payload;
            var updated = new PartUsage { Id = UpdatedId, DeclaredName = "after" };
            var created = new PartUsage { Id = CreatedId };

            List<DataVersion> change = [Version(UpdatedId, updated), Version(DeletedId, null), Version(CreatedId, created)];
            var context = new ChangeContext(this.transaction, change, this.previousState, this.dataRepository.Object);

            Assert.That(() => context.GetDataAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var local = await context.GetDataAsync([UnchangedId, UpdatedId, DeletedId, CreatedId], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(local.Value, Has.Count.EqualTo(3));
                Assert.That(local.Value[UnchangedId], Is.SameAs(unchanged));
                Assert.That(local.Value[UpdatedId], Is.SameAs(updated));
                Assert.That(local.Value[CreatedId], Is.SameAs(created));
            }

            this.dataRepository.Verify(repository => repository.ReadByIdsAsync<IData>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);

            var throughUsage = await context.GetDataAsync([UnchangedId, DeletedId, LibraryId, MissingId, LibraryId], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(throughUsage.Value.Keys, Is.EquivalentTo([UnchangedId, LibraryId]));
                Assert.That(throughUsage.Value[LibraryId], Is.SameAs(this.library));
            }

            this.dataRepository.Verify(repository => repository.ReadByIdsAsync<IData>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);

            var usageDeleted = new ChangeContext(this.transaction, [Version(UsageId, null)], this.previousState, this.dataRepository.Object);

            Assert.That((await usageDeleted.GetDataAsync([LibraryId], CancellationToken.None)).Value, Has.Count.EqualTo(0));

            this.dataRepository.Setup(repository => repository.ReadByIdsAsync<IData>(It.IsAny<IDataStoreTransaction>(), UsedCommitId,
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Unexpected("store.failed"));

            Assert.That((await context.GetDataAsync([LibraryId], CancellationToken.None)).FirstError.Code, Is.EqualTo("store.failed"));
        }

        private static DataVersion Version(Guid identityId, IData payload)
        {
            return new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = identityId }, Payload = payload };
        }
    }
}
