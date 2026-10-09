// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintUsageExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.Linq;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Systems.Constraints;
    using SysML2.NET.Core.Systems.Requirements;

    using ConstraintUsageDto = SysML2.NET.Core.DTO.Systems.Constraints.ConstraintUsage;

    /// <summary>
    /// Suite of tests for the <see cref="ConstraintUsageExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ConstraintUsageExtensionsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>massBudget</c> requirement (REQ-SYS-001) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid MassBudgetId = Guid.Parse("6b93c533-0395-7e18-1bf6-4475deb47ba5");

        /// <summary>
        /// The <c>Id</c> of the <c>eclipseEnergy</c> requirement (REQ-SYS-006) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid EclipseEnergyId = Guid.Parse("4c3c1285-20ba-ed54-45b3-f54699899da3");

        [Test]
        public void VerifyGetTerm()
        {
            var model = ModelDtoHelper.ReadSatelliteDtos();

            var result = new ModelChangeApplier().Apply(model,
            [
                new ModelChange { Identity = MassBudgetId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 } } },
                new ModelChange { Identity = EclipseEnergyId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 } } }
            ]);

            var dtos = CommitRequestHelper.Apply(model, result.Value);
            var builtConstraints = result.Value.Change.Select(dataVersion => dataVersion.Payload).OfType<ConstraintUsageDto>().ToList();

            var builder = new ConstraintDtoBuilder(dtos);
            var subject = builder.GetSubject(MassBudgetId);
            var parser = new ConstraintTextParser(builder, name => name == "subj" ? subject.Id : null);

            const string text = "if subj.isRedundant ? subj.fuelLevel >= subj.fuelTankCapacity * 0.9 else max(subj.mass, 100 [kg]) < 150 [kg] and not (subj.band == \"S-band\")";
            var textConstraint = builder.Constraint(MassBudgetId, RequirementConstraintKind.Assumption, parser.Parse(text));
            var emptyConstraint = builder.Constraint(MassBudgetId, RequirementConstraintKind.Requirement, null);
            var unresolvedConstraint = builder.Constraint(MassBudgetId, RequirementConstraintKind.Requirement, parser.Parse("vehicle.mass <= 150"));

            var elements = ModelDtoHelper.Assemble(dtos).ToDictionary(element => element.Id);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IConstraintUsage)null).GetTerm(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Read(builtConstraints[0]).Value.ToString(), Is.EqualTo("subj.mass * 1.2 <= 150"));
                Assert.That(Read(builtConstraints[1]).Value.ToString(), Is.EqualTo("subj.capacity >= 300 * 1.2"));
                Assert.That(Read(textConstraint).Value.ToString(), Is.EqualTo(text));
                Assert.That(Read(emptyConstraint).FirstError.Description, Is.EqualTo("The constraint has no expression."));
                Assert.That(Read(unresolvedConstraint).FirstError.Description, Is.EqualTo("The reference to 'vehicle' does not resolve to an element of the model."));
            }

            // Reads a constraint of the model as a term.
            ErrorOr<Term> Read(ConstraintUsageDto constraint)
            {
                return ((IConstraintUsage)elements[constraint.Id]).GetTerm();
            }
        }
    }
}
