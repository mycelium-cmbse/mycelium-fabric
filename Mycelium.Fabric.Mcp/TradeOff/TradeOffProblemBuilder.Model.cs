// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffProblemBuilder.Model.cs" company="Starion Group S.A.">
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
    using System.Linq;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <content>
    /// What the builder reads from the model without the request saying it: the parts typed by a catalogue of variants, each
    /// one a choice whose variants and numbers of units the catalogue gives, and the requirements with a value to check,
    /// written as limits for the solver when they compare sums.
    /// </content>
    public static partial class TradeOffProblemBuilder
    {
        /// <summary>
        /// The operators of the comparisons that the solver takes as limits.
        /// </summary>
        private static readonly Dictionary<string, TradeOffOperator> LimitOperators = new()
        {
            ["<"] = TradeOffOperator.LessThan,
            ["<="] = TradeOffOperator.LessThanOrEqual,
            [">"] = TradeOffOperator.GreaterThan,
            [">="] = TradeOffOperator.GreaterThanOrEqual,
            ["=="] = TradeOffOperator.Equal,
            ["==="] = TradeOffOperator.Equal
        };

        /// <summary>
        /// Finds the parts under the root that are typed by a catalogue of variants and that no choice of the request
        /// covers. A part inside a chosen part follows this part, and a part inside a part typed by a catalogue is replaced
        /// with it, so neither is a choice.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="parts">The existing part of each choice of the request, or <c>null</c> for equipment to add.</param>
        /// <returns>The parts, sorted by qualified name.</returns>
        private static List<ChosenPart> FindCatalogueParts(IModelProvider modelProvider, IElement root, IReadOnlyList<ChosenPart> parts)
        {
            var chosenIds = parts.Where(part => part != null).Select(part => part.Part.Id).ToHashSet();

            var catalogueParts = modelProvider.Elements
                .OfType<IPartUsage>()
                .Where(part => !chosenIds.Contains(part.Id) && part.GetVariants().Count > 0)
                .Select(part => ReadPartUnder(root, part))
                .Where(part => part != null && !part.Owners.Any(owner => chosenIds.Contains(owner.Id)))
                .ToList();

            var catalogueIds = catalogueParts.Select(part => part.Part.Id).Concat(parts.Where(part => part is { Variants.Count: > 0 }).Select(part => part.Part.Id)).ToHashSet();

            return [.. catalogueParts.Where(part => !part.Owners.Any(owner => catalogueIds.Contains(owner.Id))).OrderBy(part => part.Part.qualifiedName, StringComparer.Ordinal)];
        }

        /// <summary>
        /// Reads a part typed by a catalogue with its owners up to the root.
        /// </summary>
        /// <param name="root">The element under which the totals are computed.</param>
        /// <param name="part">The part.</param>
        /// <returns>
        /// The part and its owners, or <c>null</c> when the part is not under the root, or when a part that holds it has no
        /// fixed number of units or is taken 0 times, so that the totals do not count it.
        /// </returns>
        private static ChosenPart ReadPartUnder(IElement root, IPartUsage part)
        {
            List<IElement> owners = [];

            for (var owner = part.owner; owner != null; owner = owner.owner)
            {
                owners.Add(owner);

                if (owner.Id == root.Id)
                {
                    return new ChosenPart(part, owners, part.GetUnitCount(), part.GetVariants());
                }

                if (owner is not IPartUsage holder || holder.GetUnitCount() is null or 0)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a name for the choice of a part typed by a catalogue: the name of the part, with the name of its owner when
        /// another choice already has this name.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="names">The names of the choices so far.</param>
        /// <returns>The name of the choice.</returns>
        private static string GetFreeName(ChosenPart part, List<string> names)
        {
            var name = part.Part.DeclaredName.Replace(ReferenceSeparator, '_');

            if (!names.Contains(name))
            {
                return name;
            }

            name = $"{name} ({part.Owners[0].DeclaredName})";

            for (var number = 2; names.Contains(name); number++)
            {
                name = $"{part.Part.DeclaredName} ({part.Owners[0].DeclaredName} {number})";
            }

            return name;
        }

        /// <summary>
        /// Expands the variants of the catalogue of a part into options: one for each variant and each number of units that
        /// the multiplicity of the variant allows (<c>[1..4]</c>), or the number of units of the part when the variant has no
        /// multiplicity; <c>none</c> when a variant may be taken 0 times. The option that the model holds is added when the
        /// catalogue does not allow it, and when the part takes no variant yet, one of the variants must be taken. A part taken
        /// 0 times keeps its variant, as the model does with a multiplicity of 0.
        /// </summary>
        /// <param name="part">The part typed by a catalogue.</param>
        /// <returns>The options, and whether the option that the model holds may not be taken.</returns>
        private static (List<OptionDraft> Options, bool Required) ReadCatalogueOptions(ChosenPart part)
        {
            var chosenVariant = ((IPartUsage)part.Part).GetChosenVariant();
            var currentCount = part.OwnCount;
            var unitCount = currentCount is > 0 ? currentCount.Value : 1;
            var canBeNone = false;
            List<OptionDraft> options = [];

            foreach (var variant in part.Variants.Where(variant => variant.DeclaredName != null))
            {
                var (minimum, maximum) = variant.GetUnitRange() ?? (unitCount, unitCount);
                canBeNone |= minimum == 0;

                for (var quantity = Math.Max(minimum, 1); quantity <= Math.Min(maximum ?? MaximumQuantity, MaximumQuantity); quantity++)
                {
                    var isCurrent = variant.Id == chosenVariant?.Id && quantity == currentCount;

                    options.Add(new OptionDraft(variant.DeclaredName, quantity, isCurrent, new TradeOffPartContent(variant, null, quantity, IsVariant: true, IsCurrent: isCurrent)));
                }
            }

            if (chosenVariant == null)
            {
                // The part takes no variant yet: it stays as it is in the model, where it misses its values, until one is taken.
                options.Insert(0, new OptionDraft(TradeOffVariant.NoneName, 0, true, new TradeOffPartContent((IFeature)part.Part, null, unitCount, IsCurrent: true)));

                return (options, true);
            }

            if (currentCount != 0 && !options.Exists(option => option.IsCurrent))
            {
                options.Insert(0, new OptionDraft(chosenVariant.DeclaredName, unitCount, true, new TradeOffPartContent(chosenVariant, null, unitCount, IsVariant: true, IsCurrent: true)));
            }

            if (canBeNone || currentCount == 0)
            {
                options.Add(new OptionDraft(TradeOffVariant.NoneName, 0, currentCount == 0, new TradeOffPartContent(chosenVariant, null, 0, IsVariant: true, IsCurrent: currentCount == 0)));
            }

            return (options, false);
        }

        /// <summary>
        /// Reads the requirements of the model that can be checked, names them, and turns off those that the request ignores.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="ignored">The requirements to ignore, by identifier, name, qualified name or <c>Id</c>.</param>
        /// <returns>The <see cref="TradeOffRequirement"/>s.</returns>
        /// <exception cref="McpException">
        /// Thrown when an ignored requirement is not a requirement with a value to check.
        /// </exception>
        private static List<TradeOffRequirement> ReadRequirements(IModelProvider modelProvider, IReadOnlyList<string> ignored)
        {
            var satisfiedRequirements = new RequirementChecker(modelProvider.Elements).GetSatisfiedRequirements();

            var baseNames = satisfiedRequirements
                .Select(satisfied => satisfied.Requirement.ReqId ?? satisfied.Requirement.DeclaredName ?? satisfied.Requirement.qualifiedName)
                .ToList();

            // A requirement that several parts satisfy is checked for each of them, and named with the part.
            var names = satisfiedRequirements
                .Select((satisfied, index) => baseNames.Count(baseName => baseName == baseNames[index]) > 1 ? $"{baseNames[index]} ({satisfied.SatisfyingFeature.DeclaredName})" : baseNames[index])
                .ToList();

            var ignoredTexts = ignored.Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Trim()).ToList();

            var requirements = satisfiedRequirements
                .Select((satisfied, index) => new TradeOffRequirement(satisfied, names[index], IsIgnored: ignoredTexts.Exists(text => Designates(text, satisfied, names[index]))))
                .ToList();

            var unknown = ignoredTexts.Find(text => !requirements.Exists(requirement => Designates(text, requirement.Source, requirement.Name)));

            return unknown == null
                ? requirements
                : throw new McpException($"ignoredRequirements names '{unknown}', which is not a requirement with a value to check. "
                    + $"The requirements are: {(names.Count == 0 ? "none" : string.Join(", ", names))}.");
        }

        /// <summary>
        /// Tells whether a text designates a requirement: its shown name, its identifier, its name, its qualified name or
        /// its <c>Id</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="satisfied">The requirement with the part that satisfies it.</param>
        /// <param name="name">The shown name of the requirement.</param>
        /// <returns><c>true</c> when the text designates the requirement.</returns>
        private static bool Designates(string text, SatisfiedRequirement satisfied, string name)
        {
            var requirement = satisfied.Requirement;

            return text == name || text == requirement.ReqId || text == requirement.DeclaredName || text == requirement.qualifiedName
                || string.Equals(text, requirement.Id.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes each requirement that compares sums as a limit for the solver, after the limits of the request. A
        /// requirement whose check reads no choice is the same for every combination and needs no limit; a requirement that
        /// is not a comparison of sums gets none either, and reaches the solver through the penalties of its checks.
        /// </summary>
        /// <param name="requirements">The requirements of the model.</param>
        /// <param name="totals">The <see cref="TotalReader"/> that computes the totals that the limits compare.</param>
        /// <param name="limits">The limits of the problem, to which the limits of the requirements are added.</param>
        /// <returns>The requirements, each with the position of its limit when it has one.</returns>
        private static List<TradeOffRequirement> ReadRequirementLimits(List<TradeOffRequirement> requirements, TotalReader totals, List<TradeOffLimit> limits)
        {
            return
            [
                .. requirements.Select(requirement =>
                {
                    if (requirement.IsIgnored || RequirementChecker.CheckRequirement(requirement.Source, totals.ProbeSelection).ReadPartIds.Count == 0)
                    {
                        return requirement;
                    }

                    var limit = ReadRequirementLimit(requirement, totals);

                    if (limit == null)
                    {
                        return requirement;
                    }

                    limits.Add(limit);

                    return requirement with { LimitIndex = limits.Count - 1 };
                })
            ];
        }

        /// <summary>
        /// Writes a requirement as a limit when it is a single comparison between a sum, with an optional factor, and a
        /// number or another sum (<c>subj.mass * 1.2 &lt;= 150 [kg]</c>, <c>subj.power &lt;= subj.powerOutput</c>).
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="totals">The <see cref="TotalReader"/> that computes the totals that the limit compares.</param>
        /// <returns>The <see cref="TradeOffLimit"/>, or <c>null</c> when the requirement is not such a comparison.</returns>
        private static TradeOffLimit ReadRequirementLimit(TradeOffRequirement requirement, TotalReader totals)
        {
            var source = requirement.Source;

            if (source.Assumptions.Count > 0 || source.Required is not [{ IsError: false, Value: OperationTerm { Operands: [var leftTerm, var rightTerm] } comparison }]
                || !LimitOperators.TryGetValue(comparison.Operator, out var comparisonOperator))
            {
                return null;
            }

            var left = ReadSide(leftTerm, source, totals);
            var right = ReadSide(rightTerm, source, totals);

            if (left == null || right == null)
            {
                return null;
            }

            // The sum goes on the left: 150 >= subj.mass is subj.mass <= 150.
            if (left.Scope == null)
            {
                (left, right) = (right, left);
                comparisonOperator = comparisonOperator.ToSwapped();
            }

            if (left.Scope == null || left.Unit is { Offset: not 0 })
            {
                return null;
            }

            var key = totals.ReadKey(left.Scope, left.Attribute, left.Unit);

            if (key == null)
            {
                return null;
            }

            if (right.Scope == null)
            {
                var constant = right.Constant.Unit == null || left.Unit == null ? right.Constant.Number
                    : right.Constant.Unit.IsCompatibleWith(left.Unit) && right.Constant.Unit.Offset == 0 ? right.Constant.Unit.ConvertTo(right.Constant.Number, left.Unit)
                    : double.NaN;

                var limit = constant * right.Factor / left.Factor;

                return IsAllowedValue(limit) ? new TradeOffLimit(key, comparisonOperator, limit, null, 0, left.Unit, requirement.Name) { IsFromModel = true } : null;
            }

            // Between two sums, the factor is a margin on the side where it makes the comparison harder to meet.
            var isUpperBound = comparisonOperator is TradeOffOperator.LessThan or TradeOffOperator.LessThanOrEqual;
            var margin = Math.Round(((isUpperBound ? left.Factor / right.Factor : right.Factor / left.Factor) - 1) * 100, 6);
            var limitKey = margin >= 0 && (margin == 0 || comparisonOperator != TradeOffOperator.Equal) ? totals.ReadKey(right.Scope, right.Attribute, left.Unit ?? right.Unit) : null;

            return limitKey == null || limitKey == key
                ? null
                : new TradeOffLimit(key, comparisonOperator, null, limitKey, margin, left.Unit, requirement.Name) { IsFromModel = true };
        }

        /// <summary>
        /// Reads one side of a comparison: a sum that the requirement reads through a path (<c>subj.mass</c>,
        /// <c>subj.powerSystem.battery.energyCapacity</c>), or a number that no choice changes, with the plain factors that
        /// multiply or divide it.
        /// </summary>
        /// <param name="term">The expression of the side.</param>
        /// <param name="source">The requirement with the part that satisfies it.</param>
        /// <param name="totals">The <see cref="TotalReader"/> that gives the combination on which the side is read.</param>
        /// <returns>The <see cref="ComparedSide"/>, or <c>null</c> when the side is neither.</returns>
        private static ComparedSide ReadSide(Term term, SatisfiedRequirement source, TotalReader totals)
        {
            var factor = 1.0;

            while (term is OperationTerm { Operator: "*" or "/", Operands: [var first, var second] } operation)
            {
                if (second is LiteralTerm { Value: NumberValue { Unit: null, Number: > 0 and var divisorOrFactor } })
                {
                    factor = operation.Operator == "*" ? factor * divisorOrFactor : factor / divisorOrFactor;
                    term = first;
                }
                else if (operation.Operator == "*" && first is LiteralTerm { Value: NumberValue { Unit: null, Number: > 0 and var leftFactor } })
                {
                    factor *= leftFactor;
                    term = second;
                }
                else
                {
                    break;
                }
            }

            var context = new ModelEvaluationContext(source.Subject, source.SatisfyingFeature, selection: totals.ProbeSelection);
            var value = term.Evaluate(context);

            if (value.IsError || value.Value is not NumberValue number || !double.IsFinite(number.Number))
            {
                return null;
            }

            if (context.RollUps.Count == 0 && context.ReadPartIds.Count == 0)
            {
                return new ComparedSide(null, null, number, factor, number.Unit);
            }

            // Only a plain path is a sum: a function of a sum, such as max(subj.mass, 10), is not.
            return term is NavigationTerm or ReferenceTerm && context.RollUps is [var rollUp] && rollUp.Total == number
                ? new ComparedSide(rollUp.Part, rollUp.Attribute, null, factor, number.Unit)
                : null;
        }

        /// <summary>
        /// One side of the comparison of a requirement.
        /// </summary>
        /// <param name="Scope">The part under which the sum is computed, or <c>null</c> for a number.</param>
        /// <param name="Attribute">The attribute of the sum, or <c>null</c> for a number.</param>
        /// <param name="Constant">The number, or <c>null</c> for a sum.</param>
        /// <param name="Factor">The factor that multiplies the side.</param>
        /// <param name="Unit">The unit of the side, or <c>null</c> when it has none.</param>
        private sealed record ComparedSide(IFeature Scope, string Attribute, NumberValue Constant, double Factor, Unit Unit);

        /// <summary>
        /// Computes the totals that the solver adds up: for each total, the part that no choice changes and what each option
        /// of each choice adds.
        /// </summary>
        private sealed class TotalReader
        {
            /// <summary>
            /// The <see cref="TradeOffDesignSpace"/> of the trade-off.
            /// </summary>
            private readonly TradeOffDesignSpace space;

            /// <summary>
            /// The unit of each total, by name, to which the units of the totals of the requirements are added.
            /// </summary>
            private readonly Dictionary<string, Unit> units;

            /// <summary>
            /// The name of each total that a requirement reads, by part, attribute and unit.
            /// </summary>
            private readonly Dictionary<(Guid ScopeId, string Attribute, Unit Unit), string> keys = [];

            /// <summary>
            /// Initializes a new instance of the <see cref="TotalReader"/> class.
            /// </summary>
            /// <param name="space">The <see cref="TradeOffDesignSpace"/> of the trade-off.</param>
            /// <param name="drafts">The drafts of the decisions.</param>
            /// <param name="units">The unit of each total, by name.</param>
            public TotalReader(TradeOffDesignSpace space, IReadOnlyList<DecisionDraft> drafts, Dictionary<string, Unit> units)
            {
                this.space = space;
                this.units = units;
                this.OptionValues = [.. drafts.Select(draft => draft.Options.Select(_ => new Dictionary<string, double>()).ToList())];
                this.ProbeSelection = space.CreateSelection([.. drafts.Select(draft => draft.Options.FindIndex(option => option.IsCurrent != draft.Required))]);
            }

            /// <summary>
            /// Gets the part of each total that no choice changes, by name of the total.
            /// </summary>
            public Dictionary<string, double> FixedTotals { get; } = [];

            /// <summary>
            /// Gets what each option adds to each total, by choice, then by option, then by name of the total.
            /// </summary>
            public List<List<Dictionary<string, double>>> OptionValues { get; }

            /// <summary>
            /// Gets the combination on which the requirements are read to find the sums that they compare: what the parts
            /// hold in the model as it is, and the first variant of a part that takes none yet, whose values are missing.
            /// </summary>
            public IPartSelection ProbeSelection { get; }

            /// <summary>
            /// Adds a total to the problem.
            /// </summary>
            /// <param name="name">The name of the total.</param>
            /// <param name="totalParts">The <see cref="TradeOffTotalParts"/> of the total.</param>
            /// <param name="sum">Adds up contributions in the unit of the total.</param>
            public void ReadTotal(string name, TradeOffTotalParts totalParts, Func<IReadOnlyList<Contribution>, double> sum)
            {
                this.FixedTotals[name] = sum(totalParts.Fixed);

                for (var decisionIndex = 0; decisionIndex < this.OptionValues.Count; decisionIndex++)
                {
                    for (var optionIndex = 0; optionIndex < this.OptionValues[decisionIndex].Count; optionIndex++)
                    {
                        this.OptionValues[decisionIndex][optionIndex][name] = sum(totalParts.Options[decisionIndex][optionIndex]);
                    }
                }
            }

            /// <summary>
            /// Adds to the problem the total that a requirement reads: the sum of an attribute under a part, with the margins
            /// that the model gives to the parts, as <c>check_requirements</c> computes it.
            /// </summary>
            /// <param name="scope">The part under which the sum is computed.</param>
            /// <param name="attribute">The declared name of the attribute.</param>
            /// <param name="unit">The unit of the total, or <c>null</c> when it has none.</param>
            /// <returns>
            /// The name of the total, or <c>null</c> when the total is not a sum over the choices: the part belongs to a
            /// chosen part, or the values cannot be added up in the unit.
            /// </returns>
            public string ReadKey(IFeature scope, string attribute, Unit unit)
            {
                if (this.keys.TryGetValue((scope.Id, attribute, unit), out var key))
                {
                    return key;
                }

                var totalParts = this.space.GetTotalParts(scope, attribute);
                var contributions = totalParts.IsError ? [] : totalParts.Value.Options.SelectMany(options => options.SelectMany(option => option)).Concat(totalParts.Value.Fixed).ToList();

                if (totalParts.IsError || unit is { Offset: not 0 } || contributions.Exists(contribution => !CanConvert(contribution.Unit, unit)))
                {
                    return null;
                }

                key = $"{scope.DeclaredName}{ReferenceSeparator}{attribute}";

                if (this.FixedTotals.ContainsKey(key))
                {
                    key = $"{scope.qualifiedName}{ReferenceSeparator}{attribute}{(unit == null ? string.Empty : $" [{unit.Symbol}]")}";
                }

                this.ReadTotal(key, totalParts.Value, parts => parts.Sum(contribution => Convert(contribution.GetTotalWithMargin().Number, contribution.Unit, unit)));
                this.units[key] = unit;
                this.keys.Add((scope.Id, attribute, unit), key);

                return key;
            }

            /// <summary>
            /// Tells whether a value in a unit can be added to a total in another unit.
            /// </summary>
            /// <param name="unit">The unit of the value, or <c>null</c>.</param>
            /// <param name="target">The unit of the total, or <c>null</c>.</param>
            /// <returns><c>true</c> when the value can be added.</returns>
            private static bool CanConvert(Unit unit, Unit target)
            {
                return unit == null || target == null || unit == target || (unit.Offset == 0 && unit.IsCompatibleWith(target));
            }

            /// <summary>
            /// Converts a value to the unit of a total.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <param name="unit">The unit of the value, or <c>null</c>.</param>
            /// <param name="target">The unit of the total, or <c>null</c>.</param>
            /// <returns>The converted value.</returns>
            private static double Convert(double value, Unit unit, Unit target)
            {
                return unit == null || target == null || unit == target ? value : unit.ConvertTo(value, target);
            }
        }
    }
}
