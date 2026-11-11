// ------------------------------------------------------------------------------------------------
//  <copyright file="CombinationCheck.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The check of a requirement on one combination of a trade-off.
    /// </summary>
    /// <param name="Check">The <see cref="RequirementCheck"/>, as <c>check_requirements</c> gives it for the model.</param>
    /// <param name="ReadPartIds">
    /// The <c>Id</c> of the parts whose content the combination decides and that the check has read: the check is the same
    /// for every combination in which these parts hold the same thing, and it does not depend on the choices when it has
    /// read none.
    /// </param>
    public sealed record CombinationCheck(RequirementCheck Check, IReadOnlySet<Guid> ReadPartIds);
}
