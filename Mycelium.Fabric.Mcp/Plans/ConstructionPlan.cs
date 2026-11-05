// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionPlan.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Plans
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Changes;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// The plan of a work of several batches of changes, given to apply_changes by the AI assistant: its sections, each with
    /// the elements that it creates. The server keeps it and tells after each batch what is left, so that the assistant does
    /// not lose track of a long work and stop before its end.
    /// </summary>
    public sealed class ConstructionPlan
    {
        /// <summary>
        /// The maximum number of missing elements listed in a <see cref="PlanProgress"/>, the same as the new issues of a batch.
        /// </summary>
        private const int MaximumListedElementCount = 20;

        /// <summary>
        /// The separator of the names in a qualified name.
        /// </summary>
        private const string NameSeparator = "::";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstructionPlan"/> class.
        /// </summary>
        /// <param name="sections">The checked sections of the plan.</param>
        private ConstructionPlan(IReadOnlyList<PlanSection> sections)
        {
            this.Sections = sections;
        }

        /// <summary>
        /// Gets the sections of the plan, with their names trimmed and the metaclasses of their elements spelled as
        /// SysML2.NET does.
        /// </summary>
        public IReadOnlyList<PlanSection> Sections { get; }

        /// <summary>
        /// Checks the sections of a plan and creates the plan when they are valid: each section has a name of its own and
        /// lists at least one element, and each element has a name and a metaclass that apply_changes creates.
        /// </summary>
        /// <param name="sections">The sections of the plan, as the AI assistant gives them.</param>
        /// <returns>The <see cref="ConstructionPlan"/>, or one validation error per problem of the plan.</returns>
        public static ErrorOr<ConstructionPlan> Create(IReadOnlyList<PlanSection> sections)
        {
            if (sections == null || sections.Count == 0)
            {
                return Error.Validation(description: "Plan: the plan has no section. Give its sections, each with the elements it creates.");
            }

            var repeatedNames = sections.Where(section => !string.IsNullOrWhiteSpace(section?.Name))
                .GroupBy(section => section.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"Plan: several sections are named '{group.Key}'. Give each section its own name.");

            var problems = sections.SelectMany((section, index) => FindProblems(section, index + 1))
                .Concat(repeatedNames)
                .Select(problem => Error.Validation(description: problem))
                .ToList();

            return problems.Count == 0 ? new ConstructionPlan([.. sections.Select(Normalize)]) : problems;
        }

        /// <summary>
        /// Compares the plan with a model: a planned element is in the model when an element of its metaclass has its name, or
        /// a qualified name that ends with the given one.
        /// </summary>
        /// <param name="elements">The elements of the model.</param>
        /// <returns>The <see cref="PlanProgress"/>: the sections that are done and the ones that are left.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is <c>null</c>.</exception>
        public PlanProgress GetProgress(IReadOnlyList<IElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            var namedElements = elements.Select(ReadName).Where(name => name != null).ToLookup(name => name.Path[^1], StringComparer.Ordinal);

            var sections = this.Sections
                .Select(section => (section.Name, MissingElements: section.Elements.Where(element => !IsBuilt(element, namedElements)).ToList()))
                .ToList();

            var remainingSections = new List<RemainingSection>();
            var listedElementCount = 0;

            foreach (var (name, missingElements) in sections.Where(section => section.MissingElements.Count > 0))
            {
                var listedElements = missingElements.Take(MaximumListedElementCount - listedElementCount).ToList();
                listedElementCount += listedElements.Count;
                remainingSections.Add(new RemainingSection(name, missingElements.Count, listedElements));
            }

            var reminder = remainingSections.Count == 0
                ? "Every element of the plan is in the model."
                : $"Sections left: {remainingSections.Count} of {this.Sections.Count}; missing elements: {remainingSections.Sum(section => section.MissingElementCount)}. "
                    + "Go on with the next batch until no section is left.";

            return new PlanProgress([.. sections.Where(section => section.MissingElements.Count == 0).Select(section => section.Name)], remainingSections, reminder);
        }

        /// <summary>
        /// Finds the metaclass that apply_changes creates with the given name, whatever its case.
        /// </summary>
        /// <param name="type">The name given by the AI assistant.</param>
        /// <returns>The name of the metaclass as SysML2.NET spells it, or <c>null</c> when apply_changes creates no such metaclass.</returns>
        private static string FindMetaclassName(string type)
        {
            return ModelChangeBatch.CreatableMetaclassNames.FirstOrDefault(name => string.Equals(name, type.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds the problem of a planned element.
        /// </summary>
        /// <param name="element">The <see cref="PlannedElement"/>, or <c>null</c>.</param>
        /// <param name="location">The place of the element in the plan, which starts the problem.</param>
        /// <returns>The problem, or <c>null</c> when the element is valid.</returns>
        private static string FindProblem(PlannedElement element, string location)
        {
            return element switch
            {
                _ when string.IsNullOrWhiteSpace(element?.Name) => $"{location}: the element has no name.",
                _ when string.IsNullOrWhiteSpace(element.Type) => $"{location} ('{element.Name.Trim()}'): the element has no type. "
                    + "Give its metaclass, as in the payload of the change that creates it (PartUsage, RequirementUsage...).",
                _ when FindMetaclassName(element.Type) == null => $"{location} ('{element.Name.Trim()}'): '{element.Type}' is not a metaclass that apply_changes creates, "
                    + "for example Package, PartDefinition, PartUsage, AttributeUsage or RequirementUsage.",
                _ => null
            };
        }

        /// <summary>
        /// Finds the problems of a section of the plan: a missing name, no element, or invalid elements.
        /// </summary>
        /// <param name="section">The <see cref="PlanSection"/>, or <c>null</c>.</param>
        /// <param name="sectionNumber">The number of the section in the plan, from 1.</param>
        /// <returns>The problems of the section, empty when it is valid.</returns>
        private static IEnumerable<string> FindProblems(PlanSection section, int sectionNumber)
        {
            var hasName = !string.IsNullOrWhiteSpace(section?.Name);
            var location = hasName ? $"Plan, section {sectionNumber} ('{section.Name.Trim()}')" : $"Plan, section {sectionNumber}";
            string[] nameProblems = hasName ? [] : [$"{location}: the section has no name."];

            IEnumerable<string> elementProblems = section?.Elements is { Count: > 0 } elements
                ? elements.Select((element, index) => FindProblem(element, $"{location}, element {index + 1}")).Where(problem => problem != null)
                : [$"{location}: the section lists no element. List the elements it creates, each by its name and its type."];

            return nameProblems.Concat(elementProblems);
        }

        /// <summary>
        /// Tells whether the model holds a planned element: an element of its metaclass whose name, or the end of whose
        /// qualified name, is the planned one.
        /// </summary>
        /// <param name="element">The <see cref="PlannedElement"/>.</param>
        /// <param name="namedElements">The names of the elements of the model, indexed by their last name.</param>
        /// <returns><c>true</c> when the model holds the element.</returns>
        private static bool IsBuilt(PlannedElement element, ILookup<string, ElementName> namedElements)
        {
            var path = SplitName(element.Name);

            return namedElements[path[^1]].Any(named => named.Metaclass == element.Type && named.Path.Length >= path.Length
                && named.Path[^path.Length..].SequenceEqual(path, StringComparer.Ordinal));
        }

        /// <summary>
        /// Trims the names of a valid section and spells the metaclasses of its elements as SysML2.NET does.
        /// </summary>
        /// <param name="section">The valid <see cref="PlanSection"/>.</param>
        /// <returns>The normalized <see cref="PlanSection"/>.</returns>
        private static PlanSection Normalize(PlanSection section)
        {
            return new PlanSection
            {
                Name = section.Name.Trim(),
                Elements = [.. section.Elements.Select(element => new PlannedElement { Name = element.Name.Trim(), Type = FindMetaclassName(element.Type) })]
            };
        }

        /// <summary>
        /// Reads the name of an element of the model, and the names of its qualified name.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/>.</param>
        /// <returns>
        /// The <see cref="ElementName"/>, or <c>null</c> when the element has no name or belongs to an incomplete part of the
        /// model, which the validation reports.
        /// </returns>
        private static ElementName ReadName(IElement element)
        {
            try
            {
                var name = element.name;

                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                // The qualified name is missing when an owner has no name: the element can then be found by its name only.
                string[] ownerNames = element.qualifiedName == null ? [] : SplitName(element.qualifiedName)[..^1];

                return new ElementName(element.GetType().Name, [.. ownerNames, name]);
            }
            catch (IncompleteModelException)
            {
                return null;
            }
        }

        /// <summary>
        /// Splits a name or a qualified name into its names, without the quotes of the names that are not basic.
        /// </summary>
        /// <param name="name">The name or the qualified name, for example <c>camera::'focal length'</c>.</param>
        /// <returns>The names, for example <c>camera</c> and <c>focal length</c>.</returns>
        private static string[] SplitName(string name)
        {
            return [.. name.Split(NameSeparator).Select(segment => segment.Trim()).Select(segment => segment is ['\'', .., '\''] ? segment[1..^1] : segment)];
        }

        /// <summary>
        /// The metaclass and the names of the qualified name of an element of the model.
        /// </summary>
        /// <param name="Metaclass">The metaclass of the element, for example <c>PartUsage</c>.</param>
        /// <param name="Path">The names of the qualified name of the element, its own name last.</param>
        private sealed record ElementName(string Metaclass, string[] Path);
    }
}
