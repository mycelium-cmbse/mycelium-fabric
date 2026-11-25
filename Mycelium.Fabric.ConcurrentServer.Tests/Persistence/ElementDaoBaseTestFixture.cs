// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementDaoBaseTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence
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

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Common;
    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class ElementDaoBaseTestFixture
    {
        private static readonly byte[] SerializedPayloads = Encoding.UTF8.GetBytes("[{\"@type\":\"Membership\"}]");

        private Mock<ISerializer> serializer;

        private Mock<IDeSerializer> deSerializer;

        private Mock<ISqlTransaction> transaction;

        private MembershipDao dao;

        private Guid projectId;

        [SetUp]
        public void SetUp()
        {
            this.projectId = Guid.NewGuid();

            this.serializer = new Mock<ISerializer>();

            this.serializer.Setup(serializer => serializer.SerializeAsync(It.IsAny<IEnumerable<IIdentified>>(), SerializationModeKind.JSON, false, It.IsAny<Stream>(),
                    It.IsAny<JsonWriterOptions>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<IIdentified>, SerializationModeKind, bool, Stream, JsonWriterOptions, CancellationToken>((_, _, _, stream, _, _) =>
                    stream.Write(SerializedPayloads))
                .Returns(Task.CompletedTask);

            this.deSerializer = new Mock<IDeSerializer>();
            this.transaction = new Mock<ISqlTransaction>(MockBehavior.Strict);

            this.dao = new MembershipDao(this.serializer.Object, this.deSerializer.Object);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new MembershipDao(null, this.deSerializer.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new MembershipDao(this.serializer.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.dao.ClassKind, Is.EqualTo(ClassKind.Membership));
                Assert.That(this.dao.DataType, Is.EqualTo(typeof(Membership)));
            }
        }

        [Test]
        public async Task VerifyReadAsync()
        {
            var versionId = Guid.NewGuid();
            var membership = new Membership { Id = Guid.NewGuid() };
            var document = Encoding.UTF8.GetBytes("[]");
            IReadOnlyCollection<DbParameter> boundParameters = null;
            Func<DbDataReader, byte[]> readRow = null;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.dao.ReadAsync(null, this.projectId, [versionId], CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.dao.ReadAsync(this.transaction.Object, this.projectId, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            }

            var none = await this.dao.ReadAsync(this.transaction.Object, this.projectId, [], CancellationToken.None);

            Assert.That(none.Value, Is.Empty);

            this.transaction.Setup(sqlTransaction => sqlTransaction.ExecuteReaderAsync(It.Is<string>(sql => sql.Contains("FROM sysml2.element_version")),
                    It.IsAny<IReadOnlyCollection<DbParameter>>(), It.IsAny<Func<DbDataReader, byte[]>>(), It.IsAny<CancellationToken>()))
                .Callback<string, IReadOnlyCollection<DbParameter>, Func<DbDataReader, byte[]>, CancellationToken>((_, parameters, rowReader, _) =>
                {
                    boundParameters = parameters;
                    readRow = rowReader;
                })
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<byte[]>>([document]));

            this.deSerializer.Setup(deSerializer => deSerializer.DeSerialize(It.IsAny<Stream>(), SerializationModeKind.JSON, SerializationTargetKind.CORE, false))
                .Returns([membership]);

            var read = await this.dao.ReadAsync(this.transaction.Object, this.projectId, [versionId], CancellationToken.None);
            var readAsElements = await ((IElementDao)this.dao).ReadAsync(this.transaction.Object, this.projectId, [versionId], CancellationToken.None);

            var reader = new Mock<DbDataReader>();
            reader.Setup(dataReader => dataReader.GetFieldValue<byte[]>(0)).Returns(document);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(read.Value, Is.EqualTo([membership]));
                Assert.That(readAsElements.Value, Is.EqualTo([membership]));
                Assert.That(boundParameters.ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value),
                    Is.EquivalentTo(new Dictionary<string, object>
                    {
                        ["project_id"] = this.projectId,
                        ["class_kind"] = (short)ClassKind.Membership,
                        ["version_ids"] = new[] { versionId }
                    }));
                Assert.That(readRow(reader.Object), Is.SameAs(document));
            }

            this.transaction.Setup(sqlTransaction => sqlTransaction.ExecuteReaderAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<DbParameter>>(),
                    It.IsAny<Func<DbDataReader, byte[]>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Unexpected("store.failure", "The store is unavailable."));

            var failed = await this.dao.ReadAsync(this.transaction.Object, this.projectId, [versionId], CancellationToken.None);
            var failedAsElements = await ((IElementDao)this.dao).ReadAsync(this.transaction.Object, this.projectId, [versionId], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failed.FirstError.Code, Is.EqualTo("store.failure"));
                Assert.That(failedAsElements.FirstError.Code, Is.EqualTo("store.failure"));
            }
        }

        [Test]
        public async Task VerifyWriteAsync()
        {
            var commitId = Guid.NewGuid();
            var membership = new Membership { Id = Guid.NewGuid() };
            var created = new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = membership.Id }, Payload = membership };
            var deleted = new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = Guid.NewGuid() } };
            var foreign = new DataVersion { Id = Guid.NewGuid(), Commit = commitId, Identity = new DataIdentity { Id = Guid.NewGuid() }, Payload = new Comment() };
            List<(string Sql, IReadOnlyCollection<DbParameter> Parameters)> statements = [];

            this.transaction.Setup(sqlTransaction => sqlTransaction.ExecuteNonQueryAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<DbParameter>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, IReadOnlyCollection<DbParameter>, CancellationToken>((sql, parameters, _) => statements.Add((sql, parameters)))
                .ReturnsAsync(1);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.dao.WriteAsync(null, this.projectId, [created], CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.dao.WriteAsync(this.transaction.Object, this.projectId, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.dao.WriteAsync(this.transaction.Object, this.projectId, [created, foreign], CancellationToken.None), Throws.TypeOf<ArgumentException>());
                Assert.That((await this.dao.WriteAsync(this.transaction.Object, this.projectId, [], CancellationToken.None)).IsError, Is.False);
                Assert.That(statements, Is.Empty);
            }

            var written = await this.dao.WriteAsync(this.transaction.Object, this.projectId, [created, deleted], CancellationToken.None);

            var payloadParameters = statements[0].Parameters.ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value);
            var tombstoneParameters = statements[1].Parameters.ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(written.IsError, Is.False);
                Assert.That(statements, Has.Count.EqualTo(2));
                Assert.That(statements[0].Sql, Does.Contain("INSERT INTO sysml2.membership_version"));
                Assert.That(payloadParameters["version_ids"], Is.EqualTo([created.Id]));
                Assert.That(payloadParameters["identity_ids"], Is.EqualTo([membership.Id]));
                Assert.That(payloadParameters["commit_ids"], Is.EqualTo([commitId]));
                Assert.That(payloadParameters["class_kind"], Is.EqualTo((short)ClassKind.Membership));
                Assert.That(((ReadOnlyMemory<byte>)payloadParameters["payloads"]).ToArray(), Is.EqualTo(SerializedPayloads));
                Assert.That(statements[1].Sql, Does.Contain("tombstone"));
                Assert.That(tombstoneParameters["version_ids"], Is.EqualTo([deleted.Id]));
                Assert.That(tombstoneParameters["identity_ids"], Is.EqualTo([deleted.Identity.Id]));
                Assert.That(tombstoneParameters, Does.Not.ContainKey("payloads"));
            }

            this.serializer.Verify(serializer => serializer.SerializeAsync(It.Is<IEnumerable<IIdentified>>(payloads => payloads.Single() == membership),
                SerializationModeKind.JSON, false, It.IsAny<Stream>(), It.IsAny<JsonWriterOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            this.transaction.Setup(sqlTransaction => sqlTransaction.ExecuteNonQueryAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<DbParameter>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("store.reference", "The reference is unknown."));

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.dao.WriteAsync(this.transaction.Object, this.projectId, [created], CancellationToken.None)).FirstError.Code, Is.EqualTo("store.reference"));
                Assert.That((await this.dao.WriteAsync(this.transaction.Object, this.projectId, [deleted], CancellationToken.None)).FirstError.Code, Is.EqualTo("store.reference"));
            }
        }
    }
}
