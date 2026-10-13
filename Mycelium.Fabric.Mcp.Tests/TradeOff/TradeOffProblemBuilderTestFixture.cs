// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffProblemBuilderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TradeOff
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.TradeOff;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffProblemBuilder"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffProblemBuilderTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid SatelliteId = Guid.Parse("5d06ef4d-4489-500c-e94f-a8cf563d3fc1");

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>propulsionSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PropulsionSubsystemId = Guid.Parse("aad3dd41-500a-9562-ec2b-00c5a473c153");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the <c>solarArray1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid SolarArrayId = Guid.Parse("857708c0-ee5c-1b29-eb11-3a9ce306a75a");

        /// <summary>
        /// The <c>Id</c> of the <c>propellantTank</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PropellantTankId = Guid.Parse("28f0a4e5-b851-0df6-e99e-6eec6eabf4b9");

        /// <summary>
        /// The <c>Id</c> of the <c>pcdu</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PcduId = Guid.Parse("3708e315-c55d-47a2-6a1b-c097a6645b4a");

        /// <summary>
        /// The <c>Id</c> of the <c>reactionWheel1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid ReactionWheelId = Guid.Parse("9aab9372-4d6c-cea9-7431-6321877c8f8f");

        /// <summary>
        /// The <c>Id</c> of the <c>Battery</c> part definition in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid BatteryDefinitionId = Guid.Parse("dfd5f1e1-48e1-111e-20da-c0374a9aca4a");

        private InMemoryModelProvider modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object, new DeSerializer());
            this.modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
        }

        [Test]
        public void VerifyBuild()
        {
            var request = new TradeOffRequest(SatelliteId,
            [
                Choice(CameraId, Variant("light", ("mass", 30))),
                Choice(SolarArrayId, Variant("SA-120", ("mass", 8), ("powerOutput", 120)) with { MaxQuantity = 2 }),
                new TradeOffChoice { Name = " fuelTank ", Variants = [Variant("small", ("mass", 8))] },
                Choice(PropellantTankId, Variant("none")),
                Choice(PcduId, Variant("current", ("maxPower", 300)), Variant("high", ("mass", 6), ("maxPower", 600)))
            ],
            [new TradeOffObjective { Attribute = " mass " }],
            [Constraint("power", "<=", null, "powerOutput", 10), Constraint("powerOutput", " <= ", null, "maxPower", null)],
            [new TradeOffRule { If = "fuelTank.small", Kind = TradeOffRuleKind.Excludes, Then = ["propellantTank.current"] }],
            2, "propellantTank");

            var problem = TradeOffProblemBuilder.Build(this.modelProvider, request);
            var decisions = problem.Decisions;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(problem.Count, Is.EqualTo(2));
                Assert.That(problem.CompareDecisionIndex, Is.EqualTo(3));
                Assert.That(problem.GetAttributes(), Is.EqualTo(["mass", "power", "powerOutput", "maxPower"]));
                Assert.That(problem.FixedTotals["mass"], Is.EqualTo(72.6).Within(1e-9));
                Assert.That(problem.FixedTotals["power"], Is.EqualTo(166).Within(1e-9));
                Assert.That(problem.FixedTotals["powerOutput"], Is.EqualTo(90).Within(1e-9));
                Assert.That(problem.FixedTotals["maxPower"], Is.EqualTo(0));
                Assert.That(problem.Limits.Select(limit => limit.Description), Is.EqualTo(["power * 1.1 <= powerOutput", "powerOutput <= maxPower"]));
                Assert.That(decisions.Select(decision => decision.Name), Is.EqualTo(["camera", "solarArray1", "fuelTank", "propellantTank", "pcdu"]));
                Assert.That(decisions[0].PartId, Is.EqualTo(CameraId));
                Assert.That(decisions[0].QualifiedName, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem::camera"));
                Assert.That(decisions[0].Options.Select(option => option.Variant), Is.EqualTo(["current", "light"]));
                Assert.That(decisions[0].Options[0].Values, Is.EquivalentTo(new Dictionary<string, double> { ["mass"] = 38, ["power"] = 55, ["powerOutput"] = 0, ["maxPower"] = 0 }));
                Assert.That(decisions[0].Options[1].Values, Is.EquivalentTo(new Dictionary<string, double> { ["mass"] = 30, ["power"] = 55, ["powerOutput"] = 0, ["maxPower"] = 0 }));
                Assert.That(decisions[1].Options.Select(option => option.Quantity), Is.EqualTo([1, 1, 2]));
                Assert.That(decisions[1].Options.Select(option => option.Values["powerOutput"]), Is.EqualTo([90, 120, 240]));
                Assert.That(decisions[1].Options.Select(option => option.Values["mass"]), Is.EqualTo([6.5, 8, 16]));
                Assert.That(decisions[2].PartId, Is.Null);
                Assert.That(decisions[2].QualifiedName, Is.Null);
                Assert.That(decisions[2].Required, Is.False);
                Assert.That(decisions[2].Options.Select(option => (option.Variant, option.Quantity, option.IsCurrent)), Is.EqualTo([("none", 0, true), ("small", 1, false)]));
                Assert.That(decisions[3].Options.Select(option => (option.Variant, option.Quantity, option.IsCurrent)), Is.EqualTo([("current", 1, true), ("none", 0, false)]));
                Assert.That(decisions[3].Options[1].Values.Values, Is.All.EqualTo(0));
                Assert.That(decisions[4].Options[0].Values["maxPower"], Is.EqualTo(300));
                Assert.That(decisions[4].Options[0].Values["mass"], Is.EqualTo(4.2));
                Assert.That(decisions[4].Options[1].Values["power"], Is.EqualTo(6));
                Assert.That(problem.Dependencies.Single().Kind, Is.EqualTo(TradeOffRuleKind.Excludes));
                Assert.That(problem.Dependencies.Single().If, Is.EqualTo(new TradeOffVariantReference(2, "small")));
                Assert.That(problem.Dependencies.Single().Then, Is.EqualTo([new TradeOffVariantReference(3, "current")]));
                Assert.That(problem.Dependencies.Single().Description, Is.EqualTo("fuelTank.small excludes propellantTank.current"));
            }

            var subsystemRequest = request with
            {
                Choices = [Choice(PropulsionSubsystemId, Variant("electric", ("mass", 9))), new TradeOffChoice { Name = "radiator", Required = true, Variants = [Variant("small", ("mass", 1))] }],
                Constraints = [Constraint("mass", "<", 200, null, 5), Constraint("mass", ">", 1, null, null)],
                Rules = [new TradeOffRule { If = "propulsionSubsystem.electric", Then = ["radiator.small"] }],
                CompareBy = " "
            };

            var subsystemProblem = TradeOffProblemBuilder.Build(this.modelProvider, subsystemRequest);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(subsystemProblem.CompareDecisionIndex, Is.Null);
                Assert.That(subsystemProblem.Limits.Select(limit => limit.Operator), Is.EqualTo([TradeOffOperator.LessThan, TradeOffOperator.GreaterThan]));
                Assert.That(subsystemProblem.Limits.Select(limit => limit.Margin), Is.EqualTo([5, 0]));
                Assert.That(subsystemProblem.Decisions[0].Options[0].Values["mass"], Is.EqualTo(5.7).Within(1e-9));
                Assert.That(subsystemProblem.FixedTotals["mass"], Is.EqualTo(120.1).Within(1e-9));
                Assert.That(subsystemProblem.Decisions[1].Required, Is.True);
                Assert.That(subsystemProblem.Dependencies.Single().Description, Is.EqualTo("propulsionSubsystem.electric requires radiator.small"));
            }
        }

        [Test]
        public void VerifyBuildWithInvalidRequests()
        {
            var camera = Choice(CameraId, Variant("light", ("mass", 30)));

            TradeOffRequest Request(IReadOnlyList<TradeOffChoice> choices = null, IReadOnlyList<TradeOffConstraint> constraints = null, IReadOnlyList<TradeOffRule> rules = null) =>
                new(SatelliteId, choices ?? [camera], constraints == null ? [new TradeOffObjective { Attribute = "mass" }] : [], constraints, rules, 1, null);

            void AssertRejected(TradeOffRequest request, string message) =>
                Assert.That(() => TradeOffProblemBuilder.Build(this.modelProvider, request), Throws.TypeOf<McpException>().With.Message.Contains(message));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => TradeOffProblemBuilder.Build(null, Request()), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffProblemBuilder.Build(this.modelProvider, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffProblemBuilder.Build(this.modelProvider, Request() with { RootId = Guid.NewGuid() }), Throws.TypeOf<McpException>());

                AssertRejected(Request() with { Choices = null }, "at least one choice");
                AssertRejected(Request() with { Objectives = null }, "at least one objective");
                AssertRejected(Request() with { Count = 0 }, "between 1 and 5");
                AssertRejected(Request() with { Count = 6 }, "between 1 and 5");
                AssertRejected(Request() with { Objectives = [new TradeOffObjective { Attribute = " " }] }, "Each objective needs an attribute");
                AssertRejected(Request() with { Objectives = [new TradeOffObjective { Attribute = "temperature" }] }, "no variant has a value for 'temperature'");
                AssertRejected(Request() with { CompareBy = "battery" }, "compareBy names 'battery'");

                AssertRejected(Request(constraints: [Constraint(null, "<=", 150, null, null)]), "Constraint 1 needs an attribute");
                AssertRejected(Request(constraints: [Constraint("mass", "==", 150, null, null)]), "the operator '=='");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", 150, "power", null)]), "either a limit");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", null, " ", null)]), "either a limit");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", double.NaN, null, null)]), "not a number");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", 2e9, null, null)]), "not a number");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", null, "mass", null)]), "with itself");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", 150, null, -2.5)]), "margin of -2.5:");
                AssertRejected(Request(constraints: [Constraint("mass", "<=", 150, null, double.PositiveInfinity)]), "margin of");
                AssertRejected(Request(constraints: [Constraint("mass", ">=", 0, null, 10)]), "limit of 0 or less");

                AssertRejected(Request([null]), "A choice is empty");
                AssertRejected(Request([camera]) with { RootId = PayloadSubsystemId, Choices = [Choice(ReactionWheelId, Variant("light"))] }, "'reactionWheel1' is not a part under 'payloadSubsystem'");
                AssertRejected(Request([camera]) with { RootId = CameraId }, "is not a part under 'camera'");
                AssertRejected(Request([Choice(BatteryDefinitionId, Variant("large"))]), "'Battery' is not a part under 'eosat1'");
                AssertRejected(Request([new TradeOffChoice { Variants = [Variant("small")] }]), "needs a name");
                AssertRejected(Request([new TradeOffChoice { Name = "fuel.tank", Variants = [Variant("small")] }]), "contains '.'");
                AssertRejected(Request([camera, camera]), "Two choices are named 'camera'");
                AssertRejected(Request([camera, camera with { Name = "otherCamera" }]), "overlap");
                AssertRejected(Request([camera, Choice(PayloadSubsystemId, Variant("light"))]), "overlap");

                AssertRejected(Request([Choice(CameraId)]), "The choice 'camera' has no variant");
                AssertRejected(Request([Choice(CameraId, Variant(" "))]), "A variant of 'camera' has no name");
                AssertRejected(Request([Choice(CameraId, Variant("light"), Variant("light "))]), "two variants named 'light'");
                AssertRejected(Request([Choice(CameraId, Variant("light") with { MinQuantity = 0 })]), "quantities 0 to 1");
                AssertRejected(Request([Choice(CameraId, Variant("light") with { MinQuantity = 3, MaxQuantity = 2 })]), "quantities 3 to 2");
                AssertRejected(Request([Choice(CameraId, Variant("light") with { MaxQuantity = 21 })]), "quantities 1 to 21");
                AssertRejected(Request([Choice(CameraId, Variant("light", ("mass", double.NaN)))]), "a value that is not a number");
                AssertRejected(Request([Choice(CameraId, Variant("light", (" ", 1)))]), "a value without attribute name");
                AssertRejected(Request([Choice(CameraId, Variant("light", ("mass", 1), (" mass", 2)))]), "two values for the same attribute");
                AssertRejected(Request([Choice(CameraId, Variant("none", ("mass", 1)))]), "give it no values");
                AssertRejected(Request([new TradeOffChoice { Name = "fuelTank", Variants = [Variant("current")] }]), "does not exist");
                AssertRejected(Request([Choice(CameraId, Variant("current") with { MaxQuantity = 2 })]), "give it no quantity");
                AssertRejected(Request([Choice(CameraId, Variant("current", ("mass", 40)))]), "'mass', which the model already gives");
                AssertRejected(Request([new TradeOffChoice { Name = "fuelTank", Required = true, Variants = [Variant("none"), Variant("small")] }]), "is required");

                AssertRejected(Request(rules: [null]), "Rule 1 needs");
                AssertRejected(Request(rules: [new TradeOffRule { If = "camera.light", Then = [] }]), "Rule 1 needs");
                AssertRejected(Request(rules: [new TradeOffRule { If = "camera", Then = ["camera.current"] }]), "refers to 'camera'");
                AssertRejected(Request(rules: [new TradeOffRule { If = "camera.heavy", Then = ["camera.current"] }]), "refers to 'camera.heavy'");
                AssertRejected(Request(rules: [new TradeOffRule { If = "battery.large", Then = ["camera.current"] }]), "example 'camera.light'");
                AssertRejected(Request(rules: [new TradeOffRule { If = "camera.light", Then = ["camera.current"] }]), "same choice 'camera'");
            }
        }

        [Test]
        public void VerifyBuildWithUnits()
        {
            var unitProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            unitProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            var massMemoryId = unitProvider.Elements.Single(element => element.DeclaredName == "massMemory").Id;

            var applied = unitProvider.ApplyChanges(
            [
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "dataVolume", Value = 2, Unit = "GB" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = massMemoryId.ToString(), Name = "dataVolume", Value = 512, Unit = "MB" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "heat", Value = 5, Unit = "W" } }
            ]);

            TradeOffRequest Request(TradeOffVariant variant, IReadOnlyList<TradeOffConstraint> constraints = null) =>
                new(PayloadSubsystemId, [Choice(massMemoryId, variant)], [new TradeOffObjective { Attribute = "dataVolume" }], constraints, null, 1, null);

            TradeOffVariant VolumeVariant(string name, double value, string unit) =>
                new() { Name = name, Values = new Dictionary<string, double> { ["dataVolume"] = value }, Units = unit == null ? null : new Dictionary<string, string> { ["dataVolume"] = unit } };

            var problem = TradeOffProblemBuilder.Build(unitProvider, Request(VolumeVariant("small", 256, " MB "), [Constraint("dataVolume", "<=", 2500, null, null) with { Unit = "megabyte" }]));
            var options = problem.Decisions[0].Options;
            var inTerabytes = TradeOffProblemBuilder.Build(unitProvider, Request(VolumeVariant("big", 0.0004, "TB")));
            var withoutUnit = TradeOffProblemBuilder.Build(unitProvider, Request(VolumeVariant("big", 3, null)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(applied.Applied, Is.True);
                Assert.That(problem.GetUnit("dataVolume").Symbol, Is.EqualTo("GB"));
                Assert.That(problem.FixedTotals["dataVolume"], Is.EqualTo(2).Within(1e-9));
                Assert.That(options[0].Values["dataVolume"], Is.EqualTo(0.512).Within(1e-9));
                Assert.That(options[1].Values["dataVolume"], Is.EqualTo(0.256).Within(1e-9));
                Assert.That(problem.Limits[0].Limit, Is.EqualTo(2.5).Within(1e-9));
                Assert.That(problem.Limits[0].Unit.Symbol, Is.EqualTo("GB"));
                Assert.That(problem.Limits[0].Description, Is.EqualTo("dataVolume <= 2.5 GB"));
                Assert.That(inTerabytes.Decisions[0].Options[1].Values["dataVolume"], Is.EqualTo(0.4).Within(1e-9));
                Assert.That(withoutUnit.Decisions[0].Options[1].Values["dataVolume"], Is.EqualTo(3));
                Assert.That(TradeOffProblemBuilder.Build(this.modelProvider, new TradeOffRequest(SatelliteId, [Choice(CameraId, Variant("light", ("mass", 30)))],
                    [new TradeOffObjective { Attribute = "mass" }], null, null, 1, null)).GetUnit("mass"), Is.Null);
            }

            void AssertRejected(TradeOffRequest request, string message) =>
                Assert.That(() => TradeOffProblemBuilder.Build(unitProvider, request), Throws.TypeOf<McpException>().With.Message.Contains(message));

            using (Assert.EnterMultipleScope())
            {
                AssertRejected(Request(VolumeVariant("big", 1, "kg")), "in kg, which cannot be converted to GB, the unit of 'dataVolume'");
                AssertRejected(Request(VolumeVariant("big", 1, "parsec")), "'parsec', which the server does not know");
                AssertRejected(Request(VolumeVariant("big", 1e12, "TB")), "not a number between");
                AssertRejected(Request(VolumeVariant("big", 1, "GB") with { Units = new Dictionary<string, string> { ["heat"] = "W" } }), "gives the unit of 'heat' but no value for it");
                AssertRejected(Request(VolumeVariant("big", 1, null), [Constraint("dataVolume", "<=", 1, null, null) with { Unit = "kg" }]), "The limit of Constraint 1 is in kg");
                AssertRejected(Request(VolumeVariant("big", 1, null), [Constraint("dataVolume", "<=", 1, null, null) with { Unit = "parsec" }]), "'parsec', which the server does not know");
                AssertRejected(Request(VolumeVariant("big", 1, null), [Constraint("dataVolume", "<=", null, "heat", null) with { Unit = "GB" }]), "gives a unit with a limitAttribute");
                AssertRejected(Request(VolumeVariant("big", 1, null), [Constraint("dataVolume", "<=", null, "heat", null)]), "they have different dimensions and cannot be compared");
            }
        }

        [Test]
        public void VerifyBuildWhenAnOwnerHidesThePart()
        {
            var root = new PartUsage { Id = Guid.NewGuid(), DeclaredName = "root" };
            var subsystem = new PartUsage { Id = Guid.NewGuid(), DeclaredName = "subsystem" };
            var unit = new PartUsage { Id = Guid.NewGuid(), DeclaredName = "unit" };
            root.AssignOwnership(new FeatureMembership(), subsystem);
            subsystem.AssignOwnership(new FeatureMembership(), unit);
            AddValue(subsystem, "mass", 10);
            AddValue(unit, "mass", 2);
            AddValue(unit, "power", 3);

            var provider = new Mock<IModelProvider>();
            provider.Setup(modelProvider => modelProvider.GetRequiredElementById(root.Id)).Returns(root);
            provider.Setup(modelProvider => modelProvider.GetRequiredElementById(unit.Id)).Returns(unit);

            var massRequest = new TradeOffRequest(root.Id, [Choice(unit.Id, Variant("light", ("mass", 1)))], [new TradeOffObjective { Attribute = "mass" }], null, null, 1, null);
            var powerProblem = TradeOffProblemBuilder.Build(provider.Object, massRequest with { Objectives = [new TradeOffObjective { Attribute = "power" }] });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => TradeOffProblemBuilder.Build(provider.Object, massRequest),
                    Throws.TypeOf<McpException>().With.Message.Contains("cannot change 'mass': 'subsystem' gives its own 'mass'"));

                Assert.That(powerProblem.Decisions[0].Options[0].Values["power"], Is.EqualTo(3));
                Assert.That(powerProblem.FixedTotals["power"], Is.EqualTo(0));
            }

            provider.Verify(modelProvider => modelProvider.GetRequiredElementById(unit.Id), Times.Exactly(2));
        }

        /// <summary>
        /// Creates a choice on an existing part.
        /// </summary>
        /// <param name="partId">The <c>Id</c> of the part.</param>
        /// <param name="variants">The variants of the choice.</param>
        /// <returns>The <see cref="TradeOffChoice"/>.</returns>
        private static TradeOffChoice Choice(Guid partId, params TradeOffVariant[] variants)
        {
            return new TradeOffChoice { PartId = partId, Variants = variants };
        }

        /// <summary>
        /// Creates a variant of one unit.
        /// </summary>
        /// <param name="name">The name of the variant.</param>
        /// <param name="values">The values of the variant.</param>
        /// <returns>The <see cref="TradeOffVariant"/>.</returns>
        private static TradeOffVariant Variant(string name, params (string Attribute, double Value)[] values)
        {
            return new TradeOffVariant { Name = name, Values = values.ToDictionary(value => value.Attribute, value => value.Value) };
        }

        /// <summary>
        /// Creates a constraint.
        /// </summary>
        /// <param name="attribute">The constrained attribute.</param>
        /// <param name="comparison">The operator.</param>
        /// <param name="limit">The limit, or <c>null</c>.</param>
        /// <param name="limitAttribute">The limit attribute, or <c>null</c>.</param>
        /// <param name="margin">The margin, or <c>null</c>.</param>
        /// <returns>The <see cref="TradeOffConstraint"/>.</returns>
        private static TradeOffConstraint Constraint(string attribute, string comparison, double? limit, string limitAttribute, double? margin)
        {
            return new TradeOffConstraint { Attribute = attribute, Operator = comparison, Limit = limit, LimitAttribute = limitAttribute, Margin = margin };
        }

        /// <summary>
        /// Gives a numeric attribute to a part.
        /// </summary>
        /// <param name="part">The <see cref="PartUsage"/>.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        private static void AddValue(PartUsage part, string name, int value)
        {
            var attribute = new AttributeUsage { DeclaredName = name };
            part.AssignOwnership(new FeatureMembership(), attribute);
            attribute.AssignOwnership(new FeatureValue(), new LiteralInteger { Value = value });
        }
    }
}
