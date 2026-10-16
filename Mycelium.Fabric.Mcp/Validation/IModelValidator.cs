// ------------------------------------------------------------------------------------------------
//  <copyright file="IModelValidator.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation
{
    using System;
    using System.Collections.Generic;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Checks a SysML v2 model against a set of <see cref="IValidationRule"/>s.
    /// </summary>
    public interface IModelValidator
    {
        /// <summary>
        /// Gets the rules that the validator checks, the most serious first.
        /// </summary>
        IReadOnlyList<IValidationRule> Rules { get; }

        /// <summary>
        /// Checks each element of a model against each rule, or against the chosen rules only: the other rules are not run.
        /// </summary>
        /// <param name="elements">All the elements of the model.</param>
        /// <param name="ruleNames">The names of the rules to check, or <c>null</c> to check all of them.</param>
        /// <returns>The issues found, the most serious first, then sorted by rule and by element.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="ruleNames"/> holds a name that is not the name of a rule.
        /// </exception>
        IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<IElement> elements, IReadOnlyCollection<string> ruleNames = null);
    }
}
