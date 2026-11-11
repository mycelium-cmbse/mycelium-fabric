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

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Checks a <see cref="TradeOffRequest"/> against the model and turns it into a <see cref="TradeOffProblem"/>: reads the
    /// current values of the chosen parts as <c>sum_attribute</c> does, computes the part of each total that no choice changes,
    /// and expands each variant into one option per number of units. It also reads from the model what the request does not
    /// say: the parts typed by a catalogue of variants, which are choices too, and the requirements with a value to check,
    /// which every combination is checked against.
    /// </summary>
    public static partial class TradeOffProblemBuilder
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

            var givenChoices = request.Choices ?? [];
            var choices = givenChoices.ToList();
            var objectives = (request.Objectives ?? []).Select(ReadObjective).ToList();
            var limits = (request.Constraints ?? []).Select(ReadLimit).ToList();

            var root = modelProvider.GetRequiredElementById(request.RootId);
            var parts = choices.Select(choice => ReadPart(modelProvider, root, choice)).ToList();
            var names = choices.Select((choice, index) => ReadName(choice, parts[index]?.Part)).ToList();

            CheckDistinct(parts, names);

            // The parts typed by a catalogue of variants are choices too, without the request saying so.
            foreach (var cataloguePart in FindCatalogueParts(modelProvider, root, parts))
            {
                choices.Add(null);
                parts.Add(cataloguePart);
                names.Add(GetFreeName(cataloguePart, names));
            }

            if (parts.Count == 0)
            {
                throw new McpException("Give at least one choice: an existing part (partId) or equipment to add (name), with its variants.");
            }

            var requirements = ReadRequirements(modelProvider, request.IgnoredRequirements ?? []);

            if (objectives.Count == 0 && limits.Count == 0 && requirements.TrueForAll(requirement => requirement.IsIgnored))
            {
                throw new McpException("Give at least one objective (for example the lowest mass) or one constraint (for example mass <= 150).");
            }

            if (request.Count is < 1 or > MaximumCount)
            {
                throw new McpException($"The number of combinations must be between 1 and {MaximumCount}.");
            }

            var attributes = objectives.Select(objective => objective.Attribute)
                .Concat(limits.Select(limit => limit.Attribute))
                .Concat(limits.Select(limit => limit.LimitAttribute))
                .Where(attribute => attribute != null)
                .Distinct()
                .ToList();

            var unknownAttribute = attributes.Find(attribute => !IsKnown(root, attribute, choices, parts));

            if (unknownAttribute != null)
            {
                throw new McpException($"No part under '{root.DeclaredName}' and no variant has a value for '{unknownAttribute}'. Use get_attribute_values to get the attribute names.");
            }

            var libraryUnits = LibraryUnits.Read(modelProvider.Elements);
            var units = ReadUnits(root, attributes, givenChoices, request.Constraints ?? [], limits, libraryUnits);
            limits = limits.Select((limit, index) => ConvertLimit(limit, request.Constraints[index], index, units, libraryUnits)).ToList();

            var drafts = parts.Select((part, index) => ReadDecision(choices[index], names[index], part, attributes, units, libraryUnits)).ToList();
            var space = new TradeOffDesignSpace(root.Id, [.. drafts.Select(draft => new TradeOffOpenPart(draft.Part?.Part as IPartUsage, draft.Name, [.. draft.Options.Select(option => option.Content)]))]);
            var totals = new TotalReader(space, drafts, units);

            foreach (var attribute in attributes)
            {
                totals.ReadTotal(attribute, space.GetTotalParts(root, attribute).Value, contributions => SumContributions(contributions, units[attribute], attribute));
            }

            requirements = ReadRequirementLimits(requirements, totals, limits);

            var decisions = drafts.Select((draft, decisionIndex) => new TradeOffDecision(draft.Name, draft.Part?.Part.Id, draft.Part?.Part.qualifiedName,
                [.. draft.Options.Select((option, optionIndex) => new TradeOffOption(option.Variant, option.Quantity, option.IsCurrent, totals.OptionValues[decisionIndex][optionIndex]))],
                draft.Required)).ToList();

            var dependencies = (request.Rules ?? []).Select((rule, index) => ReadDependency(rule, index, decisions)).ToList();
            var excluded = ReadExcluded(request.Excluded ?? [], decisions);

            int? compareDecisionIndex = null;

            if (!string.IsNullOrWhiteSpace(request.CompareBy))
            {
                var compareIndex = names.IndexOf(request.CompareBy.Trim());

                compareDecisionIndex = compareIndex >= 0
                    ? compareIndex
                    : throw new McpException($"compareBy names '{request.CompareBy}', which is not a choice. Give the name of a choice: {string.Join(", ", names)}.");
            }

            return new TradeOffProblem(decisions, totals.FixedTotals, objectives, limits, dependencies, request.Count, compareDecisionIndex, units, excluded)
            {
                Requirements = requirements,
                Space = space
            };
        }

        /// <summary>
        /// Resolves the variants turned off by the user, and checks that each choice keeps at least one variant.
        /// </summary>
        /// <param name="references">The turned-off variants, written <c>choice.variant</c>.</param>
        /// <param name="decisions">The <see cref="TradeOffDecision"/>s of the problem.</param>
        /// <returns>The <see cref="TradeOffVariantReference"/> of each turned-off variant, without duplicates.</returns>
        /// <exception cref="McpException">
        /// Thrown when a reference does not designate a variant of a choice, or when every variant of a choice is turned off.
        /// </exception>
        private static List<TradeOffVariantReference> ReadExcluded(IReadOnlyList<string> references, List<TradeOffDecision> decisions)
        {
            var excluded = references.Select(reference => ReadReference(reference, "excluded", decisions)).Distinct().ToList();

            var emptyDecision = decisions
                .Where((decision, decisionIndex) => decision.Options.All(option => (decision.Required && option.IsCurrent) || excluded.Contains(new TradeOffVariantReference(decisionIndex, option.Variant))))
                .FirstOrDefault();

            return emptyDecision == null
                ? excluded
                : throw new McpException($"Every variant of '{emptyDecision.Name}' is turned off: keep at least one.");
        }

        /// <summary>
        /// Finds the unit in which each attribute is expressed: the unit of the first part under the root that has one, as
        /// <c>sum_attribute</c> does, otherwise the unit given by a variant or a constraint. A limit attribute is expressed in
        /// the unit of the attribute that it limits, so that both totals compare in the same unit.
        /// </summary>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="attributes">The names of the attributes of the problem.</param>
        /// <param name="choices">The <see cref="TradeOffChoice"/>s of the request.</param>
        /// <param name="constraints">The <see cref="TradeOffConstraint"/>s of the request.</param>
        /// <param name="limits">The checked <see cref="TradeOffLimit"/>s, in the order of the constraints.</param>
        /// <param name="libraryUnits">The units that the libraries of the model declare.</param>
        /// <returns>The unit of each attribute by name, <c>null</c> for an attribute without unit.</returns>
        /// <exception cref="McpException">
        /// Thrown when a unit is unknown or has an offset, or when two attributes that are compared have units of different
        /// dimensions or that cannot be put in the same unit.
        /// </exception>
        private static Dictionary<string, Unit> ReadUnits(IElement root, IReadOnlyList<string> attributes, IReadOnlyList<TradeOffChoice> choices,
            IReadOnlyList<TradeOffConstraint> constraints, IReadOnlyList<TradeOffLimit> limits, LibraryUnits libraryUnits)
        {
            var units = attributes.ToDictionary(attribute => attribute, attribute => FindUnit(root, attribute, choices, constraints, libraryUnits));

            foreach (var limit in limits.Where(limit => limit.LimitAttribute != null))
            {
                var unit = units[limit.Attribute];
                var limitUnit = units[limit.LimitAttribute];

                if (unit == null || limitUnit == null)
                {
                    continue;
                }

                if (!unit.IsCompatibleWith(limitUnit))
                {
                    throw new McpException($"'{limit.Attribute}' is in {unit.Symbol} and '{limit.LimitAttribute}' is in {limitUnit.Symbol}: they have different dimensions and cannot be compared.");
                }

                units[limit.LimitAttribute] = unit;
            }

            var mismatch = limits.FirstOrDefault(limit => limit.LimitAttribute != null && units[limit.Attribute] != null && units[limit.LimitAttribute] != null
                && units[limit.Attribute] != units[limit.LimitAttribute]);

            return mismatch == null
                ? units
                : throw new McpException($"'{mismatch.LimitAttribute}' limits '{mismatch.Attribute}' and another constraint, which need it in two different units: "
                    + "give the constraints other attributes, or the same unit.");
        }

        /// <summary>
        /// Finds the unit of an attribute.
        /// </summary>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="choices">The <see cref="TradeOffChoice"/>s of the request.</param>
        /// <param name="constraints">The <see cref="TradeOffConstraint"/>s of the request.</param>
        /// <param name="libraryUnits">The units that the libraries of the model declare.</param>
        /// <returns>The unit of the attribute, or <c>null</c> when nothing gives one.</returns>
        /// <exception cref="McpException">
        /// Thrown when a unit is unknown or has an offset.
        /// </exception>
        private static Unit FindUnit(IElement root, string attribute, IReadOnlyList<TradeOffChoice> choices, IReadOnlyList<TradeOffConstraint> constraints,
            LibraryUnits libraryUnits)
        {
            var unit = root.CollectContributions(attribute).Select(contribution => contribution.Unit).FirstOrDefault(contributionUnit => contributionUnit != null)
                ?? choices.SelectMany(choice => choice.Variants ?? [])
                    .Select(variant => variant?.Units?.FirstOrDefault(pair => pair.Key?.Trim() == attribute).Value)
                    .Select(text => ParseUnit(text, $"The unit of '{attribute}' in a variant", libraryUnits))
                    .FirstOrDefault(variantUnit => variantUnit != null)
                ?? constraints.Where(constraint => constraint?.Attribute?.Trim() == attribute)
                    .Select(constraint => ParseUnit(constraint.Unit, $"The unit of the limit of '{attribute}'", libraryUnits))
                    .FirstOrDefault(constraintUnit => constraintUnit != null);

            return unit is { Offset: not 0 }
                ? throw new McpException($"The unit {unit.Symbol} of '{attribute}' has an offset: its values cannot be added up. Use an absolute unit such as kelvin.")
                : unit;
        }

        /// <summary>
        /// Finds a unit from its symbol or its name: a unit that the server knows or a unit of a library of the model.
        /// </summary>
        /// <param name="text">The symbol or name of the unit, or <c>null</c> or empty for none.</param>
        /// <param name="label">What the unit belongs to, for the messages.</param>
        /// <param name="libraryUnits">The units that the libraries of the model declare.</param>
        /// <returns>The unit, or <c>null</c> when <paramref name="text"/> is empty.</returns>
        /// <exception cref="McpException">
        /// Thrown when the unit is not known.
        /// </exception>
        private static Unit ParseUnit(string text, string label, LibraryUnits libraryUnits)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return libraryUnits.TryParse(text, out var unit)
                ? unit
                : throw new McpException($"{label} is '{text}', which the server does not know: give a symbol (kg, g, W, km/h) or a name (kilogram).");
        }

        /// <summary>
        /// Converts a value to the unit of its attribute.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="unit">The unit of the value, or <c>null</c> when it is given in the unit of the attribute.</param>
        /// <param name="reference">The unit of the attribute, or <c>null</c> when it has none.</param>
        /// <param name="label">What the value belongs to, for the messages.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <returns>The value in the unit of the attribute.</returns>
        /// <exception cref="McpException">
        /// Thrown when the unit of the value and the unit of the attribute have different dimensions.
        /// </exception>
        private static double ConvertValue(double value, Unit unit, Unit reference, string label, string attribute)
        {
            if (unit == null || reference == null || unit == reference)
            {
                return value;
            }

            return unit.IsCompatibleWith(reference)
                ? unit.ConvertTo(value, reference)
                : throw new McpException($"{label} is in {unit.Symbol}, which cannot be converted to {reference.Symbol}, the unit of '{attribute}': give it in {reference.Symbol} or in a unit of the same dimension.");
        }

        /// <summary>
        /// Adds contributions, each converted to the unit of the attribute and counted for each unit of its part.
        /// </summary>
        /// <param name="contributions">The <see cref="Contribution"/>s to add.</param>
        /// <param name="reference">The unit of the attribute, or <c>null</c> when it has none.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <returns>The total in the unit of the attribute.</returns>
        /// <exception cref="McpException">
        /// Thrown when two contributions have units of different dimensions.
        /// </exception>
        private static double SumContributions(IEnumerable<Contribution> contributions, Unit reference, string attribute)
        {
            return contributions.Sum(contribution =>
                ConvertValue(contribution.Value, contribution.Unit, reference, $"The '{attribute}' of '{contribution.QualifiedName}'", attribute) * (contribution.Count ?? 1));
        }

        /// <summary>
        /// Converts the limit of a constraint to the unit of its attribute and gives the constraint this unit.
        /// </summary>
        /// <param name="limit">The checked <see cref="TradeOffLimit"/>.</param>
        /// <param name="constraint">The <see cref="TradeOffConstraint"/> that it comes from.</param>
        /// <param name="index">The index of the constraint in the request.</param>
        /// <param name="units">The unit of each attribute.</param>
        /// <param name="libraryUnits">The units that the libraries of the model declare.</param>
        /// <returns>The limit, in the unit of its attribute.</returns>
        /// <exception cref="McpException">
        /// Thrown when the unit of the limit is not known or has a different dimension from the attribute.
        /// </exception>
        private static TradeOffLimit ConvertLimit(TradeOffLimit limit, TradeOffConstraint constraint, int index, Dictionary<string, Unit> units, LibraryUnits libraryUnits)
        {
            var label = $"Constraint {index + 1}";
            var reference = units[limit.Attribute];

            if (limit.Limit is not { } value)
            {
                return limit with { Unit = reference };
            }

            var converted = ConvertValue(value, ParseUnit(constraint.Unit, $"The unit of the limit of {label}", libraryUnits), reference, $"The limit of {label}", limit.Attribute);

            return IsAllowedValue(converted)
                ? limit with { Limit = converted, Unit = reference }
                : throw new McpException($"{label} has a limit that is not a number between -{TradeOffScale.MaximumValue} and {TradeOffScale.MaximumValue} in {reference?.Symbol ?? "the unit of the attribute"}.");
        }

        /// <summary>
        /// Reads the values of a variant, each converted to the unit of its attribute when the problem knows it.
        /// </summary>
        /// <param name="variant">The <see cref="TradeOffVariant"/> to read.</param>
        /// <param name="choiceName">The name of the choice of the variant.</param>
        /// <param name="units">The unit of each attribute of the problem, by attribute name.</param>
        /// <param name="libraryUnits">The units declared in the libraries of the model.</param>
        /// <returns>The values, by attribute name, in the unit of the attribute, or else in the unit that the variant gives.</returns>
        /// <exception cref="McpException">
        /// Thrown when a unit is not known or cannot be converted, or when a converted value is out of the range of the solver.
        /// </exception>
        private static Dictionary<string, NumberValue> ReadValues(TradeOffVariant variant, string choiceName, Dictionary<string, Unit> units, LibraryUnits libraryUnits)
        {
            var label = $"The variant '{choiceName}{ReferenceSeparator}{variant.Name.Trim()}'";

            var variantUnits = (variant.Units ?? new Dictionary<string, string>())
                .ToDictionary(pair => pair.Key.Trim(), pair => ParseUnit(pair.Value, $"{label} gives the unit of '{pair.Key.Trim()}', which", libraryUnits));

            return (variant.Values ?? new Dictionary<string, double>()).ToDictionary(pair => pair.Key.Trim(), pair =>
            {
                var attribute = pair.Key.Trim();
                var variantUnit = variantUnits.GetValueOrDefault(attribute);

                var converted = units.TryGetValue(attribute, out var reference)
                    ? ConvertValue(pair.Value, variantUnit, reference, $"The '{attribute}' of the variant '{choiceName}{ReferenceSeparator}{variant.Name.Trim()}'", attribute)
                    : pair.Value;

                return IsAllowedValue(converted)
                    ? new NumberValue(converted, reference ?? variantUnit)
                    : throw new McpException($"{label} has a '{attribute}' that is not a number between -{TradeOffScale.MaximumValue} and {TradeOffScale.MaximumValue} in {reference?.Symbol ?? "the unit of the attribute"}.");
            });
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
                "==" => TradeOffOperator.Equal,
                _ => throw new McpException($"{label} has the operator '{constraint.Operator}': use '<', '<=', '>', '>=' or '=='.")
            };

            var attribute = constraint.Attribute.Trim();
            var limitAttribute = string.IsNullOrWhiteSpace(constraint.LimitAttribute) ? null : constraint.LimitAttribute.Trim();
            var margin = constraint.Margin ?? 0;

            if ((constraint.Limit == null) == (limitAttribute == null))
            {
                throw new McpException($"{label} needs either a limit (a number) or a limitAttribute, not both.");
            }

            if (limitAttribute != null && !string.IsNullOrWhiteSpace(constraint.Unit))
            {
                throw new McpException($"{label} gives a unit with a limitAttribute: a unit goes with a numeric limit, and the totals of both attributes are compared in the unit of the attribute.");
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

            if (margin > 0 && comparison == TradeOffOperator.Equal)
            {
                throw new McpException($"{label} has a margin with '==': a margin only applies to '<', '<=', '>' or '>='. Write the margin into the limit instead.");
            }

            if (margin > 0 && constraint.Limit <= 0)
            {
                throw new McpException($"{label} has a margin with a limit of 0 or less, where a margin would make it easier to meet: write the margin into the limit instead.");
            }

            return new TradeOffLimit(attribute, comparison, constraint.Limit, limitAttribute, margin, Name: string.IsNullOrWhiteSpace(constraint.Name) ? null : constraint.Name.Trim());
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

            // The part counts once for each unit of itself and of its owners under the root (wheels[4] in bogies[2] counts 8 times).
            foreach (var countedPart in owners.SkipLast(1).Prepend(part).Cast<IPartUsage>().Where(countedPart => countedPart.GetUnitCount() == null))
            {
                throw new McpException($"The number of units of '{countedPart.DeclaredName}' is not fixed ({countedPart.DescribeMultiplicity()}), so '{part.DeclaredName}' cannot be counted: "
                    + "give it a single number as multiplicity first.");
            }

            return new ChosenPart(part, owners, ((IPartUsage)part).GetUnitCount(), ((IPartUsage)part).GetVariants());
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
        /// Reads a choice into the draft of a decision: checks its variants and expands them into options, each with what the
        /// part then holds. The variants of the catalogue that types the part come first, then the variants that the request
        /// gives.
        /// </summary>
        /// <param name="choice">The <see cref="TradeOffChoice"/> to read, or <c>null</c> for a part that only the model makes a choice.</param>
        /// <param name="name">The name of the choice.</param>
        /// <param name="part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <param name="attributes">The attributes of the objectives and constraints.</param>
        /// <param name="units">The unit of each attribute of the problem, by attribute name.</param>
        /// <param name="libraryUnits">The units declared in the libraries of the model.</param>
        /// <returns>The <see cref="DecisionDraft"/>.</returns>
        /// <exception cref="McpException">
        /// Thrown when the choice has no variant, or when a variant is invalid.
        /// </exception>
        private static DecisionDraft ReadDecision(TradeOffChoice choice, string name, ChosenPart part, IReadOnlyList<string> attributes, Dictionary<string, Unit> units,
            LibraryUnits libraryUnits)
        {
            var variants = choice?.Variants ?? [];
            var catalogueVariants = part?.Variants ?? [];

            if (variants.Count == 0 && catalogueVariants.Count == 0)
            {
                throw new McpException($"The choice '{name}' has no variant.");
            }

            foreach (var variant in variants)
            {
                CheckVariant(variant, name, part);
            }

            var duplicateVariant = variants.Select(variant => variant.Name.Trim())
                .Concat(catalogueVariants.Select(variant => variant.DeclaredName))
                .GroupBy(variantName => variantName)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateVariant != null)
            {
                throw new McpException($"The choice '{name}' has two variants named '{duplicateVariant.Key}'.");
            }

            var givenCurrent = variants.FirstOrDefault(variant => variant.Name.Trim() == TradeOffVariant.CurrentName);
            var givenValues = givenCurrent == null ? [] : ReadValues(givenCurrent, name, units, libraryUnits);

            if (choice != null)
            {
                foreach (var attribute in attributes)
                {
                    CheckNotHidden(part, attribute, name);
                }
            }

            var required = part == null && choice.Required;
            List<OptionDraft> options;

            if (part == null)
            {
                options = [new OptionDraft(TradeOffVariant.NoneName, 0, true, new TradeOffPartContent(null, null, 0, IsCurrent: true))];
            }
            else if (catalogueVariants.Count > 0)
            {
                (options, required) = ReadCatalogueOptions(part);
            }
            else
            {
                options = [new OptionDraft(TradeOffVariant.CurrentName, 1, true, new TradeOffPartContent((IFeature)part.Part, givenValues, part.OwnCount ?? 1, IsCurrent: true))];
            }

            // A variant of the request counts for each unit of an existing part (wheels[4]), and keeps the values of the part
            // that it does not give.
            var unitCount = Math.Max(part?.OwnCount ?? 1, 1);

            foreach (var variant in variants.Where(variant => variant.Name.Trim() is not (TradeOffVariant.CurrentName or TradeOffVariant.NoneName)))
            {
                var values = ReadValues(variant, name, units, libraryUnits);

                foreach (var givenValue in givenValues.Where(givenValue => !values.ContainsKey(givenValue.Key)))
                {
                    values.Add(givenValue.Key, givenValue.Value);
                }

                options.AddRange(Enumerable.Range(variant.MinQuantity, variant.MaxQuantity - variant.MinQuantity + 1)
                    .Select(quantity => new OptionDraft(variant.Name.Trim(), quantity, false, new TradeOffPartContent(part?.Part as IFeature, values, unitCount * quantity))));
            }

            if (part != null && variants.Any(variant => variant.Name.Trim() == TradeOffVariant.NoneName) && !options.Exists(option => option.Quantity == 0))
            {
                options.Add(new OptionDraft(TradeOffVariant.NoneName, 0, false, new TradeOffPartContent(null, null, 0)));
            }

            if (part == null && choice.Required && variants.Any(variant => variant.Name.Trim() == TradeOffVariant.NoneName))
            {
                throw new McpException($"The equipment '{name}' is required, so it cannot take the variant '{TradeOffVariant.NoneName}'.");
            }

            return new DecisionDraft(name, part, required, options);
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

            if (values.Any(value => string.IsNullOrWhiteSpace(value.Key) || !double.IsFinite(value.Value)))
            {
                throw new McpException($"{label} has a value without attribute name, or a value that is not a number.");
            }

            var unitWithoutValue = (variant.Units ?? new Dictionary<string, string>()).Keys.FirstOrDefault(attribute => !values.Keys.Any(key => key.Trim() == attribute?.Trim()));

            if (unitWithoutValue != null)
            {
                throw new McpException($"{label} gives the unit of '{unitWithoutValue}' but no value for it.");
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
        /// Checks that a choice on an existing part can change an attribute: no owner of the part gives its own value for
        /// it, which would replace the values of its parts in the total.
        /// </summary>
        /// <param name="part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="choiceName">The name of the choice.</param>
        /// <exception cref="McpException">
        /// Thrown when an owner of the part gives its own value for the attribute, so that the value of the part is not in the
        /// total.
        /// </exception>
        private static void CheckNotHidden(ChosenPart part, string attribute, string choiceName)
        {
            var hidingOwner = part?.Owners.FirstOrDefault(owner => HasOwnValue(owner, attribute));

            if (hidingOwner != null)
            {
                throw new McpException($"The choice '{choiceName}' cannot change '{attribute}': '{hidingOwner.DeclaredName}' gives its own '{attribute}', "
                    + "which replaces the values of its parts in the total. Make the choice on it instead.");
            }
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
        /// Tells whether an attribute has a value somewhere: in a part under the root, in a variant of the request or in a
        /// variant of a catalogue.
        /// </summary>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="choices">The <see cref="TradeOffChoice"/>s of the request, <c>null</c> for a part that only the model makes a choice.</param>
        /// <param name="parts">The existing part of each choice, or <c>null</c> for equipment to add.</param>
        /// <returns><c>true</c> when the attribute has a value somewhere.</returns>
        private static bool IsKnown(IElement root, string attribute, IReadOnlyList<TradeOffChoice> choices, IReadOnlyList<ChosenPart> parts)
        {
            return root.CollectContributions(attribute).Count > 0
                || choices.SelectMany(choice => choice?.Variants ?? []).Any(variant => (variant?.Values?.Keys ?? []).Any(key => key?.Trim() == attribute))
                || parts.SelectMany(part => part?.Variants ?? []).Any(variant => variant.CollectContributions(attribute).Count > 0);
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
        /// <param name="OwnCount">The number of units of the part itself, or <c>null</c> when its multiplicity does not fix it.</param>
        /// <param name="Variants">The variants of the catalogues that type the part, empty when none does.</param>
        private sealed record ChosenPart(IElement Part, IReadOnlyList<IElement> Owners, int? OwnCount, IReadOnlyList<IUsage> Variants);

        /// <summary>
        /// An option of a choice before its values are computed.
        /// </summary>
        /// <param name="Variant">The name of the variant.</param>
        /// <param name="Quantity">The number of units of the variant.</param>
        /// <param name="IsCurrent">Whether the option is what the model holds.</param>
        /// <param name="Content">What the part holds when the option is taken.</param>
        private sealed record OptionDraft(string Variant, int Quantity, bool IsCurrent, TradeOffPartContent Content);

        /// <summary>
        /// A choice before the values of its options are computed.
        /// </summary>
        /// <param name="Name">The name of the choice.</param>
        /// <param name="Part">The existing part of the choice, or <c>null</c> for equipment to add.</param>
        /// <param name="Required">Whether the option that the model holds may not be taken.</param>
        /// <param name="Options">The options of the choice.</param>
        private sealed record DecisionDraft(string Name, ChosenPart Part, bool Required, List<OptionDraft> Options);
    }
}
