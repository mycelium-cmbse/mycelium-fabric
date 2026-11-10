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

        private static readonly SavedRow[] FirstRows = [new SavedRow { ReqId = "REQ-PAY-001", Option = ImportOption.AsWritten, Ticked = true }];

        private static readonly SavedRow[] SecondRows = [new SavedRow { ReqId = "REQ-PAY-004", Option = ImportOption.AsGoal, Ticked = true }];

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
        public void VerifySaveRows()
        {
            var otherSection = Proposal with { SectionNumber = 6 };
            var otherDocument = Proposal with { FileName = "Other.md" };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.store.Add(Proposal).SavedRows, Is.Null);
                Assert.That(() => this.store.SaveRows(null, FirstRows), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.store.SaveRows(Proposal, null), Throws.TypeOf<ArgumentNullException>());
            }

            this.store.SaveRows(Proposal, FirstRows);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.store.Add(Proposal).SavedRows, Is.SameAs(FirstRows));
                Assert.That(this.store.Add(otherSection).SavedRows, Is.Null);
                Assert.That(this.store.Add(otherDocument).SavedRows, Is.Null);
            }

            this.store.SaveRows(Proposal, SecondRows);

            Assert.That(this.store.Add(Proposal).SavedRows, Is.SameAs(SecondRows));

            // Once full, the store forgets the oldest saved section first.
            foreach (var section in Enumerable.Range(100, RequirementProposalStore.MaximumCount - 1))
            {
                this.store.SaveRows(Proposal with { SectionNumber = section }, FirstRows);
            }

            Assert.That(this.store.Add(Proposal).SavedRows, Is.SameAs(SecondRows));

            this.store.SaveRows(otherSection, FirstRows);

            Assert.That(this.store.Add(Proposal).SavedRows, Is.Null);
        }

        [Test]
        public void VerifyRemove()
        {
            var kept = this.store.Add(Proposal);
            var other = this.store.Add(Proposal);
            var otherSection = this.store.Add(Proposal with { SectionNumber = 6 });

            this.store.SaveRows(Proposal, FirstRows);
            this.store.SaveRows(otherSection, SecondRows);
            this.store.Remove(Guid.NewGuid());

            Assert.That(this.store.Add(Proposal).SavedRows, Is.SameAs(FirstRows));

            this.store.Remove(kept.Id);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.store.TryGet(kept.Id, kept.Token), Is.Null);
                Assert.That(this.store.TryGet(other.Id, other.Token), Is.SameAs(other));
                Assert.That(this.store.Add(Proposal).SavedRows, Is.Null);
                Assert.That(this.store.Add(otherSection).SavedRows, Is.SameAs(SecondRows));
            }
        }
    }
}
