// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintUsageExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Linq;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Expressions;

    using SysML2.NET.Core.POCO.Kernel.Functions;
    using SysML2.NET.Core.POCO.Systems.Constraints;

    /// <summary>
    /// Extension methods that read, from the <see cref="IConstraintUsage"/>s of a SysML v2 model, the information used by the
    /// MCP tools.
    /// </summary>
    public static class ConstraintUsageExtensions
    {
        /// <param name="constraint">The <see cref="IConstraintUsage"/> to read.</param>
        extension(IConstraintUsage constraint)
        {
            /// <summary>
            /// Reads the expression of the constraint (its result expression) as a <see cref="Term"/> that the server can
            /// evaluate, for example <c>subj.mass * 1.2 &lt;= 150 [kg] and subj.isRedundant</c>.
            /// </summary>
            /// <returns>The <see cref="Term"/>, or an error that tells why the expression cannot be read.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="constraint"/> is <c>null</c>.
            /// </exception>
            public ErrorOr<Term> GetTerm()
            {
                ArgumentNullException.ThrowIfNull(constraint);

                var expression = (constraint.OwnedRelationship ?? [])
                    .OfType<IResultExpressionMembership>()
                    .Select(membership => membership.ownedResultExpression)
                    .FirstOrDefault();

                return expression == null ? Error.Validation(description: "The constraint has no expression.") : TermReader.Read(expression);
            }
        }
    }
}
