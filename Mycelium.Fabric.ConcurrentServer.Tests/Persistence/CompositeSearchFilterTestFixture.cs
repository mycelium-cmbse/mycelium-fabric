// ------------------------------------------------------------------------------------------------
//  <copyright file="CompositeSearchFilterTestFixture.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Suite of tests for the <see cref="CompositeSearchFilter"/> class.
    /// </summary>
    [TestFixture]
    public class CompositeSearchFilterTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        [Test]
        public void VerifyProperties()
        {
            var filter = new CompositeSearchFilter();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter, Is.InstanceOf<SearchFilter>());
                Assert.That(filter.Filters, Has.Count.EqualTo(0));
                Assert.That(filter.Operator, Is.EqualTo(JoinOperator.AND));
            }

            var owningProject = new PrimitiveSearchFilter { Property = "owningProject", Operator = Operator.equalto, Value = [ProjectId] };
            var head = new PrimitiveSearchFilter { Property = "head", Operator = Operator.equalto, Value = [CommitId] };

            filter.Filters = [owningProject, head];
            filter.Operator = JoinOperator.OR;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Filters, Has.Count.EqualTo(2));
                Assert.That(filter.Filters[0], Is.SameAs(owningProject));
                Assert.That(filter.Filters[^1], Is.SameAs(head));
                Assert.That(filter.Operator, Is.EqualTo(JoinOperator.OR));
            }
        }

        [Test]
        public void VerifyFiltersNestToAnyDepth()
        {
            var owningProject = new PrimitiveSearchFilter { Property = "owningProject", Operator = Operator.equalto, Value = [ProjectId] };
            var head = new PrimitiveSearchFilter { Property = "head", Operator = Operator.equalto, Value = [CommitId] };

            var inner = new CompositeSearchFilter { Filters = [owningProject, head], Operator = JoinOperator.OR };

            var named = new PrimitiveSearchFilter { Property = "name", Operator = Operator.equalto, Value = ["main"] };

            var outer = new CompositeSearchFilter { Filters = [inner, named], Operator = JoinOperator.AND };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(outer.Filters, Has.Count.EqualTo(2));
                Assert.That(outer.Filters[0], Is.InstanceOf<CompositeSearchFilter>());
                Assert.That(outer.Filters[^1], Is.InstanceOf<PrimitiveSearchFilter>());
                Assert.That(((CompositeSearchFilter)outer.Filters[0]).Filters, Has.Count.EqualTo(2));
            }
        }
    }
}
