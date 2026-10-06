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
            + "Kinds of change: CreatePackage (name; owner package, or none for the top level), CreatePartDefinition (name, owner package), CreatePart (name; owner package, part definition or part; optional definition), "
            + "CreateAttribute (name; owner part definition or part; optional value), CreateRequirement (name, owner package, text; optional reqId), "
            + "SetConstraint (element requirement, attribute, operator, limit; optional margin in percent), Satisfy (element requirement, satisfyingPart; one per part), Rename (element, name), SetValue (element attribute, value), "
            + "SetDefinition (element part, definition), Delete (element, refused while another element references it). "
            + "A created element can get a text (its documentation) and a temporaryName, that later changes of the same batch use instead of an identifier. Existing elements are designated by their identifier (Id).")]
        [return: Description("Whether the batch has been applied; if so, the identifier, name and type of each created element with its temporary name; if not, the problems, each with the number of its change.")]
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
