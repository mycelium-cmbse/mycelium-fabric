// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementPayload.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json.Serialization;

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.Systems.States;

    /// <summary>
    /// The payload of a <see cref="ModelChange"/>: a compact description of an element, from which the server builds the
    /// SysML v2 element and its relationships. Only the given properties are used.
    /// </summary>
    /// <remarks>
    /// The payload is not the DTO of the element: the AI assistant never writes SysML. It names the metaclass and the
    /// related elements, and the server builds the memberships, typings, feature values and documentation, as the textual
    /// notation does.
    /// </remarks>
    public sealed record ElementPayload
    {
        /// <summary>
        /// Gets the name of the SysML v2 metaclass of the element to create, for example <c>PartUsage</c>. A payload with a
        /// type creates an element.
        /// </summary>
        [Description("Create only: the SysML v2 metaclass of the new element, for example Package, PartDefinition, PartUsage, AttributeUsage or RequirementUsage. Relationships are not created directly: the server builds them from owner, definition and value.")]
        public string Type { get; init; }

        /// <summary>
        /// Gets the element that owns the created element.
        /// </summary>
        [Description("Create: the element that owns the new element (a package, a definition or a usage), as an identifier (Id) or a temporary name. Without owner, the element is created at the top level of the model. An update cannot change it.")]
        public string Owner { get; init; }

        /// <summary>
        /// Gets the declared name of the created element, or the new name of the updated element.
        /// </summary>
        [Description("Create: the name of the new element. Update: the new name. It must differ from the names of the other members of the owner.")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the definition that types the element, which must be a feature.
        /// </summary>
        [Description("Create or update, for a feature such as a part: the definition that types it, for example the part definition of a part, as an identifier (Id) or a temporary name. An update replaces the current definition.")]
        public string Definition { get; init; }

        /// <summary>
        /// Gets the value of the element, which must be an attribute.
        /// </summary>
        [Description("Create or update, for an attribute: its value, a number (38 or 1.5), a Boolean (true) or a text ('S-band'); a text that names an enumeration value of the model ('sunSynchronous' or 'OrbitKind::sunSynchronous') references it. An update replaces the current value.")]
        public PayloadValue? Value { get; init; }

        /// <summary>
        /// Gets the unit of the numeric value of the element.
        /// </summary>
        [Description("Optional, with a numeric value: its unit, as a symbol (kg, W, km/h, m/s^2, arcsec, kWh, Mbit/s) or a name (kilogram). The value is then 38 [kg], and the budgets and checks convert the units.")]
        public string Unit { get; init; }

        /// <summary>
        /// Gets the documentation of the element, which is the text of a requirement.
        /// </summary>
        [Description("Create or update: the documentation of the element, for example the unit of an attribute ('Dry mass of the unit [kg].'). Required for a RequirementUsage: the text of the requirement. An update replaces the current documentation.")]
        public string Text { get; init; }

        /// <summary>
        /// Gets the identifier of the requirement in its specification, which is its short name.
        /// </summary>
        [Description("Create or update, for a RequirementUsage: its identifier in the specification, for example 'REQ-SYS-001'. It must differ from the identifiers of the other requirements.")]
        public string ReqId { get; init; }

        /// <summary>
        /// Gets the constraint that makes the requirement verifiable.
        /// </summary>
        [Description("Create or update, for a RequirementUsage: a constraint that makes it verifiable (a required constraint, or an assumption with kind Assumption), on an attribute of its subject. It replaces the current constraints of the same kind. For several limits, give constraints instead.")]
        public ConstraintPayload Constraint { get; init; }

        /// <summary>
        /// Gets the constraints that make the requirement verifiable, when it has several.
        /// </summary>
        [Description("Create or update, for a RequirementUsage, instead of constraint: several constraints that must all hold, for example a mass limit and a power limit. They replace the current constraints of the kinds they have.")]
        public IReadOnlyList<ConstraintPayload> Constraints { get; init; }

        /// <summary>
        /// Gets the multiplicity of the usage: how many there are, as the textual notation writes it between brackets.
        /// </summary>
        [Description("Create or update, for a usage such as a part, a port, an item or an attribute: how many there are, as written between brackets: '4' (part wheels : Wheel[4]), '1..4' or '0..*'. sum_attribute and check_requirements count a part 4 times for '4'; a number that is not fixed ('1..4', '0..*') cannot be counted. An update replaces it.")]
        public string Multiplicity { get; init; }

        /// <summary>
        /// Gets a value indicating whether the created part definition is a catalogue of variants (a variation definition).
        /// </summary>
        [Description("Create, for a PartDefinition: true makes it a catalogue of variants (variation part def BatteryOptions :> Battery). A catalogue only owns its variants: "
            + "create each variant as a PartUsage whose owner is the catalogue, with its own attribute values. "
            + "The multiplicity of a variant is the number of units that a trade-off may take of it ('1..4', or '0..1' for optional equipment); without multiplicity, the number of units does not change.")]
        public bool? IsVariation { get; init; }

        /// <summary>
        /// Gets the variant that the part takes among the variants of the catalogue that types it.
        /// </summary>
        [Description("Create or update, for a part whose definition is a catalogue (a PartDefinition with isVariation): the variant that it takes (part battery : BatteryOptions = BatteryOptions::liIon40), "
            + "by its name in the catalogue, its identifier (Id) or its temporary name. sum_attribute, check_requirements and solve_trade_off then count the values of this variant, "
            + "times the multiplicity of the part. An update replaces the variant taken.")]
        public string Variant { get; init; }

        /// <summary>
        /// Gets the requirement that the created <c>SatisfyRequirementUsage</c> satisfies.
        /// </summary>
        [Description("Create, for a SatisfyRequirementUsage: the satisfied requirement, as an identifier (Id) or a temporary name. Without owner, the satisfy link is created next to the requirement.")]
        public string SatisfiedRequirement { get; init; }

        /// <summary>
        /// Gets the part that satisfies the requirement of the created <c>SatisfyRequirementUsage</c>.
        /// </summary>
        [Description("Create, for a SatisfyRequirementUsage: the usage that satisfies the requirement, as an identifier (Id) or a temporary name. Usually a part (the satellite, a subsystem), but also an action (a function) or an interface.")]
        public string SatisfyingPart { get; init; }

        /// <summary>
        /// Gets the two ends of the created connector, as paths of names from its owner.
        /// </summary>
        [Description("Create, for a ConnectionUsage, an InterfaceUsage, a BindingConnectorAsUsage, an AllocationUsage (from the allocated element to its target), a SuccessionAsUsage (from the action that comes first to the one that follows), a TransitionUsage (from the state or action that it leaves to the one that it enters) or a FlowUsage (from the feature that the item leaves to the one that it enters, each through the usage that holds it, for example 'takeImage.image' or 'camera.dataOut'): its two ends, as paths of names from its owner through the features of each element and of its definitions, for example ['payloadSubsystem.camera.dataOut', 'dataHandlingSubsystem.obc.dataIn']. An end that is not a feature of the owner is given by its identifier (Id), its temporary name or its qualified name, followed by '.' and names when needed ('<Id>.dataOut'). The ends of an interface are ports. They cannot be updated: delete the element and create it again.")]
        public IReadOnlyList<string> Ends { get; init; }

        /// <summary>
        /// Gets the element that the created or updated definition or usage specializes.
        /// </summary>
        [Description("Create or update, for a definition or a usage: the element it specializes (:>), as an identifier (Id) or a temporary name. A definition specializes a definition of the same kind or of a more general kind (a part definition can specialize an item definition), a usage a usage likewise. An update replaces the current specialization.")]
        public string Specializes { get; init; }

        /// <summary>
        /// Gets the action that the created <c>PerformActionUsage</c> performs.
        /// </summary>
        [Description("Create, for a PerformActionUsage: the action usage that its owner performs, as an identifier (Id) or a temporary name. The owner (a part, a part definition, an action, or a state with stateAction) is required; the name is optional.")]
        public string Performed { get; init; }

        /// <summary>
        /// Gets the direction of the element, which must be a feature.
        /// </summary>
        [Description("Create or update, for a feature such as a port, an attribute of a port definition or a parameter of an action (a ReferenceUsage, an ItemUsage or an AttributeUsage owned by an ActionDefinition or an ActionUsage: in currentStatus : VehicleStatus;): its direction, In, Out or Inout. A port definition has none: give it to what flows through it, an item or attribute that it owns (port def FuelPort { out item fuel : Fuel; }).")]
        [JsonConverter(typeof(JsonStringEnumConverter<FeatureDirectionKind>))]
        public FeatureDirectionKind? Direction { get; init; }

        /// <summary>
        /// Gets a value indicating whether the port is typed by the conjugate of its port definition.
        /// </summary>
        [Description("Create or update, for a port: true types it by the conjugate of its port definition (port cmd : ~CommandPort), whose directions are reversed, for the receiving side of an interface. With a definition, it defaults to false.")]
        public bool? Conjugated { get; init; }

        /// <summary>
        /// Gets a value indicating whether the created feature is an end of its connection or interface definition.
        /// </summary>
        [Description("Create, for a feature of a ConnectionDefinition or a port of an InterfaceDefinition: true makes it one of its ends (end port supplier : PowerPort).")]
        public bool? IsEnd { get; init; }

        /// <summary>
        /// Gets the definition of the event that triggers the created <c>TransitionUsage</c>.
        /// </summary>
        [Description("Create, for a TransitionUsage: the definition of the event that triggers it (accept), for example the ItemDefinition LaunchPhaseCompletedNotification or an AttributeDefinition of a signal, as an identifier (Id) or a temporary name. Without trigger, the transition happens when its source is done.")]
        public string Trigger { get; init; }

        /// <summary>
        /// Gets the condition that must hold for the created <c>TransitionUsage</c> to happen.
        /// </summary>
        [Description("Create, for a TransitionUsage: the condition that must hold for it to happen (if), an attribute compared with a limit or with another attribute.")]
        public GuardPayload Guard { get; init; }

        /// <summary>
        /// Gets the action that the created <c>TransitionUsage</c> performs when it happens.
        /// </summary>
        [Description("Create, for a TransitionUsage: the action usage that it performs when it happens (do), as an identifier (Id) or a temporary name.")]
        public string Effect { get; init; }

        /// <summary>
        /// Gets the kind of the action of a state that the created action is: its entry, do or exit action.
        /// </summary>
        [Description("Create, for an ActionUsage or a PerformActionUsage owned by a StateUsage or a StateDefinition: Entry, Do or Exit makes it the action that the state performs when it is entered, while it is active, or when it is left (entry action initialise;, do action operate : Operate;, or with performed: exit applyBrake;). A state has at most one action of each kind.")]
        [JsonConverter(typeof(JsonStringEnumConverter<StateSubactionKind>))]
        public StateSubactionKind? StateAction { get; init; }

        /// <summary>
        /// Gets the definition of what flows through the created <c>FlowUsage</c>.
        /// </summary>
        [Description("Create, for a FlowUsage: the definition of what flows (flow of Image), for example an ItemDefinition or an AttributeDefinition, as an identifier (Id) or a temporary name.")]
        public string Item { get; init; }
    }
}
