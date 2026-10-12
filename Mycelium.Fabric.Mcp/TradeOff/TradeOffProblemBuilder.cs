// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffProblemBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Checks a <see cref="TradeOffRequest"/> against the model and turns it into a <see cref="TradeOffProblem"/>: reads the
    /// current values of the chosen parts as <c>sum_attribute</c> does, computes the part of each total that no choice changes,
    /// and expands each variant into one option per number of units.
    /// </summary>
    public static class TradeOffProblemBuilder
    {
        /// <summary>
        /// The largest number of combinations that a request may ask for.
        /// </summary>
        public const int MaximumCount = 5;

        /// <summary>
        /// The largest number of units of a variant.
        /// </summary>
        public const int MaximumQuantity = 20;

        /// <summary>
        /// The character that separates the choice from the variant in the rules (<c>propulsion.electric</c>).
        /// </summary>
        private const char ReferenceSeparator = '.';

        /// <summary>
        /// Checks the request against the model and builds the problem.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="request">The <see cref="TradeOffRequest"/> to check.</param>
        /// <returns>The <see cref="TradeOffProblem"/> to solve.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> or <paramref name="request"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="McpException">
        /// Thrown when the request is invalid, with a message that tells the AI assistant what to correct.
        /// </exception>
        public static TradeOffProblem Build(IModelProvider modelProvider, TradeOffRequest request)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(request);

            var choices = request.Choices ?? [];
            var objectives = (request.Objectives ?? []).Select(ReadObjective).ToList();
            var limits = (request.Constraints ?? []).Select(ReadLimit).ToList();

            if (choices.Count == 0)
            {
                throw new McpException("Give at least one choice: an existing part (partId) or equipment to add (name), with its variants.");
            }

            if (objectives.Count == 0 && limits.Count == 0)
            {
                throw new McpException("Give at least one objective (for example the lowest mass) or one constraint (for example mass <= 150).");
            }

            if (request.Count is < 1 or > MaximumCount)
            {
                throw new McpException($"The number of combinations must be between 1 and {MaximumCount}.");
            }

            var root = modelProvider.GetRequiredElementById(request.RootId);
            var parts = choices.Select(choice => ReadPart(modelProvider, root, choice)).ToList();
            var names = choices.Select((choice, index) => ReadName(choice, parts[index]?.Part)).ToList();

            CheckDistinct(parts, names);

            var attributes = objectives.Select(objective => objective.Attribute)
                .Concat(limits.Select(limit => limit.Attribute))
                .Concat(limits.Select(limit => limit.LimitAttribute))
                .Where(attribute => attribute != null)
                .Distinct()
                .ToList();

            var unknownAttribute = attributes.Find(attribute => !IsKnown(root, attribute, choices));

            if (unknownAttribute != null)
            {
                throw new McpException($"No part under '{root.DeclaredName}' and no variant has a value for '{unknownAttribute}'. Use get_attribute_values to get the attribute names.");
            }

            var decisions = choices.Select((choice, index) => ReadDecision(choice, names[index], parts[index], attributes)).ToList();
            var fixedTotals = attributes.ToDictionary(attribute => attribute, attribute => GetFixedTotal(root, attribute, parts));
            var dependencies = (request.Rules ?? []).Select((rule, index) => ReadDependency(rule, index, decisions)).ToList();
            int? compareDecisionIndex = null;

            if (!string.IsNullOrWhiteSpace(request.CompareBy))
            {
                var compareIndex = names.IndexOf(request.CompareBy.Trim());

                compareDecisionIndex = compareIndex >= 0
                    ? compareIndex
                    : throw new McpException($"compareBy names '{request.CompareBy}', which is not a choice. Give the name of a choice: {string.Join(", ", names)}.");
            }

            return new TradeOffProblem(decisions, fixedTotals, objectives, limits, dependencies, request.Count, compareDecisionIndex);
        }

        /// <summary>
        /// Checks an objective.
        /// </summary>
        /// <param name="objective">The <see cref="TradeOffObjective"/> to check.</param>
        /// <returns>The objective, with its attribute name trimmed.</returns>
        /// <exception cref="McpException">
        /// Thrown when the objective has no attribute.
        /// </exception>
        private static TradeOffObjective ReadObjective(TradeOffObjective objective)
        {
            if (string.IsNullOrWhiteSpace(objective?.Attribute))
            {
                throw new McpException("Each objective needs an attribute, for example 'mass'.");
            }

            return objective with { Attribute = objective.Attribute.Trim() };
        }

        /// <summary>
        /// Checks a constraint and turns it into a <see cref="TradeOffLimit"/>.
        /// </summary>
        /// <param name="constraint">The <see cref="TradeOffConstraint"/> to check.</param>
        /// <param name="index">The index of the constraint in the request.</param>
        /// <returns>The <see cref="TradeOffLimit"/> of the constraint.</returns>
        /// <exception cref="McpException">
        /// Thrown when the constraint is invalid.
        /// </exception>
        private static TradeOffLimit ReadLimit(TradeOffConstraint constraint, int index)
        {
            var label = $"Constraint {index + 1}";

            if (string.IsNullOrWhiteSpace(constraint?.Attribute))
            {
                throw new McpException($"{label} needs an attribute, for example 'mass'.");
            }

            var comparison = constraint.Operator?.Trim() switch
            {
                "<" => TradeOffOperator.LessThan,
                "<=" => TradeOffOperator.LessThanOrEqual,
                ">" => TradeOffOperator.GreaterThan,
                ">=" => TradeOffOperator.GreaterThanOrEqual,
                _ => throw new McpException($"{label} has the operator '{constraint.Operator}': use '<', '<=', '>' or '>='.")
            };

            var attribute = constraint.Attribute.Trim();
            var limitAttribute = string.IsNullOrWhiteSpace(constraint.LimitAttribute) ? null : constraint.LimitAttribute.Trim();
            var margin = constraint.Margin ?? 0;

            if ((constraint.Limit == null) == (limitAttribute == null))
            {
                throw new McpException($"{label} needs either a limit (a number) or a limitAttribute, not both.");
            }

            if (constraint.Limit is { } limit && !IsAllowedValue(limit))
            {
                throw new McpException($"{label} has a limit that is not a number between -{TradeOffScale.MaximumValue} and {TradeOffScale.MaximumValue}.");
            }

            if (limitAttribute == attribute)
            {
                throw new McpException($"{label} compares '{attribute}' with itself.");
            }

            if (!double.IsFinite(margin) || margin < 0 || margin > 1000)
            {
                throw new McpException($"{label} has a margin of {margin.ToString(CultureInfo.InvariantCulture)}: give a percentage between 0 and 1000.");
            }

            if (margin > 0 && constraint.Limit <= 0)
            {
                throw new McpException($"{label} has a margin with a limit of 0 or less, where a margin would make it easier to meet: write the margin into the limit instead.");
            }

            return new TradeOffLimit(attribute, comparison, constraint.Limit, limitAttribute, margin);
        }

        /// <summary>
        /// Reads the existing part of a choice and checks that its totals are computed under the root.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="choice">The <see cref="TradeOffChoice"/> to read.</param>
        /// <returns>
        /// The part and its owners up to the root, or <c>null</c> when the choice is about equipment to add.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when the choice is <c>null</c>, or when its part is not in the model or not a part under the root.
        /// </exception>
        private static ChosenPart ReadPart(IModelProvider modelProvider, IElement root, TradeOffChoice choice)
        {
            if (choice == null)
            {
                throw new McpException("A choice is empty.");
            }

            if (choice.PartId is not { } partId)
            {
                return null;
            }

            var part = modelProvider.GetRequiredElementById(partId);
            List<IElement> owners = [];
            var current = part;

            while (part is IPartUsage && current.Id != root.Id)
            {
                current = current.owner;

                if (current == null || (current.Id != root.Id && current is not IPartUsage))
                {
                    break;
                }

                owners.Add(current);
            }

            if (part is not IPartUsage || part.Id == root.Id || owners.Count == 0 || owners[^1].Id != root.Id)
            {
                throw new McpException($"'{part.DeclaredName}' is not a part under '{root.DeclaredName}': the totals under '{root.DeclaredName}' do not include it. "
                    + "Use list_children to find the parts, or give the equipment a name instead of a partId to add it.");
            }

            return new ChosenPart(part, owners);
        }

        /// <summary>
        /// Reads the name of a choice: the given name, or the name of its part.
        /// </summary>
        /// <param name="choice">The <see cref="TradeOffChoice"/> to read.</param>
        /// <param name="part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <returns>The name of the choice.</returns>
        /// <exception cref="McpException">
        /// Thrown when equipment to add has no name, or when the name contains the separator of the rules.
        /// </exception>
        private static string ReadName(TradeOffChoice choice, IElement part)
        {
            var name = string.IsNullOrWhiteSpace(choice.Name) ? part?.DeclaredName : choice.Name.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new McpException("Equipment to add needs a name, for example 'fuelTank'.");
            }

            if (name.Contains(ReferenceSeparator))
            {
                throw new McpException($"The choice name '{name}' contains '{ReferenceSeparator}', which separates the choice from the variant in the rules: give another name.");
            }

            return name;
        }

        /// <summary>
        /// Checks that no two choices have the same name or part, and that no chosen part is inside another one.
        /// </summary>
        /// <param name="parts">The chosen part of each choice, <c>null</c> for equipment to add.</param>
        /// <param name="names">The name of each choice.</param>
        /// <exception cref="McpException">
        /// Thrown when two choices overlap.
        /// </exception>
        private static void CheckDistinct(IReadOnlyList<ChosenPart> parts, IReadOnlyList<string> names)
        {
            var duplicateName = names.GroupBy(name => name).FirstOrDefault(group => group.Count() > 1);

            if (duplicateName != null)
            {
                throw new McpException($"Two choices are named '{duplicateName.Key}': give each choice a unique name.");
            }

            var chosenParts = parts.Where(part => part != null).ToList();

            foreach (var inner in chosenParts)
            {
                var outer = chosenParts.Find(candidate => candidate != inner && (candidate.Part.Id == inner.Part.Id || inner.Owners.Any(owner => owner.Id == candidate.Part.Id)));

                if (outer != null)
                {
                    throw new McpException($"The choices on '{inner.Part.DeclaredName}' and '{outer.Part.DeclaredName}' overlap: a part may only be in one choice, "
                        + "and a variant of a part replaces the values of its sub-parts.");
                }
            }
        }

        /// <summary>
        /// Checks the variants of a choice and expands them into options.
        /// </summary>
        /// <param name="choice">The <see cref="TradeOffChoice"/> to read.</param>
        /// <param name="name">The name of the choice.</param>
        /// <param name="part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <param name="attributes">The attributes of the problem.</param>
        /// <returns>The <see cref="TradeOffDecision"/> of the choice.</returns>
        /// <exception cref="McpException">
        /// Thrown when a variant is invalid.
        /// </exception>
        private static TradeOffDecision ReadDecision(TradeOffChoice choice, string name, ChosenPart part, IReadOnlyList<string> attributes)
        {
            var variants = choice.Variants ?? [];

            if (variants.Count == 0)
            {
                throw new McpException($"The choice '{name}' has no variant.");
            }

            foreach (var variant in variants)
            {
                CheckVariant(variant, name, part);
            }

            var duplicateVariant = variants.GroupBy(variant => variant.Name.Trim()).FirstOrDefault(group => group.Count() > 1);

            if (duplicateVariant != null)
            {
                throw new McpException($"The choice '{name}' has two variants named '{duplicateVariant.Key}'.");
            }

            var givenCurrent = variants.FirstOrDefault(variant => variant.Name.Trim() == TradeOffVariant.CurrentName);
            var currentValues = attributes.ToDictionary(attribute => attribute, attribute => GetCurrentValue(part, givenCurrent, attribute, name));

            List<TradeOffOption> options = part == null
                ? [new TradeOffOption(TradeOffVariant.NoneName, 0, true, attributes.ToDictionary(attribute => attribute, _ => 0d))]
                : [new TradeOffOption(TradeOffVariant.CurrentName, 1, true, currentValues)];

            var others = variants.Where(variant => variant.Name.Trim() is not (TradeOffVariant.CurrentName or TradeOffVariant.NoneName));
            options.AddRange(others.SelectMany(variant => ExpandVariant(variant, currentValues, attributes)));

            if (part != null && variants.Any(variant => variant.Name.Trim() == TradeOffVariant.NoneName))
            {
                options.Add(new TradeOffOption(TradeOffVariant.NoneName, 0, false, attributes.ToDictionary(attribute => attribute, _ => 0d)));
            }

            if (part == null && choice.Required && variants.Any(variant => variant.Name.Trim() == TradeOffVariant.NoneName))
            {
                throw new McpException($"The equipment '{name}' is required, so it cannot take the variant '{TradeOffVariant.NoneName}'.");
            }

            return new TradeOffDecision(name, part?.Part.Id, part?.Part.qualifiedName, options, part == null && choice.Required);
        }

        /// <summary>
        /// Checks one variant of a choice.
        /// </summary>
        /// <param name="variant">The <see cref="TradeOffVariant"/> to check.</param>
        /// <param name="choiceName">The name of the choice.</param>
        /// <param name="part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <exception cref="McpException">
        /// Thrown when the variant is invalid.
        /// </exception>
        private static void CheckVariant(TradeOffVariant variant, string choiceName, ChosenPart part)
        {
            if (string.IsNullOrWhiteSpace(variant?.Name))
            {
                throw new McpException($"A variant of '{choiceName}' has no name.");
            }

            var label = $"The variant '{choiceName}{ReferenceSeparator}{variant.Name.Trim()}'";
            var values = variant.Values ?? new Dictionary<string, double>();

            if (values.Any(value => string.IsNullOrWhiteSpace(value.Key) || !IsAllowedValue(value.Value)))
            {
                throw new McpException($"{label} has a value without attribute name, or a value that is not a number between -{TradeOffScale.MaximumValue} and {TradeOffScale.MaximumValue}.");
            }

            if (values.Keys.Select(attribute => attribute.Trim()).Distinct().Count() != values.Count)
            {
                throw new McpException($"{label} gives two values for the same attribute.");
            }

            if (variant.MinQuantity < 1 || variant.MinQuantity > variant.MaxQuantity || variant.MaxQuantity > MaximumQuantity)
            {
                throw new McpException($"{label} has the quantities {variant.MinQuantity} to {variant.MaxQuantity}: give 1 <= minQuantity <= maxQuantity <= {MaximumQuantity}.");
            }

            switch (variant.Name.Trim())
            {
                case TradeOffVariant.NoneName when values.Count > 0 || variant.MaxQuantity > 1:
                    throw new McpException($"{label} removes the part or does not add the equipment, so all its values are 0: give it no values and no quantity.");

                case TradeOffVariant.CurrentName when part == null:
                    throw new McpException($"{label} does not exist: equipment to add starts from '{TradeOffVariant.NoneName}' (not added).");

                case TradeOffVariant.CurrentName when variant.MaxQuantity > 1:
                    throw new McpException($"{label} is the part as it is in the model: give it no quantity.");

                case TradeOffVariant.CurrentName:
                    var known = values.Keys.FirstOrDefault(attribute => part.Part.CollectContributions(attribute.Trim()).Count > 0);

                    if (known != null)
                    {
                        throw new McpException($"{label} gives a value for '{known}', which the model already gives: give only the values that the model lacks.");
                    }

                    break;
            }
        }

        /// <summary>
        /// Gets the current value of an attribute for one unit of a choice: the value in the model of an existing part (its own
        /// value, or the sum over its sub-parts), completed by the given <c>current</c> variant, and 0 for equipment to add.
        /// </summary>
        /// <param name="part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <param name="givenCurrent">The <c>current</c> variant given by the AI assistant, or <c>null</c>.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="choiceName">The name of the choice.</param>
        /// <returns>The current value of the attribute.</returns>
        /// <exception cref="McpException">
        /// Thrown when an owner of the part gives its own value for the attribute, so that the value of the part is not in the
        /// total.
        /// </exception>
        private static double GetCurrentValue(ChosenPart part, TradeOffVariant givenCurrent, string attribute, string choiceName)
        {
            if (part == null)
            {
                return 0;
            }

            var hidingOwner = part.Owners.FirstOrDefault(owner => HasOwnValue(owner, attribute));

            if (hidingOwner != null)
            {
                throw new McpException($"The choice '{choiceName}' cannot change '{attribute}': '{hidingOwner.DeclaredName}' gives its own '{attribute}', "
                    + "which replaces the values of its parts in the total. Make the choice on it instead.");
            }

            var givenValue = givenCurrent?.Values?.FirstOrDefault(value => value.Key.Trim() == attribute);

            return givenValue is { Key: not null } ? givenValue.Value.Value : part.Part.CollectContributions(attribute).Sum(contribution => contribution.Value);
        }

        /// <summary>
        /// Expands a variant into one option per number of units.
        /// </summary>
        /// <param name="variant">The <see cref="TradeOffVariant"/> to expand.</param>
        /// <param name="currentValues">The current value of each attribute for one unit, used when the variant does not give it.</param>
        /// <param name="attributes">The attributes of the problem.</param>
        /// <returns>The <see cref="TradeOffOption"/>s of the variant.</returns>
        private static IEnumerable<TradeOffOption> ExpandVariant(TradeOffVariant variant, Dictionary<string, double> currentValues, IReadOnlyList<string> attributes)
        {
            var values = (variant.Values ?? new Dictionary<string, double>()).ToDictionary(value => value.Key.Trim(), value => value.Value);

            return Enumerable.Range(variant.MinQuantity, variant.MaxQuantity - variant.MinQuantity + 1)
                .Select(quantity => new TradeOffOption(variant.Name.Trim(), quantity, false,
                    attributes.ToDictionary(attribute => attribute, attribute => values.GetValueOrDefault(attribute, currentValues[attribute]) * quantity)));
        }

        /// <summary>
        /// Computes the part of the total of an attribute that no choice changes: the total under the root, as
        /// <c>sum_attribute</c> computes it, minus the current value of each chosen part.
        /// </summary>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="parts">The chosen part of each choice, <c>null</c> for equipment to add.</param>
        /// <returns>The fixed part of the total.</returns>
        private static double GetFixedTotal(IElement root, string attribute, IReadOnlyList<ChosenPart> parts)
        {
            var total = root.CollectContributions(attribute).Sum(contribution => contribution.Value);
            var chosenTotal = parts.Where(part => part != null).SelectMany(part => part.Part.CollectContributions(attribute)).Sum(contribution => contribution.Value);

            return total - chosenTotal;
        }

        /// <summary>
        /// Tells whether an element gives its own numeric value for an attribute, owned or inherited from its types, so that
        /// the values of its sub-parts are not in the totals.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to read.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <returns><c>true</c> when the element has its own value.</returns>
        private static bool HasOwnValue(IElement element, string attribute)
        {
            return element.GetAttributeUsages().FirstOrDefault(attributeUsage => attributeUsage.DeclaredName == attribute)?.GetNumericValue() != null;
        }

        /// <summary>
        /// Tells whether an attribute has a value somewhere: in a part under the root, or in a variant.
        /// </summary>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="choices">The <see cref="TradeOffChoice"/>s of the request.</param>
        /// <returns><c>true</c> when the attribute has a value somewhere.</returns>
        private static bool IsKnown(IElement root, string attribute, IReadOnlyList<TradeOffChoice> choices)
        {
            return root.CollectContributions(attribute).Count > 0
                || choices.SelectMany(choice => choice.Variants ?? []).Any(variant => (variant?.Values?.Keys ?? []).Any(key => key?.Trim() == attribute));
        }

        /// <summary>
        /// Checks a rule and resolves its variants.
        /// </summary>
        /// <param name="rule">The <see cref="TradeOffRule"/> to check.</param>
        /// <param name="index">The index of the rule in the request.</param>
        /// <param name="decisions">The <see cref="TradeOffDecision"/>s of the problem.</param>
        /// <returns>The <see cref="TradeOffDependency"/> of the rule.</returns>
        /// <exception cref="McpException">
        /// Thrown when the rule is invalid.
        /// </exception>
        private static TradeOffDependency ReadDependency(TradeOffRule rule, int index, List<TradeOffDecision> decisions)
        {
            var label = $"Rule {index + 1}";

            if (rule == null || (rule.Then ?? []).Count == 0)
            {
                throw new McpException($"{label} needs 'if' and at least one variant in 'then'.");
            }

            var ifReference = ReadReference(rule.If, label, decisions);
            var thenReferences = rule.Then.Select(reference => ReadReference(reference, label, decisions)).ToList();

            if (thenReferences.Exists(reference => reference.DecisionIndex == ifReference.DecisionIndex))
            {
                throw new McpException($"{label} links variants of the same choice '{decisions[ifReference.DecisionIndex].Name}': a choice already takes exactly one variant.");
            }

            var thenText = string.Join(rule.Kind == TradeOffRuleKind.Requires ? " or " : " and ", thenReferences.Select(reference => Describe(reference, decisions)));
            var description = $"{Describe(ifReference, decisions)} {(rule.Kind == TradeOffRuleKind.Requires ? "requires" : "excludes")} {thenText}";

            return new TradeOffDependency(rule.Kind, ifReference, thenReferences, description);
        }

        /// <summary>
        /// Resolves a variant written <c>choice.variant</c>.
        /// </summary>
        /// <param name="reference">The text of the reference.</param>
        /// <param name="label">The label of the rule, for the messages.</param>
        /// <param name="decisions">The <see cref="TradeOffDecision"/>s of the problem.</param>
        /// <returns>The <see cref="TradeOffVariantReference"/>.</returns>
        /// <exception cref="McpException">
        /// Thrown when the reference does not designate a variant of a choice.
        /// </exception>
        private static TradeOffVariantReference ReadReference(string reference, string label, List<TradeOffDecision> decisions)
        {
            var separatorIndex = reference?.IndexOf(ReferenceSeparator) ?? -1;
            var decisionName = separatorIndex > 0 ? reference[..separatorIndex].Trim() : null;
            var variant = separatorIndex > 0 ? reference[(separatorIndex + 1)..].Trim() : null;

            var decisionIndex = decisions.Select((decision, decisionIndex) => (decision, decisionIndex))
                .Where(pair => pair.decision.Name == decisionName && pair.decision.GetOptionIndexes(variant).Count > 0)
                .Select(pair => pair.decisionIndex)
                .DefaultIfEmpty(-1)
                .First();

            if (decisionIndex < 0)
            {
                throw new McpException($"{label} refers to '{reference}', which is not a variant of a choice. Write choice.variant, for example '{Describe(new TradeOffVariantReference(0, decisions[0].Options[^1].Variant), decisions)}'.");
            }

            return new TradeOffVariantReference(decisionIndex, variant);
        }

        /// <summary>
        /// Writes a variant as <c>choice.variant</c>.
        /// </summary>
        /// <param name="reference">The <see cref="TradeOffVariantReference"/> to write.</param>
        /// <param name="decisions">The <see cref="TradeOffDecision"/>s of the problem.</param>
        /// <returns>The text of the variant.</returns>
        private static string Describe(TradeOffVariantReference reference, List<TradeOffDecision> decisions)
        {
            return $"{decisions[reference.DecisionIndex].Name}{ReferenceSeparator}{reference.Variant}";
        }

        /// <summary>
        /// Tells whether a value may be used in a problem without overflowing the computations of the solver.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns><c>true</c> when the value is a finite number of at most <see cref="TradeOffScale.MaximumValue"/>.</returns>
        private static bool IsAllowedValue(double value)
        {
            return double.IsFinite(value) && Math.Abs(value) <= TradeOffScale.MaximumValue;
        }

        /// <summary>
        /// An existing part chosen by a <see cref="TradeOffChoice"/>, with its owners up to the root.
        /// </summary>
        /// <param name="Part">The chosen part.</param>
        /// <param name="Owners">The owners of the part, from its direct owner to the root.</param>
        private sealed record ChosenPart(IElement Part, IReadOnlyList<IElement> Owners);
    }
}
