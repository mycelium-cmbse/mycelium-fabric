// ------------------------------------------------------------------------------------------------
//  <copyright file="OrphanElementRuleTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Validation.Rules
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    /// <summary>
    /// Suite of tests for the <see cref="OrphanElementRule"/> class.
    /// </summary>
    [TestFixture]
    public class OrphanElementRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new OrphanElementRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("orphan-element"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(rule.Description, Does.StartWith("An element has no owner and is not a root namespace"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // The membership of the Requirements package is removed, and so is the camera definition, whose members lose their
            // owner.
            var builder = DtoModelBuilder.FromSatellite();
            var requirements = builder.Get("Requirements");
            var opticalCamera = builder.Get("OpticalCamera");
            var orphanMembershipId = opticalCamera.OwnedRelationship[0];
            var components = builder.Get("Components");

            builder.Remove(builder.Dtos.Single(element => element.Id == requirements.OwningRelationship));
            builder.Remove(opticalCamera);

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new OrphanElementRule();

            string FindProblem(Guid? elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(requirements.Id), Is.EqualTo("The Package has no owner: the element or the relationship that owned it is not in the model."));
                Assert.That(FindProblem(orphanMembershipId), Does.StartWith("The "));
                Assert.That(FindProblem(builder.RootNamespace.Id), Is.Null);
                Assert.That(FindProblem(components.Id), Is.Null);
                Assert.That(FindProblem(components.OwningRelationship), Is.Null);
            }
        }
    }
}
