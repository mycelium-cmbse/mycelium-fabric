// ------------------------------------------------------------------------------------------------
//  <copyright file="SearchFilter.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    /// <summary>
    /// Represents the condition that the records of a read must satisfy.
    /// </summary>
    /// <remarks>
    /// This is the <c>Constraint</c> of Systems Modeling API and Services 1.0 §7.1.4 (pp. 36–39): a
    /// <see cref="PrimitiveSearchFilter"/> is the property-operator-value tuple the specification
    /// defines, and a <see cref="CompositeSearchFilter"/> joins two or more of them. It is declared here
    /// rather than taken from <c>SysML2.NET.PIM.DTO</c> because <c>PrimitiveConstraint</c> does not
    /// specialize <c>Constraint</c> there, which leaves that tree unable to hold a leaf.
    /// </remarks>
    public abstract class SearchFilter
    {
    }
}
