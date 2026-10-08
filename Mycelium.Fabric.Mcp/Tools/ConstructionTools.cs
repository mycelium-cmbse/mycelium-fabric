// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionTools.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// The MCP tools that let an AI assistant build and modify the SysML v2 model. The assistant describes the changes in a
    /// structured form and never writes SysML: the server builds the elements and checks them.
    /// </summary>
    [McpServerToolType]
    public class ConstructionTools
    {
        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstructionTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> is <c>null</c>.
        /// </exception>
        public ConstructionTools(IModelProvider modelProvider)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);

            this.modelProvider = modelProvider;
        }

        /// <summary>
        /// Applies a batch of changes to the model, all or nothing.
        /// </summary>
        /// <param name="changes">The changes to apply, in order.</param>
        /// <returns>The <see cref="ApplyChangesResult"/> that tells whether the batch has been applied.</returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="changes"/> is <c>null</c> or empty.
        /// </exception>
        [McpServerTool(Name = "apply_changes", ReadOnly = false, Destructive = true)]
        [Description("Applies a batch of changes to the model, in order and all or nothing: if one change is invalid, nothing is applied and every problem is returned, so that the whole batch can be corrected and sent again. "
            + "Each change is shaped like a data version of a commit: an identity and a payload. "
            + "Create: a payload with a type (the SysML v2 metaclass: Package, PartDefinition, PartUsage, AttributeUsage, RequirementUsage...), a name, an owner (none for the top level) "
            + "and, if needed, a definition (for a part), a value (for an attribute), a text (required for a requirement), a reqId and a constraint (for a requirement); "
            + "the identity is then an optional temporary name, that later changes of the same batch use instead of an identifier. "
            + "A SatisfyRequirementUsage (satisfy link) takes a satisfiedRequirement and a satisfyingPart instead of a name; one per part that satisfies the requirement. "
            + "Update: the identity of the element and a payload with only the properties to change (name, definition, value, text, reqId or constraint). "
            + "Delete: the identity of the element and no payload; refused while another element references it. Existing elements are designated by their identifier (Id).")]
        [return: Description("Whether the batch has been applied; if so, the identifier, qualified name and type of each created package, definition and usage; if not, the problems, each with the number of its change.")]
        public ApplyChangesResult ApplyChanges([Description("The changes to apply, in order.")] IReadOnlyList<ModelChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                throw new McpException("The batch contains no change.");
            }

            return this.modelProvider.ApplyChanges(changes);
        }
    }
}
