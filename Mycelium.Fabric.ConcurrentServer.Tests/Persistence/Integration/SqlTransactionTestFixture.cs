// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlTransactionTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence.Integration
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using Npgsql;

    [TestFixture]
    [Category("Integration")]
    public class SqlTransactionTestFixture
    {
        private const string InsertProjectSql = "INSERT INTO sysml2.project (id, name) VALUES (@id, 'the project')";

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new SqlTransaction(null, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new SqlTransaction(new NpgsqlConnection(), null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public async Task VerifyExecuteNonQueryAsync()
        {
            await using var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadWrite);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => transaction.ExecuteNonQueryAsync(null, [], CancellationToken.None), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => transaction.ExecuteNonQueryAsync(string.Empty, [], CancellationToken.None), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => transaction.ExecuteNonQueryAsync(InsertProjectSql, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            }

            var inserted = await transaction.ExecuteNonQueryAsync(InsertProjectSql, [new NpgsqlParameter("id", Guid.NewGuid())], CancellationToken.None);
            var refused = await transaction.ExecuteNonQueryAsync("INSERT INTO sysml2.no_such_table VALUES (1)", [], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(inserted.Value, Is.EqualTo(1));
                Assert.That(refused.FirstError.Type, Is.EqualTo(ErrorType.Unexpected));
            }
        }

        [Test]
        public async Task VerifyExecuteReaderAsync()
        {
            await using var transaction = await PostgreSqlTestHost.BeginAsync(TransactionMode.ReadOnly);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => transaction.ExecuteReaderAsync("SELECT 1", [], (Func<DbDataReader, int>)null, CancellationToken.None),
                    Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => transaction.ExecuteReaderAsync(string.Empty, [], reader => reader.GetInt32(0), CancellationToken.None), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => transaction.ExecuteReaderAsync("SELECT 1", null, reader => reader.GetInt32(0), CancellationToken.None),
                    Throws.TypeOf<ArgumentNullException>());
            }

            var rows = await transaction.ExecuteReaderAsync("SELECT value FROM generate_series(1, 3) AS value ORDER BY value", [], reader => reader.GetInt32(0),
                CancellationToken.None);
            var none = await transaction.ExecuteReaderAsync("SELECT 1 WHERE false", [], reader => reader.GetInt32(0), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rows.Value, Is.EqualTo([1, 2, 3]));
                Assert.That(none.Value, Is.Empty);
            }
        }
    }
}
