// ------------------------------------------------------------------------------------------------
//  <copyright file="BrokenReferenceRuleTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Core.Features;

    /// <summary>
    /// Suite of tests for the <see cref="BrokenReferenceRule"/> class.
    /// </summary>
    [TestFixture]
    public class BrokenReferenceRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new BrokenReferenceRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("broken-reference"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(rule.Description, Does.StartWith("A relationship refers to an element that is not in the model"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // The definition of the camera is removed: the typing of the camera loses its type, and the membership of the
            // Components package loses its owned element. The members of the definition lose their owner.
            var builder = DtoModelBuilder.FromSatellite();
            var opticalCamera = builder.Get("OpticalCamera");
            var camera = builder.Get("camera");
            var cameraTyping = builder.Dtos.OfType<FeatureTyping>().Single(typing => typing.TypedFeature == camera.Id);
            var satelliteTyping = builder.Dtos.OfType<FeatureTyping>().Single(typing => typing.TypedFeature == builder.Get("eosat1").Id);
            var orphanMembershipId = opticalCamera.OwnedRelationship[0];

            builder.Remove(opticalCamera);

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new BrokenReferenceRule();

            string FindProblem(Guid? elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(cameraTyping.Id), Is.EqualTo("The FeatureTyping refers to an element that is not in the model: it has been removed, or it was never loaded."));
                Assert.That(FindProblem(opticalCamera.OwningRelationship), Does.StartWith("The OwningMembership refers to an element that is not in the model"));
                Assert.That(FindProblem(satelliteTyping.Id), Is.Null);
                Assert.That(FindProblem(orphanMembershipId), Is.Null);
                Assert.That(FindProblem(camera.Id), Is.Null);
            }
        }
    }
}
