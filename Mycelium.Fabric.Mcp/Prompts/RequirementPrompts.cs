// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementPrompts.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Prompts
{
    using System.ComponentModel;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    /// <summary>
    /// The MCP prompts that guide an AI assistant through the modelling of requirements. A prompt is a message template that
    /// the user chooses in the client: the server calls no language model, it returns the message that the client sends.
    /// </summary>
    [McpServerPromptType]
    public static class RequirementPrompts
    {
        /// <summary>
        /// The procedure that the assistant follows to model the requirements of a long document, one section at a time.
        /// </summary>
        private const string ExtractionProcedure = """
            Model the requirements of a document of the document folder as SysML v2 requirements, with the tools of this server.
            You never write SysML: you describe changes to apply_changes, and the server builds and checks the elements.
            The document can be long: never ask for it whole, read it with read_document one section at a time.
            Work in two phases, and never call apply_changes before the user has approved your plan.

            Phase 1, in your first answer:
            1. Read the model with get_model_overview. If it already has requirements, explore them with list_children and
               follow its way of modelling them: packages and names.
            2. Read the outline with read_document, without section. Read the first section that holds requirements, and write
               a regular expression that matches their identifiers, for example SSS-[A-Z]+-[A-Z0-9]+-[A-Z0-9]+ or
               REQ-[A-Z]+-\d{3}. Read the outline again with this idPattern: for each section, it counts the identifiers found
               and the ones already modelled.
            3. Propose a plan: the idPattern; the sections of the scope that hold requirements, with their counts; the packages,
               one per section under one package for the document, unless the model already organises its requirements in
               another way; and the requirements whose statement sets a numeric limit, which get a constraint.
            4. Ask only the questions whose answer changes the model. For any other gap, make a reasonable choice and state it
               as a question with the answer you chose. End your answer by asking the user to approve or correct the plan, and
               stop there.

            Phase 2, once the user has approved the plan, section by section in the order of the outline:
            5. Read the section with read_document and the idPattern, page by page until nextOffset is null. Its
               requirements.missing lists the identifiers of the section that the model does not have yet.
            6. Send one apply_changes batch for the section, or one per page for a long section: its package if it does not
               exist yet, then one RequirementUsage per missing identifier. Never create a requirement whose identifier is
               already modelled. If a batch is refused, correct every reported problem and send it again.
            7. Report the section in one line: the requirements created, the ones with a constraint, and the identifiers to
               clarify.
            8. Once the scope is done, read the outline again with the idPattern: every identifier found in the scope must be
               modelled. Model the ones you missed. An identifier that the document only mentions, without a statement (a
               reference to a requirement defined elsewhere or that does not exist), is not modelled: list it.
            9. Summarise: the identifiers found, modelled and missing, as read_document counts them, never from memory; the
               requirements with a constraint; the requirements to clarify, each with its reason; and the choices you made.

            Modelling rules:
            - A requirement is a RequirementUsage with its reqId (the identifier, exactly as written), a short lowerCamelCase
              name unique in its package, and its text: the statement copied word for word, never rewritten, split or merged.
              The other columns, such as the priority or the roles, are not modelled.
            - Give a constraint only when the statement sets a numeric limit that the system must meet on a quantity (a mass,
              a power, a duration, a data rate, a number of units): the attribute is that quantity (mass, power, loadTime...),
              with the operator, the limit, its unit, and the margin if the statement gives one. A number that only describes a
              condition of use is not a limit. Never invent a limit: a limit that is TBD or missing gives no constraint.
            - Do not add satisfy links: the parts that satisfy the requirements are chosen later, with the system model.
            - A requirement is to clarify when its statement has a TBD, a vague term without a measurable criterion (responsive,
              user-friendly, fast...), or several requirements in one. Report it, without changing its text.
            """;

        /// <summary>
        /// Gets the message that asks the assistant to model the requirements of a document, one section at a time.
        /// </summary>
        /// <param name="fileName">The simple name of the document in the document folder.</param>
        /// <param name="scope">The sections to model, or <c>null</c> for the whole document.</param>
        /// <returns>The procedure to follow, the document and the scope, then a reminder of the first phase.</returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is <c>null</c>, empty or only white space.
        /// </exception>
        [McpServerPrompt(Name = "extract_requirements", Title = "Extract the requirements of a document")]
        [Description("Models the requirements of a long document of the document folder, one section at a time: a plan that the user approves, then apply_changes per section.")]
        [return: Description("The procedure to follow, the document and the scope, then a reminder of the first phase.")]
        public static string ExtractRequirements(
            [Description("The simple name of the document in the document folder, for example 'EOSat1-requirements.md'.")] string fileName,
            [Description("Optional: the sections to model, for example '5.2.2' or 'Payload requirements'. By default: the whole document.")] string scope = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new McpException("The file name of the document is empty.");
            }

            var scopeText = string.IsNullOrWhiteSpace(scope) ? "the whole document" : $"the sections '{scope.Trim()}' and their subsections";

            // The reminder comes last because a long procedure pushes the first phase far from the end of the message.
            return $"""
                {ExtractionProcedure}

                Document: '{fileName.Trim()}' in the document folder. Scope: {scopeText}.

                Start with phase 1: your first answer ends with the plan and your questions, without calling apply_changes.
                """;
        }
    }
}
