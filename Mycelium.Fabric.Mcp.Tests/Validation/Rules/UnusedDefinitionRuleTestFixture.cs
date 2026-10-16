// ------------------------------------------------------------------------------------------------
//  <copyright file="UnusedDefinitionRuleTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="UnusedDefinitionRule"/> class.
    /// </summary>
    [TestFixture]
    public class UnusedDefinitionRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new UnusedDefinitionRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("unused-definition"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Information));
                Assert.That(rule.Description, Does.StartWith("No feature is typed by a definition"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // All the definitions of EOSat-1 type a part; a definition that nothing uses is added.
            var builder = DtoModelBuilder.FromSatellite();
            var unused = builder.AddMember(builder.Get("Components"), new PartDefinition { DeclaredName = "LaserTerminal" });

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new UnusedDefinitionRule();

            string FindProblem(Guid elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(unused.Id), Is.EqualTo("No feature is typed by the PartDefinition and nothing specializes it: use it, or remove it if it is left over."));
                Assert.That(builder.Dtos.OfType<PartDefinition>().Where(definition => definition != unused).Select(definition => FindProblem(definition.Id)), Is.All.Null);
                Assert.That(FindProblem(builder.Get("camera").Id), Is.Null);
            }
        }
    }
}
