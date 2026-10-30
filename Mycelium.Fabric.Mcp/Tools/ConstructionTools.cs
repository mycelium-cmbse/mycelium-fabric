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
    using System.Linq;

    using Microsoft.Extensions.Options;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Validation;

    /// <summary>
    /// The MCP tools that let an AI assistant build and modify the SysML v2 model. The assistant describes the changes in a
    /// structured form and never writes SysML: the server builds the elements and checks them.
    /// </summary>
    [McpServerToolType]
    public class ConstructionTools
    {
        /// <summary>
        /// The maximum number of new issues returned with an applied batch, the same as a page of validate_model.
        /// </summary>
        private const int MaximumIssueCount = 20;

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="IModelValidator"/> that finds the issues of the model before and after a batch.
        /// </summary>
        private readonly IModelValidator modelValidator;

        /// <summary>
        /// The <see cref="ConstructionOptions"/>, which tell whether the new issues are returned.
        /// </summary>
        private readonly ConstructionOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstructionTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="modelValidator">The <see cref="IModelValidator"/> that finds the issues of the model.</param>
        /// <param name="options">The <see cref="ConstructionOptions"/> read from the configuration of the host.</param>
        /// <exception cref="ArgumentNullException">Thrown when a parameter is <c>null</c>.</exception>
        public ConstructionTools(IModelProvider modelProvider, IModelValidator modelValidator, IOptions<ConstructionOptions> options)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(modelValidator);
            ArgumentNullException.ThrowIfNull(options);

            this.modelProvider = modelProvider;
            this.modelValidator = modelValidator;
            this.options = options.Value;
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
            + "and, if needed, a definition (for a part), a value and a unit (for an attribute: 38 with kg, true, 'S-band'), a text (required for a requirement), a reqId and a constraint (for a requirement: "
            + "an attribute or path of the subject, an operator, a limit with an optional unit or a limitAttribute, a margin, and kind Assumption for an assumption); "
            + "the identity is then an optional temporary name, that later changes of this batch and of the next batches use instead of an identifier. "
            + "A SatisfyRequirementUsage (satisfy link) takes a satisfiedRequirement and a satisfyingPart (a part, an action or an interface) instead of a name; one per element that satisfies the requirement. "
            + "A definition or a usage takes specializes for :> (part def Camera :> Equipment). A PerformActionUsage takes an owner (the performer) and a performed action usage. "
            + "Ports: a PortDefinition owns the attributes or items that cross the port, with a direction (In, Out, Inout); a PortUsage takes it as definition, with conjugated true on the receiving side. "
            + "Connectors: a ConnectionUsage, an InterfaceUsage (between two ports, definition: an InterfaceDefinition whose ends are ports created with isEnd true), a BindingConnectorAsUsage, "
            + "an AllocationUsage (allocate the first end to the second) or a SuccessionAsUsage (first the first end, then the second, between actions) "
            + "takes two ends, paths of names from its owner (['payloadSubsystem.camera.dataOut', 'dataHandlingSubsystem.obc.dataIn']), and an optional name. "
            + "Update: the identity of the element and a payload with only the properties to change (name, definition, specializes, value, text, direction, conjugated, reqId or constraint). "
            + "Delete: the identity of the element and no payload; refused while another element references it, for example a port connected by an interface. Existing elements are designated by their identifier (Id). "
            + "Once a batch is applied, the result also lists the errors and warnings that it has introduced (newIssues, at most 20, and newIssueCount) and the number of issues it has resolved: "
            + "correct the errors in the next batch; a warning can wait when a later batch of your plan resolves it.")]
        [return: Description("Whether the batch has been applied; if so, the identifier, qualified name and type of each created package, definition and usage, the errors and warnings that the batch has introduced "
            + "and the number of issues it has resolved; if not, the problems, each with the number of its change.")]
        public ApplyChangesResult ApplyChanges([Description("The changes to apply, in order.")] IReadOnlyList<ModelChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                throw new McpException("The batch contains no change.");
            }

            if (!this.options.ReportNewIssues)
            {
                return this.modelProvider.ApplyChanges(changes);
            }

            var issuesBefore = this.FindIssues();
            var result = this.modelProvider.ApplyChanges(changes);

            if (!result.Applied)
            {
                return result;
            }

            var issuesAfter = this.FindIssues();
            var keysBefore = issuesBefore.Select(issue => (issue.Rule, issue.ElementId)).ToHashSet();
            var keysAfter = issuesAfter.Select(issue => (issue.Rule, issue.ElementId)).ToHashSet();

            var newIssues = issuesAfter.Where(issue => !keysBefore.Contains((issue.Rule, issue.ElementId))).ToList();
            var resolvedIssueCount = issuesBefore.Count(issue => !keysAfter.Contains((issue.Rule, issue.ElementId)));

            return result with { NewIssues = [.. newIssues.Take(MaximumIssueCount)], NewIssueCount = newIssues.Count, ResolvedIssueCount = resolvedIssueCount };
        }

        /// <summary>
        /// Finds the errors and warnings of the whole model, the information being left out: a definition is often unused
        /// until a later batch creates its usages.
        /// </summary>
        /// <returns>The issues found, the most serious first.</returns>
        private List<ValidationIssue> FindIssues()
        {
            return this.modelValidator.Validate(this.modelProvider.Elements)
                .Where(issue => issue.Severity <= ValidationSeverity.Warning)
                .ToList();
        }
    }
}
