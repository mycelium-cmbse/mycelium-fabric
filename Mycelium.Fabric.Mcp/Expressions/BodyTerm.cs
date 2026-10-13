// ------------------------------------------------------------------------------------------------
//  <copyright file="BodyTerm.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// The body of a <c>select</c> or a <c>collect</c>, for example <c>{in p; p.mass &gt; 10}</c>: an expression of a
    /// parameter, which the <see cref="InvocationTerm"/> evaluates for each item of a sequence.
    /// </summary>
    /// <param name="Parameter">The parameter of the body, for example <c>p</c>.</param>
    /// <param name="Result">The expression of the body, for example <c>p.mass &gt; 10</c>.</param>
    public sealed record BodyTerm(IFeature Parameter, Term Result) : Term
    {
        /// <summary>
        /// Refuses to evaluate the body alone: it has a value only for an item of a sequence.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>An error.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            return Error.Validation(description: $"The body '{this}' has a value only as the second argument of select or collect.");
        }

        /// <summary>
        /// Evaluates the body for an item, its parameter standing for the item.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <param name="item">The item, for example a part.</param>
        /// <returns>The value of the body, or an error that tells why it cannot be evaluated.</returns>
        public ErrorOr<ModelValue> Apply(IEvaluationContext context, ModelValue item)
        {
            ArgumentNullException.ThrowIfNull(context);

            return this.Result.Evaluate(new BodyEvaluationContext(context, this.Parameter, item));
        }

        /// <summary>
        /// Writes the body as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The body, for example <c>{in p; p.mass &gt; 10}</c>.</returns>
        public override string ToString()
        {
            return $"{{in {this.Parameter.DeclaredName}; {this.Result}}}";
        }
    }
}
