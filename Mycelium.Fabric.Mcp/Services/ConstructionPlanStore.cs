// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionPlanStore.cs" company="Starion Group S.A.">
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
    /// An <see cref="IConstructionPlanStore"/> that keeps the plan in memory: it is lost when the server stops.
    /// </summary>
    public class ConstructionPlanStore : IConstructionPlanStore
    {
        /// <summary>
        /// Gets or sets the plan of the current work, which a new plan replaces; <c>null</c> when no plan has been given.
        /// </summary>
        public ConstructionPlan Plan { get; set; }
    }
}
