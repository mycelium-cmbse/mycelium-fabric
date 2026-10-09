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
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Systems.Requirements;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

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
            var model = ModelDtoHelper.ReadSatelliteDtos();

            var result = new ModelChangeApplier().Apply(model,
            [
                new ModelChange { Identity = RequirementIds[0], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[0], SatisfyingPart = Eosat1Id } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[0], SatisfyingPart = PayloadSubsystemId } },
                new ModelChange { Identity = RequirementIds[1], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "power", Operator = "<", Limit = 70 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[2], SatisfyingPart = AocsSubsystemId } },
                new ModelChange { Identity = RequirementIds[3], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "wheelCount", Operator = "==", Limit = 4 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[3], SatisfyingPart = AocsSubsystemId } },
                new ModelChange { Identity = RequirementIds[4], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "temperature", Operator = "<=", Limit = 40 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[4], SatisfyingPart = Eosat1Id } },
                new ModelChange { Identity = RequirementIds[5], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[5], SatisfyingPart = Eosat1Id } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[5], SatisfyingPart = PayloadSubsystemId } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Type = "RequirementUsage", Owner = RequirementsPackageId, Name = "cameraMass", ReqId = "REQ-PL-001", Text = "The camera shall weigh at most 40 kg." } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 40 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = "cameraMass", SatisfyingPart = CameraId.ToString() } },
                new ModelChange { Payload = new ElementPayload { Type = "RequirementUsage", Owner = RequirementsPackageId, Name = "untracedRequirement", Text = "A requirement without identifier." } }
            ]);

            var dtos = CommitRequestHelper.Apply(model, result.Value);
            var satisfyLinks = result.Value.Change.Select(dataVersion => dataVersion.Payload).OfType<SatisfyRequirementUsage>().ToList();

            // REQ-SYS-004 gets a constraint on a value that its satisfying part does not have, the satisfaction of REQ-SYS-006 by
            // the payload is negated, and REQ-PL-001 gets a second required constraint, without expression.
            dtos.OfType<OperatorExpression>().Single(expression => expression.Operator == "==").Operator = "!=";
            satisfyLinks[6].IsNegated = true;
            new ConstraintDtoBuilder(dtos).Constraint(dtos.Single(dto => dto.DeclaredName == "cameraMass").Id, RequirementConstraintKind.Requirement, null);

            var checks = new RequirementChecker(ModelDtoHelper.Assemble(dtos)).CheckRequirements();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(satisfyLinks, Has.Count.EqualTo(8));
                Assert.That(checks.Select(check => check.ReqId),
                    Is.EqualTo(["REQ-PL-001", "REQ-SYS-001", "REQ-SYS-001", "REQ-SYS-002", "REQ-SYS-003", "REQ-SYS-004", "REQ-SYS-005", "REQ-SYS-006", null]));
                Assert.That(checks.Select(check => check.Status), Is.EqualTo([
                    RequirementStatus.NotEvaluated, RequirementStatus.NotSatisfied, RequirementStatus.Satisfied, RequirementStatus.NotVerifiable, RequirementStatus.NotVerifiable,
                    RequirementStatus.NotEvaluated, RequirementStatus.NotEvaluated, RequirementStatus.Satisfied, RequirementStatus.NotVerifiable]));
                Assert.That(checks[0].Constraint, Is.EqualTo("subj.mass <= 40 and (unreadable)"));
                Assert.That(checks[0].Value, Is.EqualTo(38));
                Assert.That(checks[0].Gap, Is.Null);
                Assert.That(checks[0].Explanation, Is.EqualTo("require subj.mass <= 40: 38 <= 40; require (unreadable): The constraint cannot be read: The constraint has no expression."));
                Assert.That(checks[1].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1"));
                Assert.That(checks[1].Assumption, Is.Null);
                Assert.That(checks[1].Explanation, Is.EqualTo("150.96 > 150"));
                Assert.That(checks[2].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem"));
                Assert.That(checks[2].Value, Is.EqualTo(39.5));
                Assert.That(checks[2].Gap, Is.EqualTo(102.6));
                Assert.That(checks[3].Constraint, Is.EqualTo("subj.power < 70"));
                Assert.That(checks[3].SatisfiedBy, Is.Null);
                Assert.That(checks[3].Explanation, Does.StartWith("No part satisfies the requirement"));
                Assert.That(checks[4].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1::aocsSubsystem"));
                Assert.That(checks[4].Explanation, Does.StartWith("The requirement has no required constraint"));
                Assert.That(checks[5].Constraint, Is.EqualTo("subj.wheelCount != 4"));
                Assert.That(checks[5].Explanation, Is.EqualTo("No part under 'EOSat1::Architecture::eosat1::aocsSubsystem' has a numeric value for 'wheelCount'."));
                Assert.That(checks[6].Constraint, Is.EqualTo("subj.temperature <= 40"));
                Assert.That(checks[6].Value, Is.Null);
                Assert.That(checks[6].Explanation, Is.EqualTo("No part under 'EOSat1::Architecture::eosat1' has a numeric value for 'temperature'."));
                Assert.That(checks[7].Constraint, Is.EqualTo("subj.capacity >= 300 * 1.2"));
                Assert.That(checks[7].SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1"));
                Assert.That(checks[7].Value, Is.EqualTo(420));
                Assert.That(checks[7].Gap, Is.EqualTo(60));
                Assert.That(checks[7].Explanation, Is.EqualTo("420 >= 360"));
                Assert.That(checks[8].Name, Is.EqualTo("untracedRequirement"));
                Assert.That(checks[8].Explanation, Does.StartWith("The requirement has no required constraint").And.Contains("No part satisfies the requirement"));
            }
        }

        [Test]
        public void VerifyCheckRequirementsWithAssumptionsAndOtherComparisons()
        {
            var model = ModelDtoHelper.ReadSatelliteDtos();

            var result = new ModelChangeApplier().Apply(model,
            [
                new ModelChange { Identity = RequirementIds[0], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 } } },
                new ModelChange { Identity = RequirementIds[1], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "power", Operator = "<", Limit = 70 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[1], SatisfyingPart = PayloadSubsystemId } },
                new ModelChange { Identity = RequirementIds[3], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 100 } } },
                .. new[] { 0, 2, 3, 4, 5 }.Select(index =>
                    new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[index], SatisfyingPart = Eosat1Id } })
            ]);

            var dtos = CommitRequestHelper.Apply(model, result.Value);
            var builder = new ConstraintDtoBuilder(dtos);
            var requirementIds = RequirementIds.Select(Guid.Parse).ToList();

            // REQ-SYS-001 does not meet its mass budget, but its assumption subj.power <= 200 does not hold either.
            var massBudgetSubject = builder.GetSubject(requirementIds[0]);
            Assume(requirementIds[0], builder.Operation("<=", Chain(massBudgetSubject, builder.Attribute(massBudgetSubject.Id, "power")), builder.Integer(200)));

            // REQ-SYS-002 meets its payload power, whatever its assumption of another form.
            builder.Constraint(requirementIds[1], RequirementConstraintKind.Assumption, null);

            // REQ-SYS-003 compares two attributes of its subject: subj.powerOutput >= subj.power * 1.2.
            var powerSubject = builder.Subject(requirementIds[2]);
            var powerOutput = Chain(powerSubject, builder.Attribute(powerSubject.Id, "powerOutput"));
            var power = Chain(powerSubject, builder.Attribute(powerSubject.Id, "power"));
            Require(requirementIds[2], builder.Operation(">=", powerOutput, builder.Operation("*", power, builder.Literal(1.2))));

            // REQ-SYS-004 does not meet subj.mass <= 100, and its assumption cannot be evaluated.
            var temperatureSubject = builder.GetSubject(requirementIds[3]);
            Assume(requirementIds[3], builder.Operation("<=", Chain(temperatureSubject, builder.Attribute(temperatureSubject.Id, "temperature")), builder.Integer(40)));

            // REQ-SYS-005 has an assumption and no required constraint.
            var assumptionSubject = builder.Subject(requirementIds[4]);
            Assume(requirementIds[4], builder.Operation("<=", Chain(assumptionSubject, builder.Attribute(assumptionSubject.Id, "power")), builder.Integer(300)));

            // REQ-SYS-006 has two required constraints: one with an attribute that has a value, one that divides by zero.
            var massSubject = builder.Subject(requirementIds[5]);
            var mass = builder.Attribute(massSubject.Id, "mass");
            var massLimit = builder.Attribute(requirementIds[5], "massLimit", builder.Integer(200));
            Require(requirementIds[5], builder.Operation("<=", Chain(massSubject, mass), builder.Reference(massLimit.Id)));
            Require(requirementIds[5], builder.Operation("<=", builder.Operation("/", Chain(massSubject, mass), builder.Integer(0)), builder.Integer(1)));

            var checks = new RequirementChecker(ModelDtoHelper.Assemble(dtos)).CheckRequirements();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(checks.Select(check => check.ReqId), Is.EqualTo(["REQ-SYS-001", "REQ-SYS-002", "REQ-SYS-003", "REQ-SYS-004", "REQ-SYS-005", "REQ-SYS-006"]));
                Assert.That(checks.Select(check => check.Status), Is.EqualTo([
                    RequirementStatus.Satisfied, RequirementStatus.Satisfied, RequirementStatus.NotSatisfied, RequirementStatus.NotEvaluated, RequirementStatus.NotVerifiable,
                    RequirementStatus.NotEvaluated]));

                Assert.That(checks[0].Assumption, Is.EqualTo("subj.power <= 200"));
                Assert.That(checks[0].Value, Is.EqualTo(125.8));
                Assert.That(checks[0].Gap, Is.EqualTo(-0.96));
                Assert.That(checks[0].Explanation, Is.EqualTo("assume subj.power <= 200: 227 > 200; require subj.mass * 1.2 <= 150: 150.96 > 150"));

                Assert.That(checks[1].Assumption, Is.EqualTo("(unreadable)"));
                Assert.That(checks[1].Explanation, Is.EqualTo("assume (unreadable): The constraint cannot be read: The constraint has no expression.; require subj.power < 70: 64 < 70"));

                Assert.That(checks[2].Constraint, Is.EqualTo("subj.powerOutput >= subj.power * 1.2"));
                Assert.That(checks[2].Value, Is.Null);
                Assert.That(checks[2].Gap, Is.EqualTo(-92.4));
                Assert.That(checks[2].Explanation, Is.EqualTo("180 < 272.4"));

                Assert.That(checks[3].Explanation,
                    Is.EqualTo("assume subj.temperature <= 40: No part under 'EOSat1::Architecture::eosat1' has a numeric value for 'temperature'.; require subj.mass <= 100: 125.8 > 100"));

                Assert.That(checks[4].Constraint, Is.Null);
                Assert.That(checks[4].Assumption, Is.EqualTo("subj.power <= 300"));
                Assert.That(checks[4].Explanation, Does.StartWith("The requirement has no required constraint"));

                Assert.That(checks[5].Constraint, Is.EqualTo("subj.mass <= massLimit and subj.mass / 0 <= 1"));
                Assert.That(checks[5].Value, Is.EqualTo(125.8));
                Assert.That(checks[5].Gap, Is.Null);
                Assert.That(checks[5].Explanation, Is.EqualTo("require subj.mass <= massLimit: 125.8 <= 200; require subj.mass / 0 <= 1: The expression divides by zero."));
            }

            // Builds a feature chain from a subject to one of its attributes, such as subj.power.
            DtoElement Chain(DtoElement subject, DtoElement attribute)
            {
                return builder.Chain(builder.Reference(subject.Id), attribute.Id);
            }

            // Adds an assumption to a requirement.
            void Assume(Guid requirementId, DtoElement expression)
            {
                builder.Constraint(requirementId, RequirementConstraintKind.Assumption, expression);
            }

            // Adds a required constraint to a requirement.
            void Require(Guid requirementId, DtoElement expression)
            {
                builder.Constraint(requirementId, RequirementConstraintKind.Requirement, expression);
            }
        }

        [Test]
        public void VerifyCheckRequirementsWithAnyExpression()
        {
            // Each case is a requirement satisfied by eosat1, with an optional assumption and a required constraint, and the
            // expected outcome. eosat1 gets an orbit and an altitude, and its camera a redundancy and a band.
            (string Assumption, string Constraint, RequirementStatus Status, string Explanation)[] cases =
            [
                (null, "subj.mass <= 130 [kg]", RequirementStatus.Satisfied, "125.8 [kg] <= 130 [kg]"),
                (null, "subj.payloadSubsystem.camera.mass < 40", RequirementStatus.Satisfied, "38 < 40"),
                (null, "subj.payloadSubsystem.camera.isRedundant and subj.payloadSubsystem.camera.band == \"S-band\"", RequirementStatus.Satisfied,
                    "subj.payloadSubsystem.camera.isRedundant is true and \"S-band\" == \"S-band\""),
                (null, "subj.orbit == OrbitKind::polar", RequirementStatus.NotSatisfied, "sunSynchronous != polar"),
                (null, "subj.altitude >= 500 [km] and subj.altitude <= 800000 [m]", RequirementStatus.Satisfied, "600 [km] >= 500 [km] and 600 [km] <= 800 [km]"),
                (null, "subj.powerOutput >= subj.power * 1.2 or subj.capacity >= 400", RequirementStatus.Satisfied, "420 >= 400"),
                (null, "max(subj.payloadSubsystem.camera.mass, subj.payloadSubsystem.massMemory.mass) <= 40", RequirementStatus.Satisfied, "38 <= 40"),
                (null, "if subj.payloadSubsystem.camera.isRedundant ? subj.mass <= 150 else subj.mass <= 120", RequirementStatus.Satisfied, "125.8 <= 150"),
                (null, "subj.orbit == OrbitKind::polar implies subj.altitude < 500 [km]", RequirementStatus.Satisfied, "sunSynchronous != polar"),
                (null, "size((subj.mass, subj.power)) == 2", RequirementStatus.Satisfied, "2 == 2"),
                ("subj.altitude <= 700 [km]", "subj.mass <= 120", RequirementStatus.NotSatisfied, "assume subj.altitude <= 700 [km]: 600 [km] <= 700 [km]; require subj.mass <= 120: 125.8 > 120"),
                ("subj.orbit == OrbitKind::polar", "subj.mass <= 120", RequirementStatus.Satisfied, "assume subj.orbit == OrbitKind::polar: sunSynchronous != polar; require subj.mass <= 120: 125.8 > 120"),
                (null, "subj.temperature <= 40 or subj.mass > 1000", RequirementStatus.NotEvaluated, "No part under 'EOSat1::Architecture::eosat1' has a numeric value for 'temperature'."),
                (null, "subj.altitude <= 150 [kg]", RequirementStatus.NotEvaluated, "Cannot compare 600 [km] and 150 [kg]: their units have different dimensions."),
                (null, "subj.mass", RequirementStatus.NotEvaluated, "The constraint gives a number (125.8), not a Boolean."),
                (null, "subj.payloadSubsystem.camera", RequirementStatus.NotEvaluated, "The constraint gives a part (EOSat1::Architecture::eosat1::payloadSubsystem::camera), not a Boolean."),
                (null, "vehicle.mass <= 1", RequirementStatus.NotEvaluated, "The constraint cannot be read: The reference to 'vehicle' does not resolve to an element of the model.")
            ];

            var model = ModelDtoHelper.ReadSatelliteDtos();

            var result = new ModelChangeApplier().Apply(model,
            [
                .. cases.SelectMany((_, index) => new[]
                {
                    new ModelChange { Identity = $"requirement{index}", Payload = new ElementPayload { Type = "RequirementUsage", Owner = RequirementsPackageId, Name = $"requirement{index}", ReqId = $"REQ-X-{index:D2}", Text = "Any expression." } },
                    new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = $"requirement{index}", SatisfyingPart = Eosat1Id } }
                })
            ]);

            var dtos = CommitRequestHelper.Apply(model, result.Value);
            var builder = new ConstraintDtoBuilder(dtos);
            var orbitKinds = builder.Enumeration(Guid.Parse(RequirementsPackageId), "OrbitKind", "sunSynchronous", "polar");
            var modelParser = new ConstraintTextParser(builder, name => name.StartsWith("OrbitKind::") ? orbitKinds.Single(value => name.EndsWith(value.DeclaredName)).Id : null);
            var cameraId = CameraId;

            builder.Attribute(Guid.Parse(Eosat1Id), "orbit", modelParser.Parse("OrbitKind::sunSynchronous"));
            builder.Attribute(Guid.Parse(Eosat1Id), "altitude", modelParser.Parse("600 [km]"));
            builder.Attribute(cameraId, "isRedundant", modelParser.Parse("true"));
            builder.Attribute(cameraId, "band", modelParser.Parse("\"S-band\""));

            foreach (var (testCase, index) in cases.Select((testCase, index) => (testCase, index)))
            {
                var requirementId = dtos.Single(dto => dto.DeclaredName == $"requirement{index}").Id;
                var subject = builder.Subject(requirementId);
                var parser = new ConstraintTextParser(builder, name => name == "subj" ? subject.Id : orbitKinds.FirstOrDefault(value => name == $"OrbitKind::{value.DeclaredName}")?.Id);

                if (testCase.Assumption != null)
                {
                    builder.Constraint(requirementId, RequirementConstraintKind.Assumption, parser.Parse(testCase.Assumption));
                }

                builder.Constraint(requirementId, RequirementConstraintKind.Requirement, parser.Parse(testCase.Constraint));
            }

            var checks = new RequirementChecker(ModelDtoHelper.Assemble(dtos)).CheckRequirements().Where(check => check.ReqId.StartsWith("REQ-X-")).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(checks, Has.Count.EqualTo(cases.Length));

                foreach (var (testCase, check) in cases.Zip(checks))
                {
                    Assert.That((check.Status, check.Explanation), Is.EqualTo((testCase.Status, testCase.Explanation)), testCase.Constraint);
                }

                Assert.That(checks[0].Constraint, Is.EqualTo("subj.mass <= 130 [kg]"));
                Assert.That(checks[0].Value, Is.EqualTo(125.8));
                Assert.That(checks[0].Unit, Is.Null);
                Assert.That(checks[0].Gap, Is.EqualTo(4.2));
                Assert.That(checks[4].Value, Is.EqualTo(600));
                Assert.That(checks[4].Unit.Symbol, Is.EqualTo("km"));
                Assert.That(checks[4].Gap, Is.Null);
                Assert.That(checks[3].Constraint, Is.EqualTo("subj.orbit == OrbitKind::polar"));
                Assert.That(checks[10].Assumption, Is.EqualTo("subj.altitude <= 700 [km]"));
                Assert.That(checks[10].Value, Is.EqualTo(125.8));
            }
        }

        [Test]
        public void VerifyEvaluateImpacts()
        {
            var model = ModelDtoHelper.ReadSatelliteDtos();

            var result = new ModelChangeApplier().Apply(model,
            [
                new ModelChange { Identity = RequirementIds[0], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[0], SatisfyingPart = Eosat1Id } },
                new ModelChange { Identity = RequirementIds[1], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "power", Operator = "<", Limit = 70 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[1], SatisfyingPart = PayloadSubsystemId } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[2], SatisfyingPart = AocsSubsystemId } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[3], SatisfyingPart = Eosat1Id } },
                new ModelChange { Identity = RequirementIds[4], Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "temperature", Operator = "<=", Limit = 40 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[4], SatisfyingPart = Eosat1Id } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = RequirementIds[5], SatisfyingPart = Eosat1Id } }
            ]);

            var dtos = CommitRequestHelper.Apply(model, result.Value);
            var builder = new ConstraintDtoBuilder(dtos);

            // REQ-SYS-004 has an assumption on the power and no required constraint, REQ-SYS-006 compares the power output
            // with the power: subj.powerOutput >= subj.power * 1.2.
            var assumptionSubject = builder.Subject(Guid.Parse(RequirementIds[3]));
            var assumedPower = builder.Chain(builder.Reference(assumptionSubject.Id), builder.Attribute(assumptionSubject.Id, "power").Id);
            builder.Constraint(Guid.Parse(RequirementIds[3]), RequirementConstraintKind.Assumption, builder.Operation("<=", assumedPower, builder.Integer(300)));

            var powerSubject = builder.Subject(Guid.Parse(RequirementIds[5]));
            var powerOutput = builder.Chain(builder.Reference(powerSubject.Id), builder.Attribute(powerSubject.Id, "powerOutput").Id);
            var power = builder.Chain(builder.Reference(powerSubject.Id), builder.Attribute(powerSubject.Id, "power").Id);
            builder.Constraint(Guid.Parse(RequirementIds[5]), RequirementConstraintKind.Requirement, builder.Operation(">=", powerOutput, builder.Operation("*", power, builder.Literal(1.2))));

            var checker = new RequirementChecker(ModelDtoHelper.Assemble(dtos));

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
                Assert.That(hungrierCamera.Select(impact => impact.Current.ReqId), Is.EqualTo(["REQ-SYS-002", "REQ-SYS-006"]));
                Assert.That(hungrierCamera[0].Current.Status, Is.EqualTo(RequirementStatus.Satisfied));
                Assert.That(hungrierCamera[0].New.Status, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(hungrierCamera[0].New.Explanation, Is.EqualTo("79 >= 70"));
                Assert.That(hungrierCamera[1].Current.Explanation, Is.EqualTo("180 < 272.4"));
                Assert.That(hungrierCamera[1].New.Explanation, Is.EqualTo("180 < 290.4"));
                Assert.That(checker.EvaluateImpacts(StarTracker1Id, "power", 10).Select(impact => impact.Current.ReqId), Is.EqualTo(["REQ-SYS-006"]));
                Assert.That(checker.EvaluateImpacts(Guid.NewGuid(), "mass", 10), Has.Count.EqualTo(0));
            }
        }
    }
}
