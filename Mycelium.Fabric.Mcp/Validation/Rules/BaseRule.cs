// ------------------------------------------------------------------------------------------------
//  <copyright file="BaseRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The base class of the <see cref="IValidationRule"/>s: it holds the name, the severity and the description that each rule
    /// gives, and checks the arguments of <see cref="FindProblem"/> before the rule checks the element.
    /// </summary>
    public abstract class BaseRule : IValidationRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseRule"/> class.
        /// </summary>
        /// <param name="name">The name of the rule, in kebab case, for example <c>broken-reference</c>.</param>
        /// <param name="severity">The <see cref="ValidationSeverity"/> of the issues found by the rule.</param>
        /// <param name="description">The description of what the rule checks, in one sentence.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> or <paramref name="description"/> is <c>null</c>, empty or white space.
        /// </exception>
        protected BaseRule(string name, ValidationSeverity severity, string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            this.Name = name;
            this.Severity = severity;
            this.Description = description;
        }

        /// <summary>
        /// Gets the name of the rule, in kebab case, for example <c>broken-reference</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Gets the description of what the rule checks, in one sentence.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Checks an element of the model.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> that tells what the whole model holds.</param>
        /// <returns>The problem of the element, which tells what is wrong, or <c>null</c> when the rule finds none.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            return this.FindProblemCore(element, context);
        }

        /// <summary>
        /// Checks an element of the model, once its arguments are known not to be <c>null</c>.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the element, or <c>null</c> when the rule finds none.</returns>
        protected abstract string FindProblemCore(IElement element, ValidationContext context);
    }
}
