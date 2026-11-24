// ------------------------------------------------------------------------------------------------
//  <copyright file="ThingRepository.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Mycelium.SDK.DTO;

    using Npgsql;

    using NpgsqlTypes;

    /// <summary>
    /// Reads stored objects of any concrete class: it first reads their class kinds from the <c>fabric.thing</c> root table, then reads the
    /// objects of each class kind through the data access object that the <see cref="IDaoProvider"/> resolves.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ThingRepository : IThingRepository
    {
        /// <summary>
        /// The query that reads the identifier and class kind of every stored object among the bound identifiers.
        /// </summary>
        private const string ReadClassKindsSql = """
            SELECT id, class_kind
            FROM fabric.thing
            WHERE id = ANY(@ids);
            """;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThingRepository"/> class.
        /// </summary>
        /// <param name="daoProvider">The provider of the data access object of each class kind.</param>
        /// <exception cref="ArgumentNullException"><paramref name="daoProvider"/> is null.</exception>
        public ThingRepository(IDaoProvider daoProvider)
        {
            this.DaoProvider = daoProvider ?? throw new ArgumentNullException(nameof(daoProvider));
        }

        /// <summary>
        /// Gets the provider of the data access object of each class kind.
        /// </summary>
        private IDaoProvider DaoProvider { get; }

        /// <summary>
        /// Reads the stored object that has the specified identifier, whatever its concrete class.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the queries.</param>
        /// <param name="id">The identifier of the object to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored object, or <see langword="null"/> when no object has <paramref name="id"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="ArgumentException">The stored class kind of the object is handled by no data access object.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task<IThing> ReadByIdAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            var classKinds = await ReadClassKindsAsync(transaction, [id], cancellationToken);

            return classKinds is [var (_, classKind)]
                ? await this.DaoProvider.Resolve(classKind).ReadThingByIdAsync(transaction, id, cancellationToken)
                : null;
        }

        /// <summary>
        /// Reads the stored objects that have the specified identifiers, whatever their concrete classes.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the queries.</param>
        /// <param name="ids">The identifiers of the objects to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// The stored objects found, each once and in no guaranteed order; identifiers with no object are skipped, and an empty list of identifiers
        /// gives an empty list without a query.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="ids"/> is null.</exception>
        /// <exception cref="ArgumentException">The stored class kind of an object is handled by no data access object.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task<IReadOnlyList<IThing>> ReadByIdsAsync(NpgsqlTransaction transaction, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ids);
            ArgumentNullException.ThrowIfNull(transaction);

            if (ids.Count == 0)
            {
                return [];
            }

            var classKinds = await ReadClassKindsAsync(transaction, ids, cancellationToken);

            List<IThing> things = [];

            foreach (var classKindGroup in classKinds.GroupBy(row => row.ClassKind, row => row.Id, StringComparer.Ordinal))
            {
                var dao = this.DaoProvider.Resolve(classKindGroup.Key);

                things.AddRange(await dao.ReadThingsByIdsAsync(transaction, [.. classKindGroup], cancellationToken));
            }

            return things;
        }

        /// <summary>
        /// Reads the identifier and class kind of every stored object among the specified identifiers.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="ids">The identifiers of the objects.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>One row per stored object found, each object once.</returns>
        private static async Task<IReadOnlyList<(Guid Id, string ClassKind)>> ReadClassKindsAsync(NpgsqlTransaction transaction, IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand(ReadClassKindsSql, transaction.Connection, transaction);

            command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                Value = ids as Guid[] ?? [.. ids]
            });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            List<(Guid Id, string ClassKind)> classKinds = [];

            while (await reader.ReadAsync(cancellationToken))
            {
                classKinds.Add((reader.GetGuid(0), reader.GetString(1)));
            }

            return classKinds;
        }
    }
}
