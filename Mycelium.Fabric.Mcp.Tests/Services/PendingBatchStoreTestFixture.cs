// ------------------------------------------------------------------------------------------------
//  <copyright file="PendingBatchStoreTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="PendingBatchStore"/> class.
    /// </summary>
    [TestFixture]
    public class PendingBatchStoreTestFixture
    {
        private PendingBatchStore pendingBatchStore;

        private IReadOnlyList<ModelChange> changes;

        [SetUp]
        public void SetUp()
        {
            this.pendingBatchStore = new PendingBatchStore();
            this.changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
        }

        [Test]
        public void VerifyTryAdd()
        {
            var hasPendingBatchBefore = this.pendingBatchStore.HasPendingBatch;
            var pendingBatch = this.pendingBatchStore.TryAdd(this.changes);
            var secondBatch = this.pendingBatchStore.TryAdd(this.changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.pendingBatchStore.TryAdd(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(hasPendingBatchBefore, Is.False);
                Assert.That(pendingBatch.Changes, Is.SameAs(this.changes));
                Assert.That(pendingBatch.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(pendingBatch.Token, Does.Match("^[0-9a-f]{32}$"));
                Assert.That(secondBatch, Is.Null);
                Assert.That(this.pendingBatchStore.HasPendingBatch, Is.True);
                Assert.That(this.pendingBatchStore.PendingBatch, Is.SameAs(pendingBatch));
            }
        }

        [Test]
        public void VerifyTryTake()
        {
            var pendingBatch = this.pendingBatchStore.TryAdd(this.changes);

            var withWrongToken = this.pendingBatchStore.TryTake(pendingBatch.Id, new string('0', 32));
            var withNullToken = this.pendingBatchStore.TryTake(pendingBatch.Id, null);
            var withWrongId = this.pendingBatchStore.TryTake(Guid.NewGuid(), pendingBatch.Token);
            var hasPendingBatchAfterWrongCalls = this.pendingBatchStore.HasPendingBatch;

            var taken = this.pendingBatchStore.TryTake(pendingBatch.Id, pendingBatch.Token);
            var takenAgain = this.pendingBatchStore.TryTake(pendingBatch.Id, pendingBatch.Token);
            var nextBatch = this.pendingBatchStore.TryAdd(this.changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withWrongToken, Is.Null);
                Assert.That(withNullToken, Is.Null);
                Assert.That(withWrongId, Is.Null);
                Assert.That(hasPendingBatchAfterWrongCalls, Is.True);
                Assert.That(taken, Is.SameAs(pendingBatch));
                Assert.That(takenAgain, Is.Null);
                Assert.That(nextBatch.Token, Is.Not.EqualTo(pendingBatch.Token));
            }
        }
    }
}
