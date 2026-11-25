// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementDaoTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using Npgsql;

    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.Serializer.Json;

    [TestFixture]
    [Category("Integration")]
    public class ElementDaoTestFixture
    {
        private const string TombstoneCountSql =
            "SELECT count(*) FROM sysml2.element_version WHERE project_id = @project_id AND version_id = @version_id AND tombstone";

        private const string DerivedRelatedElementsSql = """
            SELECT count(*)
            FROM sysml2.element_version
            JOIN sysml2.relationship_source USING (project_id, version_id)
            WHERE element_version.project_id = @project_id AND element_version.commit_id = @commit_id AND element_version.class_kind = @class_kind
            """;

        private const string ReferencesInStoredJsonSql = """
            SELECT count(*)
            FROM sysml2.element_version, jsonb_each(element_version.stored_json) AS entry(key, value)
            WHERE element_version.project_id = @project_id
                AND element_version.commit_id = @commit_id
                AND ((jsonb_typeof(entry.value) = 'object' AND entry.value ? '@id')
                    OR (jsonb_typeof(entry.value) = 'array' AND EXISTS (SELECT 1 FROM jsonb_array_elements(entry.value) AS item
                                                                     WHERE jsonb_typeof(item) = 'object' AND item ? '@id')))
            """;

        private ServiceProvider services;

        private AsyncServiceScope scope;

        private ISerializer serializer;

        private IElementDaoProvider daoProvider;

        private Guid projectId;

        private Guid[] targetIds;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            this.services = new ServiceCollection()
                .AddSingleton<DbDataSource>(PostgreSqlTestHost.DataSource)
                .AddSingleton<ISerializer, Serializer>()
                .AddSingleton<IDeSerializer, DeSerializer>()
                .AddCoreDataAccess()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            this.scope = this.services.CreateAsyncScope();
            this.serializer = this.scope.ServiceProvider.GetRequiredService<ISerializer>();
            this.daoProvider = this.scope.ServiceProvider.GetRequiredService<IElementDaoProvider>();

            this.projectId = await PostgreSqlTestHost.SeedProjectAsync();
            this.targetIds = [Guid.NewGuid(), Guid.NewGuid()];

            await PostgreSqlTestHost.SeedIdentitiesAsync(this.projectId, ClassKind.Namespace, this.targetIds);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (this.services != null)
            {
                await this.scope.DisposeAsync();
                await this.services.DisposeAsync();
            }
        }

        [Test]
        public async Task VerifyWriteAsync()
        {
            var commitId = await PostgreSqlTestHost.SeedCommitAsync(this.projectId);
            var elementDaos = typeof(IElement).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IElement).IsAssignableFrom(type))
                .Select(this.daoProvider.Resolve)
                .OrderBy(dao => dao.ClassKind)
                .ToList();
            List<string> mismatches = [];

            foreach (var dao in elementDaos)
            {
                var element = this.CreatePopulatedElement(dao.DataType);
                var version = new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = element.Id }, Payload = element };

                await PostgreSqlTestHost.SeedIdentitiesAsync(this.projectId, dao.ClassKind, [element.Id]);

                await using var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite);

                var written = await dao.WriteAsync(transaction, this.projectId, [version], CancellationToken.None);
                var read = written.IsError ? written.Errors : await dao.ReadAsync(transaction, this.projectId, [version.Id], CancellationToken.None);

                if (read.IsError || read.Value.Count != 1 || this.Serialize(read.Value[0]) != this.Serialize(element))
                {
                    mismatches.Add($"{dao.ClassKind}: {(read.IsError ? read.FirstError.Description : "the element read back differs from the one written")}");
                }

                await transaction.CommitAsync(CancellationToken.None);
            }

            var dangling = new Comment { Id = Guid.NewGuid(), ElementId = "the element id", Body = "the body", OwningRelationship = Guid.NewGuid() };
            await PostgreSqlTestHost.SeedIdentitiesAsync(this.projectId, ClassKind.Comment, [dangling.Id]);

            await using var failingTransaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite);

            var refused = await this.daoProvider.Resolve(typeof(Comment)).WriteAsync(failingTransaction, this.projectId,
                [new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = dangling.Id }, Payload = dangling }], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(elementDaos, Has.Count.EqualTo(167));
                Assert.That(mismatches, Is.Empty);
                Assert.That(await PostgreSqlTestHost.CountAsync(ReferencesInStoredJsonSql, new NpgsqlParameter("project_id", this.projectId),
                    new NpgsqlParameter("commit_id", commitId)), Is.Zero);
                Assert.That(await PostgreSqlTestHost.CountAsync(
                    "SELECT count(*) FROM sysml2.element_version WHERE project_id = @project_id AND commit_id = @commit_id AND stored_json ? 'declaredName'",
                    new NpgsqlParameter("project_id", this.projectId), new NpgsqlParameter("commit_id", commitId)), Is.EqualTo(167));
                Assert.That(await PostgreSqlTestHost.CountAsync(DerivedRelatedElementsSql, new NpgsqlParameter("project_id", this.projectId),
                    new NpgsqlParameter("commit_id", commitId), new NpgsqlParameter("class_kind", (short)ClassKind.FeatureMembership)), Is.Zero);
                Assert.That(refused.FirstError.Type, Is.EqualTo(ErrorType.Validation));
                Assert.That(refused.FirstError.Code, Is.EqualTo("store.reference"));
            }
        }

        [Test]
        public async Task VerifyReadAsync()
        {
            var commitId = await PostgreSqlTestHost.SeedCommitAsync(this.projectId);
            var membershipDao = this.daoProvider.Resolve(ClassKind.Membership);
            var commentDao = this.daoProvider.Resolve(ClassKind.Comment);
            var live = (Membership)this.CreatePopulatedElement(typeof(Membership));
            var deleted = (Membership)this.CreatePopulatedElement(typeof(Membership));

            await PostgreSqlTestHost.SeedIdentitiesAsync(this.projectId, ClassKind.Membership, [live.Id, deleted.Id]);

            var liveVersion = new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = live.Id }, Payload = live };
            var tombstoneVersion = new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = deleted.Id } };

            await using var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite);

            var written = await membershipDao.WriteAsync(transaction, this.projectId, [liveVersion, tombstoneVersion], CancellationToken.None);

            var read = await membershipDao.ReadAsync(transaction, this.projectId, [liveVersion.Id, tombstoneVersion.Id, Guid.NewGuid()], CancellationToken.None);
            var readAsComment = await commentDao.ReadAsync(transaction, this.projectId, [liveVersion.Id], CancellationToken.None);
            var readInOtherProject = await membershipDao.ReadAsync(transaction, Guid.NewGuid(), [liveVersion.Id], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(written.IsError, Is.False);
                Assert.That(read.Value, Has.Count.EqualTo(1));
                Assert.That(read.Value[0].Id, Is.EqualTo(live.Id));
                Assert.That(((Membership)read.Value[0]).OwnedRelationship, Is.EqualTo(this.targetIds));
                Assert.That(((Membership)read.Value[0]).AliasIds, Is.EqualTo(live.AliasIds));
                Assert.That(readAsComment.Value, Is.Empty);
                Assert.That(readInOtherProject.Value, Is.Empty);
                Assert.That(await PostgreSqlTestHost.CountAsync(TombstoneCountSql,
                    new NpgsqlParameter("project_id", this.projectId), new NpgsqlParameter("version_id", tombstoneVersion.Id)), Is.Zero);
            }

            await transaction.CommitAsync(CancellationToken.None);

            Assert.That(await PostgreSqlTestHost.CountAsync(TombstoneCountSql,
                new NpgsqlParameter("project_id", this.projectId), new NpgsqlParameter("version_id", tombstoneVersion.Id)), Is.EqualTo(1));
        }

        private IElement CreatePopulatedElement(Type dataType)
        {
            var element = (IElement)Activator.CreateInstance(dataType);

            foreach (var property in dataType.GetProperties().Where(property => property.SetMethod is { IsPublic: true }))
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                object value = propertyType switch
                {
                    _ when propertyType == typeof(Guid) => this.targetIds[0],
                    _ when propertyType == typeof(List<Guid>) => this.targetIds.ToList(),
                    _ when propertyType == typeof(string) => $"the {property.Name}",
                    _ when propertyType == typeof(List<string>) => new List<string> { "the first alias", "the second alias" },
                    _ when propertyType == typeof(bool) => true,
                    _ when propertyType == typeof(int) => 7,
                    _ when propertyType == typeof(double) => 1.5,
                    { IsEnum: true } => Enum.GetValues(propertyType).GetValue(Enum.GetValues(propertyType).Length - 1),
                    _ => throw new NotSupportedException($"{dataType.Name}.{property.Name} has the unexpected type {property.PropertyType.Name}.")
                };

                property.SetValue(element, value);
            }

            element.Id = Guid.NewGuid();

            return element;
        }

        private string Serialize(IElement element)
        {
            using var stream = new MemoryStream();

            this.serializer.Serialize(element, SerializationModeKind.JSON, false, stream, new JsonWriterOptions());

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
