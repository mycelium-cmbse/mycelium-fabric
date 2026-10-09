// ------------------------------------------------------------------------------------------------
//  <copyright file="UntypedUsageRuleTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Ports;

    /// <summary>
    /// Suite of tests for the <see cref="UntypedUsageRule"/> class.
    /// </summary>
    [TestFixture]
    public class UntypedUsageRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new UntypedUsageRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("untyped-usage"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(rule.Description, Is.EqualTo("A part, item or port has no type and no feature, so nothing tells what it is."));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // The definition of the star tracker is removed, so that the star tracker of the AOCS is neither typed nor
            // described; an empty port is added to the camera.
            var builder = DtoModelBuilder.FromSatellite();
            var starTracker = builder.Get("starTracker1");
            var camera = builder.Get("camera");
            var port = builder.AddMember(camera, new PortUsage { DeclaredName = "dataPort" }, new FeatureMembership());

            builder.Remove(builder.Get("StarTracker"));

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new UntypedUsageRule();

            string FindProblem(Guid elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(starTracker.Id), Is.EqualTo("The PartUsage has no type and no feature: type it with a definition, for example camera : Camera, or give it features."));
                Assert.That(FindProblem(port.Id), Does.StartWith("The PortUsage has no type and no feature"));
                Assert.That(FindProblem(camera.Id), Is.Null);
                Assert.That(FindProblem(builder.Get("payloadSubsystem").Id), Is.Null);
                Assert.That(FindProblem(builder.Dtos.OfType<AttributeUsage>().First().Id), Is.Null);
                Assert.That(FindProblem(builder.Get("Components").Id), Is.Null);
            }
        }
    }
}
