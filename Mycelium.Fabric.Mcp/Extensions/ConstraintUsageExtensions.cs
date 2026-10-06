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
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Requirements;

    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Kernel.Functions;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Constraints;

    /// <summary>
    /// Extension methods that read, from the <see cref="IConstraintUsage"/>s of a SysML v2 model, the information used by the
    /// MCP tools.
    /// </summary>
    public static class ConstraintUsageExtensions
    {
        /// <summary>
        /// The operator of a multiplication, which carries the margin of a constraint.
        /// </summary>
        private const string MultiplicationOperator = "*";

        /// <param name="constraint">The <see cref="IConstraintUsage"/> to read.</param>
        extension(IConstraintUsage constraint)
        {
            /// <summary>
            /// Reads the constraint as an <see cref="AttributeConstraint"/> when its expression has the form that the
            /// <c>SetConstraint</c> change builds: a comparison between an attribute of the subject and a literal limit, each
            /// side optionally multiplied by a literal factor (for example <c>subj.mass * 1.2 &lt;= 150</c>).
            /// </summary>
            /// <returns>The <see cref="AttributeConstraint"/>, or <c>null</c> when the expression has another form.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="constraint"/> is <c>null</c>.
            /// </exception>
            public AttributeConstraint GetAttributeConstraint()
            {
                ArgumentNullException.ThrowIfNull(constraint);

                var expression = (constraint.OwnedRelationship ?? [])
                    .OfType<IResultExpressionMembership>()
                    .Select(membership => membership.ownedResultExpression)
                    .FirstOrDefault();

                if (expression is not IOperatorExpression comparison || !AttributeConstraint.Operators.Contains(comparison.Operator))
                {
                    return null;
                }

                var operands = GetArguments(comparison);

                if (operands.Count != 2)
                {
                    return null;
                }

                var (valueOperand, valueFactor) = SplitFactor(operands[0]);
                var (limitOperand, limitFactor) = SplitFactor(operands[1]);
                var limit = limitOperand?.GetLiteralValue();

                if (valueOperand is not IFeatureChainExpression { targetFeature: { } attribute } chain || limit == null)
                {
                    return null;
                }

                var subject = GetArguments(chain).OfType<IFeatureReferenceExpression>().FirstOrDefault()?.referent;

                return new AttributeConstraint(subject?.DeclaredName, attribute.DeclaredName, valueFactor, comparison.Operator, limit.Value, limitFactor);
            }
        }

        /// <summary>
        /// Gets the arguments of an expression: the values of its input parameters, in order. They are read from the
        /// <see cref="IFeatureValue"/>s of the parameters, since SysML2.NET 0.23.0 leaves the derived <c>argument</c> empty.
        /// </summary>
        /// <param name="expression">The expression, for example the <see cref="IOperatorExpression"/> of <c>a &lt;= b</c>.</param>
        /// <returns>The arguments of the expression, with <c>null</c> for an input parameter that has no value.</returns>
        private static List<IElement> GetArguments(IExpression expression)
        {
            return (expression.input ?? [])
                .Select(parameter => (parameter.OwnedRelationship ?? []).OfType<IFeatureValue>().Select(featureValue => (IElement)featureValue.value).FirstOrDefault())
                .ToList();
        }

        /// <summary>
        /// Splits a multiplication by a literal factor, such as <c>subj.mass * 1.2</c>, into its operand and its factor.
        /// </summary>
        /// <param name="element">The side of a comparison.</param>
        /// <returns>
        /// The operand and the factor of a multiplication by a literal number; the element itself and no factor when it is
        /// not a multiplication; no operand when it is a multiplication by something else than a literal number.
        /// </returns>
        private static (IElement Operand, double? Factor) SplitFactor(IElement element)
        {
            if (element is not IOperatorExpression { Operator: MultiplicationOperator } multiplication)
            {
                return (element, null);
            }

            var factors = GetArguments(multiplication);
            var factor = factors.Count == 2 ? factors[1]?.GetLiteralValue() : null;

            return factor == null ? (null, null) : (factors[0], factor);
        }
    }
}
