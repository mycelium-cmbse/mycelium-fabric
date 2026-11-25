// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementDaoBase.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Npgsql;

    using NpgsqlTypes;

    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Implements, once for every metaclass, the <see cref="IElementDao{TElement}"/> whose SQL a generated subclass supplies.
    /// </summary>
    /// <typeparam name="TElement">The exact data transfer object type of the metaclass.</typeparam>
    /// <remarks>
    /// The generated SQL takes the parameters <c>project_id</c>, <c>class_kind</c> and <c>version_ids</c>, and to write <c>identity_ids</c>,
    /// <c>commit_ids</c> and <c>payloads</c>, the JSON array of the payloads in the order of <c>version_ids</c>. References are kept out of
    /// <c>stored_json</c> and read back from their columns and link tables; a property the metaclass derives through a redefinition is not stored.
    /// </remarks>
    public abstract class ElementDaoBase<TElement> : IElementDao<TElement> where TElement : class, IElement
    {
        /// <summary>
        /// The statement that writes each deletion as a tombstone <c>element_version</c> row.
        /// </summary>
        private const string WriteTombstonesSql = """
            INSERT INTO sysml2.element_version (project_id, version_id, identity_id, commit_id, class_kind, tombstone)
            SELECT @project_id, version.version_id, version.identity_id, version.commit_id, @class_kind, true
            FROM unnest(@version_ids, @identity_ids, @commit_ids) AS version(version_id, identity_id, commit_id);
            """;

        /// <summary>
        /// The options of the writer that serializes the payloads.
        /// </summary>
        private static readonly JsonWriterOptions PayloadWriterOptions = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ElementDaoBase{TElement}"/> class.
        /// </summary>
        /// <param name="serializer">The <see cref="ISerializer"/> that writes the stored JSON of an element.</param>
        /// <param name="deSerializer">The <see cref="IDeSerializer"/> that reads an element back from its stored JSON and references.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serializer"/> or <paramref name="deSerializer"/> is <c>null</c>.</exception>
        protected ElementDaoBase(ISerializer serializer, IDeSerializer deSerializer)
        {
            this.Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.DeSerializer = deSerializer ?? throw new ArgumentNullException(nameof(deSerializer));
        }

        /// <summary>
        /// Gets the <see cref="SysML2.NET.Core.ClassKind"/> of the metaclass whose versions are stored.
        /// </summary>
        public abstract ClassKind ClassKind { get; }

        /// <summary>
        /// Gets the exact data transfer object type of the metaclass whose versions are stored.
        /// </summary>
        public Type DataType => typeof(TElement);

        /// <summary>
        /// Gets the query that reads the live versions with the given identifiers as one JSON array.
        /// </summary>
        protected abstract string ReadSql { get; }

        /// <summary>
        /// Gets the statement that writes versions with payload in every table of the metaclass.
        /// </summary>
        protected abstract string WriteSql { get; }

        /// <summary>
        /// Gets the <see cref="IDeSerializer"/> that reads an element back from its stored JSON and references.
        /// </summary>
        private IDeSerializer DeSerializer { get; }

        /// <summary>
        /// Gets the <see cref="ISerializer"/> that writes the stored JSON of an element.
        /// </summary>
        private ISerializer Serializer { get; }

        /// <summary>
        /// Reads, in one query, the live versions of elements of the metaclass with any of the given identifiers.
        /// </summary>
        /// <param name="transaction">The <see cref="ISqlTransaction"/> the operation takes part in.</param>
        /// <param name="projectId">The identifier of the project the versions belong to.</param>
        /// <param name="versionIds">The identifiers of the versions that are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>
        /// The elements, none for an identifier that names a deletion, another metaclass or no version, or the errors the store raised.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="versionIds"/> is <c>null</c>.</exception>
        public async Task<ErrorOr<IReadOnlyList<TElement>>> ReadAsync(ISqlTransaction transaction, Guid projectId, IReadOnlyCollection<Guid> versionIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(versionIds);

            if (versionIds.Count == 0)
            {
                IReadOnlyList<TElement> none = [];

                return ErrorOrFactory.From(none);
            }

            NpgsqlParameter[] parameters =
            [
                CreateParameter("project_id", NpgsqlDbType.Uuid, projectId),
                CreateParameter("class_kind", NpgsqlDbType.Smallint, (short)this.ClassKind),
                CreateIdentifiersParameter("version_ids", versionIds)
            ];

            var documents = await transaction.ExecuteReaderAsync(this.ReadSql, parameters, ReadDocument, cancellationToken);

            return documents.IsError
                ? documents.Errors
                : ErrorOrFactory.From<IReadOnlyList<TElement>>([.. documents.Value.SelectMany(this.DeSerialize)]);
        }

        /// <summary>
        /// Writes, in at most two statements, versions of elements of the metaclass.
        /// </summary>
        /// <param name="transaction">The <see cref="ISqlTransaction"/> the operation takes part in.</param>
        /// <param name="projectId">The identifier of the project the versions belong to.</param>
        /// <param name="versions">The versions that are written, each with its identifier, identity and commit; one without payload is a deletion.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors the store raised.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="versions"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">A payload is not exactly of type <typeparamref name="TElement"/>.</exception>
        public async Task<ErrorOr<Success>> WriteAsync(ISqlTransaction transaction, Guid projectId, IReadOnlyCollection<DataVersion> versions,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(versions);

            var foreign = versions.FirstOrDefault(version => version.Payload != null && version.Payload.GetType() != typeof(TElement));

            if (foreign != null)
            {
                throw new ArgumentException($"The version {foreign.Id} carries a {foreign.Payload.GetType().Name}, which the {this.ClassKind} DAO does not store.",
                    nameof(versions));
            }

            List<DataVersion> payloadVersions = [.. versions.Where(version => version.Payload != null)];
            List<DataVersion> tombstoneVersions = [.. versions.Where(version => version.Payload == null)];

            if (payloadVersions.Count != 0)
            {
                await using var payloads = new MemoryStream();

                await this.Serializer.SerializeAsync(payloadVersions.Select(version => version.Payload), SerializationModeKind.JSON, false, payloads,
                    PayloadWriterOptions, cancellationToken);

                var parameters = this.CreateVersionParameters(projectId, payloadVersions);
                parameters.Add(CreateParameter("payloads", NpgsqlDbType.Jsonb, new ReadOnlyMemory<byte>(payloads.GetBuffer(), 0, (int)payloads.Length)));

                var written = await transaction.ExecuteNonQueryAsync(this.WriteSql, parameters, cancellationToken);

                if (written.IsError)
                {
                    return written.Errors;
                }
            }

            if (tombstoneVersions.Count != 0)
            {
                var written = await transaction.ExecuteNonQueryAsync(WriteTombstonesSql, this.CreateVersionParameters(projectId, tombstoneVersions), cancellationToken);

                if (written.IsError)
                {
                    return written.Errors;
                }
            }

            return Result.Success;
        }

        /// <summary>
        /// Reads, in one query, the live versions of elements of the metaclass with any of the given identifiers, as <see cref="IElement"/>s.
        /// </summary>
        /// <param name="transaction">The <see cref="ISqlTransaction"/> the operation takes part in.</param>
        /// <param name="projectId">The identifier of the project the versions belong to.</param>
        /// <param name="versionIds">The identifiers of the versions that are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>
        /// The elements, none for an identifier that names a deletion, another metaclass or no version, or the errors the store raised.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="versionIds"/> is <c>null</c>.</exception>
        async Task<ErrorOr<IReadOnlyList<IElement>>> IElementDao.ReadAsync(ISqlTransaction transaction, Guid projectId, IReadOnlyCollection<Guid> versionIds,
            CancellationToken cancellationToken)
        {
            var read = await this.ReadAsync(transaction, projectId, versionIds, cancellationToken);

            return read.IsError ? read.Errors : ErrorOrFactory.From<IReadOnlyList<IElement>>(read.Value);
        }

        /// <summary>
        /// Creates a <c>uuid[]</c> parameter, whose type Npgsql infers from its <see cref="Guid"/> array value.
        /// </summary>
        /// <param name="name">The name the SQL refers to the parameter by.</param>
        /// <param name="identifiers">The identifiers the parameter holds, in order.</param>
        /// <returns>The <see cref="NpgsqlParameter{T}"/>.</returns>
        private static NpgsqlParameter<Guid[]> CreateIdentifiersParameter(string name, IEnumerable<Guid> identifiers) => new(name, [.. identifiers]);

        /// <summary>
        /// Creates a typed parameter.
        /// </summary>
        /// <param name="name">The name the SQL refers to the parameter by.</param>
        /// <param name="type">The <see cref="NpgsqlDbType"/> of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <returns>The <see cref="NpgsqlParameter"/>.</returns>
        private static NpgsqlParameter CreateParameter(string name, NpgsqlDbType type, object value) => new(name, type) { Value = value };

        /// <summary>
        /// Reads the JSON array of the current row.
        /// </summary>
        /// <param name="reader">The <see cref="DbDataReader"/> positioned on the row.</param>
        /// <returns>The UTF-8 bytes of the array.</returns>
        private static byte[] ReadDocument(DbDataReader reader) => reader.GetFieldValue<byte[]>(0);

        /// <summary>
        /// Creates the parameters that identify versions, their identities and their commits, aligned by position.
        /// </summary>
        /// <param name="projectId">The identifier of the project the versions belong to.</param>
        /// <param name="versions">The versions the parameters describe.</param>
        /// <returns>The parameters shared by both write statements.</returns>
        private List<NpgsqlParameter> CreateVersionParameters(Guid projectId, IReadOnlyCollection<DataVersion> versions) =>
        [
            CreateParameter("project_id", NpgsqlDbType.Uuid, projectId),
            CreateParameter("class_kind", NpgsqlDbType.Smallint, (short)this.ClassKind),
            CreateIdentifiersParameter("version_ids", versions.Select(version => version.Id)),
            CreateIdentifiersParameter("identity_ids", versions.Select(version => version.Identity.Id)),
            CreateIdentifiersParameter("commit_ids", versions.Select(version => version.Commit))
        ];

        /// <summary>
        /// Reads the elements of one JSON array.
        /// </summary>
        /// <param name="document">The UTF-8 bytes of the array.</param>
        /// <returns>The elements of the array, in its order.</returns>
        private IEnumerable<TElement> DeSerialize(byte[] document)
        {
            using var stream = new MemoryStream(document);

            return [.. this.DeSerializer.DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.CORE, false).Cast<TElement>()];
        }
    }
}
