// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffOpenPart.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// A part whose content a trade-off decides, or equipment that it may add: what it holds for each option of its choice.
    /// </summary>
    /// <param name="Part">The part of the model, or <c>null</c> for equipment to add under the root.</param>
    /// <param name="Name">The name of the choice.</param>
    /// <param name="Contents">The <see cref="TradeOffPartContent"/> of each option of the choice, in the order of the options.</param>
    public sealed record TradeOffOpenPart(IPartUsage Part, string Name, IReadOnlyList<TradeOffPartContent> Contents);
}
