// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChange.cs" company="Starion Group S.A.">
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
    /// One change of a batch sent to the <c>apply_changes</c> tool. Which properties are used depends on its
    /// <see cref="Kind"/>; the others are ignored.
    /// </summary>
    /// <remarks>
    /// The properties that designate an element (<see cref="Owner"/>, <see cref="Element"/>, <see cref="Definition"/> and
    /// <see cref="SatisfyingPart"/>) hold either the <c>Id</c> of an element of the model, or the <see cref="TemporaryName"/> given to an element created
    /// by a previous change of the same batch, whose <c>Id</c> is not known yet.
    /// </remarks>
    public sealed record ModelChange
    {
        /// <summary>
        /// Gets the kind of change. It is required, so that a change without kind is rejected instead of being read as the
        /// first <see cref="ChangeKind"/>.
        /// </summary>
        [Description("The kind of change.")]
        public required ChangeKind Kind { get; init; }

        /// <summary>
        /// Gets the temporary name of the element created by this change, unique in the batch.
        /// </summary>
        [Description("Create only, optional: a temporary name for the new element (for example 'camera'), unique in the batch, that later changes of the same batch use in owner, element or definition.")]
        public string TemporaryName { get; init; }

        /// <summary>
        /// Gets the element that the change modifies.
        /// </summary>
        [Description("Rename, SetValue, SetDefinition and Delete: the element to modify; SetConstraint and Satisfy: the requirement. As an identifier (Id) or a temporary name.")]
        public string Element { get; init; }

        /// <summary>
        /// Gets the element that owns the created element.
        /// </summary>
        [Description("Create: the element that owns the new element, as an identifier (Id) or a temporary name. Optional for CreatePackage only: without owner, the package is created at the top level of the model.")]
        public string Owner { get; init; }

        /// <summary>
        /// Gets the declared name of the created element, or the new name of the renamed element.
        /// </summary>
        [Description("Create: the name of the new element. Rename: the new name. It must differ from the names of the other members of the owner.")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the part definition that types the part.
        /// </summary>
        [Description("CreatePart (optional) and SetDefinition: the part definition that types the part, as an identifier (Id) or a temporary name.")]
        public string Definition { get; init; }

        /// <summary>
        /// Gets the numeric value of the attribute.
        /// </summary>
        [Description("CreateAttribute (optional) and SetValue: the numeric value of the attribute, for example 38 or 1.5.")]
        public double? Value { get; init; }

        /// <summary>
        /// Gets the documentation of the created element, which is the text of a requirement.
        /// </summary>
        [Description("Create, optional: the documentation of the new element, for example the unit of an attribute ('Dry mass of the unit [kg].'). Required for CreateRequirement: the text of the requirement.")]
        public string Text { get; init; }

        /// <summary>
        /// Gets the identifier of the created requirement in its specification, which is its short name.
        /// </summary>
        [Description("CreateRequirement, optional: the identifier of the requirement in the specification, for example 'REQ-SYS-001'. It must differ from the identifiers of the other requirements.")]
        public string ReqId { get; init; }

        /// <summary>
        /// Gets the name of the attribute of the subject that the constraint compares with the limit.
        /// </summary>
        [Description("SetConstraint: the name of the constrained attribute of the subject, for example 'mass' or 'power'. check_requirements computes its value on the satisfying part as sum_attribute does.")]
        public string Attribute { get; init; }

        /// <summary>
        /// Gets the comparison operator of the constraint.
        /// </summary>
        [Description("SetConstraint: how the attribute compares with the limit: '<', '<=', '>', '>=' or '=='.")]
        public string Operator { get; init; }

        /// <summary>
        /// Gets the limit of the constraint.
        /// </summary>
        [Description("SetConstraint: the limit, in the unit of the attribute, for example 150 for 150 kg.")]
        public double? Limit { get; init; }

        /// <summary>
        /// Gets the margin of the constraint, in percent.
        /// </summary>
        [Description("SetConstraint, optional: a margin in percent, for example 20, that always makes the constraint harder to meet. With '<' or '<=', the value plus the margin must stay under the limit (subj.mass * 1.2 <= 150); with '>' or '>=', the value must reach the limit plus the margin (subj.capacity >= 300 * 1.2). Not allowed with '=='.")]
        public double? Margin { get; init; }

        /// <summary>
        /// Gets the part that satisfies the requirement.
        /// </summary>
        [Description("Satisfy: the part that satisfies the requirement, for example the satellite or a subsystem, as an identifier (Id) or a temporary name.")]
        public string SatisfyingPart { get; init; }
    }
}
