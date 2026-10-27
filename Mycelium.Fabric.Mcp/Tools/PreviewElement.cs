// ------------------------------------------------------------------------------------------------
//  <copyright file="PreviewElement.cs" company="Starion Group S.A.">
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
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Multiplicities;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// An element that a batch waiting for the decision of the user would create, modify or delete, as the view of the batch
    /// shows it.
    /// </summary>
    /// <param name="Id">The identifier of the element, provisional for a created element.</param>
    /// <param name="QualifiedName">
    /// The qualified name of the element, for example <c>EOSat1::Architecture::eosat1::camera</c>, or <c>null</c> when the
    /// element or one of its owners has no name.
    /// </param>
    /// <param name="Type">The metaclass of the element, for example <c>PartUsage</c>.</param>
    /// <param name="Definition">The names of the definitions of a usage, for example <c>OpticalCamera</c>, or <c>null</c>.</param>
    /// <param name="Value">
    /// The value of the element as the view shows it: the value of an attribute (<c>38 kg</c>), the mass and power of a part
    /// with its sub-parts (<c>38 kg · 55 W</c>, the unit being read from the documentation of the attribute when the values have
    /// none, or <c>mass 38 · power 55</c> without any unit) or the identifier of a requirement
    /// (<c>REQ-SYS-001</c>); for a modified element
    /// whose value changes, the value before and after the batch (<c>38 kg → 35 kg</c>); <c>null</c> when it has none.
    /// </param>
    /// <param name="Domain">
    /// The code of the subsystem that the element belongs to, for example <c>THE</c> for an element under
    /// <c>eosat1::thermal</c> or <c>kestrelMission::kestrel1::thermalSubsystem</c>, <c>SYS</c> for the system part and its own
    /// features, or <c>null</c> outside any part or inside a definition.
    /// </param>
    /// <param name="Multiplicity">
    /// The multiplicity of a feature as the textual notation writes it between brackets, for example <c>4</c> or <c>0..*</c>,
    /// or <c>null</c> when it has none.
    /// </param>
    public sealed partial record PreviewElement(Guid Id, string QualifiedName, string Type, string Definition = null, string Value = null, string Domain = null,
        string Multiplicity = null)
    {
        /// <summary>
        /// The code of the system part and of its own features.
        /// </summary>
        public const string SystemDomain = "SYS";

        /// <summary>
        /// The names of the attributes added up over a part and its sub-parts to give its value.
        /// </summary>
        private static readonly string[] BudgetAttributeNames = ["mass", "power"];

        /// <summary>
        /// Creates the <see cref="PreviewElement"/> of an element from the element before and after the batch.
        /// </summary>
        /// <param name="id">The identifier of the element.</param>
        /// <param name="qualifiedName">The qualified name of the element.</param>
        /// <param name="type">The metaclass of the element.</param>
        /// <param name="before">The element before the batch, <c>null</c> for a created element.</param>
        /// <param name="after">The element after the batch, <c>null</c> for a deleted element.</param>
        /// <returns>The new <see cref="PreviewElement"/>, without definition, value nor domain when both elements are <c>null</c>.</returns>
        public static PreviewElement Create(Guid id, string qualifiedName, string type, IElement before, IElement after)
        {
            var element = after ?? before;

            if (element == null)
            {
                return new PreviewElement(id, qualifiedName, type);
            }

            var valueBefore = before == null ? null : DescribeValue(before);
            var valueAfter = after == null ? null : DescribeValue(after);
            var value = before == null || after == null || valueBefore == valueAfter ? valueAfter ?? valueBefore : $"{valueBefore ?? "none"} → {valueAfter ?? "none"}";
            var definitionNames = element is IUsage and not IAttributeUsage ? element.GetTypeNames() : [];

            return new PreviewElement(id, qualifiedName, type, definitionNames.Count == 0 ? null : string.Join(", ", definitionNames), value, GetDomain(element),
                DescribeMultiplicity(element));
        }

        /// <summary>
        /// Adds up the budget attributes, the mass and the power, over a part and its sub-parts, as sum_attribute does.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns>
        /// The total of each budget attribute that the part or its sub-parts have, with the symbol of its unit, read from the
        /// documentation of the attribute when the values have none, or <c>null</c>.
        /// </returns>
        internal static List<(string Attribute, double Total, string Unit)> GetBudgetTotals(IPartUsage part)
        {
            return BudgetAttributeNames
                .Select(attributeName => (Name: attributeName, Contributions: part.CollectContributions(attributeName)))
                .Where(budget => budget.Contributions.Count > 0)
                .Select(budget => (budget.Name, Total: NumberValue.Sum(budget.Contributions.Select(contribution => new NumberValue(contribution.Value, contribution.Unit)))))
                .Where(budget => !budget.Total.IsError)
                .Select(budget => (budget.Name, budget.Total.Value.Number, budget.Total.Value.Unit?.ToString() ?? FindDocumentedUnit(part, budget.Name)))
                .ToList();
        }

        /// <summary>
        /// Describes the value of an element: the value of an attribute, the mass and power of a part with its sub-parts, or
        /// the identifier of a requirement.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The value of the element, or <c>null</c> when it has none.</returns>
        private static string DescribeValue(IElement element)
        {
            switch (element)
            {
                case IAttributeUsage attribute:
                    var value = attribute.GetConstantValue();
                    return value is NumberValue number ? FormatNumber(number, ReadDocumentedUnit(attribute)) : value?.ToString();

                case IPartUsage part:
                    var totals = GetBudgetTotals(part)
                        .Select(budget => budget.Unit == null ? $"{budget.Attribute} {NumberValue.Format(budget.Total)}" : $"{NumberValue.Format(budget.Total)} {budget.Unit}")
                        .ToList();

                    return totals.Count == 0 ? null : string.Join(" · ", totals);

                case IRequirementUsage requirement:
                    return requirement.ReqId;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Describes the multiplicity that a feature owns, as the textual notation writes it between brackets.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The multiplicity, for example <c>4</c>, <c>1..4</c> or <c>0..*</c>, or <c>null</c> when the element owns none.</returns>
        private static string DescribeMultiplicity(IElement element)
        {
            if (element is not IFeature feature || (feature.ownedElement ?? []).OfType<IMultiplicityRange>().FirstOrDefault() is not { } multiplicity)
            {
                return null;
            }

            var bounds = (multiplicity.bound ?? []).Select(bound => bound switch
            {
                ILiteralInteger integer => integer.Value.ToString(CultureInfo.InvariantCulture),
                ILiteralInfinity => "*",
                _ => "?"
            });

            return string.Join("..", bounds);
        }

        /// <summary>
        /// Finds the unit documented on an attribute of an element or, when the element has no such attribute, of its sub-parts.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <returns>The first documented unit found, or <c>null</c>.</returns>
        private static string FindDocumentedUnit(IElement element, string attributeName)
        {
            var attribute = element.GetAttributeUsages().FirstOrDefault(candidate => candidate.DeclaredName == attributeName);

            if (attribute != null)
            {
                return ReadDocumentedUnit(attribute);
            }

            return (element.ownedElement ?? [])
                .OfType<IPartUsage>()
                .Select(part => FindDocumentedUnit(part, attributeName))
                .FirstOrDefault(unit => unit != null);
        }

        /// <summary>
        /// Reads the unit that the documentation of an attribute gives between brackets, as in <c>Dry mass of the unit [kg].</c>
        /// Models whose values have no unit give it this way.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <returns>The documented unit, for example <c>kg</c>, or <c>null</c>.</returns>
        private static string ReadDocumentedUnit(IAttributeUsage attribute)
        {
            var match = DocumentedUnit().Match(attribute.GetDocumentationBodies() ?? string.Empty);

            return match.Success ? match.Groups["unit"].Value.Trim() : null;
        }

        /// <summary>
        /// Writes a number with the symbol of its unit, as the view shows it.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <param name="documentedUnit">The unit to write when the number has none, or <c>null</c>.</param>
        /// <returns>The number, for example <c>38 kg</c>.</returns>
        private static string FormatNumber(NumberValue number, string documentedUnit = null)
        {
            var unit = number.Unit?.ToString() ?? documentedUnit;

            return unit == null ? NumberValue.Format(number.Number) : $"{NumberValue.Format(number.Number)} {unit}";
        }

        /// <summary>
        /// Matches a unit between brackets in a documentation, for example <c>[kg]</c>.
        /// </summary>
        /// <returns>The <see cref="Regex"/>.</returns>
        [GeneratedRegex(@"\[(?<unit>[^\[\]]+)\]", RegexOptions.None, 1000)]
        private static partial Regex DocumentedUnit();

        /// <summary>
        /// Gets the code of the subsystem that an element belongs to: the part directly under the system that owns the element,
        /// or the element itself, abbreviated to three letters without its <c>Subsystem</c> suffix. The system is the outermost
        /// part that owns the element, or the deepest part below it, on the way to the element, that is a system too (see
        /// <see cref="IsSystem"/>), as the spacecraft of a mission.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>
        /// The code of the subsystem, <see cref="SystemDomain"/> for the system part and its own features, or <c>null</c> outside any
        /// part or inside a definition.
        /// </returns>
        private static string GetDomain(IElement element)
        {
            var parts = new List<IPartUsage>();

            for (var current = element; current != null; current = current.owner)
            {
                // A feature of a definition belongs to the subsystem of each usage that the definition types, not to one.
                if (current is IDefinition)
                {
                    return null;
                }

                if (current is IPartUsage part)
                {
                    parts.Insert(0, part);
                }
            }

            if (parts.Count == 0)
            {
                return null;
            }

            // The system is the outermost part, or the part below it that is a system too, as the spacecraft of a mission.
            var systemIndex = 0;

            while (systemIndex + 1 < parts.Count && IsSystem(parts[systemIndex + 1]))
            {
                systemIndex++;
            }

            if (systemIndex + 1 == parts.Count)
            {
                return SystemDomain;
            }

            var name = parts[systemIndex + 1].DeclaredName ?? string.Empty;

            if (name.Length > "Subsystem".Length && name.EndsWith("Subsystem", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^"Subsystem".Length];
            }

            return name.Length == 0 ? null : name[..Math.Min(3, name.Length)].ToUpper(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tells whether a part is a system made of subsystems: at least two of its sub-parts have sub-parts of their own.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns><c>true</c> when the part is a system.</returns>
        private static bool IsSystem(IPartUsage part)
        {
            return (part.ownedElement ?? [])
                .OfType<IPartUsage>()
                .Count(subPart => (subPart.ownedElement ?? []).OfType<IPartUsage>().Any()) >= 2;
        }
    }
}
