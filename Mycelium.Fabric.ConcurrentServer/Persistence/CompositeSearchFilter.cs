// ------------------------------------------------------------------------------------------------
//  <copyright file="CompositeSearchFilter.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System.Collections.Generic;

    using SysML2.NET.PIM;

    /// <summary>
    /// Narrows a read by joining two or more conditions with a logical operator.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.1.4 (pp. 36–39) composes conditions this way, and allows
    /// a composite to hold composites, so the filter is a tree of arbitrary depth.
    /// </remarks>
    public class CompositeSearchFilter : SearchFilter
    {
        /// <summary>
        /// Gets or sets the conditions that are joined.
        /// </summary>
        /// <remarks>
        /// The specification requires at least two.
        /// </remarks>
        public IReadOnlyList<SearchFilter> Filters { get; set; } = [];

        /// <summary>
        /// Gets or sets the logical operator the conditions are joined with.
        /// </summary>
        public JoinOperator Operator { get; set; }
    }
}
