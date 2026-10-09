// ------------------------------------------------------------------------------------------------
//  <copyright file="Term.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// An expression of the model, read by the <see cref="TermReader"/> in a form that the server can evaluate and write:
    /// a <see cref="LiteralTerm"/>, a <see cref="ReferenceTerm"/>, a <see cref="NavigationTerm"/>, a <see cref="QuantityTerm"/>,
    /// an <see cref="OperationTerm"/> or an <see cref="InvocationTerm"/>.
    /// </summary>
    public abstract record Term
    {
        /// <summary>
        /// The precedence of a term that is not an operation, higher than the one of any operator, so that it never needs
        /// parentheses.
        /// </summary>
        public const int HighestPrecedence = 100;

        /// <summary>
        /// Gets how tightly the term binds its operands when it is written, as in the SysML v2 textual notation: <c>*</c>
        /// binds more tightly than <c>+</c>, which binds more tightly than <c>&lt;=</c>.
        /// </summary>
        public virtual int Precedence => HighestPrecedence;

        /// <summary>
        /// Evaluates the term.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The value of the term, or an error that tells why it cannot be evaluated.</returns>
        public abstract ErrorOr<ModelValue> Evaluate(IEvaluationContext context);

        /// <summary>
        /// Writes a term that is an operand of this one, between parentheses when it binds less tightly than this term.
        /// </summary>
        /// <param name="operand">The operand.</param>
        /// <returns>The operand as text.</returns>
        protected string Enclose(Term operand)
        {
            return operand.Precedence <= this.Precedence && operand.Precedence < HighestPrecedence ? $"({operand})" : operand.ToString();
        }
    }
}
