// ------------------------------------------------------------------------------------------------
//  <copyright file="SearchFilterTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence
{
    using System;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.PIM;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Suite of tests for the <see cref="SearchFilter"/> class.
    /// </summary>
    [TestFixture]
    public class SearchFilterTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        [Test]
        public void VerifyEqualTo()
        {
            var filter = SearchFilter.EqualTo("owningProject", ProjectId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Property, Is.EqualTo("owningProject"));
                Assert.That(filter.Operator, Is.EqualTo(Operator.equalto));
                Assert.That(filter.Value, Has.Count.EqualTo(1));
                Assert.That(filter.Value[0].Kind, Is.EqualTo(ConstraintValueKind.Guid));
                Assert.That(filter.Inverse, Is.False);
            }

            Assert.That(() => SearchFilter.EqualTo(null, ProjectId), Throws.TypeOf<ArgumentException>());

            Assert.That(() => SearchFilter.EqualTo("  ", ProjectId), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void VerifyIn()
        {
            var filter = SearchFilter.In("id", ProjectId, CommitId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Property, Is.EqualTo("id"));
                Assert.That(filter.Operator, Is.EqualTo(Operator.@in));
                Assert.That(filter.Value, Has.Count.EqualTo(2));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => SearchFilter.In("id"), Throws.TypeOf<ArgumentException>());
                Assert.That(() => SearchFilter.In("id", null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => SearchFilter.In(null, ProjectId), Throws.TypeOf<ArgumentException>());
            }
        }

        [Test]
        public void VerifyAnd()
        {
            var filter = SearchFilter.And(SearchFilter.EqualTo("owningProject", ProjectId), SearchFilter.EqualTo("head", CommitId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Operator, Is.EqualTo(JoinOperator.AND));
                Assert.That(filter.Filters, Has.Count.EqualTo(2));
            }

            // Systems Modeling API and Services 1.0 §7.1.4 composes at least two conditions.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => SearchFilter.And(SearchFilter.EqualTo("head", CommitId)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => SearchFilter.And(null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public void VerifyOr()
        {
            var filter = SearchFilter.Or(SearchFilter.EqualTo("owningProject", ProjectId), SearchFilter.EqualTo("head", CommitId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Operator, Is.EqualTo(JoinOperator.OR));
                Assert.That(filter.Filters, Has.Count.EqualTo(2));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => SearchFilter.Or(SearchFilter.EqualTo("head", CommitId)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => SearchFilter.Or(null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public void VerifyNegate()
        {
            var leaf = SearchFilter.EqualTo("owningProject", ProjectId);

            var negatedLeaf = (PrimitiveSearchFilter)leaf.Negate();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(negatedLeaf.Inverse, Is.True);
                Assert.That(negatedLeaf.Property, Is.EqualTo("owningProject"));
                Assert.That(negatedLeaf.Operator, Is.EqualTo(Operator.equalto));
                Assert.That(leaf.Inverse, Is.False, "Negation returns a copy and leaves the original alone.");
            }

            Assert.That(((PrimitiveSearchFilter)negatedLeaf.Negate()).Inverse, Is.False);

            // De Morgan: the negation of a conjunction is the disjunction of the negations.
            var composite = SearchFilter.And(leaf, SearchFilter.EqualTo("head", CommitId));

            var negatedComposite = (CompositeSearchFilter)composite.Negate();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(negatedComposite.Operator, Is.EqualTo(JoinOperator.OR));
                Assert.That(negatedComposite.Filters, Has.Count.EqualTo(2));
                Assert.That(((PrimitiveSearchFilter)negatedComposite.Filters[0]).Inverse, Is.True);
                Assert.That(((PrimitiveSearchFilter)negatedComposite.Filters[^1]).Inverse, Is.True);
                Assert.That(composite.Operator, Is.EqualTo(JoinOperator.AND), "Negation returns a copy and leaves the original alone.");
            }

            Assert.That(((CompositeSearchFilter)negatedComposite.Negate()).Operator, Is.EqualTo(JoinOperator.AND));
        }
    }
}
