// ------------------------------------------------------------------------------------------------
//  <copyright file="PlanSection.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Plans
{
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// A section of a <see cref="ConstructionPlan"/>, for example a package of the model, with the elements that it creates.
    /// </summary>
    public sealed record PlanSection
    {
        /// <summary>
        /// Gets the name of the section, unique in the plan.
        /// </summary>
        [Description("The name of the section, unique in the plan, for example the package it builds ('Behaviour').")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the elements that the section creates.
        /// </summary>
        [Description("The elements that the section creates, each by its name and its type.")]
        public IReadOnlyList<PlannedElement> Elements { get; init; }
    }
}
