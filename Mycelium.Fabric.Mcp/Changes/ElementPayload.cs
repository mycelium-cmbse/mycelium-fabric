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
    using System.ComponentModel;

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
        /// Gets the numeric value of the element, which must be an attribute.
        /// </summary>
        [Description("Create or update, for an attribute: its numeric value, for example 38 or 1.5. An update replaces the current value.")]
        public double? Value { get; init; }

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
        [Description("Create or update, for a RequirementUsage: the constraint that makes it verifiable, on an attribute of its subject. It replaces the current required constraint.")]
        public ConstraintPayload Constraint { get; init; }

        /// <summary>
        /// Gets the requirement that the created <c>SatisfyRequirementUsage</c> satisfies.
        /// </summary>
        [Description("Create, for a SatisfyRequirementUsage: the satisfied requirement, as an identifier (Id) or a temporary name. Without owner, the satisfy link is created next to the requirement.")]
        public string SatisfiedRequirement { get; init; }

        /// <summary>
        /// Gets the part that satisfies the requirement of the created <c>SatisfyRequirementUsage</c>.
        /// </summary>
        [Description("Create, for a SatisfyRequirementUsage: the part that satisfies the requirement, for example the satellite or a subsystem, as an identifier (Id) or a temporary name.")]
        public string SatisfyingPart { get; init; }
    }
}
