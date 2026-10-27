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
            The specification is in this message: do not look for it with read_document.
            You never write SysML: you describe changes to apply_changes, and the server builds and checks the elements.
            Work in two phases, and never call apply_changes before the user has approved your plan.

            Phase 1, in your first answer:
            1. Read the model with get_model_overview. If it already has elements, explore it with list_children and follow
               its way of modelling: package layout, part definitions, names.
            2. Read the whole specification, every section, not only the technical system. Each element that it names is
               modelled, with the name it has in the specification: the purpose (program, mission, goals, stakeholders and
               their needs, capabilities), the context (environments, facilities, external systems), the behaviour (phases,
               operations, functions), the logical components, the technical system (breakdown into subsystems and units,
               every numeric property, ports), the analyses and calculations, and every requirement.
            3. Propose a plan that follows the modelling rules below: one package per part of the specification, with the
               kinds of elements it holds and how many, the attributes and values of the technical system, and for each
               requirement with a limit its constraint (attribute, operator, limit, margin) and the parts that satisfy it.
               Summarise long lists by their count instead of naming every element.
            4. Ask only the questions whose answer changes the model. For any other gap, make a reasonable choice and state
               it as a question with the answer you chose. End your answer by asking the user to approve or correct the
               plan, and stop there.

            Phase 2, once the user has approved the plan:
            5. Build with apply_changes, one batch per package or subsystem, until every section of the specification is
               built. Give each new element a temporary name, unique in the whole model, and refer to it by this name
               in the same batch and in the next ones (or by its identifier from createdElements). If a batch is refused,
               correct every reported problem and send it again. After each applied batch, read newIssues: correct its
               errors in the next batch; a warning can wait when a later batch of your plan resolves it. Send one batch
               at a time, never several apply_changes calls at once, and wait for its result before the next batch.
               Phase 2 is one job: do not stop to report a partial result or to ask the user, and do not give up on a
               refused batch. Fix it and go on until every section of the specification is built.
            6. Check with validate_model (severity Warning), then with check_requirements. Correct every error, and every
               warning that a missing element explains, then check again, until no error is left or for 3 rounds at most.
               A requirement that is not satisfied is not an error: report it, and never change a value of the
               specification to make it pass.
            7. Go through the sections of the specification once more and add what is missing.
            8. Summarise: what was built per section, the choices you made, the status of each requirement, the textual
               requirements, the relations written in documentation only, and the open questions.

            Modelling the specification (the metaclass to give in the payload type):
            - Program, mission, stakeholders, external systems, environments and facilities: a PartDefinition for each kind,
              and a PartUsage for each one that the specification names, typed by its kind; the context that contains them
              is a part too. The concerns of a stakeholder go in its documentation.
            - A named realisation of the system, such as a serial number or a person, is a PartUsage with that name
              (SA-506, Neil Armstrong), in the part it realises or in the context.
            - Goals and stakeholder needs: a RequirementUsage each, with its text and, when the specification gives one, its
              identifier as reqId (SHN-N001). Capabilities: a PartDefinition each, with its description.
            - Kinds: when the specification names the kinds of an element (kinds of stakeholders, of vehicles), each kind is
              a PartDefinition with specializes: the general one (GovernmentAgency specializes Stakeholder).
            - Phases: a StateUsage per phase, in the order of the specification, inside a StateDefinition of the mission.
              Operations: an ActionUsage inside the StateUsage of each phase that performs it. When the operations of a
              phase come in sequence, a SuccessionAsUsage owned by the StateUsage links each operation to the next one
              (ends: ['loadConsumablesAndPropellants', 'transferCrewToVehicle']). Functions: an ActionDefinition for the
              top-level function, and an ActionUsage per function, inside the function that it decomposes; sub-functions
              that the specification orders are linked by successions too.
            - Logical components: a PartDefinition each, and a PartUsage in a logical architecture part. Each function that
              a component performs is a PerformActionUsage owned by its PartDefinition, with performed: the ActionUsage of
              the function.
            - Analyses: an AnalysisCaseDefinition each; calculations: a CalculationDefinition each; with their description.
            - A requirement satisfied by an operation, a function or an interface: a SatisfyRequirementUsage whose
              satisfyingPart is the ActionUsage or the InterfaceUsage.
            - A relation that apply_changes cannot create yet, such as a refinement or a transition between phases, goes in
              the documentation of its source element ('Refines: HLR-R001.'), so that it is not lost.

            Modelling rules:
            - Two members of a package cannot have the same name: name a usage differently from its definition
              (part definition Camera, part usage camera), and a package differently from what it contains.
            - One part per unit: four wheels are four parts. Put the numeric values on the units or on their part
              definitions, never on a subsystem or on the system when its units carry the same quantity: sum_attribute
              computes the totals, and a value on a part hides the values of its sub-parts.
            - Every numeric property that the specification gives to a unit or to a vehicle becomes an attribute with its
              value, whatever the quantity (a pressure, a duration, a payload capacity), not only mass and power.
            - Use the same attribute name for the same quantity everywhere (mass, power), and give every numeric value the
              unit of the specification (value 38 with unit kg): the budgets and the checks convert the units.
            - Make every requirement verifiable: a RequirementUsage with its text, its reqId and a constraint on the
              attribute it limits (with the margin the specification states, if any), and one SatisfyRequirementUsage per
              part, action or interface it applies to. check_requirements adds up the attribute over each satisfying part, as sum_attribute
              does: for a quantity that adds up (mass, power), the satisfying part is the one that contains the units; for
              a quantity that does not (an accuracy, a data rate), it is each unit concerned, with one
              SatisfyRequirementUsage per unit.
            - Every requirement of the specification is a RequirementUsage with its reqId, including the stakeholder needs
              and the requirements that stay textual.
            - Constrain a requirement whenever its text gives a limit: a number with its unit, a count (no injury:
              injuryCount <= 0), or an outcome that is achieved or avoided, as a Boolean (flagPlanted == true,
              contaminationReleased == false). Take the operator from the wording: 'at least' is '>=', 'at most' and 'not
              exceed' are '<=', 'less than' and 'below' are '<'. When the text gives several limits, constrain the first
              one and name the others in the summary. Only a requirement without any limit stays textual.
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
        [return: Description("The message to send to the assistant: the procedure, the specification between specification tags, then a reminder of the first phase.")]
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
