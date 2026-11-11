// ------------------------------------------------------------------------------------------------
//  <copyright file="OrbiterChangesHelper.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Builds a small orbiter whose equipment is chosen in catalogues, and whose needs are requirements with a value to
    /// check, so that a trade-off can follow every consequence of a choice without the AI assistant.
    /// <para>
    /// The <c>orbiter</c> holds a <c>structure</c> (40 kg), a <c>computer</c> (5 kg, 20 W, 50 Wh per eclipse), a
    /// <c>payload</c> with a <c>camera</c>, a <c>propulsion</c> with an <c>engine</c> and a <c>tank</c>, and a
    /// <c>powerSystem</c> with a <c>battery</c> and a <c>solarPanel</c>. Five catalogues give the variants:
    /// </para>
    /// <list type="bullet">
    /// <item><c>EngineOptions</c>: <c>thermal</c> (12 kg, 15 W, 40 kg of fuel, 20 Wh, hydrazine) or <c>electric</c> (8 kg,
    /// 180 W, no fuel, 400 Wh, xenon);</item>
    /// <item><c>TankOptions</c>: <c>small</c> (6 kg, 30 kg of fuel) or <c>large</c> (9 kg, 60 kg of fuel), 0 or 1 unit;</item>
    /// <item><c>BatteryOptions</c>: <c>liIon150</c> (3 kg, 150 Wh) or <c>liIon300</c> (5 kg, 300 Wh), 1 to 4 units;</item>
    /// <item><c>SolarPanelOptions</c>: <c>regular</c> (2.5 kg, 60 W) or <c>highEfficiency</c> (3 kg, 90 W), 2 to 6 units;</item>
    /// <item><c>CameraOptions</c>: <c>wide</c> (18 kg, 35 W) or <c>sharp</c> (30 kg, 60 W).</item>
    /// </list>
    /// <para>
    /// Today the orbiter takes the thermal engine, one large tank, one <c>liIon150</c> battery, two regular panels and the
    /// wide camera: 92 kg, 70 W used for 120 W generated, 70 Wh needed for 150 Wh stored, 40 kg of fuel for a tank of 60 kg.
    /// Every requirement holds except <c>REQ-PRP-002</c> (no hydrazine), which only the electric engine meets; the electric
    /// engine in turn needs more panels and more battery, and no tank.
    /// </para>
    /// </summary>
    internal static class OrbiterChangesHelper
    {
        /// <summary>
        /// The name of the part that holds the orbiter.
        /// </summary>
        public const string OrbiterName = "orbiter";

        /// <summary>
        /// Loads an empty model and applies the changes that build the orbiter.
        /// </summary>
        /// <returns>The <see cref="InMemoryModelProvider"/> that holds the model.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the changes are refused.</exception>
        public static InMemoryModelProvider CreateModel()
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var result = modelProvider.ApplyChanges(CreateChanges());

            return result.Applied ? modelProvider : throw new InvalidOperationException(string.Join(" ", result.Problems));
        }

        /// <summary>
        /// Creates the changes that build the orbiter, its catalogues and its requirements.
        /// </summary>
        /// <returns>The <see cref="ModelChange"/>s of one batch.</returns>
        public static List<ModelChange> CreateChanges()
        {
            return
            [
                Create("Orbiter", "Package", null, "Orbiter"),
                Create("Catalogues", "Package", "Orbiter", "Catalogues"),
                Create("Requirements", "Package", "Orbiter", "Requirements"),

                .. CreateCatalogue("Engine", "EngineOptions",
                    ("thermal", null, [("mass", 12, "kg"), ("power", 15, "W"), ("fuelNeed", 40, "kg"), ("energyNeed", 20, "Wh")], "hydrazine"),
                    ("electric", null, [("mass", 8, "kg"), ("power", 180, "W"), ("fuelNeed", 0, "kg"), ("energyNeed", 400, "Wh")], "xenon")),
                .. CreateCatalogue("Tank", "TankOptions",
                    ("small", "0..1", [("mass", 6, "kg"), ("fuelCapacity", 30, "kg")], null),
                    ("large", "0..1", [("mass", 9, "kg"), ("fuelCapacity", 60, "kg")], null)),
                .. CreateCatalogue("Battery", "BatteryOptions",
                    ("liIon150", "1..4", [("mass", 3, "kg"), ("energyCapacity", 150, "Wh")], null),
                    ("liIon300", "1..4", [("mass", 5, "kg"), ("energyCapacity", 300, "Wh")], null)),
                .. CreateCatalogue("SolarPanel", "SolarPanelOptions",
                    ("regular", "2..6", [("mass", 2.5, "kg"), ("powerOutput", 60, "W")], null),
                    ("highEfficiency", "2..6", [("mass", 3, "kg"), ("powerOutput", 90, "W")], null)),
                .. CreateCatalogue("Camera", "CameraOptions",
                    ("wide", null, [("mass", 18, "kg"), ("power", 35, "W")], null),
                    ("sharp", null, [("mass", 30, "kg"), ("power", 60, "W")], null)),

                Create(OrbiterName, "PartUsage", "Orbiter", OrbiterName),
                Create("structure", "PartUsage", OrbiterName, "structure"),
                CreateAttribute("structure", "mass", 40, "kg"),
                Create("computer", "PartUsage", OrbiterName, "computer"),
                CreateAttribute("computer", "mass", 5, "kg"),
                CreateAttribute("computer", "power", 20, "W"),
                CreateAttribute("computer", "energyNeed", 50, "Wh"),
                Create("payload", "PartUsage", OrbiterName, "payload"),
                CreateChoice("camera", "payload", "CameraOptions", "wide", null),
                Create("propulsion", "PartUsage", OrbiterName, "propulsion"),
                CreateChoice("engine", "propulsion", "EngineOptions", "thermal", null),
                CreateChoice("tank", "propulsion", "TankOptions", "large", "1"),
                Create("powerSystem", "PartUsage", OrbiterName, "powerSystem"),
                CreateChoice("battery", "powerSystem", "BatteryOptions", "liIon150", "1"),
                CreateChoice("solarPanel", "powerSystem", "SolarPanelOptions", "regular", "2"),

                .. CreateRequirement("massBudget", "REQ-SYS-001", "The orbiter shall weigh at most 150 kg, with a margin of 20 %.", OrbiterName,
                    new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Unit = "kg", Margin = 20 }),
                .. CreateRequirement("powerBalance", "REQ-PWR-001", "The solar panels shall generate the power that the equipment uses, with a margin of 10 %.", OrbiterName,
                    new ConstraintPayload { Attribute = "power", Operator = "<=", LimitAttribute = "powerOutput", Margin = 10 }),
                .. CreateRequirement("eclipseEnergy", "REQ-PWR-002", "The batteries shall store the energy that the equipment needs during an eclipse.", OrbiterName,
                    new ConstraintPayload { Attribute = "energyCapacity", Operator = ">=", LimitAttribute = "energyNeed" }),
                .. CreateRequirement("fuelCapacity", "REQ-PRP-001", "The tank shall hold the fuel that the engine needs.", OrbiterName,
                    new ConstraintPayload { Attribute = "fuelCapacity", Operator = ">=", LimitAttribute = "fuelNeed" }),
                .. CreateRequirement("greenPropellant", "REQ-PRP-002", "The engine shall not use hydrazine.", "propulsion",
                    new ConstraintPayload { Attribute = "engine.propellant", Operator = "!=", Limit = "hydrazine" }),
                .. CreateRequirement("payloadMass", "REQ-PL-001", "The payload shall weigh at most 25 kg.", "payload",
                    new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 25, Unit = "kg" }),
                new ModelChange
                {
                    Payload = new ElementPayload { Type = "RequirementUsage", Owner = "Requirements", Name = "lifetime", ReqId = "REQ-SYS-002", Text = "The orbiter shall operate for five years." }
                }
            ];
        }

        /// <summary>
        /// Creates the change that creates an element.
        /// </summary>
        /// <param name="temporaryName">The temporary name of the element.</param>
        /// <param name="type">The metaclass of the element.</param>
        /// <param name="owner">The temporary name of its owner, or <c>null</c>.</param>
        /// <param name="name">The name of the element.</param>
        /// <returns>The <see cref="ModelChange"/>.</returns>
        private static ModelChange Create(string temporaryName, string type, string owner, string name)
        {
            return new ModelChange { Identity = temporaryName, Payload = new ElementPayload { Type = type, Owner = owner, Name = name } };
        }

        /// <summary>
        /// Creates the change that gives an attribute with a value to an element.
        /// </summary>
        /// <param name="owner">The temporary name of the element.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <param name="unit">The unit of the value.</param>
        /// <returns>The <see cref="ModelChange"/>.</returns>
        private static ModelChange CreateAttribute(string owner, string name, double value, string unit)
        {
            return new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = owner, Name = name, Value = value, Unit = unit } };
        }

        /// <summary>
        /// Creates the change that creates a part typed by a catalogue, with the variant that it takes.
        /// </summary>
        /// <param name="name">The name and temporary name of the part.</param>
        /// <param name="owner">The temporary name of its owner.</param>
        /// <param name="catalogue">The temporary name of the catalogue.</param>
        /// <param name="variant">The name of the variant taken.</param>
        /// <param name="multiplicity">The number of units, or <c>null</c> for one.</param>
        /// <returns>The <see cref="ModelChange"/>.</returns>
        private static ModelChange CreateChoice(string name, string owner, string catalogue, string variant, string multiplicity)
        {
            return new ModelChange
            {
                Identity = name,
                Payload = new ElementPayload { Type = "PartUsage", Owner = owner, Name = name, Definition = catalogue, Variant = variant, Multiplicity = multiplicity }
            };
        }

        /// <summary>
        /// Creates the changes that create a part definition and the catalogue of its variants.
        /// </summary>
        /// <param name="definition">The name of the part definition that the catalogue specializes.</param>
        /// <param name="catalogue">The name and temporary name of the catalogue.</param>
        /// <param name="variants">
        /// The variants: their name, the number of units that a trade-off may take, their numeric attributes and their
        /// propellant, or <c>null</c> for none.
        /// </param>
        /// <returns>The <see cref="ModelChange"/>s.</returns>
        private static IEnumerable<ModelChange> CreateCatalogue(string definition, string catalogue,
            params (string Name, string Multiplicity, (string Name, double Value, string Unit)[] Attributes, string Propellant)[] variants)
        {
            yield return Create(definition, "PartDefinition", "Catalogues", definition);
            yield return new ModelChange { Identity = catalogue, Payload = new ElementPayload { Type = "PartDefinition", Owner = "Catalogues", Name = catalogue, Specializes = definition, IsVariation = true } };

            foreach (var variant in variants)
            {
                var temporaryName = $"{catalogue}.{variant.Name}";

                yield return new ModelChange { Identity = temporaryName, Payload = new ElementPayload { Type = "PartUsage", Owner = catalogue, Name = variant.Name, Multiplicity = variant.Multiplicity } };

                foreach (var attribute in variant.Attributes)
                {
                    yield return CreateAttribute(temporaryName, attribute.Name, attribute.Value, attribute.Unit);
                }

                if (variant.Propellant != null)
                {
                    yield return new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = temporaryName, Name = "propellant", Value = variant.Propellant } };
                }
            }
        }

        /// <summary>
        /// Creates the changes that create a requirement with one constraint, satisfied by a part.
        /// </summary>
        /// <param name="name">The name and temporary name of the requirement.</param>
        /// <param name="reqId">The identifier of the requirement.</param>
        /// <param name="text">The text of the requirement.</param>
        /// <param name="satisfyingPart">The temporary name of the part that satisfies it.</param>
        /// <param name="constraint">The constraint of the requirement.</param>
        /// <returns>The <see cref="ModelChange"/>s.</returns>
        private static IEnumerable<ModelChange> CreateRequirement(string name, string reqId, string text, string satisfyingPart, ConstraintPayload constraint)
        {
            yield return new ModelChange { Identity = name, Payload = new ElementPayload { Type = "RequirementUsage", Owner = "Requirements", Name = name, ReqId = reqId, Text = text, Constraint = constraint } };
            yield return new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = name, SatisfyingPart = satisfyingPart } };
        }
    }
}
