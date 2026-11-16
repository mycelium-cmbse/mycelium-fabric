// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectDataVersioningServiceTestFixture.cs" company="Starion Group S.A.">
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

    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Services;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class ProjectDataVersioningServiceTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid OtherProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid UnknownProjectId = Guid.Parse("11111111-1111-1111-1111-999999999999");

        private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid FeatureBranchId = Guid.Parse("33333333-3333-3333-3333-555555555555");

        private static readonly Guid OtherBranchId = Guid.Parse("33333333-3333-3333-3333-444444444444");

        private static readonly Guid BaseCommitId = Guid.Parse("44444444-4444-4444-4444-000000000000");

        private static readonly Guid HeadCommitId = Guid.Parse("44444444-4444-4444-4444-111111111111");

        private static readonly Guid TheirCommitId = Guid.Parse("44444444-4444-4444-4444-222222222222");

        private static readonly Guid SecondTheirCommitId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private static readonly Guid ForeignCommitId = Guid.Parse("44444444-4444-4444-4444-333333333333");

        private static readonly Guid ElementXId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private static readonly Guid ElementYId = Guid.Parse("55555555-5555-5555-5555-000000000002");

        private static readonly Guid ElementZId = Guid.Parse("55555555-5555-5555-5555-000000000003");

        private static readonly Guid RelationshipId = Guid.Parse("55555555-5555-5555-5555-000000000004");

        private static readonly Guid UsageId = Guid.Parse("55555555-5555-5555-5555-000000000005");

        private ServiceMocks serviceMocks;

        private Mock<ILogger<ProjectDataVersioningService>> logger;

        private Mock<IBranchRepository> branchRepository;

        private Mock<ITagRepository> tagRepository;

        private Mock<ICommitRepository> commitRepository;

        private Mock<IDataVersionRepository> dataVersionRepository;

        private Mock<IDataIdentityRepository> dataIdentityRepository;

        private Mock<ICommitGraphRepository> commitGraphRepository;

        private Mock<IDataRepository> dataRepository;

        private Dictionary<Guid, Commit> commits;

        private Dictionary<Guid, List<DataVersion>> states;

        private Branch branch;

        private ProjectDataVersioningService service;

        [SetUp]
        public void SetUp()
        {
            this.serviceMocks = new ServiceMocks(ProjectId);
            this.logger = new Mock<ILogger<ProjectDataVersioningService>>();
            this.logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            this.branchRepository = new Mock<IBranchRepository>();
            this.tagRepository = new Mock<ITagRepository>();
            this.commitRepository = new Mock<ICommitRepository>();
            this.dataVersionRepository = new Mock<IDataVersionRepository>();
            this.dataIdentityRepository = new Mock<IDataIdentityRepository>();
            this.commitGraphRepository = new Mock<ICommitGraphRepository>();
            this.dataRepository = new Mock<IDataRepository>();

            this.commits = new Dictionary<Guid, Commit>
            {
                [BaseCommitId] = new() { Id = BaseCommitId, OwningProject = ProjectId, Created = DateTime.UtcNow.AddDays(-2) },
                [HeadCommitId] = new() { Id = HeadCommitId, OwningProject = ProjectId, Created = DateTime.UtcNow.AddDays(1), PreviousCommits = [BaseCommitId] },
                [TheirCommitId] = new() { Id = TheirCommitId, OwningProject = ProjectId, Created = DateTime.UtcNow.AddDays(-1), PreviousCommits = [BaseCommitId] },
                [SecondTheirCommitId] = new() { Id = SecondTheirCommitId, OwningProject = ProjectId, Created = DateTime.UtcNow.AddDays(-1), PreviousCommits = [BaseCommitId] },
                [ForeignCommitId] = new() { Id = ForeignCommitId, OwningProject = OtherProjectId, Created = DateTime.UtcNow.AddDays(-1) }
            };

            this.states = new Dictionary<Guid, List<DataVersion>>
            {
                [BaseCommitId] = [Version(ElementXId), Version(ElementYId)],
                [ForeignCommitId] = []
            };

            this.states[HeadCommitId] = [.. this.states[BaseCommitId]];
            this.states[TheirCommitId] = [.. this.states[BaseCommitId]];
            this.states[SecondTheirCommitId] = [.. this.states[BaseCommitId]];

            this.branch = new Branch { Id = BranchId, Name = "main", OwningProject = ProjectId, Head = HeadCommitId };

            this.serviceMocks.ProjectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Project { Id = ProjectId, Name = "project", DefaultBranch = BranchId });

            this.branchRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("branch.notFound"));

            this.branchRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), BranchId, It.IsAny<CancellationToken>())).ReturnsAsync(() => this.branch);

            this.branchRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), FeatureBranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Branch { Id = FeatureBranchId, OwningProject = ProjectId, Head = TheirCommitId });

            this.branchRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), OtherBranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Branch { Id = OtherBranchId, OwningProject = OtherProjectId, Head = ForeignCommitId });

            this.branchRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Branch>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Branch created, CancellationToken _) => created);

            this.branchRepository.Setup(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Branch>());

            this.commitRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Guid commitId, CancellationToken _) =>
                    this.commits.TryGetValue(commitId, out var commit) ? commit : Error.NotFound("commit.notFound"));

            this.commitRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Commit created, CancellationToken _) => created);

            this.commitGraphRepository.Setup(x => x.ReadVersionedDataAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Guid commitId, CancellationToken _) => ErrorOrFactory.From<IReadOnlyList<DataVersion>>(this.states[commitId]));

            this.commitGraphRepository.Setup(x => x.ReadAncestryAsync(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<Commit>>([this.commits[HeadCommitId], this.commits[BaseCommitId]]));

            this.commitGraphRepository.Setup(x => x.ReadMergeBaseAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.commits[BaseCommitId]);

            this.commitGraphRepository.Setup(x => x.MoveHeadAsync(It.IsAny<IDataStoreTransaction>(), BranchId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Guid _, Guid expectedHeadId, Guid newHeadId, CancellationToken _) => this.MoveHead(expectedHeadId, newHeadId));

            this.dataIdentityRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("dataIdentity.notFound"));

            this.dataIdentityRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<DataIdentity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, DataIdentity created, CancellationToken _) => created);

            this.dataVersionRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<DataVersion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, DataVersion created, CancellationToken _) => created);

            this.dataRepository.Setup(x => x.ReadIdentityOwnerAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("dataIdentity.notFound"));

            this.dataRepository.Setup(x => x.ReadDependentDataClosureAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<Guid>>(Array.Empty<Guid>()));

            this.service = new ProjectDataVersioningService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object,
                this.serviceMocks.ProjectRepository.Object, this.branchRepository.Object, this.tagRepository.Object, this.commitRepository.Object,
                this.dataVersionRepository.Object, this.dataIdentityRepository.Object, this.commitGraphRepository.Object, this.dataRepository.Object, this.logger.Object);
        }

        [Test]
        public async Task VerifyGetBranchByIdAsync()
        {
            var branch = await this.service.GetBranchByIdAsync(ProjectId, BranchId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(branch.Value, Is.SameAs(this.branch));
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadOnly);
            }

            var foreignBranch = await this.service.GetBranchByIdAsync(ProjectId, OtherBranchId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreignBranch.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
                Assert.That(foreignBranch.FirstError.Code, Is.EqualTo("branch.notFound"));
                this.serviceMocks.Scope.Verify(scope => scope.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            Assert.That((await this.service.GetBranchByIdAsync(ProjectId, Guid.NewGuid(), CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));

            Assert.That(() => this.service.GetBranchByIdAsync(OtherProjectId, BranchId, CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            this.logger.VerifyLogged(LogLevel.Error, "while the request is scoped to project", Times.Once());

            this.serviceMocks.ProjectScope.Setup(x => x.ProjectId).Returns(UnknownProjectId);

            var unknownProject = await this.service.GetBranchByIdAsync(UnknownProjectId, BranchId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unknownProject.FirstError.Code, Is.EqualTo("project.notFound"));
                this.branchRepository.Verify(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), BranchId, It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyGetBranchesAsync()
        {
            var branches = await this.service.GetBranchesAsync(ProjectId, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(branches.IsError, Is.False);

                this.branchRepository.Verify(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<PrimitiveSearchFilter>(filter => filter.Property == "owningProject" && filter.Value[0] == ProjectId), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }

            this.serviceMocks.ProjectScope.Setup(x => x.ProjectId).Returns(UnknownProjectId);

            var unknownProject = await this.service.GetBranchesAsync(UnknownProjectId, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unknownProject.FirstError.Code, Is.EqualTo("project.notFound"));

                this.branchRepository.Verify(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyCreateBranchAsync()
        {
            Assert.That(() => this.service.CreateBranchAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var unnamed = await this.service.CreateBranchAsync(new Branch { OwningProject = ProjectId, Head = HeadCommitId }, CancellationToken.None);

            Assert.That(unnamed.FirstError.Code, Is.EqualTo("branch.name.missing"));

            var headless = await this.service.CreateBranchAsync(new Branch { OwningProject = ProjectId, Name = "feature" }, CancellationToken.None);

            Assert.That(headless.FirstError.Code, Is.EqualTo("branch.head.missing"));

            var foreignHead = await this.service.CreateBranchAsync(new Branch { OwningProject = ProjectId, Name = "feature", Head = ForeignCommitId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreignHead.FirstError.Type, Is.EqualTo(ErrorType.Validation));
                Assert.That(foreignHead.FirstError.Code, Is.EqualTo("commit.invalid"));
            }

            var missingHead = await this.service.CreateBranchAsync(new Branch { OwningProject = ProjectId, Name = "feature", Head = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(missingHead.FirstError.Type, Is.EqualTo(ErrorType.NotFound));

            var created = await this.service.CreateBranchAsync(new Branch { OwningProject = ProjectId, Name = "feature", Head = HeadCommitId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created.Value.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(created.Value.Head, Is.EqualTo(HeadCommitId));
                Assert.That(created.Value.OwningProject, Is.EqualTo(ProjectId));
                Assert.That(created.Value.Alias, Does.Contain("feature"));
            }

            this.branchRepository.Setup(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Branch> { Records = [this.branch] });

            var homonym = await this.service.CreateBranchAsync(new Branch { OwningProject = ProjectId, Name = "main", Head = HeadCommitId }, CancellationToken.None);

            Assert.That(homonym.FirstError.Type, Is.EqualTo(ErrorType.Conflict));

            this.branchRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Branch>(), It.IsAny<CancellationToken>()), Times.Once);

            this.serviceMocks.ProjectScope.Setup(x => x.ProjectId).Returns(UnknownProjectId);

            var unknownProject = await this.service.CreateBranchAsync(new Branch { OwningProject = UnknownProjectId, Name = "feature", Head = HeadCommitId },
                CancellationToken.None);

            Assert.That(unknownProject.FirstError.Code, Is.EqualTo("project.notFound"));
        }

        [Test]
        public async Task VerifyDeleteBranchAsync()
        {
            this.branchRepository.Setup(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Deleted);

            var defaultBranch = await this.service.DeleteBranchAsync(ProjectId, BranchId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(defaultBranch.FirstError.Code, Is.EqualTo("branch.default"));
                this.serviceMocks.VerifyRolledBack();
            }

            var deleted = await this.service.DeleteBranchAsync(ProjectId, FeatureBranchId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(deleted.Value.Id, Is.EqualTo(FeatureBranchId));
                this.branchRepository.Verify(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), FeatureBranchId, It.IsAny<CancellationToken>()), Times.Once);
            }

            Assert.That((await this.service.DeleteBranchAsync(ProjectId, OtherBranchId, CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That((await this.service.DeleteBranchAsync(ProjectId, Guid.NewGuid(), CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));
        }

        [Test]
        public async Task VerifySetDefaultBranchAsync()
        {
            this.serviceMocks.ProjectRepository.Setup(x => x.UpdateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Project updated, CancellationToken _) => updated);

            var foreign = await this.service.SetDefaultBranchAsync(ProjectId, OtherBranchId, CancellationToken.None);
            var unknown = await this.service.SetDefaultBranchAsync(ProjectId, Guid.NewGuid(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreign.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
                Assert.That(unknown.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
                this.serviceMocks.ProjectRepository.Verify(x => x.UpdateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            var updated = await this.service.SetDefaultBranchAsync(ProjectId, FeatureBranchId, CancellationToken.None);

            Assert.That(updated.Value.DefaultBranch, Is.EqualTo(FeatureBranchId));
        }

        [Test]
        public async Task VerifyGetDefaultBranchAsync()
        {
            Assert.That((await this.service.GetDefaultBranchAsync(ProjectId, CancellationToken.None)).Value, Is.SameAs(this.branch));

            this.serviceMocks.ProjectScope.Setup(x => x.ProjectId).Returns(UnknownProjectId);

            Assert.That((await this.service.GetDefaultBranchAsync(UnknownProjectId, CancellationToken.None)).FirstError.Code, Is.EqualTo("project.notFound"));
        }

        [Test]
        public async Task VerifyGetHeadCommitAsync()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.GetHeadCommitAsync(ProjectId, null, CancellationToken.None)).Value.Id, Is.EqualTo(HeadCommitId));
                Assert.That((await this.service.GetHeadCommitAsync(ProjectId, FeatureBranchId, CancellationToken.None)).Value.Id, Is.EqualTo(TheirCommitId));
                Assert.That((await this.service.GetHeadCommitAsync(ProjectId, Guid.NewGuid(), CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));
                Assert.That((await this.service.GetHeadCommitAsync(ProjectId, OtherBranchId, CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.Validation));
            }
        }

        [Test]
        public async Task VerifyGetCommitByIdAsync()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.GetCommitByIdAsync(ProjectId, HeadCommitId, CancellationToken.None)).Value.Id, Is.EqualTo(HeadCommitId));
                Assert.That((await this.service.GetCommitByIdAsync(ProjectId, ForeignCommitId, CancellationToken.None)).FirstError.Code, Is.EqualTo("commit.notFound"));
                Assert.That((await this.service.GetCommitByIdAsync(ProjectId, Guid.NewGuid(), CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));
            }
        }

        [Test]
        public async Task VerifyCreateCommitAsync()
        {
            Assert.That(() => this.service.CreateCommitAsync(null, BranchId, new Commit { OwningProject = ProjectId }, CancellationToken.None),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => this.service.CreateCommitAsync([Version(ElementZId)], BranchId, new Commit { OwningProject = OtherProjectId }, CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That((await this.service.CreateCommitAsync([], BranchId, new Commit { OwningProject = ProjectId }, CancellationToken.None)).FirstError.Code,
                Is.EqualTo("change.missing"));

            Assert.That((await this.service.CreateCommitAsync([new DataVersion { Payload = new PartUsage() }], BranchId, new Commit { OwningProject = ProjectId },
                CancellationToken.None)).FirstError.Code, Is.EqualTo("change.payload.id.missing"));

            this.serviceMocks.Reset();

            var newElement = new PartUsage { Id = Guid.NewGuid(), DeclaredName = "new part" };
            var updatedElement = new PartUsage { Id = ElementXId, DeclaredName = "renamed part" };
            var clientCommitId = Guid.NewGuid();
            var clientVersionId = Guid.NewGuid();

            List<DataVersion> change =
            [
                new() { Id = clientVersionId, Payload = newElement },
                new() { Identity = new DataIdentity { Id = ElementXId }, Payload = updatedElement }
            ];

            var commit = await this.service.CreateCommitAsync(change, null, new Commit { Id = clientCommitId, OwningProject = ProjectId, Description = "the change" },
                CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(commit.Value.Id, Is.Not.EqualTo(clientCommitId));
                Assert.That(commit.Value.Change, Has.Count.EqualTo(2));
                Assert.That(commit.Value.Change, Does.Not.Contain(clientVersionId));
                Assert.That(commit.Value.PreviousCommits, Is.EqualTo([HeadCommitId]));
                Assert.That(commit.Value.Created, Is.GreaterThan(this.commits[HeadCommitId].Created));
                Assert.That(commit.Value.Created.Ticks % 10, Is.Zero);
                Assert.That(commit.Value.Description, Is.EqualTo("the change"));
                Assert.That(this.branch.Head, Is.EqualTo(commit.Value.Id));
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadWrite);
            }

            this.dataIdentityRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.Is<DataIdentity>(identity => identity.Id == newElement.Id),
                It.IsAny<CancellationToken>()), Times.Once);

            this.dataIdentityRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.Is<DataIdentity>(identity => identity.Id == ElementXId),
                It.IsAny<CancellationToken>()), Times.Never);

            this.branch.Head = HeadCommitId;
            this.serviceMocks.Reset();

            this.commitGraphRepository.Setup(x => x.MoveHeadAsync(It.IsAny<IDataStoreTransaction>(), BranchId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Conflict("branch.head.moved"));

            var concurrent = await this.service.CreateCommitAsync([Version(ElementZId)], BranchId, new Commit { OwningProject = ProjectId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(concurrent.FirstError.Code, Is.EqualTo("branch.head.moved"));
                this.serviceMocks.VerifyRolledBack();
            }

            this.commitGraphRepository.Setup(x => x.MoveHeadAsync(It.IsAny<IDataStoreTransaction>(), BranchId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Guid _, Guid expectedHeadId, Guid newHeadId, CancellationToken _) => this.MoveHead(expectedHeadId, newHeadId));

            this.dataRepository.Setup(x => x.ReadIdentityOwnerAsync(It.IsAny<IDataStoreTransaction>(), ElementZId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OtherProjectId);

            var adopted = await this.service.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = ElementZId }, Payload = new PartUsage { Id = ElementZId } }],
                BranchId, new Commit { OwningProject = ProjectId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(adopted.FirstError.Code, Is.EqualTo("change.identity.taken"));
                Assert.That(this.branch.Head, Is.EqualTo(HeadCommitId));
            }

            this.dataRepository.Setup(x => x.ReadIdentityOwnerAsync(It.IsAny<IDataStoreTransaction>(), ElementZId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ProjectId);

            this.dataIdentityRepository.Invocations.Clear();

            var recreated = await this.service.CreateCommitAsync([new DataVersion { Payload = new PartUsage { Id = ElementZId } }], BranchId,
                new Commit { OwningProject = ProjectId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recreated.IsError, Is.False);

                this.dataIdentityRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.Is<DataIdentity>(identity => identity.Id == ElementZId),
                    It.IsAny<CancellationToken>()), Times.Never);
            }

            this.branch.Head = HeadCommitId;

            var unknownDeletion = await this.service.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = ElementZId } }], BranchId,
                new Commit { OwningProject = ProjectId }, CancellationToken.None);

            Assert.That(unknownDeletion.FirstError.Code, Is.EqualTo("change.identity.unknown"));

            var duplicate = await this.service.CreateCommitAsync([new DataVersion { Payload = updatedElement },
                new DataVersion { Identity = new DataIdentity { Id = ElementXId } }],
                BranchId, new Commit { OwningProject = ProjectId }, CancellationToken.None);

            Assert.That(duplicate.FirstError.Code, Is.EqualTo("change.identity.duplicate"));

            var mismatch = await this.service.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = ElementYId }, Payload = updatedElement }], BranchId,
                new Commit { OwningProject = ProjectId }, CancellationToken.None);

            Assert.That(mismatch.FirstError.Code, Is.EqualTo("change.identity.mismatch"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.CreateCommitAsync([Version(ElementZId)], OtherBranchId, new Commit { OwningProject = ProjectId }, CancellationToken.None))
                    .FirstError.Code, Is.EqualTo("branch.invalid"));
                Assert.That((await this.service.CreateCommitAsync([Version(ElementZId)], Guid.NewGuid(), new Commit { OwningProject = ProjectId }, CancellationToken.None))
                    .FirstError.Type, Is.EqualTo(ErrorType.NotFound));
                Assert.That((await this.service.CreateCommitAsync([Version(ElementZId)], BranchId, new Commit { OwningProject = ProjectId, PreviousCommits = [ForeignCommitId] },
                    CancellationToken.None)).FirstError.Code, Is.EqualTo("commit.invalid"));
            }

            this.serviceMocks.ProjectScope.Setup(x => x.ProjectId).Returns(UnknownProjectId);

            Assert.That((await this.service.CreateCommitAsync([Version(ElementZId)], BranchId, new Commit { OwningProject = UnknownProjectId }, CancellationToken.None))
                .FirstError.Code, Is.EqualTo("project.notFound"));
        }

        [Test]
        public async Task CreateCommitAsync_DeletingData_DeletesWhatOnlyAnElementTakesWithIt()
        {
            var relationship = new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = RelationshipId }, Payload = new Dependency { Id = RelationshipId } };
            var usage = new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = UsageId }, Payload = new ProjectUsage { Id = UsageId } };
            this.states[HeadCommitId] = [.. this.states[BaseCommitId], relationship, usage];

            this.dataRepository.Setup(x => x.ReadDependentDataClosureAsync(It.IsAny<IDataStoreTransaction>(), HeadCommitId,
                    It.Is<IReadOnlyList<Guid>>(elementIds => elementIds.Count == 1 && elementIds[0] == ElementYId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<Guid>>([RelationshipId, ElementYId]));

            var deletion = await this.service.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = ElementYId } }], BranchId,
                new Commit { OwningProject = ProjectId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(deletion.Value.Change, Has.Count.EqualTo(2));

                this.dataVersionRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<DataVersion>(version => version.Identity.Id == RelationshipId && version.Payload == null), It.IsAny<CancellationToken>()), Times.Once);
            }

            this.branch.Head = HeadCommitId;

            var dependentUpdated = await this.service.CreateCommitAsync(
                [new DataVersion { Identity = new DataIdentity { Id = ElementYId } }, new DataVersion { Payload = new Dependency { Id = RelationshipId } }], BranchId,
                new Commit { OwningProject = ProjectId }, CancellationToken.None);

            Assert.That(dependentUpdated.FirstError.Code, Is.EqualTo("change.dependent.updated"));

            this.dataRepository.Invocations.Clear();

            var usageDeletion = await this.service.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = UsageId } }], BranchId,
                new Commit { OwningProject = ProjectId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(usageDeletion.Value.Change, Has.Count.EqualTo(1));

                this.dataRepository.Verify(x => x.ReadDependentDataClosureAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public async Task CreateCommitAsync_WithOtherPreviousCommits_SettlesWhatTheyDisagreeOn()
        {
            this.states[HeadCommitId] = [Version(ElementXId), this.states[BaseCommitId][1]];
            this.states[TheirCommitId] = [this.states[BaseCommitId][0], Version(ElementYId)];

            var merged = await this.service.CreateCommitAsync([new DataVersion { Payload = new PartUsage { Id = Guid.NewGuid(), DeclaredName = "new part" } }], BranchId,
                new Commit { OwningProject = ProjectId, PreviousCommits = [TheirCommitId] }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(merged.Value.PreviousCommits, Is.EqualTo([HeadCommitId, TheirCommitId]));
                Assert.That(merged.Value.Change, Has.Count.EqualTo(3));

                this.dataVersionRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<DataVersion>(version => version.Identity.Id == ElementYId && version.Payload == this.states[TheirCommitId][1].Payload), It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            this.branch.Head = HeadCommitId;

            var ancestorParent = await this.service.CreateCommitAsync([new DataVersion { Payload = new PartUsage { Id = Guid.NewGuid(), DeclaredName = "other part" } }],
                BranchId,
                new Commit { OwningProject = ProjectId, PreviousCommits = [BaseCommitId] }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ancestorParent.Value.PreviousCommits, Is.EqualTo([HeadCommitId, BaseCommitId]));
                Assert.That(ancestorParent.Value.Change, Has.Count.EqualTo(2));
            }

            this.branch.Head = HeadCommitId;
            this.states[TheirCommitId] = [Version(ElementXId), this.states[BaseCommitId][1]];

            var unresolved = await this.service.CreateCommitAsync([new DataVersion { Payload = new PartUsage { Id = Guid.NewGuid() } }], BranchId,
                new Commit { OwningProject = ProjectId, PreviousCommits = [TheirCommitId] }, CancellationToken.None);

            Assert.That(unresolved.FirstError.Code, Is.EqualTo("commit.conflict.unresolved"));

            var resolved = await this.service.CreateCommitAsync([new DataVersion { Payload = new PartUsage { Id = ElementXId, DeclaredName = "resolved" } }], BranchId,
                new Commit { OwningProject = ProjectId, PreviousCommits = [TheirCommitId] }, CancellationToken.None);

            Assert.That(resolved.Value.Change, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task VerifyMergeIntoBranchAsync()
        {
            Assert.That(() => this.service.MergeIntoBranchAsync(BranchId, null, [], null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            Assert.That((await this.service.MergeIntoBranchAsync(BranchId, [], [], null, CancellationToken.None)).FirstError.Code, Is.EqualTo("commitsToMerge.missing"));

            this.states[HeadCommitId] = [Version(ElementXId), this.states[BaseCommitId][1]];

            var oursOnly = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [], null, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(oursOnly.Value.MergeCommit, Is.EqualTo(this.branch.Head));

                this.dataVersionRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<DataVersion>(version => version.Identity.Id == ElementXId && version.Payload == this.states[HeadCommitId][0].Payload), It.IsAny<CancellationToken>()),
                    Times.Once);

                this.dataVersionRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.Is<DataVersion>(version => version.Identity.Id == ElementYId),
                    It.IsAny<CancellationToken>()), Times.Never);
            }

            this.branch.Head = HeadCommitId;
            this.states[TheirCommitId] = [this.states[BaseCommitId][0], Version(ElementYId), Version(ElementZId)];
            this.commitRepository.Invocations.Clear();

            var merged = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [], "the merge", CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(merged.Value.Conflict, Has.Count.EqualTo(0));

                this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<Commit>(commit => commit.PreviousCommits.SequenceEqual(new[] { HeadCommitId, TheirCommitId }) && commit.Change.Count == 3
                                            && commit.Description == "the merge"), It.IsAny<CancellationToken>()), Times.Once);
            }

            this.branch.Head = HeadCommitId;
            this.states[TheirCommitId] = [Version(ElementXId), this.states[BaseCommitId][1]];
            this.commitRepository.Invocations.Clear();

            var conflicting = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [], null, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(conflicting.Value.Conflict, Is.EqualTo([ElementXId]));
                Assert.That(conflicting.Value.MergeCommit, Is.Null);
                this.logger.VerifyLogged(LogLevel.Warning, "left 1 conflicts unresolved", Times.Once());
                this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            var resolved = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [new PartUsage { Id = ElementXId, DeclaredName = "resolved" }], null,
                CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolved.Value.MergeCommit, Is.Not.Null);

                this.dataVersionRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<DataVersion>(version => version.Identity.Id == ElementXId && ((PartUsage)version.Payload).DeclaredName == "resolved"), It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            this.branch.Head = HeadCommitId;
            this.commitRepository.Invocations.Clear();

            var alreadyMerged = await this.service.MergeIntoBranchAsync(BranchId, [BaseCommitId], [], null, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(alreadyMerged.Value.MergeCommit, Is.EqualTo(HeadCommitId));
                this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.MergeIntoBranchAsync(BranchId, [ForeignCommitId], [], null, CancellationToken.None)).FirstError.Code,
                    Is.EqualTo("commit.invalid"));
                Assert.That((await this.service.MergeIntoBranchAsync(BranchId, [Guid.NewGuid()], [], null, CancellationToken.None)).FirstError.Type,
                    Is.EqualTo(ErrorType.NotFound));
                Assert.That((await this.service.MergeIntoBranchAsync(OtherBranchId, [TheirCommitId], [], null, CancellationToken.None)).FirstError.Code,
                    Is.EqualTo("branch.invalid"));
                Assert.That((await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [new PartUsage()], null, CancellationToken.None)).FirstError.Code,
                    Is.EqualTo("resolution.invalid"));
            }
        }

        [Test]
        public async Task MergeIntoBranchAsync_WithSeveralCommits_MergesThemTogether()
        {
            this.states[TheirCommitId] = [this.states[BaseCommitId][0], Version(ElementYId)];
            this.states[SecondTheirCommitId] = [.. this.states[BaseCommitId], Version(ElementZId)];

            var merged = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId, SecondTheirCommitId], [], null, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(merged.Value.Conflict, Has.Count.EqualTo(0));

                this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                    It.Is<Commit>(commit => commit.PreviousCommits.Count == 3 && commit.Change.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
            }

            this.branch.Head = HeadCommitId;
            this.states[SecondTheirCommitId] = [this.states[BaseCommitId][0], Version(ElementYId)];

            var conflicting = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId, SecondTheirCommitId], [], null, CancellationToken.None);

            Assert.That(conflicting.Value.Conflict, Is.EqualTo([ElementYId]));
        }

        [Test]
        public async Task MergeIntoBranchAsync_DeletionAgainstDependentData_ReportsAConflict()
        {
            var baseRelationship = new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = RelationshipId }, Payload = new Dependency { Id = RelationshipId } };
            var editedRelationship = new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = RelationshipId }, Payload = new Dependency { Id = RelationshipId } };
            this.states[BaseCommitId] = [.. this.states[BaseCommitId], baseRelationship];
            this.states[HeadCommitId] = [this.states[BaseCommitId][1], baseRelationship];
            this.states[TheirCommitId] = [this.states[BaseCommitId][0], this.states[BaseCommitId][1], editedRelationship];

            this.dataRepository.Setup(x => x.ReadDependentDataClosureAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(),
                    It.Is<IReadOnlyList<Guid>>(elementIds => elementIds.Contains(ElementXId)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<Guid>>([RelationshipId]));

            var edited = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [], null, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(edited.Value.Conflict, Is.EquivalentTo([RelationshipId, ElementXId]));
                Assert.That(edited.Value.MergeCommit, Is.Null);
                this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()), Times.Never);
                this.logger.VerifyLogged(LogLevel.Warning, "left 2 conflicts unresolved", Times.Once());
            }

            this.states[TheirCommitId] = [this.states[BaseCommitId][0], this.states[BaseCommitId][1], baseRelationship,
                new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = ElementZId }, Payload = new Dependency { Id = ElementZId } }];

            this.dataRepository.Setup(x => x.ReadDependentDataClosureAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(),
                    It.Is<IReadOnlyList<Guid>>(elementIds => elementIds.Contains(ElementXId)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<Guid>>([RelationshipId, ElementZId]));

            var created = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [], null, CancellationToken.None);

            Assert.That(created.Value.Conflict, Is.EquivalentTo([ElementZId, ElementXId]));

            var restored = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [new PartUsage { Id = ElementXId }], null, CancellationToken.None);

            Assert.That(restored.Value.MergeCommit, Is.Not.Null);
        }

        [Test]
        public async Task MergeIntoBranchAsync_BringingNoChange_AnswersTheHead()
        {
            this.states[HeadCommitId] = [this.states[BaseCommitId][1]];
            this.states[TheirCommitId] = [this.states[BaseCommitId][1]];

            var merged = await this.service.MergeIntoBranchAsync(BranchId, [TheirCommitId], [], null, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(merged.Value.MergeCommit, Is.EqualTo(HeadCommitId));
                Assert.That(merged.Value.Conflict, Has.Count.EqualTo(0));
                this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Test]
        public async Task VerifyDiffCommitsAsync()
        {
            var page = new PagedResult<DataDifference> { Records = [new DataDifference { CompareData = Guid.NewGuid() }] };

            this.commitGraphRepository.Setup(x => x.ReadDifferencesAsync(It.IsAny<IDataStoreTransaction>(), HeadCommitId, TheirCommitId, It.IsAny<IReadOnlyList<ChangeType>>(),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(page);

            var differences = await this.service.DiffCommitsAsync(HeadCommitId, TheirCommitId, [ChangeType.CREATED, ChangeType.CREATED], new QueryParameters(),
                CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(differences.Value, Is.SameAs(page));

                this.commitGraphRepository.Verify(x => x.ReadDifferencesAsync(It.IsAny<IDataStoreTransaction>(), HeadCommitId, TheirCommitId,
                    It.Is<IReadOnlyList<ChangeType>>(changeTypes => changeTypes.Count == 1 && changeTypes[0] == ChangeType.CREATED), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.DiffCommitsAsync(BaseCommitId, ForeignCommitId, [], new QueryParameters(), CancellationToken.None)).FirstError.Type,
                    Is.EqualTo(ErrorType.Validation));
                Assert.That((await this.service.DiffCommitsAsync(Guid.NewGuid(), BaseCommitId, [], new QueryParameters(), CancellationToken.None)).FirstError.Type,
                    Is.EqualTo(ErrorType.NotFound));
            }

            this.commitGraphRepository.Verify(x => x.ReadDifferencesAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<ChangeType>>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task VerifyGetCommitChangeAsync()
        {
            var page = new PagedResult<DataVersion> { Records = [Version(ElementXId)] };

            this.commitGraphRepository.Setup(x => x.ReadChangeAsync(It.IsAny<IDataStoreTransaction>(), TheirCommitId, It.IsAny<IReadOnlyList<ChangeType>>(),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(page);

            var change = await this.service.GetCommitChangeAsync(ProjectId, TheirCommitId, null, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(change.Value, Is.SameAs(page));

                this.commitGraphRepository.Verify(x => x.ReadChangeAsync(It.IsAny<IDataStoreTransaction>(), TheirCommitId,
                    It.Is<IReadOnlyList<ChangeType>>(changeTypes => changeTypes.Count == 0), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            Assert.That((await this.service.GetCommitChangeAsync(ProjectId, ForeignCommitId, [], new QueryParameters(), CancellationToken.None)).FirstError.Type,
                Is.EqualTo(ErrorType.Validation));

            this.commitGraphRepository.Verify(x => x.ReadChangeAsync(It.IsAny<IDataStoreTransaction>(), ForeignCommitId, It.IsAny<IReadOnlyList<ChangeType>>(),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task VerifyGetCommitChangeByIdAsync()
        {
            var change = new DataVersion { Id = Guid.NewGuid(), Commit = TheirCommitId };
            var otherChange = new DataVersion { Id = Guid.NewGuid(), Commit = HeadCommitId };

            this.dataVersionRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), change.Id, It.IsAny<CancellationToken>())).ReturnsAsync(change);
            this.dataVersionRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), otherChange.Id, It.IsAny<CancellationToken>())).ReturnsAsync(otherChange);

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.GetCommitChangeByIdAsync(ProjectId, TheirCommitId, change.Id, CancellationToken.None)).Value, Is.SameAs(change));
                Assert.That((await this.service.GetCommitChangeByIdAsync(ProjectId, TheirCommitId, otherChange.Id, CancellationToken.None)).FirstError.Code,
                    Is.EqualTo("dataVersion.notFound"));
            }
        }

        private static DataVersion Version(Guid identityId)
        {
            return new DataVersion { Id = Guid.NewGuid(), Identity = new DataIdentity { Id = identityId }, Payload = new PartUsage { Id = identityId } };
        }

        private ErrorOr<Branch> MoveHead(Guid expectedHeadId, Guid newHeadId)
        {
            if (this.branch.Head != expectedHeadId)
            {
                return Error.Conflict("branch.head.moved");
            }

            this.branch.Head = newHeadId;

            return this.branch;
        }

        [Test]
        public async Task VerifyDeleteTagAsync()
        {
            var tagId = Guid.NewGuid();
            var foreignTagId = Guid.NewGuid();

            this.tagRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("tag.notFound"));

            this.tagRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), tagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Tag { Id = tagId, OwningProject = ProjectId });

            this.tagRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), foreignTagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Tag { Id = foreignTagId, OwningProject = OtherProjectId });

            this.tagRepository.Setup(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Deleted);

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.DeleteTagAsync(ProjectId, tagId, CancellationToken.None)).Value.Id, Is.EqualTo(tagId));
                Assert.That((await this.service.DeleteTagAsync(ProjectId, foreignTagId, CancellationToken.None)).FirstError.Code, Is.EqualTo("tag.notFound"));
                this.tagRepository.Verify(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), foreignTagId, It.IsAny<CancellationToken>()), Times.Never);
            }
        }
    }
}
