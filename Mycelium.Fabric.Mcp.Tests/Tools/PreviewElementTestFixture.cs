// ------------------------------------------------------------------------------------------------
//  <copyright file="PreviewElementTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;

    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Functions;
    using SysML2.NET.Core.POCO.Kernel.Multiplicities;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="PreviewElement"/> class.
    /// </summary>
    [TestFixture]
    public class PreviewElementTestFixture
    {
        [Test]
        public void VerifyCreate()
        {
            var id = Guid.NewGuid();
            var heaters = CreatePart("heaters", new LiteralInteger { Value = 4 });
            var spares = CreatePart("spares", new LiteralInteger { Value = 0 }, new LiteralInfinity());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(PreviewElement.Create(id, "heaters", "PartUsage", null, null), Is.EqualTo(new PreviewElement(id, "heaters", "PartUsage")));
                Assert.That(PreviewElement.Create(id, "heaters", "PartUsage", null, heaters).Multiplicity, Is.EqualTo("4"));
                Assert.That(PreviewElement.Create(id, "spares", "PartUsage", spares, null).Multiplicity, Is.EqualTo("0..*"));
                Assert.That(PreviewElement.Create(id, "radiator", "PartUsage", null, new PartUsage { DeclaredName = "radiator" }).Multiplicity, Is.Null);
            }
        }

        /// <summary>
        /// Creates a part that owns a multiplicity range with the given bounds.
        /// </summary>
        /// <param name="name">The declared name of the part.</param>
        /// <param name="bounds">The bounds of the multiplicity range, the upper one alone or the lower one then the upper one.</param>
        /// <returns>The <see cref="PartUsage"/>.</returns>
        private static PartUsage CreatePart(string name, params IExpression[] bounds)
        {
            var part = new PartUsage { DeclaredName = name };
            var multiplicity = new MultiplicityRange();
            part.AssignOwnership(new OwningMembership(), multiplicity);

            foreach (var bound in bounds)
            {
                multiplicity.AssignOwnership(new OwningMembership(), bound);
            }

            return part;
        }
    }
}
