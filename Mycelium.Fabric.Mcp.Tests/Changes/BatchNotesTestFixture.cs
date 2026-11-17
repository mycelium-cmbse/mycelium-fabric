// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchNotesTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Changes
{
    using System;
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Suite of tests for the <see cref="BatchNotes"/> class.
    /// </summary>
    [TestFixture]
    public class BatchNotesTestFixture
    {
        [Test]
        public void VerifyDescribe()
        {
            var modelProvider = RedefinitionChangesHelper.CreateModel();

            // A new value of Instrument changes both cameras. A new attribute of a definition, a new value of a usage and a new
            // kind of value change no other element.
            var newMass = modelProvider.ApplyChanges(
            [
                new ModelChange { Identity = "instrumentMass", Payload = new ElementPayload { Value = 12, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Instrument", Name = "cost", Value = 1000 } },
                new ModelChange { Identity = "length", Payload = new ElementPayload { Value = 4, Unit = "m" } },
                new ModelChange { Identity = "cameraPower", Payload = new ElementPayload { IsDefault = false } }
            ]);

            // Once each camera has a mass of its own, the value of Instrument is shared by none.
            var ownMasses = modelProvider.ApplyChanges(
            [
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "camera", Name = "mass", Value = 38, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "spareCamera", Name = "mass", Value = 39, Unit = "kg" } }
            ]);

            var unsharedMass = modelProvider.ApplyChanges([new ModelChange { Identity = "instrumentMass", Payload = new ElementPayload { Value = 11, Unit = "kg" } }]);

            // A Battery does not specialize Engine: the engine of the rover becomes both.
            var unrelatedType = modelProvider.ApplyChanges([new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "rover", Redefines = "engine", Definition = "Battery" } }]);
            var refused = modelProvider.ApplyChanges([new ModelChange { Identity = "instrumentMass", Payload = new ElementPayload { Value = 13, Unit = "parsec" } }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => BatchNotes.Describe(null, new HashSet<Guid>(), new Dictionary<Guid, IElement>()), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => BatchNotes.Describe(new CommitRequest(), null, new Dictionary<Guid, IElement>()), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => BatchNotes.Describe(new CommitRequest(), new HashSet<Guid>(), null), Throws.TypeOf<ArgumentNullException>());

                Assert.That(newMass.Notes, Is.EqualTo(
                [
                    "The value of 'Garage::Instrument::mass' is shared by 2 usages ('Garage::rover::camera', 'Garage::rover::spareCamera'): they all take the new value. "
                        + "To change the value of one usage only, keep the value of the definition and create under the usage an element that redefines 'mass' (redefines) with its own value."
                ]));

                Assert.That(ownMasses.Notes, Is.Empty);
                Assert.That(unsharedMass.Applied, Is.True);
                Assert.That(unsharedMass.Notes, Is.Empty);

                Assert.That(unrelatedType.Notes, Is.EqualTo(
                [
                    "'Battery' does not specialize 'Engine': the PartUsage 'Garage::rover::engine' is now both, with the features of both. "
                        + "If a Battery is a kind of Engine, make Battery specialize it (specializes)."
                ]));

                Assert.That(refused.Applied, Is.False);
                Assert.That(refused.Notes, Is.Empty);
            }
        }
    }
}
