// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementChange.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;

    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// A requirement whose status or constrained value a batch of changes would change, for example from satisfied to not
    /// satisfied, or from 150.96 to 154.68 kg against a limit of 150 kg.
    /// </summary>
    /// <param name="Id">The identifier of the requirement usage.</param>
    /// <param name="ReqId">The identifier of the requirement in the specification, for example <c>REQ-SYS-001</c>.</param>
    /// <param name="Name">The name of the requirement usage.</param>
    /// <param name="SatisfiedBy">
    /// The qualified name of the element that satisfies the requirement, as in check_requirements: a requirement satisfied by
    /// several elements changes for each of them; <c>null</c> when nothing satisfies it.
    /// </param>
    /// <param name="StatusBefore">The status before the batch, or <c>null</c> when the batch creates the requirement.</param>
    /// <param name="StatusAfter">The status after the batch, or <c>null</c> when the batch deletes the requirement.</param>
    /// <param name="Value">
    /// The constrained value with its unit, before and after the batch when it changes (<c>150.96 → 154.68 kg</c>), or
    /// <c>null</c> when the requirement does not read a single attribute.
    /// </param>
    /// <param name="GapChange">
    /// How much the distance to the limit changes, margin included: negative when the value moves towards or beyond the limit,
    /// or <c>null</c> when the gap is not known before and after the batch.
    /// </param>
    public sealed record RequirementChange(Guid Id, string ReqId, string Name, string SatisfiedBy, RequirementStatus? StatusBefore, RequirementStatus? StatusAfter,
        string Value = null, double? GapChange = null);
}
