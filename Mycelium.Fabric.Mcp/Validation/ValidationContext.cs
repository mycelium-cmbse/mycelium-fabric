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

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// What the <see cref="IValidationRule"/>s need to know about the whole model to check one element, computed once per
    /// validation: the types that are specialized, and the requirements that a satisfy link refers to.
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>
        /// The <c>Id</c> of the types that are the general type of a specialization, such as the definition of a typed feature.
        /// </summary>
        private readonly HashSet<Guid> generalTypeIds;

        /// <summary>
        /// The <c>Id</c> of the requirements that a satisfy link, which is not negated, refers to.
        /// </summary>
        private readonly HashSet<Guid> satisfiedRequirementIds;

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

            this.generalTypeIds = elements
                .OfType<ISpecialization>()
                .Where(specialization => specialization.General != null && specialization.General != specialization.Specific)
                .Select(specialization => specialization.General.Id)
                .ToHashSet();

            this.satisfiedRequirementIds = elements
                .OfType<ISatisfyRequirementUsage>()
                .Where(satisfy => !satisfy.IsNegated)
                .Select(satisfy => satisfy.satisfiedRequirement)
                .Where(requirement => requirement != null)
                .Select(requirement => requirement.Id)
                .ToHashSet();
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

            return this.generalTypeIds.Contains(type.Id);
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
                if (this.satisfiedRequirementIds.Contains(current.Id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
