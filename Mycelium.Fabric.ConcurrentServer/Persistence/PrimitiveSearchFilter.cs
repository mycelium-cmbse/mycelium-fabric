// ------------------------------------------------------------------------------------------------
//  <copyright file="PrimitiveSearchFilter.cs" company="Starion Group S.A.">
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
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Narrows a read to the records whose property stands in the given relation to the given values.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.1.4 (pp. 36–39) models a simple condition as the
    /// property-operator-value tuple reproduced here.
    /// </remarks>
    public class PrimitiveSearchFilter : SearchFilter
    {
        /// <summary>
        /// Gets or sets the name of the property that is constrained.
        /// </summary>
        /// <remarks>
        /// The name is the one the model declares, as in <c>owningProject</c> or <c>head</c>.
        /// </remarks>
        public string Property { get; set; }

        /// <summary>
        /// Gets or sets the relation the property stands in to the values.
        /// </summary>
        public Operator Operator { get; set; }

        /// <summary>
        /// Gets or sets the values the property is compared against.
        /// </summary>
        /// <remarks>
        /// Several values are carried because the <c>in</c> operator compares against a set; every other
        /// operator reads the first.
        /// </remarks>
        public IReadOnlyList<ConstraintValue> Value { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether the condition is negated.
        /// </summary>
        /// <remarks>
        /// The specification declares no inequality operator, so <c>not equal to</c> is
        /// <see cref="SysML2.NET.PIM.Operator.equalto"/> with this flag set.
        /// </remarks>
        public bool Inverse { get; set; }
    }
}
