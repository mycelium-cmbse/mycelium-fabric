// ------------------------------------------------------------------------------------------------
//  <copyright file="IValidationRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation
{
    using System;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// A deterministic rule that the <see cref="IModelValidator"/> checks on each element of the model. A rule is added by
    /// registering a new implementation in the dependency injection container, without a new MCP tool.
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule, in kebab case, for example <c>broken-reference</c>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        ValidationSeverity Severity { get; }

        /// <summary>
        /// Gets the description of what the rule checks, in one sentence.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Checks an element of the model.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> that tells what the whole model holds.</param>
        /// <returns>The problem of the element, which tells what is wrong, or <c>null</c> when the rule finds none.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IncompleteModelException">
        /// Thrown when SysML2.NET cannot compute a derived property of the element, because the model is incomplete. The
        /// validator then skips the element for this rule: the broken reference is reported by its own rule.
        /// </exception>
        string FindProblem(IElement element, ValidationContext context);
    }
}
