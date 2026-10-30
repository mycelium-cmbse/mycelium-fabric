// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffToolsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using ModelContextProtocol;
    using ModelContextProtocol.Protocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.TradeOff;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffTools"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffToolsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid SatelliteId = Guid.Parse("5d06ef4d-4489-500c-e94f-a8cf563d3fc1");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the <c>propulsionSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PropulsionSubsystemId = Guid.Parse("aad3dd41-500a-9562-ec2b-00c5a473c153");

        /// <summary>
        /// The <c>Id</c> of the <c>battery</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid BatteryId = Guid.Parse("75297346-35de-bb2f-d9a1-5abdaa5c6955");

        /// <summary>
        /// The <c>Id</c> of the <c>solarArray1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid SolarArrayId = Guid.Parse("857708c0-ee5c-1b29-eb11-3a9ce306a75a");

        /// <summary>
        /// The <c>Id</c> of the <c>pcdu</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PcduId = Guid.Parse("3708e315-c55d-47a2-6a1b-c097a6645b4a");

        /// <summary>
        /// The qualified name of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string CameraQualifiedName = "EOSat1::Architecture::eosat1::payloadSubsystem::camera";

        /// <summary>
        /// The <c>Id</c> of the <c>xBandTransmitter</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid TransmitterId = Guid.Parse("324f00ec-273e-5fe1-c724-a63af46fee09");

        /// <summary>
        /// The choices of the trade-off of the view: two other cameras and a lighter transmitter.
        /// </summary>
        private static readonly TradeOffChoice[] CameraAndTransmitter =
        [
            new TradeOffChoice
            {
                Name = "camera",
                PartId = CameraId,
                Variants =
                [
                    new TradeOffVariant { Name = "A", Values = new Dictionary<string, double> { ["mass"] = 30, ["power"] = 70 } },
                    new TradeOffVariant { Name = "B", Values = new Dictionary<string, double> { ["mass"] = 34, ["power"] = 50 } }
                ]
            },
            new TradeOffChoice
            {
                Name = "xBandTransmitter",
                PartId = TransmitterId,
                Variants = [new TradeOffVariant { Name = "XT-low", Values = new Dictionary<string, double> { ["mass"] = 1.2, ["power"] = 28 } }]
            }
        ];

        /// <summary>
        /// The objectives of the trade-off of the view: the lowest mass, then the lowest power.
        /// </summary>
        private static readonly TradeOffObjective[] MassThenPower = [new TradeOffObjective { Attribute = "mass" }, new TradeOffObjective { Attribute = "power" }];

        /// <summary>
        /// The constraint of the trade-off of the view: REQ-SYS-001, with a margin of 20 %.
        /// </summary>
        private static readonly TradeOffConstraint[] MassRequirement = [new TradeOffConstraint { Name = "REQ-SYS-001", Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 }];

        private InMemoryModelProvider modelProvider;

        private Mock<ITradeOffSolver> solver;

        private Mock<IAppHostDetector> appHostDetector;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object, new DeSerializer());
            this.modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            this.solver = new Mock<ITradeOffSolver>();
            this.appHostDetector = new Mock<IAppHostDetector>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new TradeOffTools(null, this.solver.Object, this.appHostDetector.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new TradeOffTools(this.modelProvider, null, this.appHostDetector.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new TradeOffTools(this.modelProvider, this.solver.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new TradeOffTools(this.modelProvider, this.solver.Object, this.appHostDetector.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifySolveTradeOff()
        {
            this.solver.Setup(tradeOffSolver => tradeOffSolver.Solve(It.Is<TradeOffProblem>(problem => problem.CompareDecisionIndex == null)))
                .Returns(new TradeOffSolution(TradeOffStatus.Optimal, [new TradeOffAlternative(null, TradeOffStatus.Optimal, [[1]], [])]));

            this.solver.Setup(tradeOffSolver => tradeOffSolver.Solve(It.Is<TradeOffProblem>(problem => problem.CompareDecisionIndex == 0)))
                .Returns(new TradeOffSolution(TradeOffStatus.Optimal,
                [
                    new TradeOffAlternative("current", TradeOffStatus.Optimal, [[0]], []),
                    new TradeOffAlternative("light", TradeOffStatus.Infeasible, [], ["constraint 1: mass * 1.2 <= 150"])
                ]));

            var tools = new TradeOffTools(this.modelProvider, this.solver.Object, this.appHostDetector.Object);
            TradeOffChoice[] choices = [new TradeOffChoice { PartId = CameraId, Variants = [new TradeOffVariant { Name = "light", Values = new Dictionary<string, double> { ["mass"] = 30 } }] }];
            TradeOffConstraint[] constraints = [new TradeOffConstraint { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 }];

            var result = ReadResult(tools.SolveTradeOff(SatelliteId, choices, constraints: constraints));
            var compared = ReadResult(tools.SolveTradeOff(SatelliteId, choices, constraints: constraints, compareBy: "camera"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.SolveTradeOff(SatelliteId, []), Throws.TypeOf<McpException>().With.Message.Contains("at least one choice"));
                Assert.That(result.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(result.Conflicts, Is.Empty);
                Assert.That(result.Current.Alternative, Is.Null);
                Assert.That(result.Current.Rank, Is.Zero);
                Assert.That(result.Current.ChangeCount, Is.Zero);
                Assert.That(result.Current.Choices, Is.EqualTo([new ChosenVariant("camera", CameraId, CameraQualifiedName, "current", 1, false)]));
                Assert.That(result.Current.Totals, Is.EqualTo([new AttributeTotal("mass", 125.8, 0)]));
                Assert.That(result.Current.Constraints, Is.EqualTo([new ConstraintCheck("mass * 1.2 <= 150", false, null, 150.96, 150)]));
                Assert.That(result.Combinations, Has.Count.EqualTo(1));
                Assert.That(result.Combinations[0].Rank, Is.EqualTo(1));
                Assert.That(result.Combinations[0].ChangeCount, Is.EqualTo(1));
                Assert.That(result.Combinations[0].Choices, Is.EqualTo([new ChosenVariant("camera", CameraId, CameraQualifiedName, "light", 1, true)]));
                Assert.That(result.Combinations[0].Totals, Is.EqualTo([new AttributeTotal("mass", 117.8, -8)]));
                Assert.That(result.Combinations[0].Constraints, Is.EqualTo([new ConstraintCheck("mass * 1.2 <= 150", true, null, 141.36, 150)]));
                Assert.That(compared.Combinations.Single().Alternative, Is.EqualTo("camera.current"));
                Assert.That(compared.Conflicts, Is.EqualTo(["camera.light: constraint 1: mass * 1.2 <= 150"]));
            }

            this.solver.Verify(tradeOffSolver => tradeOffSolver.Solve(It.Is<TradeOffProblem>(problem => problem.Decisions.Count == 1 && problem.Limits.Count == 1)), Times.Exactly(2));
        }

        [Test]
        public void VerifySolveTradeOffWithUnits()
        {
            var unitProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            unitProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            var massMemoryId = unitProvider.Elements.Single(element => element.DeclaredName == "massMemory").Id;
            var payloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

            unitProvider.ApplyChanges(
            [
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "dataVolume", Value = 2, Unit = "GB" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = massMemoryId.ToString(), Name = "dataVolume", Value = 512, Unit = "MB" } }
            ]);

            TradeOffChoice[] memory =
            [
                new TradeOffChoice
                {
                    PartId = massMemoryId,
                    Variants =
                    [
                        new TradeOffVariant { Name = "small", Values = new Dictionary<string, double> { ["dataVolume"] = 256 }, Units = new Dictionary<string, string> { ["dataVolume"] = "MB" } },
                        new TradeOffVariant { Name = "medium", Values = new Dictionary<string, double> { ["dataVolume"] = 0.0004 }, Units = new Dictionary<string, string> { ["dataVolume"] = "TB" } }
                    ]
                }
            ];

            TradeOffObjective[] lowestVolume = [new TradeOffObjective { Attribute = "dataVolume" }];
            TradeOffConstraint[] atLeast = [new TradeOffConstraint { Attribute = "dataVolume", Operator = ">=", Limit = 2400, Unit = "MB" }];

            var result = ReadResult(new TradeOffTools(unitProvider, new CpSatTradeOffSolver(), this.appHostDetector.Object).SolveTradeOff(payloadSubsystemId, memory, lowestVolume, atLeast));
            var best = result.Combinations.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(result.Current.Totals.Single().Total, Is.EqualTo(2.512));
                Assert.That(result.Current.Totals.Single().Unit.Symbol, Is.EqualTo("GB"));
                Assert.That(best.Choices.Single().Variant, Is.EqualTo("medium"));
                Assert.That(best.Totals.Single().Total, Is.EqualTo(2.4));
                Assert.That(best.Totals.Single().Difference, Is.EqualTo(-0.112));
                Assert.That(best.Totals.Single().Unit.Symbol, Is.EqualTo("GB"));
                Assert.That(best.Constraints.Single().Constraint, Is.EqualTo("dataVolume >= 2.4 GB"));
            }
        }

        [Test]
        public void VerifySolveTradeOffWithTheSolver()
        {
            var tools = new TradeOffTools(this.modelProvider, new CpSatTradeOffSolver(), this.appHostDetector.Object);

            TradeOffChoice[] cameraChoice = [new TradeOffChoice { PartId = CameraId, Variants = [new TradeOffVariant { Name = "light", Values = new Dictionary<string, double> { ["mass"] = 30 } }] }];
            TradeOffConstraint[] massBudget = [new TradeOffConstraint { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 }];

            var lighterCamera = ReadResult(tools.SolveTradeOff(SatelliteId, cameraChoice, constraints: massBudget));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(lighterCamera.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(lighterCamera.Current.Constraints.Single().Satisfied, Is.False);
                Assert.That(lighterCamera.Combinations.Single().Choices.Single().Variant, Is.EqualTo("light"));
                Assert.That(lighterCamera.Combinations.Single().Totals.Single().Total, Is.EqualTo(117.8));
                Assert.That(lighterCamera.Combinations.Single().Constraints.Single().Satisfied, Is.True);
            }

            TradeOffChoice[] propulsionChoices =
            [
                new TradeOffChoice { PartId = PropulsionSubsystemId, Variants = [new TradeOffVariant { Name = "electric", Values = new Dictionary<string, double> { ["mass"] = 9, ["power"] = 120 } }] },
                new TradeOffChoice { PartId = BatteryId, Variants = [new TradeOffVariant { Name = "large", Values = new Dictionary<string, double> { ["mass"] = 9.5, ["capacity"] = 700 } }] },
                new TradeOffChoice { PartId = SolarArrayId, Variants = [new TradeOffVariant { Name = "SA-150", Values = new Dictionary<string, double> { ["mass"] = 8.5, ["powerOutput"] = 150 }, MaxQuantity = 2 }] },
                new TradeOffChoice
                {
                    PartId = PcduId,
                    Variants =
                    [
                        new TradeOffVariant { Name = "current", Values = new Dictionary<string, double> { ["maxPower"] = 300 } },
                        new TradeOffVariant { Name = "high", Values = new Dictionary<string, double> { ["mass"] = 6, ["power"] = 8, ["maxPower"] = 500 } }
                    ]
                }
            ];

            TradeOffConstraint[] powerBalance =
            [
                new TradeOffConstraint { Attribute = "power", Operator = "<=", LimitAttribute = "powerOutput" },
                new TradeOffConstraint { Attribute = "powerOutput", Operator = "<=", LimitAttribute = "maxPower" }
            ];

            TradeOffObjective[] lowestMass = [new TradeOffObjective { Attribute = "mass" }];
            TradeOffRule[] electricRule = [new TradeOffRule { If = "propulsionSubsystem.electric", Then = ["battery.large"] }];

            var propulsion = ReadResult(tools.SolveTradeOff(SatelliteId, propulsionChoices, lowestMass, powerBalance, electricRule, "propulsionSubsystem"));
            var chemical = propulsion.Combinations[0];
            var electric = propulsion.Combinations[1];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(propulsion.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(propulsion.Current.Totals.Select(total => total.Total), Is.EqualTo([125.8, 227, 180, 300]));
                Assert.That(propulsion.Current.Constraints.Select(constraint => constraint.Satisfied), Is.EqualTo([false, true]));
                Assert.That(propulsion.Combinations.Select(combination => combination.Alternative), Is.EqualTo(["propulsionSubsystem.current", "propulsionSubsystem.electric"]));
                Assert.That(chemical.Choices.Select(choice => (choice.Variant, choice.Quantity)), Is.EqualTo([("current", 1), ("current", 1), ("SA-150", 1), ("current", 1)]));
                Assert.That(chemical.Totals[0], Is.EqualTo(new AttributeTotal("mass", 127.8, 2)));
                Assert.That(chemical.ChangeCount, Is.EqualTo(1));
                Assert.That(electric.Choices.Select(choice => (choice.Variant, choice.Quantity)), Is.EqualTo([("electric", 1), ("large", 1), ("SA-150", 2), ("high", 1)]));
                Assert.That(electric.Totals, Is.EqualTo([new AttributeTotal("mass", 145.1, 19.3), new AttributeTotal("power", 349, 122), new AttributeTotal("powerOutput", 390, 210), new AttributeTotal("maxPower", 500, 200)]));
                Assert.That(electric.Constraints.Select(constraint => constraint.Satisfied), Is.All.True);
            }

            TradeOffConstraint[] tightBudget = [.. powerBalance, new TradeOffConstraint { Attribute = "mass", Operator = "<=", Limit = 139 }];
            var withoutElectric = ReadResult(tools.SolveTradeOff(SatelliteId, propulsionChoices, lowestMass, tightBudget, electricRule, "propulsionSubsystem"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutElectric.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(withoutElectric.Combinations.Single().Alternative, Is.EqualTo("propulsionSubsystem.current"));
                Assert.That(withoutElectric.Conflicts, Is.EqualTo(["propulsionSubsystem.electric: constraint 1: power <= powerOutput", "propulsionSubsystem.electric: constraint 3: mass <= 139"]));
            }
        }

        [Test]
        public void VerifySolveTradeOffForTheView()
        {
            var tools = new TradeOffTools(this.modelProvider, new CpSatTradeOffSolver(), this.appHostDetector.Object);

            var withoutView = tools.SolveTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, MassRequirement, count: 3, title: "Camera and X-band transmitter");

            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);
            var withView = tools.SolveTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, MassRequirement, count: 3, title: "Camera and X-band transmitter");
            var study = tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, MassRequirement, count: 3, title: "Camera and X-band transmitter");
            var expectedText = JsonSerializer.Serialize(new TradeOffResult(study.Status, study.Current, study.Best, study.Conflicts), McpJsonUtilities.DefaultOptions);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutView.StructuredContent, Is.Null);
                Assert.That(ReadText(withoutView), Is.EqualTo(expectedText));
                Assert.That(ReadText(withView), Is.EqualTo(expectedText));
                Assert.That(withView.StructuredContent?.GetProperty("title").GetString(), Is.EqualTo("Camera and X-band transmitter"));
                Assert.That(withView.StructuredContent?.GetProperty("points").GetArrayLength(), Is.EqualTo(6));
                Assert.That(withView.StructuredContent?.GetProperty("request").GetProperty("excluded").GetArrayLength(), Is.Zero);
                Assert.That(withView.StructuredContent?.GetProperty("request").GetProperty("compareBy").ValueKind, Is.EqualTo(JsonValueKind.Null));
            }
        }

        [Test]
        public void VerifyExploreTradeOff()
        {
            var tools = new TradeOffTools(this.modelProvider, new CpSatTradeOffSolver(), this.appHostDetector.Object);
            var elementCount = this.modelProvider.Elements.Count;

            var study = tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, MassRequirement, count: 3);
            var byVariants = study.Points.ToDictionary(point => string.Join("+", point.Choices.Select(choice => choice.Variant)));
            var rankOne = study.Points.Single(point => point.Rank == 1);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(study.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(study.Root, Is.EqualTo(new TradeOffRoot(SatelliteId, "EOSat1::Architecture::eosat1")));
                Assert.That(study.Attributes.Select(attribute => attribute.Name), Is.EqualTo(["mass", "power"]));
                Assert.That(study.Choices.Select(choice => choice.Name), Is.EqualTo(["camera", "xBandTransmitter"]));
                Assert.That(study.Choices[0].Variants.Select(variant => (variant.Name, variant.IsCurrent, variant.Excluded)), Is.EqualTo([("current", true, false), ("A", false, false), ("B", false, false)]));
                Assert.That(study.Choices[0].Variants[0].Values, Is.EqualTo(new Dictionary<string, double> { ["mass"] = 38, ["power"] = 55 }));
                Assert.That(study.Constraints.Single(), Is.EqualTo(new TradeOffStudyConstraint("REQ-SYS-001", "mass * 1.2 <= 150", "mass", "<=", 150, null, 20, 125)));
                Assert.That(study.PointsComplete, Is.True);
                Assert.That(study.Points, Has.Count.EqualTo(6));
                Assert.That(byVariants["current+current"].IsCurrent, Is.True);
                Assert.That(byVariants["current+current"].Feasible, Is.False);
                Assert.That(byVariants["current+current"].Broken, Is.EqualTo(["REQ-SYS-001: mass * 1.2 = 150.96 > 150"]));
                Assert.That(byVariants["current+current"].Totals, Is.EqualTo(new Dictionary<string, double> { ["mass"] = 125.8, ["power"] = 227 }));
                Assert.That(byVariants["A+XT-low"].Rank, Is.EqualTo(1));
                Assert.That(byVariants["A+current"].Rank, Is.EqualTo(2));
                Assert.That(byVariants["B+XT-low"].Rank, Is.EqualTo(3));
                Assert.That(byVariants["B+current"].Rank, Is.EqualTo(4));
                Assert.That(rankOne.Choices.Select(choice => choice.Variant), Is.EqualTo(study.Best[0].Choices.Select(choice => choice.Variant)));
                Assert.That(study.Best.Select(combination => combination.Rank), Is.EqualTo([1, 2, 3]));
                Assert.That(study.Best[0].Constraints.Single(), Is.EqualTo(new ConstraintCheck("mass * 1.2 <= 150", true, "REQ-SYS-001", 140.88, 150)));
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(elementCount));
            }
        }

        [Test]
        public void VerifyExploreTradeOffWithOtherSettings()
        {
            var tools = new TradeOffTools(this.modelProvider, new CpSatTradeOffSolver(), this.appHostDetector.Object);
            TradeOffObjective[] powerThenMass = [.. MassThenPower.Reverse()];
            TradeOffRule[] rules = [new TradeOffRule { If = "camera.A", Then = ["xBandTransmitter.XT-low"] }];

            var swapped = tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, powerThenMass, MassRequirement, count: 3);
            var withoutCurrentCamera = tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, MassRequirement, count: 3, excluded: ["camera.current"]);
            var withRule = tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, MassRequirement, rules, count: 3);
            var tooTight = tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, [MassRequirement[0] with { Margin = 30 }], count: 3);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(swapped.Best.Select(combination => string.Join("+", combination.Choices.Select(choice => choice.Variant))), Is.EqualTo(["B+XT-low", "B+current", "A+XT-low"]));
                Assert.That(withoutCurrentCamera.Points, Has.Count.EqualTo(4));
                Assert.That(withoutCurrentCamera.Points.Any(point => point.IsCurrent), Is.False);
                Assert.That(withoutCurrentCamera.Current.Choices[0].Variant, Is.EqualTo("current"));
                Assert.That(withoutCurrentCamera.Choices[0].Variants[0].Excluded, Is.True);
                Assert.That(withoutCurrentCamera.Request.Excluded, Is.EqualTo(["camera.current"]));
                Assert.That(withoutCurrentCamera.Best.SelectMany(combination => combination.Choices).Any(choice => choice.Choice == "camera" && choice.Variant == "current"), Is.False);
                Assert.That(withRule.Rules, Is.EqualTo(["camera.A requires xBandTransmitter.XT-low"]));
                Assert.That(withRule.Points.Single(point => point.Choices[0].Variant == "A" && point.Choices[1].Variant == "current").Broken, Is.EqualTo(["rule 1: camera.A requires xBandTransmitter.XT-low"]));
                Assert.That(tooTight.Status, Is.EqualTo(TradeOffStatus.Infeasible));
                Assert.That(tooTight.Best, Is.Empty);
                Assert.That(tooTight.Points.Any(point => point.Feasible), Is.False);
                Assert.That(tooTight.Conflicts, Is.EqualTo(["REQ-SYS-001: mass * 1.3 <= 150"]));
            }
        }

        [Test]
        public void VerifyExploreTradeOffRefusesUnknownSettings()
        {
            var tools = new TradeOffTools(this.modelProvider, new CpSatTradeOffSolver(), this.appHostDetector.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, excluded: ["camera.Z"]),
                    Throws.TypeOf<McpException>().With.Message.Contains("excluded refers to 'camera.Z', which is not a variant of a choice"));
                Assert.That(() => tools.ExploreTradeOff(SatelliteId, CameraAndTransmitter, MassThenPower, excluded: ["camera.current", "camera.A", "camera.B"]),
                    Throws.TypeOf<McpException>().With.Message.Contains("Every variant of 'camera' is turned off"));
                Assert.That(() => tools.ExploreTradeOff(Guid.NewGuid(), CameraAndTransmitter, MassThenPower), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ExploreTradeOff(SatelliteId, [CameraAndTransmitter[0] with { PartId = Guid.NewGuid() }], MassThenPower), Throws.TypeOf<McpException>());
            }
        }

        /// <summary>
        /// Reads the <see cref="TradeOffResult"/> that solve_trade_off writes for the AI assistant.
        /// </summary>
        /// <param name="toolResult">The <see cref="CallToolResult"/> of solve_trade_off.</param>
        /// <returns>The <see cref="TradeOffResult"/>.</returns>
        private static TradeOffResult ReadResult(CallToolResult toolResult)
        {
            return JsonSerializer.Deserialize<TradeOffResult>(ReadText(toolResult), McpJsonUtilities.DefaultOptions);
        }

        /// <summary>
        /// Reads the text that a tool writes for the AI assistant.
        /// </summary>
        /// <param name="toolResult">The <see cref="CallToolResult"/> of the tool.</param>
        /// <returns>The text of the result.</returns>
        private static string ReadText(CallToolResult toolResult)
        {
            return ((TextContentBlock)toolResult.Content.Single()).Text;
        }
    }
}
