// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffVerifierTestFixture.cs" company="Starion Group S.A.">
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
    using System.Linq;

    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.TradeOff;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffVerifier"/> class, on the orbiter of the <see cref="OrbiterChangesHelper"/>:
    /// its choices are, in order, the camera, the battery, the solar panel, the engine and the tank.
    /// </summary>
    [TestFixture]
    public class TradeOffVerifierTestFixture
    {
        /// <summary>
        /// The orbiter with the electric engine, three batteries of 150 Wh, three high-efficiency panels and no tank: it
        /// meets every requirement.
        /// </summary>
        private static readonly int[] Electric = [0, 2, 6, 1, 2];

        private InMemoryModelProvider modelProvider;

        private Guid orbiterId;

        private TradeOffProblem problem;

        private TradeOffVerifier verifier;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = OrbiterChangesHelper.CreateModel();
            this.orbiterId = this.modelProvider.Elements.OfType<IPartUsage>().Single(part => part.DeclaredName == OrbiterChangesHelper.OrbiterName).Id;
            this.problem = TradeOffProblemBuilder.Build(this.modelProvider, new TradeOffRequest(this.orbiterId, null, null, null, null, 1, null));
            this.verifier = new TradeOffVerifier(this.problem);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new TradeOffVerifier(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.verifier.Problem, Is.SameAs(this.problem));
                Assert.That(new TradeOffVerifier(new TradeOffProblem([], new Dictionary<string, double>(), [], [], [], 1, null)).Verify([]), Is.Empty);
            }
        }

        [Test]
        public void VerifyVerify()
        {
            var current = this.problem.GetCurrentSelection();
            var checks = this.verifier.Verify(current);
            var electricChecks = this.verifier.Verify(Electric);
            var withAnotherCamera = this.verifier.Verify([1, .. current.Skip(1)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.verifier.Verify(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(checks.Select(check => check.Requirement.Name), Is.EqualTo(["REQ-PL-001", "REQ-PRP-001", "REQ-PRP-002", "REQ-PWR-001", "REQ-PWR-002", "REQ-SYS-001"]));
                Assert.That(checks.Select(check => check.IsMet), Is.EqualTo([true, true, false, true, true, true]));
                Assert.That(checks.Select(check => check.DependsOnChoices), Is.All.True);

                // Each check only depends on the choices that it reads: the camera, the engine and the tank, the engine...
                Assert.That(checks.Select(check => string.Join(",", check.ReadOptions.Keys.Order())), Is.EqualTo(["0", "3,4", "3", "0,2,3", "1,3", "0,1,2,3,4"]));
                Assert.That(checks[2].ReadOptions, Is.EqualTo(new Dictionary<int, int> { [3] = 0 }));
                Assert.That(checks[2].Check.Status, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(checks[2].Check.Explanation, Is.EqualTo("\"hydrazine\" == \"hydrazine\""));
                Assert.That(electricChecks.Select(check => check.IsMet), Is.All.True);
                Assert.That(electricChecks[1].Check.Explanation, Is.EqualTo("0 [kg] >= 0 [kg]"));
                Assert.That(electricChecks[3].Check.Explanation, Is.EqualTo("258.5 [W] <= 270 [W]"));

                // The check of a requirement is done once for the combinations that take the same options where it reads.
                Assert.That(withAnotherCamera[2], Is.SameAs(checks[2]));
                Assert.That(withAnotherCamera[0], Is.Not.SameAs(checks[0]));
                Assert.That(withAnotherCamera[0].Check.Explanation, Is.EqualTo("30 [kg] > 25 [kg]"));
            }
        }

        [Test]
        public void VerifyVerifyWithIgnoredRequirements()
        {
            var ignoring = new TradeOffVerifier(TradeOffProblemBuilder.Build(this.modelProvider, new TradeOffRequest(this.orbiterId, null, null, null, null, 1, null, IgnoredRequirements: ["REQ-PRP-002"])));
            var current = ignoring.Problem.GetCurrentSelection();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ignoring.Verify(current).Select(check => check.Requirement.Name), Has.No.Member("REQ-PRP-002"));
                Assert.That(ignoring.Verify(current, true).Select(check => check.Requirement.Name), Has.Member("REQ-PRP-002"));
                Assert.That(ignoring.CountBroken(current), Is.Zero);
                Assert.That(ignoring.FindPenalties(current), Is.Empty);
            }
        }

        [Test]
        public void VerifyCountBrokenAndFindPenalties()
        {
            var current = this.problem.GetCurrentSelection();
            var penalty = this.verifier.FindPenalties(current).Single();

            // With the sharp camera and two standard panels, the electric engine breaks the mass of the payload and the power balance.
            int[] overloaded = [1, 2, 0, 1, 2];
            var withConstraint = this.problem with { Limits = [new TradeOffLimit("orbiter.mass", TradeOffOperator.LessThanOrEqual, 50, null, 0), .. this.problem.Limits] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.verifier.CountBroken(current), Is.EqualTo(1));
                Assert.That(this.verifier.CountBroken(Electric), Is.Zero);
                Assert.That(this.verifier.CountBroken(overloaded), Is.EqualTo(2));
                Assert.That(new TradeOffVerifier(withConstraint).CountBroken(Electric), Is.EqualTo(1));

                // The solver cannot read the requirement on the propellant: its check teaches it that the thermal engine breaks it.
                Assert.That(penalty.RequirementIndex, Is.EqualTo(2));
                Assert.That(penalty.Options, Is.EqualTo(new Dictionary<int, int> { [3] = 0 }));
                Assert.That(penalty.Weight, Is.EqualTo(1));
                Assert.That(this.verifier.FindPenalties(Electric), Is.Empty);
                Assert.That(this.verifier.FindPenalties(overloaded), Is.Empty);
            }
        }
    }
}
