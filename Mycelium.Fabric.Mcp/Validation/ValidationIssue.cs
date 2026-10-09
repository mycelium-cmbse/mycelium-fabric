// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationIssue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation
{
    using System;

    /// <summary>
    /// An issue that an <see cref="IValidationRule"/> has found on an element of the model.
    /// </summary>
    /// <param name="Rule">The name of the rule, for example <c>broken-reference</c>.</param>
    /// <param name="Severity">The <see cref="ValidationSeverity"/> of the rule.</param>
    /// <param name="ElementId">The <c>Id</c> of the element that has the issue.</param>
    /// <param name="Element">
    /// The qualified name of the element, or its metaclass and the element that owns it when it has no name.
    /// </param>
    /// <param name="Message">What is wrong with the element.</param>
    public sealed record ValidationIssue(string Rule, ValidationSeverity Severity, Guid ElementId, string Element, string Message);
}
