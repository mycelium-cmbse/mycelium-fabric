// ------------------------------------------------------------------------------------------------
//  <copyright file="GenerationPrompts.cs" company="Starion Group S.A.">
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
    /// The MCP prompts that guide an AI assistant through the generation of a SysML v2 model. A prompt is a message template
    /// that the user chooses in the client: the server calls no language model, it returns the message that the client sends.
    /// </summary>
    [McpServerPromptType]
    public static class GenerationPrompts
    {
        /// <summary>
        /// The procedure that the assistant follows to build a model from a specification with the tools of the server.
        /// </summary>
        private const string Procedure = """
            Build a SysML v2 model from the specification at the end of this message, with the tools of this server.
            You never write SysML: you describe changes to apply_changes, and the server builds and checks the elements.
            Work in two phases, and never call apply_changes before the user has approved your plan.

            Phase 1, in your first answer:
            1. Read the model with get_model_overview. If it already has elements, explore it with list_children and follow
               its way of modelling: package layout, part definitions, names.
            2. Read the specification: the system, its breakdown into subsystems and units, their numeric properties
               (mass, power...) and the requirements.
            3. Propose a plan that follows the modelling rules below: the packages, the part definitions and parts, their
               attributes and values, and for each requirement its identifier, its constraint (attribute, operator, limit,
               margin) and the parts that satisfy it.
            4. Ask only the questions whose answer changes the model. For any other gap, make a reasonable choice and state
               it as a question with the answer you chose. End your answer by asking the user to approve or correct the
               plan, and stop there.

            Phase 2, once the user has approved the plan:
            5. Build with apply_changes, one batch per package or subsystem. Refer to the elements created in the same batch
               by their temporary names. If a batch is refused, correct every reported problem and send it again.
            6. Check with check_requirements. Report every requirement that is not satisfied, and never change a value to
               make it pass without the user's approval.
            7. Summarise: what was built, the choices you made, the status of each requirement, the textual requirements and
               the open questions.

            Modelling rules:
            - One part per unit: four wheels are four parts. Put the numeric values on the units or on their part
              definitions, never on a subsystem or on the system when its units carry the same quantity: sum_attribute
              computes the totals, and a value on a part hides the values of its sub-parts.
            - Use the same attribute name for the same quantity everywhere (mass, power) and state the unit in the text of
              the attribute, for example 'Dry mass [kg].'.
            - Make every requirement verifiable: CreateRequirement with its reqId, SetConstraint on the attribute it limits
              (with the margin the specification states, if any), and one Satisfy per part it applies to.
              check_requirements adds up the attribute over each satisfying part, as sum_attribute does: for a quantity
              that adds up (mass, power), the satisfying part is the one that contains the units; for a quantity that does
              not (an accuracy, a data rate), it is each unit concerned, with one Satisfy per unit.
            - Constrain the numeric part of a requirement even when another part of it, such as a number of units, cannot
              be constrained. Only a requirement without any numeric limit stays textual.
            """;

        /// <summary>
        /// Gets the message that asks the assistant to build a model from a specification.
        /// </summary>
        /// <param name="specification">The textual specification of the system: its description and its requirements.</param>
        /// <returns>
        /// The procedure to follow, the specification between <c>specification</c> tags, then a reminder of the first phase.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="specification"/> is <c>null</c>, empty or only white space.
        /// </exception>
        [McpServerPrompt(Name = "generate_model_from_spec", Title = "Generate a model from a specification")]
        [Description("Builds a SysML v2 model from a textual specification (system description and requirements) with the tools of this server: "
            + "questions to the user, a plan that the user approves, batches of apply_changes, then check_requirements.")]
        public static string GenerateModelFromSpec([Description("The specification: the system, its units with their mass and power, and its requirements.")] string specification)
        {
            if (string.IsNullOrWhiteSpace(specification))
            {
                throw new McpException("The specification is empty.");
            }

            // The tags separate the specification from the procedure: the assistant reads it as data, not as instructions.
            // The reminder comes last because a long specification pushes the procedure far from the end of the message.
            return $"""
                {Procedure}

                <specification>
                {specification.Trim()}
                </specification>

                Start with phase 1: your first answer ends with the plan and your questions, without calling apply_changes.
                """;
        }
    }
}
