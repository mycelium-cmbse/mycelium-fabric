// ------------------------------------------------------------------------------------------------
//  <copyright file="PrimitiveSearchFilterTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="PrimitiveSearchFilter"/> class.
    /// </summary>
    [TestFixture]
    public class PrimitiveSearchFilterTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        [Test]
        public void VerifyProperties()
        {
            var filter = new PrimitiveSearchFilter();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter, Is.InstanceOf<SearchFilter>());
                Assert.That(filter.Property, Is.Null);
                Assert.That(filter.Value, Has.Count.EqualTo(0));
                Assert.That(filter.Inverse, Is.False);
                Assert.That(filter.Operator, Is.EqualTo(Operator.instanceOf));
            }

            filter.Property = "owningProject";
            filter.Operator = Operator.equalto;
            filter.Value = [ProjectId];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Property, Is.EqualTo("owningProject"));
                Assert.That(filter.Operator, Is.EqualTo(Operator.equalto));
                Assert.That(filter.Value, Has.Count.EqualTo(1));
                Assert.That(filter.Value[0].Kind, Is.EqualTo(ConstraintValueKind.Guid));
            }

            // The in operator is the reason a condition carries several values rather than one.
            filter.Operator = Operator.@in;
            filter.Value = [ProjectId, Guid.Empty];

            Assert.That(filter.Value, Has.Count.EqualTo(2));

            filter.Inverse = true;

            Assert.That(filter.Inverse, Is.True);
        }

        [Test]
        public void VerifyValueCarriesEveryConstraintValueKind()
        {
            var filter = new PrimitiveSearchFilter { Value = [ProjectId, "main", 42d, true, ConstraintValue.Null] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(filter.Value, Has.Count.EqualTo(5));
                Assert.That(filter.Value[0].Kind, Is.EqualTo(ConstraintValueKind.Guid));
                Assert.That(filter.Value[1].Kind, Is.EqualTo(ConstraintValueKind.String));
                Assert.That(filter.Value[2].Kind, Is.EqualTo(ConstraintValueKind.Number));
                Assert.That(filter.Value[3].Kind, Is.EqualTo(ConstraintValueKind.Boolean));
                Assert.That(filter.Value[^1].IsNull, Is.True);
            }
        }
    }
}
