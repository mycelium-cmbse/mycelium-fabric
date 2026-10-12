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

    using ModelContextProtocol;

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

        private InMemoryModelProvider modelProvider;

        private Mock<ITradeOffSolver> solver;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object, new DeSerializer());
            this.modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));
            this.solver = new Mock<ITradeOffSolver>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new TradeOffTools(null, this.solver.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new TradeOffTools(this.modelProvider, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new TradeOffTools(this.modelProvider, this.solver.Object), Throws.Nothing);
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

            var tools = new TradeOffTools(this.modelProvider, this.solver.Object);
            TradeOffChoice[] choices = [new TradeOffChoice { PartId = CameraId, Variants = [new TradeOffVariant { Name = "light", Values = new Dictionary<string, double> { ["mass"] = 30 } }] }];
            TradeOffConstraint[] constraints = [new TradeOffConstraint { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 }];

            var result = tools.SolveTradeOff(SatelliteId, choices, constraints: constraints);
            var compared = tools.SolveTradeOff(SatelliteId, choices, constraints: constraints, compareBy: "camera");

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
                Assert.That(result.Current.Constraints, Is.EqualTo([new ConstraintCheck("mass * 1.2 <= 150", false)]));
                Assert.That(result.Combinations, Has.Count.EqualTo(1));
                Assert.That(result.Combinations[0].Rank, Is.EqualTo(1));
                Assert.That(result.Combinations[0].ChangeCount, Is.EqualTo(1));
                Assert.That(result.Combinations[0].Choices, Is.EqualTo([new ChosenVariant("camera", CameraId, CameraQualifiedName, "light", 1, true)]));
                Assert.That(result.Combinations[0].Totals, Is.EqualTo([new AttributeTotal("mass", 117.8, -8)]));
                Assert.That(result.Combinations[0].Constraints, Is.EqualTo([new ConstraintCheck("mass * 1.2 <= 150", true)]));
                Assert.That(compared.Combinations.Single().Alternative, Is.EqualTo("camera.current"));
                Assert.That(compared.Conflicts, Is.EqualTo(["camera.light: constraint 1: mass * 1.2 <= 150"]));
            }

            this.solver.Verify(tradeOffSolver => tradeOffSolver.Solve(It.Is<TradeOffProblem>(problem => problem.Decisions.Count == 1 && problem.Limits.Count == 1)), Times.Exactly(2));
        }

        [Test]
        public void VerifySolveTradeOffWithTheSolver()
        {
            var tools = new TradeOffTools(this.modelProvider, new CpSatTradeOffSolver());

            TradeOffChoice[] cameraChoice = [new TradeOffChoice { PartId = CameraId, Variants = [new TradeOffVariant { Name = "light", Values = new Dictionary<string, double> { ["mass"] = 30 } }] }];
            TradeOffConstraint[] massBudget = [new TradeOffConstraint { Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 }];

            var lighterCamera = tools.SolveTradeOff(SatelliteId, cameraChoice, constraints: massBudget);

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

            var propulsion = tools.SolveTradeOff(SatelliteId, propulsionChoices, lowestMass, powerBalance, electricRule, "propulsionSubsystem");
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
            var withoutElectric = tools.SolveTradeOff(SatelliteId, propulsionChoices, lowestMass, tightBudget, electricRule, "propulsionSubsystem");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutElectric.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(withoutElectric.Combinations.Single().Alternative, Is.EqualTo("propulsionSubsystem.current"));
                Assert.That(withoutElectric.Conflicts, Is.EqualTo(["propulsionSubsystem.electric: constraint 1: power <= powerOutput", "propulsionSubsystem.electric: constraint 3: mass <= 139"]));
            }
        }
    }
}
