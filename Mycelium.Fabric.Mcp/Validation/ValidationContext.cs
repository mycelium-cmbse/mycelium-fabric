// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationContext.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Requirements;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// What the <see cref="IValidationRule"/>s need to know about the whole model to check one element: the types that are
    /// specialized, the requirements that a satisfy link refers to, the features that satisfy them, and the number of elements of each qualified name. Each one
    /// is computed once per validation, when a rule first needs it, so that the rules that are not checked cost nothing.
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>
        /// The <c>Id</c> of the types that are the general type of a specialization, such as the definition of a typed feature.
        /// </summary>
        private readonly Lazy<HashSet<Guid>> generalTypeIds;

        /// <summary>
        /// The <c>Id</c> of the requirements that a satisfy link, which is not negated, refers to.
        /// </summary>
        private readonly Lazy<HashSet<Guid>> satisfiedRequirementIds;

        /// <summary>
        /// The <c>Id</c> of the features that a satisfy link, which is not negated, names as satisfying its requirement.
        /// </summary>
        private readonly Lazy<HashSet<Guid>> satisfyingFeatureIds;

        /// <summary>
        /// The number of elements of each qualified name, for the elements whose qualified name can be computed.
        /// </summary>
        private readonly Lazy<Dictionary<string, int>> qualifiedNameCounts;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationContext"/> class.
        /// </summary>
        /// <param name="elements">All the elements of the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        public ValidationContext(IReadOnlyList<IElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            this.generalTypeIds = new Lazy<HashSet<Guid>>(() => elements
                .OfType<ISpecialization>()
                .Where(specialization => specialization.General != null && specialization.General != specialization.Specific)
                .Select(specialization => specialization.General.Id)
                .ToHashSet());

            this.satisfiedRequirementIds = new Lazy<HashSet<Guid>>(() => elements
                .OfType<ISatisfyRequirementUsage>()
                .Where(satisfy => !satisfy.IsNegated)
                .Select(satisfy => satisfy.satisfiedRequirement)
                .Where(requirement => requirement != null)
                .Select(requirement => requirement.Id)
                .ToHashSet());

            this.satisfyingFeatureIds = new Lazy<HashSet<Guid>>(() => elements
                .OfType<ISatisfyRequirementUsage>()
                .Where(satisfy => !satisfy.IsNegated)
                .Select(satisfy => satisfy.ResolveSatisfyingFeature())
                .Where(feature => feature != null)
                .Select(feature => feature.Id)
                .ToHashSet());

            this.qualifiedNameCounts = new Lazy<Dictionary<string, int>>(() => elements
                .Select(GetQualifiedName)
                .Where(qualifiedName => qualifiedName != null)
                .CountBy(qualifiedName => qualifiedName, StringComparer.Ordinal)
                .ToDictionary(StringComparer.Ordinal));
        }

        /// <summary>
        /// Gets the qualified name of an element. SysML2.NET cannot compute it when a namespace that contains the element has
        /// a membership whose member is not in the model.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The qualified name of the element, or <c>null</c> when it has none or it cannot be computed.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> is <c>null</c>.
        /// </exception>
        public static string GetQualifiedName(IElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            try
            {
                return element.qualifiedName;
            }
            catch (IncompleteModelException)
            {
                return null;
            }
        }

        /// <summary>
        /// Tells whether a type is specialized: a feature is typed by it, or another type specializes it.
        /// </summary>
        /// <param name="type">The type, for example a definition.</param>
        /// <returns><c>true</c> when the type is the general type of a specialization.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="type"/> is <c>null</c>.
        /// </exception>
        public bool IsSpecialized(IElement type)
        {
            ArgumentNullException.ThrowIfNull(type);

            return this.generalTypeIds.Value.Contains(type.Id);
        }

        /// <summary>
        /// Tells whether a requirement is satisfied: a satisfy link refers to it, or to a requirement that owns it.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <returns><c>true</c> when a satisfy link refers to the requirement or to one of the requirements that own it.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requirement"/> is <c>null</c>.
        /// </exception>
        public bool IsSatisfied(IRequirementUsage requirement)
        {
            ArgumentNullException.ThrowIfNull(requirement);

            for (var current = requirement; current != null; current = current.owner as IRequirementUsage)
            {
                if (this.satisfiedRequirementIds.Value.Contains(current.Id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tells whether an element satisfies a requirement: a satisfy link names it as the satisfying feature.
        /// </summary>
        /// <param name="element">The element, for example the part that holds the system.</param>
        /// <returns><c>true</c> when a satisfy link, which is not negated, names the element.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> is <c>null</c>.
        /// </exception>
        public bool SatisfiesRequirements(IElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            return this.satisfyingFeatureIds.Value.Contains(element.Id);
        }

        /// <summary>
        /// Counts the elements of the model that have a qualified name.
        /// </summary>
        /// <param name="qualifiedName">The qualified name, for example <c>EOSat1::eosat1::camera</c>.</param>
        /// <returns>The number of elements whose qualified name is <paramref name="qualifiedName"/>, 0 when there is none.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="qualifiedName"/> is <c>null</c>.
        /// </exception>
        public int CountElementsNamed(string qualifiedName)
        {
            ArgumentNullException.ThrowIfNull(qualifiedName);

            return this.qualifiedNameCounts.Value.GetValueOrDefault(qualifiedName);
        }
    }
}
