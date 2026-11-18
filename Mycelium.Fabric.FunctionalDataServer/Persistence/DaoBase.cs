// ------------------------------------------------------------------------------------------------
//  <copyright file="DaoBase.cs" company="Starion Group S.A.">
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
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Mycelium.SDK.DTO;
    using Mycelium.SDK.Serializer.Json;

    using Npgsql;

    /// <summary>
    /// Implements the <see cref="IDao{TDto}"/> operations once for the generated data access objects. Each generated data access object supplies the
    /// statements of its concrete class and binds all their parameters; this base decides which bindings each command uses and writes no SQL.
    /// </summary>
    /// <typeparam name="TDto">The DTO interface of the concrete model class.</typeparam>
    [ExcludeFromCodeCoverage]
    public abstract class DaoBase<TDto> : IDao<TDto> where TDto : class, IThing
    {
        /// <summary>
        /// The serializer that produces the JSON document stored in the <c>data</c> column.
        /// </summary>
        private readonly ISerializer serializer;

        /// <summary>
        /// The deserializer that turns a stored JSON document back into a DTO.
        /// </summary>
        private readonly IDeSerializer deSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaoBase{TDto}"/> class.
        /// </summary>
        /// <param name="serializer">The serializer that produces the JSON document stored in the <c>data</c> column.</param>
        /// <param name="deSerializer">The deserializer that turns a stored JSON document back into a DTO.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serializer"/> or <paramref name="deSerializer"/> is null.</exception>
        protected DaoBase(ISerializer serializer, IDeSerializer deSerializer)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.deSerializer = deSerializer ?? throw new ArgumentNullException(nameof(deSerializer));
        }

        /// <summary>
        /// Gets the query that reads the JSON document of every stored object of the concrete class, ordered by identifier.
        /// </summary>
        protected abstract string ReadAllSql { get; }

        /// <summary>
        /// Gets the query that reads the JSON document of the stored object of the concrete class with the bound identifier.
        /// </summary>
        protected abstract string ReadByIdSql { get; }

        /// <summary>
        /// Gets the batch that stores a new object: its root row, one row per class of its inheritance chain and its link-table rows.
        /// </summary>
        protected abstract string CreateSql { get; }

        /// <summary>
        /// Gets the statement that replaces the JSON document of the stored object of the concrete class with the bound identifier.
        /// </summary>
        protected abstract string UpdateDataSql { get; }

        /// <summary>
        /// Gets the statement that deletes the stored object of the concrete class with the bound identifier.
        /// </summary>
        protected abstract string DeleteSql { get; }

        /// <summary>
        /// Reads every stored object of the exact concrete class, ordered by identifier.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored objects, or an empty list when none exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task<IReadOnlyList<TDto>> ReadAllAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            await using var command = CreateCommand(transaction, this.ReadAllSql);

            this.AddClassKindParameter(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            List<TDto> dtos = [];

            while (await reader.ReadAsync(cancellationToken))
            {
                dtos.Add(this.DeSerialize(reader));
            }

            return dtos;
        }

        /// <summary>
        /// Reads the stored object of the exact concrete class that has the specified identifier.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="id">The identifier of the object to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored object, or <see langword="null"/> when no object of the class has <paramref name="id"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task<TDto> ReadByIdAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            await using var command = CreateCommand(transaction, this.ReadByIdSql);

            this.AddIdentityParameter(command, id);
            this.AddClassKindParameter(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return await reader.ReadAsync(cancellationToken) ? this.DeSerialize(reader) : null;
        }

        /// <summary>
        /// Stores a new object: its JSON document, one row per class of its inheritance chain and its link-table rows.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statements.</param>
        /// <param name="dto">The object to store, identified by its application-supplied <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>A task that completes when every statement has been executed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="dto"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a statement.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task CreateAsync(NpgsqlTransaction transaction, TDto dto, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(dto);

            await using var data = new MemoryStream();

            await this.serializer.SerializeAsync(dto, data, new JsonWriterOptions(), cancellationToken);

            await using var command = CreateCommand(transaction, this.CreateSql);

            this.AddIdentityParameter(command, dto.Id);
            this.AddClassKindParameter(command);
            this.AddDataParameter(command, GetBytes(data));
            this.AddReferenceParameters(command, dto);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the JSON document of a stored object, then only the scalar reference columns and link-table rows that differ from
        /// <paramref name="dto"/>.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statements.</param>
        /// <param name="dto">The new state of the object, identified by its <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// <see langword="true"/> when the object was updated; <see langword="false"/> when no object of the class has the identifier, in which case
        /// nothing is written.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="dto"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a statement.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task<bool> UpdateAsync(NpgsqlTransaction transaction, TDto dto, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(dto);

            await using var data = new MemoryStream();

            await this.serializer.SerializeAsync(dto, data, new JsonWriterOptions(), cancellationToken);

            await using (var command = CreateCommand(transaction, this.UpdateDataSql))
            {
                this.AddIdentityParameter(command, dto.Id);
                this.AddClassKindParameter(command);
                this.AddDataParameter(command, GetBytes(data));

                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    return false;
                }
            }

            await this.UpdateRelationsAsync(transaction, dto, cancellationToken);

            return true;
        }

        /// <summary>
        /// Deletes a stored object; the database removes its inheritance rows, link rows and contained objects.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statement.</param>
        /// <param name="id">The identifier of the object to delete.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// <see langword="true"/> when the object was deleted; <see langword="false"/> when no object of the class has <paramref name="id"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the statement.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        public async Task<bool> DeleteAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            await using var command = CreateCommand(transaction, this.DeleteSql);

            this.AddIdentityParameter(command, id);
            this.AddClassKindParameter(command);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        /// <summary>
        /// Adds the identifier parameter that selects one stored object.
        /// </summary>
        /// <param name="command">The command that receives the parameter.</param>
        /// <param name="id">The identifier of the object.</param>
        protected abstract void AddIdentityParameter(NpgsqlCommand command, Guid id);

        /// <summary>
        /// Adds the <c>class_kind</c> parameter that restricts the statements to objects of the exact concrete class.
        /// </summary>
        /// <param name="command">The command that receives the parameter.</param>
        protected abstract void AddClassKindParameter(NpgsqlCommand command);

        /// <summary>
        /// Adds the serialized JSON document stored in the <c>data</c> column.
        /// </summary>
        /// <param name="command">The command that receives the parameter.</param>
        /// <param name="data">The UTF-8 bytes of the serialized JSON document.</param>
        protected abstract void AddDataParameter(NpgsqlCommand command, ReadOnlyMemory<byte> data);

        /// <summary>
        /// Adds one parameter per scalar reference column and per link-table collection of the concrete class and its ancestors.
        /// </summary>
        /// <param name="command">The command that receives the parameters.</param>
        /// <param name="dto">The object whose references are stored.</param>
        protected abstract void AddReferenceParameters(NpgsqlCommand command, TDto dto);

        /// <summary>
        /// Writes the scalar reference columns and link-table rows of a stored object that differ from <paramref name="dto"/>; this base
        /// implementation writes nothing, for a class that stores no relational state.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statements.</param>
        /// <param name="dto">The new state of the object, identified by its <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>A task that completes when every statement has been executed.</returns>
        protected virtual Task UpdateRelationsAsync(NpgsqlTransaction transaction, TDto dto, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes a relation-update batch bound to the identifier and the references of <paramref name="dto"/>.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the batch.</param>
        /// <param name="commandText">The relation-update batch to execute.</param>
        /// <param name="dto">The new state of the object, identified by its <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>A task that completes when the batch has been executed.</returns>
        protected async Task ExecuteRelationStatementsAsync(NpgsqlTransaction transaction, string commandText, TDto dto, CancellationToken cancellationToken)
        {
            await using var command = CreateCommand(transaction, commandText);

            this.AddIdentityParameter(command, dto.Id);
            this.AddReferenceParameters(command, dto);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Creates a command that runs on the connection of the caller's transaction.
        /// </summary>
        /// <param name="transaction">The caller's open transaction.</param>
        /// <param name="commandText">The parameterized SQL to execute.</param>
        /// <returns>The command, enlisted in <paramref name="transaction"/>.</returns>
        private static NpgsqlCommand CreateCommand(NpgsqlTransaction transaction, string commandText)
        {
            return new NpgsqlCommand(commandText, transaction.Connection, transaction);
        }

        /// <summary>
        /// Gets the written bytes of a serialized JSON document without copying them.
        /// </summary>
        /// <param name="data">The stream holding the serialized JSON document.</param>
        /// <returns>The written bytes of <paramref name="data"/>.</returns>
        private static ReadOnlyMemory<byte> GetBytes(MemoryStream data)
        {
            return new ReadOnlyMemory<byte>(data.GetBuffer(), 0, (int)data.Length);
        }

        /// <summary>
        /// Deserializes the JSON document in the first column of the current row.
        /// </summary>
        /// <param name="reader">The reader positioned on a row.</param>
        /// <returns>The DTO the document describes.</returns>
        private TDto DeSerialize(NpgsqlDataReader reader)
        {
            using var stream = new MemoryStream(reader.GetFieldValue<byte[]>(0));

            return (TDto)this.deSerializer.DeSerialize(stream).Single();
        }
    }
}
