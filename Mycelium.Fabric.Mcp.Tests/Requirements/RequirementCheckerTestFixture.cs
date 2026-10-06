// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementCheckerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Requirements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Requirements;

    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Systems.Constraints;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Systems.Requirements;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using PocoElement = SysML2.NET.Core.POCO.Root.Elements.IElement;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementChecker"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementCheckerTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>Requirements</c> package in <c>Satellite.json</c>.
        /// </summary>
        private const string RequirementsPackageId = "896440c0-d061-7856-fb8f-fe278e26de93";

        /// <summary>
        /// The <c>Id</c> of the requirements REQ-SYS-001 to REQ-SYS-006 in <c>Satellite.json</c>, in this order.
        /// </summary>
        private static readonly string[] RequirementIds =
        [
            "6b93c533-0395-7e18-1bf6-4475deb47ba5", "d17c3498-3404-4a62-9728-c58e59a4954f", "24253a34-a8ba-fc0a-0dba-e5e83d42bd5f",
            "a2c360e5-0019-9ec5-031e-14eadfab77b8", "1e9fa5b1-208e-12b8-440b-e106061d3eb3", "4c3c1285-20ba-ed54-45b3-f54699899da3"
        ];

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string Eosat1Id = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string PayloadSubsystemId = "95a8c184-a12e-1125-c0ee-bbe7a025de5b";

        /// <summary>
        /// The <c>Id</c> of the <c>aocsSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string AocsSubsystemId = "aed7ac47-7926-ea14-6637-ac50ef557797";

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the <c>starTracker1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid StarTracker1Id = Guid.Parse("cf1f0613-e435-413f-e2af-68f376ac856f");

        [Test]
        public void VerifyConstructor()
        {
            var checker = new RequirementChecker([]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new RequirementChecker(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(checker.CheckRequirements(), Has.Count.EqualTo(0));
                Assert.That(checker.EvaluateImpacts(CameraId, "mass", 35), Has.Count.EqualTo(0));
            }
        }

        [Test]
        public void VerifyCheckRequirements()
        {
            var applier = new ModelChangeApplier(ReadSatelliteDtos());

            var result = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[0], Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[0], SatisfyingPart = Eosat1Id },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[0], SatisfyingPart = PayloadSubsystemId },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[1], Attribute = "power", Operator = "<", Limit = 70 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[2], SatisfyingPart = AocsSubsystemId },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[3], Attribute = "wheelCount", Operator = "==", Limit = 4 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[3], SatisfyingPart = AocsSubsystemId },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[4], Attribute = "temperature", Operator = "<=", Limit = 40 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[4], SatisfyingPart = Eosat1Id },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[5], Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[5], SatisfyingPart = Eosat1Id },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[5], SatisfyingPart = PayloadSubsystemId },
                new ModelChange { Kind = ChangeKind.CreateRequirement, TemporaryName = "cameraMass", Owner = RequirementsPackageId, Name = "cameraMass", ReqId = "REQ-PL-001", Text = "The camera shall weigh at most 40 kg." },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = "cameraMass", Attribute = "mass", Operator = "<=", Limit = 40 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = "cameraMass", SatisfyingPart = CameraId.ToString() },
                new ModelChange { Kind = ChangeKind.CreateRequirement, Owner = RequirementsPackageId, Name = "untracedRequirement", Text = "A requirement without identifier." }
            ]);

            var dtos = applier.Elements.ToList();

            // REQ-SYS-004 gets a constraint of another form, the satisfaction of REQ-SYS-006 by the payload is negated, and
            // REQ-PL-001 gets a second required constraint.
            dtos.OfType<OperatorExpression>().Single(expression => expression.Operator == "==").Operator = "!=";
            ((SatisfyRequirementUsage)dtos.Single(dto => dto.Id == result.CreatedElements[6].Id)).IsNegated = true;
            AddEmptyRequiredConstraint(dtos, result.CreatedElements[7].Id);

            var checks = new RequirementChecker(Assemble(dtos)).CheckRequirements();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(checks.Select(check => check.ReqId),
                    Is.EqualTo(["REQ-PL-001", "REQ-SYS-001", "REQ-SYS-001", "REQ-SYS-002", "REQ-SYS-003", "REQ-SYS-004", "REQ-SYS-005", "REQ-SYS-006", null]));
                Assert.That(checks.Select(check => check.Status), Is.EqualTo([
                    RequirementStatus.NotEvaluated, RequirementStatus.NotSatisfied, RequirementStatus.Satisfied, RequirementStatus.NotVerifiable, RequirementStatus.NotVerifiable,
                    RequirementStatus.NotEvaluated, RequirementStatus.NotEvaluated, RequirementStatus.Satisfied, RequirementStatus.NotVerifiable]));
                Assert.That(checks[0].Explanation, Is.EqualTo("The requirement has 2 required constraints, and only a single one is evaluated."));
                Assert.That(checks[0].Constraint, Is.Null);
                Assert.That(checks[1].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1"));
                Assert.That(checks[1].Explanation, Is.EqualTo("150.96 > 150"));
                Assert.That(checks[2].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem"));
                Assert.That(checks[2].Value, Is.EqualTo(39.5));
                Assert.That(checks[2].Gap, Is.EqualTo(102.6));
                Assert.That(checks[3].Constraint, Is.EqualTo("subj.power < 70"));
                Assert.That(checks[3].SatisfiedBy, Is.Null);
                Assert.That(checks[3].Explanation, Does.StartWith("No part satisfies the requirement"));
                Assert.That(checks[4].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1::aocsSubsystem"));
                Assert.That(checks[4].Explanation, Does.StartWith("The requirement has no constraint"));
                Assert.That(checks[5].Constraint, Is.Null);
                Assert.That(checks[5].Explanation, Does.StartWith("The constraint is not a comparison"));
                Assert.That(checks[6].Constraint, Is.EqualTo("subj.temperature <= 40"));
                Assert.That(checks[6].Value, Is.Null);
                Assert.That(checks[6].Explanation, Is.EqualTo("No part under 'EOSat1::Architecture::eosat1' has a numeric value for 'temperature'."));
                Assert.That(checks[7].Constraint, Is.EqualTo("subj.capacity >= 300 * 1.2"));
                Assert.That(checks[7].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1"));
                Assert.That(checks[7].Value, Is.EqualTo(420));
                Assert.That(checks[7].Gap, Is.EqualTo(60));
                Assert.That(checks[7].Explanation, Is.EqualTo("420 >= 360"));
                Assert.That(checks[8].Name, Is.EqualTo("untracedRequirement"));
                Assert.That(checks[8].Explanation, Does.StartWith("The requirement has no constraint").And.Contains("No part satisfies the requirement"));
            }
        }

        [Test]
        public void VerifyEvaluateImpacts()
        {
            var applier = new ModelChangeApplier(ReadSatelliteDtos());

            applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[0], Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[0], SatisfyingPart = Eosat1Id },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[1], Attribute = "power", Operator = "<", Limit = 70 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[1], SatisfyingPart = PayloadSubsystemId },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[2], SatisfyingPart = AocsSubsystemId },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = RequirementIds[4], Attribute = "temperature", Operator = "<=", Limit = 40 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = RequirementIds[4], SatisfyingPart = Eosat1Id }
            ]);

            var checker = new RequirementChecker(Assemble(applier.Elements));

            var heavierCamera = checker.EvaluateImpacts(CameraId, "mass", 45);
            var lighterCamera = checker.EvaluateImpacts(CameraId, "mass", 30);
            var hungrierCamera = checker.EvaluateImpacts(CameraId, "power", 70);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(heavierCamera, Has.Count.EqualTo(1));
                Assert.That(heavierCamera[0].Current.ReqId, Is.EqualTo("REQ-SYS-001"));
                Assert.That(heavierCamera[0].Current.Status, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(heavierCamera[0].Current.Value, Is.EqualTo(125.8));
                Assert.That(heavierCamera[0].New.Status, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(heavierCamera[0].New.Value, Is.EqualTo(132.8));
                Assert.That(heavierCamera[0].New.Gap, Is.EqualTo(-9.36));
                Assert.That(heavierCamera[0].New.Explanation, Is.EqualTo("159.36 > 150"));
                Assert.That(lighterCamera[0].New.Status, Is.EqualTo(RequirementStatus.Satisfied));
                Assert.That(lighterCamera[0].New.Explanation, Is.EqualTo("141.36 <= 150"));
                Assert.That(hungrierCamera, Has.Count.EqualTo(1));
                Assert.That(hungrierCamera[0].Current.ReqId, Is.EqualTo("REQ-SYS-002"));
                Assert.That(hungrierCamera[0].Current.Status, Is.EqualTo(RequirementStatus.Satisfied));
                Assert.That(hungrierCamera[0].New.Status, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(hungrierCamera[0].New.Explanation, Is.EqualTo("79 >= 70"));
                Assert.That(checker.EvaluateImpacts(StarTracker1Id, "power", 10), Has.Count.EqualTo(0));
                Assert.That(checker.EvaluateImpacts(Guid.NewGuid(), "mass", 10), Has.Count.EqualTo(0));
            }
        }

        /// <summary>
        /// Adds to a requirement a required constraint without expression, as <c>require constraint;</c> does.
        /// </summary>
        /// <param name="dtos">The DTOs of the model.</param>
        /// <param name="requirementId">The <c>Id</c> of the requirement.</param>
        private static void AddEmptyRequiredConstraint(List<DtoElement> dtos, Guid requirementId)
        {
            var constraint = new ConstraintUsage { Id = Guid.NewGuid(), IsComposite = true };
            var membership = new RequirementConstraintMembership { Id = Guid.NewGuid(), Kind = RequirementConstraintKind.Requirement, OwningRelatedElement = requirementId };

            membership.OwnedRelatedElement.Add(constraint.Id);
            constraint.OwningRelationship = membership.Id;
            dtos.Single(dto => dto.Id == requirementId).OwnedRelationship.Add(membership.Id);
            dtos.AddRange([constraint, membership]);
        }

        /// <summary>
        /// Reads the DTOs of the <c>Satellite.json</c> test model.
        /// </summary>
        /// <returns>The DTOs of the elements of the model.</returns>
        private static List<DtoElement> ReadSatelliteDtos()
        {
            using var stream = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json"));

            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();
        }

        /// <summary>
        /// Builds the POCOs of the given DTOs with SysML2.NET.
        /// </summary>
        /// <param name="dtos">The DTOs of the model.</param>
        /// <returns>The POCOs of the model.</returns>
        private static List<PocoElement> Assemble(IEnumerable<DtoElement> dtos)
        {
            var assembler = new Assembler();
            assembler.Synchronize(dtos);

            return assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToList();
        }
    }
}
