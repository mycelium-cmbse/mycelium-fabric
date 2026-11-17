// ------------------------------------------------------------------------------------------------
//  <copyright file="DatabaseHandlerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    [TestFixture]
    public class DatabaseHandlerTestFixture
    {
        private Mock<IDataStoreTransactionFactory> transactionFactory;

        private Mock<IDataStoreTransaction> transaction;

        private DatabaseHandler handler;

        [SetUp]
        public void SetUp()
        {
            this.transaction = new Mock<IDataStoreTransaction>();
            this.transaction.Setup(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success);
            this.transaction.Setup(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success);

            this.transactionFactory = new Mock<IDataStoreTransactionFactory>();

            this.transactionFactory.Setup(factory => factory.BeginAsync(It.IsAny<TransactionMode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ErrorOrFactory.From(this.transaction.Object));

            this.handler = new DatabaseHandler(this.transactionFactory.Object);
        }

        [Test]
        public async Task VerifyBeginTransactionAsync()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new DatabaseHandler(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.handler.HasActiveTransaction, Is.False);
            }

            var owner = await this.handler.BeginTransactionAsync(TransactionMode.ReadOnly, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(owner.Value.IsOwner, Is.True);
                Assert.That(owner.Value.Mode, Is.EqualTo(TransactionMode.ReadOnly));
                Assert.That(owner.Value.Transaction, Is.SameAs(this.transaction.Object));
                Assert.That(this.handler.HasActiveTransaction, Is.True);
            }

            var joined = await this.handler.BeginTransactionAsync(TransactionMode.ReadOnly, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(joined.Value.IsOwner, Is.False);
                Assert.That(joined.Value.Transaction, Is.SameAs(this.transaction.Object));
            }

            Assert.That(() => this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            this.transactionFactory.Verify(factory => factory.BeginAsync(TransactionMode.ReadOnly, It.IsAny<CancellationToken>()), Times.Once);

            await joined.Value.DisposeAsync();

            Assert.That(this.handler.HasActiveTransaction, Is.True);

            await owner.Value.DisposeAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.handler.HasActiveTransaction, Is.False);
                this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.DisposeAsync(), Times.Once);
            }

            await using (var _ = (await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None)).Value)
            {
                var reader = await this.handler.BeginTransactionAsync(TransactionMode.ReadOnly, CancellationToken.None);

                Assert.That(reader.Value.Mode, Is.EqualTo(TransactionMode.ReadWrite));
            }

            this.transactionFactory.Setup(factory => factory.BeginAsync(It.IsAny<TransactionMode>(), It.IsAny<CancellationToken>())).ReturnsAsync(Error.Unexpected("store.unavailable"));

            var failed = await this.handler.BeginTransactionAsync(TransactionMode.ReadOnly, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failed.IsError, Is.True);
                Assert.That(failed.FirstError.Code, Is.EqualTo("store.unavailable"));
                Assert.That(this.handler.HasActiveTransaction, Is.False);
            }
        }

        [Test]
        public async Task VerifyCommitAsync()
        {
            await using (var owner = (await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None)).Value)
            {
                var joined = await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None);

                Assert.That((await joined.Value.CommitAsync(CancellationToken.None)).IsError, Is.False);

                this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

                Assert.That((await owner.CommitAsync(CancellationToken.None)).IsError, Is.False);

                this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

                Assert.That((await owner.RollbackAsync(CancellationToken.None)).IsError, Is.False);

                this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            await using (var owner = (await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None)).Value)
            {
                var joined = await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None);

                await joined.Value.RollbackAsync(CancellationToken.None);

                var commit = await owner.CommitAsync(CancellationToken.None);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(commit.FirstError.Code, Is.EqualTo("transaction.doomed"));
                    this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
                    this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [Test]
        public async Task VerifyRollbackAsync()
        {
            await using (var owner = (await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None)).Value)
            {
                Assert.That((await owner.RollbackAsync(CancellationToken.None)).IsError, Is.False);
                Assert.That((await owner.RollbackAsync(CancellationToken.None)).IsError, Is.False);

                this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            this.transaction.Setup(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Error.Conflict("store.serialization"));

            await using (var owner = (await this.handler.BeginTransactionAsync(TransactionMode.ReadWrite, CancellationToken.None)).Value)
            {
                Assert.That((await owner.CommitAsync(CancellationToken.None)).FirstError.Code, Is.EqualTo("store.serialization"));

                await owner.RollbackAsync(CancellationToken.None);

                this.transaction.Verify(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
        }
    }
}
