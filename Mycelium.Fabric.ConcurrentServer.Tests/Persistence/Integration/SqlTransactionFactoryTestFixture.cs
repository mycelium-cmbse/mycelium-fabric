// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlTransactionFactoryTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using Npgsql;

    [TestFixture]
    [Category("Integration")]
    public class SqlTransactionFactoryTestFixture
    {
        private const string InsertProjectSql = "INSERT INTO sysml2.project (id, name) VALUES (@id, 'the project')";

        private const string CountProjectSql = "SELECT count(*) FROM sysml2.project WHERE id = @id";

        [Test]
        public async Task VerifyBeginAsync()
        {
            Assert.That(() => new SqlTransactionFactory(null), Throws.TypeOf<ArgumentNullException>());

            var committedId = Guid.NewGuid();

            await using (var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite))
            {
                var inserted = await transaction.ExecuteNonQueryAsync(InsertProjectSql, [new NpgsqlParameter("id", committedId)], CancellationToken.None);
                var duplicate = await transaction.ExecuteNonQueryAsync(InsertProjectSql, [new NpgsqlParameter("id", committedId)], CancellationToken.None);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(inserted.Value, Is.EqualTo(1));
                    Assert.That(duplicate.FirstError.Type, Is.EqualTo(ErrorType.Conflict));
                    Assert.That((await transaction.CommitAsync(CancellationToken.None)).FirstError.Code, Is.EqualTo("store.aborted"));
                }
            }

            await using (var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite))
            {
                await transaction.ExecuteNonQueryAsync(InsertProjectSql, [new NpgsqlParameter("id", committedId)], CancellationToken.None);

                Assert.That((await transaction.CommitAsync(CancellationToken.None)).IsError, Is.False);
            }

            var rolledBackId = Guid.NewGuid();

            await using (var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite))
            {
                await transaction.ExecuteNonQueryAsync(InsertProjectSql, [new NpgsqlParameter("id", rolledBackId)], CancellationToken.None);

                Assert.That((await transaction.RollbackAsync(CancellationToken.None)).IsError, Is.False);
            }

            await using (var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadOnly))
            {
                var counted = await transaction.ExecuteReaderAsync(CountProjectSql, [new NpgsqlParameter("id", committedId)], reader => reader.GetInt64(0), CancellationToken.None);
                var refused = await transaction.ExecuteNonQueryAsync(InsertProjectSql, [new NpgsqlParameter("id", Guid.NewGuid())], CancellationToken.None);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(counted.Value, Is.EqualTo([1L]));
                    Assert.That(refused.FirstError.Code, Is.EqualTo("store.failure"));
                }
            }

            await using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Timeout=1");

            var failed = await new SqlTransactionFactory(unreachable).BeginAsync(TransactionMode.ReadWrite, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await PostgreSqlTestHost.CountAsync(CountProjectSql, new NpgsqlParameter("id", committedId)), Is.EqualTo(1));
                Assert.That(await PostgreSqlTestHost.CountAsync(CountProjectSql, new NpgsqlParameter("id", rolledBackId)), Is.Zero);
                Assert.That(failed.FirstError.Type, Is.EqualTo(ErrorType.Unexpected));
            }
        }
    }
}
