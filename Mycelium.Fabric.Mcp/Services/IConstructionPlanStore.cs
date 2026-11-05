// ------------------------------------------------------------------------------------------------
//  <copyright file="IConstructionPlanStore.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using Mycelium.Fabric.Mcp.Plans;

    /// <summary>
    /// Keeps the <see cref="ConstructionPlan"/> that the AI assistant has given to apply_changes, from one batch to the next.
    /// </summary>
    public interface IConstructionPlanStore
    {
        /// <summary>
        /// Gets or sets the plan of the current work, which a new plan replaces; <c>null</c> when no plan has been given.
        /// </summary>
        ConstructionPlan Plan { get; set; }
    }
}
