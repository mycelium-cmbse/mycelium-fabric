// ------------------------------------------------------------------------------------------------
//  <copyright file="ProposedRequirement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using System.ComponentModel;

    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// A requirement that the AI assistant has found in a section of a document, as it gives it to the
    /// <c>propose_requirements</c> tool. Nothing is modelled until the user imports it.
    /// </summary>
    public sealed record ProposedRequirement
    {
        /// <summary>
        /// Gets the identifier of the requirement in the document.
        /// </summary>
        [Description("The identifier of the requirement, exactly as written in the document, for example REQ-PAY-001.")]
        public string ReqId { get; init; }

        /// <summary>
        /// Gets the name of the requirement in the model.
        /// </summary>
        [Description("A short lowerCamelCase name for the requirement usage, unique in its package, for example cameraMass.")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the statement of the requirement, as written in the document.
        /// </summary>
        [Description("The statement, copied word for word from the section: never rewritten, split or merged.")]
        public string Text { get; init; }

        /// <summary>
        /// Gets what the requirement is about, shown to the user and not modelled, or <c>null</c> when unknown.
        /// </summary>
        [Description("Optional: what the requirement is about, shown to the user and not modelled: Functional, Performance, Interface, Design, Operational or Other.")]
        public RequirementKind? Kind { get; init; }

        /// <summary>
        /// Gets the constraint of the requirement, or <c>null</c> when its statement sets no numeric limit.
        /// </summary>
        [Description("Optional: the constraint, only when the statement sets a numeric limit that the system must meet, as in apply_changes (attribute, operator, limit, unit, margin). Never invent a limit: a TBD or missing limit gives no constraint.")]
        public ConstraintPayload Constraint { get; init; }

        /// <summary>
        /// Gets the reason why the requirement must be clarified, or <c>null</c> when it is clear.
        /// </summary>
        [Description("Optional: why the requirement must be clarified, for example 'TBD target' or 'vague term: responsive'. Leave it out when the requirement is clear.")]
        public string Clarification { get; init; }

        /// <summary>
        /// Gets a statement rewritten so that the requirement becomes clear, or <c>null</c>.
        /// </summary>
        [Description("Optional, with a clarification: a rewritten statement that the document allows, for example from the next sentence. The user chooses whether it is imported.")]
        public string RewrittenText { get; init; }

        /// <summary>
        /// Gets the constraint of the rewritten statement, or <c>null</c>.
        /// </summary>
        [Description("Optional, with a rewritten statement: its constraint, as in apply_changes.")]
        public ConstraintPayload RewrittenConstraint { get; init; }

        /// <summary>
        /// Gets the identifier of the requirement of the list that already says the same as this one, or <c>null</c>.
        /// </summary>
        [Description("Optional: the reqId of another requirement of this list when this one only repeats it. The user chooses whether it is merged, and then it is not created.")]
        public string MergeInto { get; init; }
    }
}
