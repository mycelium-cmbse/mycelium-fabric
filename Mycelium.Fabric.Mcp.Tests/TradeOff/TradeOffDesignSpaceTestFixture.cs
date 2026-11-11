// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffDesignSpaceTestFixture.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.TradeOff;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffDesignSpace"/> class, on the orbiter of the <see cref="OrbiterChangesHelper"/>.
    /// </summary>
    [TestFixture]
    public class TradeOffDesignSpaceTestFixture
    {
        private Dictionary<string, IPartUsage> parts;

        private IPartUsage orbiter;

        private TradeOffDesignSpace space;

        [SetUp]
        public void SetUp()
        {
            this.parts = OrbiterChangesHelper.CreateModel().Elements.OfType<IPartUsage>().ToDictionary(part => part.qualifiedName);
            this.orbiter = this.parts["Orbiter::orbiter"];

            var liIon150 = this.parts["Orbiter::Catalogues::BatteryOptions::liIon150"];
            var liIon300 = this.parts["Orbiter::Catalogues::BatteryOptions::liIon300"];
            var large = this.parts["Orbiter::Catalogues::TankOptions::large"];
            var camera = this.parts["Orbiter::orbiter::payload::camera"];
            var lighterCamera = new Dictionary<string, NumberValue> { ["mass"] = new NumberValue(25, UnitCatalog.TryParse("kg", out var kilogram) ? kilogram : null) };

            // Two batteries of 300 Wh, the tank taken 0 times, a lighter camera or none, and two radiators to add.
            this.space = new TradeOffDesignSpace(this.orbiter.Id,
            [
                new TradeOffOpenPart(this.parts["Orbiter::orbiter::powerSystem::battery"], "battery",
                    [new TradeOffPartContent(liIon150, null, 1, true, true), new TradeOffPartContent(liIon300, null, 2, true)]),
                new TradeOffOpenPart(this.parts["Orbiter::orbiter::propulsion::tank"], "tank",
                    [new TradeOffPartContent(large, null, 1, true, true), new TradeOffPartContent(large, null, 0, true)]),
                new TradeOffOpenPart(camera, "camera",
                    [new TradeOffPartContent(camera, null, 1, IsCurrent: true), new TradeOffPartContent(camera, lighterCamera, 1), new TradeOffPartContent(null, null, 0)]),
                new TradeOffOpenPart(null, "radiator",
                    [new TradeOffPartContent(null, null, 0, IsCurrent: true), new TradeOffPartContent(null, new Dictionary<string, NumberValue> { ["mass"] = new NumberValue(4) }, 2)])
            ]);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new TradeOffDesignSpace(this.orbiter.Id, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.space.Keys, Has.Count.EqualTo(4));
                Assert.That(this.space.Keys[0], Is.EqualTo(this.parts["Orbiter::orbiter::powerSystem::battery"].Id));
                Assert.That(this.space.Keys[3], Is.Not.EqualTo(Guid.Empty));
                Assert.That(this.space.Keys, Is.Unique);
            }
        }

        [Test]
        public void VerifyGetTotalParts()
        {
            var mass = this.space.GetTotalParts(this.orbiter, "mass").Value;
            var payloadMass = this.space.GetTotalParts(this.parts["Orbiter::orbiter::payload"], "mass").Value;
            var batteryEnergy = this.space.GetTotalParts(this.parts["Orbiter::orbiter::powerSystem::battery"], "energyCapacity").Value;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.space.GetTotalParts(null, "mass"), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Sum(mass.Fixed), Is.EqualTo(62));
                Assert.That(mass.Options.Select(options => options.Select(Sum)), Is.EqualTo(new double[][] { [3, 10], [9, 0], [18, 25, 0], [0, 8] }));
                Assert.That(Sum(payloadMass.Fixed), Is.Zero);
                Assert.That(payloadMass.Options.Select(options => options.Select(Sum)), Is.EqualTo(new double[][] { [0, 0], [0, 0], [18, 25, 0], [0, 0] }));

                // The total of an open part itself is the one of one unit of what it holds.
                Assert.That(batteryEnergy.Fixed, Is.Empty);
                Assert.That(batteryEnergy.Options.Select(options => options.Select(Sum)), Is.EqualTo(new double[][] { [150, 300], [0, 0], [0, 0, 0], [0, 0] }));
            }

            var propulsion = this.parts["Orbiter::orbiter::propulsion"];
            var withPropulsion = new TradeOffDesignSpace(this.orbiter.Id, [new TradeOffOpenPart(propulsion, "propulsion", [new TradeOffPartContent(propulsion, null, 1, IsCurrent: true)])]);
            var engineMass = withPropulsion.GetTotalParts(this.parts["Orbiter::orbiter::propulsion::engine"], "mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineMass.IsError, Is.True);
                Assert.That(engineMass.FirstError.Description, Is.EqualTo("'engine' belongs to 'propulsion', whose content is a choice."));
            }
        }

        [Test]
        public void VerifyCreateSelection()
        {
            var changed = this.space.CreateSelection([1, 1, 1, 1]);
            var readForMass = new HashSet<Guid>();
            var readForFuel = new HashSet<Guid>();
            var readForPayload = new HashSet<Guid>();
            var ignored = new HashSet<Guid>();

            var mass = changed.CollectValues(this.orbiter, "mass", readForMass).Value;
            var fuelCapacity = changed.CollectValues(this.orbiter, "fuelCapacity", readForFuel).Value;
            var payloadMass = changed.CollectValues(this.parts["Orbiter::orbiter::payload"], "mass", readForPayload).Value;
            var batteryMass = changed.CollectValues(this.parts["Orbiter::orbiter::powerSystem::battery"], "mass", ignored).Value;
            var camera = this.parts["Orbiter::orbiter::payload::camera"];
            var tank = this.parts["Orbiter::orbiter::propulsion::tank"];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.space.CreateSelection(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => changed.CollectValues(null, "mass", ignored), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => changed.CollectValues(this.orbiter, "mass", null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Sum(mass.Contributions), Is.EqualTo(105));
                Assert.That(mass.MissingValues, Is.Empty);
                Assert.That(readForMass, Is.EquivalentTo(this.space.Keys));

                // The tank taken 0 times stays in the model and counts for 0.
                Assert.That(fuelCapacity.Contributions.Single().Count, Is.Zero);
                Assert.That(readForFuel, Is.EquivalentTo(new[] { tank.Id }));
                Assert.That(Sum(payloadMass.Contributions), Is.EqualTo(25));
                Assert.That(readForPayload, Is.EquivalentTo(new[] { camera.Id }));
                Assert.That(Sum(batteryMass.Contributions), Is.EqualTo(5));

                Assert.That(changed.Resolve(this.parts["Orbiter::orbiter::powerSystem::battery"], ignored).Value, Is.SameAs(this.parts["Orbiter::Catalogues::BatteryOptions::liIon300"]));
                Assert.That(changed.Resolve(tank, ignored).Value, Is.SameAs(this.parts["Orbiter::Catalogues::TankOptions::large"]));
                Assert.That(changed.Resolve(this.parts["Orbiter::orbiter::structure"], ignored).Value, Is.SameAs(this.parts["Orbiter::orbiter::structure"]));
                Assert.That(changed.Resolve(this.parts["Orbiter::orbiter::propulsion::engine"], ignored).Value, Is.SameAs(this.parts["Orbiter::Catalogues::EngineOptions::thermal"]));
                Assert.That(changed.GetGivenValue(camera, "mass", ignored).Number, Is.EqualTo(25));
                Assert.That(changed.GetGivenValue(camera, "power", ignored), Is.Null);
                Assert.That(changed.GetGivenValue(this.parts["Orbiter::orbiter::structure"], "mass", ignored), Is.Null);
            }

            var withoutCamera = this.space.CreateSelection([0, 0, 2, 0]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Sum(withoutCamera.CollectValues(this.orbiter, "mass", ignored).Value.Contributions), Is.EqualTo(74));
                Assert.That(withoutCamera.CollectValues(camera, "mass", ignored).FirstError.Description, Is.EqualTo("'camera' is not taken in this combination."));
                Assert.That(withoutCamera.Resolve(camera, ignored).FirstError.Description, Is.EqualTo("'camera' is not taken in this combination."));
                Assert.That(withoutCamera.GetGivenValue(camera, "mass", ignored), Is.Null);
            }
        }

        [Test]
        public void VerifyCreateSelectionWithASubPartOfAnOpenPart()
        {
            var propulsion = this.parts["Orbiter::orbiter::propulsion"];
            var engine = this.parts["Orbiter::orbiter::propulsion::engine"];

            var withPropulsion = new TradeOffDesignSpace(this.orbiter.Id,
                [new TradeOffOpenPart(propulsion, "propulsion", [new TradeOffPartContent(propulsion, null, 1, IsCurrent: true), new TradeOffPartContent(null, null, 0)])]);

            var read = new HashSet<Guid>();
            var kept = withPropulsion.CreateSelection([0]);
            var removed = withPropulsion.CreateSelection([1]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Sum(kept.CollectValues(engine, "mass", read).Value.Contributions), Is.EqualTo(12));
                Assert.That(read, Is.EquivalentTo(new[] { propulsion.Id }));
                Assert.That(kept.Resolve(engine, read).Value, Is.SameAs(this.parts["Orbiter::Catalogues::EngineOptions::thermal"]));
                Assert.That(removed.CollectValues(engine, "mass", read).FirstError.Description, Is.EqualTo("'engine' belongs to 'propulsion', which this combination replaces or removes."));
                Assert.That(removed.Resolve(engine, read).IsError, Is.True);
                Assert.That(Sum(removed.CollectValues(this.orbiter, "mass", read).Value.Contributions), Is.EqualTo(71));
            }
        }

        /// <summary>
        /// Adds up contributions, each counted as many times as it says.
        /// </summary>
        /// <param name="contributions">The contributions.</param>
        /// <returns>The total.</returns>
        private static double Sum(IReadOnlyList<Contribution> contributions)
        {
            return contributions.Sum(contribution => contribution.GetTotal().Number);
        }
    }
}
