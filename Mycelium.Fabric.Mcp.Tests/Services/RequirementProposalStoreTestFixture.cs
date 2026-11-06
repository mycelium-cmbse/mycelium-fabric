// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementProposalStoreTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extraction;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementProposalStore"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementProposalStoreTestFixture
    {
        private static readonly RequirementProposal Proposal = new("EOSat1-requirements.md", 5, "3. Payload requirements", null, "PayloadRequirements", [], []);

        private RequirementProposalStore store;

        [SetUp]
        public void SetUp()
        {
            this.store = new RequirementProposalStore();
        }

        [Test]
        public void VerifyAdd()
        {
            var first = this.store.Add(Proposal);
            var second = this.store.Add(Proposal);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(first.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(first.Token, Has.Length.EqualTo(32).And.Match("^[0-9a-f]+$"));
                Assert.That(first.SectionTitle, Is.EqualTo(Proposal.SectionTitle));
                Assert.That(second.Id, Is.Not.EqualTo(first.Id));
                Assert.That(second.Token, Is.Not.EqualTo(first.Token));
                Assert.That(Proposal.Token, Is.Null);
                Assert.That(() => this.store.Add(null), Throws.TypeOf<ArgumentNullException>());
            }

            // Once full, the store forgets the oldest proposal first.
            var added = Enumerable.Range(0, RequirementProposalStore.MaximumCount).Select(_ => this.store.Add(Proposal)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.store.TryGet(first.Id, first.Token), Is.Null);
                Assert.That(this.store.TryGet(second.Id, second.Token), Is.Null);
                Assert.That(this.store.TryGet(added[0].Id, added[0].Token), Is.SameAs(added[0]));
                Assert.That(this.store.TryGet(added[^1].Id, added[^1].Token), Is.SameAs(added[^1]));
            }
        }

        [Test]
        public void VerifyTryGet()
        {
            var kept = this.store.Add(Proposal);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.store.TryGet(kept.Id, kept.Token), Is.SameAs(kept));
                Assert.That(this.store.TryGet(kept.Id, kept.Token), Is.SameAs(kept));
                Assert.That(this.store.TryGet(kept.Id, "0123456789abcdef0123456789abcdef"), Is.Null);
                Assert.That(this.store.TryGet(kept.Id, null), Is.Null);
                Assert.That(this.store.TryGet(Guid.NewGuid(), kept.Token), Is.Null);
            }
        }

        [Test]
        public void VerifyRemove()
        {
            var kept = this.store.Add(Proposal);
            var other = this.store.Add(Proposal);

            this.store.Remove(kept.Id);
            this.store.Remove(Guid.NewGuid());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.store.TryGet(kept.Id, kept.Token), Is.Null);
                Assert.That(this.store.TryGet(other.Id, other.Token), Is.SameAs(other));
            }
        }
    }
}
