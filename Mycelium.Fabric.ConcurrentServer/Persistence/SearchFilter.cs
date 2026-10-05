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
    using System;

    using SysML2.NET.PIM;
    using SysML2.NET.PSM.DTO;

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
        /// <summary>
        /// Creates the condition that a property equals a value.
        /// </summary>
        /// <param name="property">
        /// The name of the property that is constrained, as the model declares it.
        /// </param>
        /// <param name="value">
        /// The value the property is compared against.
        /// </param>
        /// <returns>
        /// The created <see cref="PrimitiveSearchFilter"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="property"/> is <see langword="null"/> or white space.
        /// </exception>
        public static PrimitiveSearchFilter EqualTo(string property, ConstraintValue value)
        {
            ThrowIfPropertyIsMissing(property);

            return new PrimitiveSearchFilter { Property = property, Operator = Operator.equalto, Value = [value] };
        }

        /// <summary>
        /// Creates the condition that a property is one of a set of values.
        /// </summary>
        /// <param name="property">
        /// The name of the property that is constrained, as the model declares it.
        /// </param>
        /// <param name="values">
        /// The values the property is compared against.
        /// </param>
        /// <returns>
        /// The created <see cref="PrimitiveSearchFilter"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="property"/> is <see langword="null"/> or white space, or when no
        /// value is supplied.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="values"/> is <see langword="null"/>.
        /// </exception>
        public static PrimitiveSearchFilter In(string property, params ConstraintValue[] values)
        {
            ThrowIfPropertyIsMissing(property);
            ArgumentNullException.ThrowIfNull(values);

            return values.Length == 0 ? throw new ArgumentException("A set membership condition is created with at least one value.", nameof(values)) : new PrimitiveSearchFilter { Property = property, Operator = Operator.@in, Value = values };
        }

        /// <summary>
        /// Creates the condition that every one of several conditions holds.
        /// </summary>
        /// <param name="filters">
        /// The conditions that are joined, of which there must be at least two.
        /// </param>
        /// <returns>
        /// The created <see cref="CompositeSearchFilter"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="filters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when fewer than two conditions are supplied.
        /// </exception>
        public static CompositeSearchFilter And(params SearchFilter[] filters)
        {
            return Join(filters, JoinOperator.AND);
        }

        /// <summary>
        /// Creates the condition that at least one of several conditions holds.
        /// </summary>
        /// <param name="filters">
        /// The conditions that are joined, of which there must be at least two.
        /// </param>
        /// <returns>
        /// The created <see cref="CompositeSearchFilter"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="filters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when fewer than two conditions are supplied.
        /// </exception>
        public static CompositeSearchFilter Or(params SearchFilter[] filters)
        {
            return Join(filters, JoinOperator.OR);
        }

        /// <summary>
        /// Negates this condition.
        /// </summary>
        /// <returns>
        /// A negated copy of this condition.
        /// </returns>
        /// <remarks>
        /// The specification declares no inequality operator, so a negated leaf is the leaf itself
        /// carrying <see cref="PrimitiveSearchFilter.Inverse"/>, and a negated composite applies De
        /// Morgan's law to its parts.
        /// </remarks>
        public abstract SearchFilter Negate();

        /// <summary>
        /// Joins several conditions with a logical operator.
        /// </summary>
        /// <param name="filters">
        /// The conditions that are joined.
        /// </param>
        /// <param name="joinOperator">
        /// The logical operator the conditions are joined with.
        /// </param>
        /// <returns>
        /// The created <see cref="CompositeSearchFilter"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="filters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when fewer than two conditions are supplied.
        /// </exception>
        private static CompositeSearchFilter Join(SearchFilter[] filters, JoinOperator joinOperator)
        {
            ArgumentNullException.ThrowIfNull(filters);

            return filters.Length < 2 ? throw new ArgumentException("Systems Modeling API and Services 1.0 §7.1.4 composes at least two conditions.", nameof(filters)) : new CompositeSearchFilter { Filters = filters, Operator = joinOperator };
        }

        /// <summary>
        /// Verifies that a condition names the property it constrains.
        /// </summary>
        /// <param name="property">
        /// The name of the property that is constrained.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="property"/> is <see langword="null"/> or white space.
        /// </exception>
        private static void ThrowIfPropertyIsMissing(string property)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                throw new ArgumentException("A condition is created against a named property.", nameof(property));
            }
        }
    }
}
