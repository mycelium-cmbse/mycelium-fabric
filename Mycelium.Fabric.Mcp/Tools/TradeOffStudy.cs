// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStudy.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// What the view of a trade-off shows: the settings of the request as the server understood them, the best combinations
    /// of the solver, and every combination with its totals, so that the view draws them without computing anything.
    /// </summary>
    /// <param name="Title">The short title of the trade-off, or <c>null</c>.</param>
    /// <param name="Root">The <see cref="TradeOffRoot"/> under which the totals are computed.</param>
    /// <param name="Status">The <see cref="TradeOffStatus"/> of the search.</param>
    /// <param name="Request">The <see cref="TradeOffRequest"/>, that the view sends again with other settings.</param>
    /// <param name="Attributes">The <see cref="TradeOffAttribute"/> of each total.</param>
    /// <param name="Choices">The <see cref="TradeOffStudyChoice"/> of each choice, with all its variants.</param>
    /// <param name="Objectives">The <see cref="TradeOffObjective"/>s, by order of priority.</param>
    /// <param name="Constraints">The <see cref="TradeOffStudyConstraint"/> of each constraint.</param>
    /// <param name="Rules">The text of each rule, for example <c>propulsion.thermal requires fuelTank.small</c>.</param>
    /// <param name="Current">The <see cref="TradeOffCombination"/> of the current state.</param>
    /// <param name="Best">The best <see cref="TradeOffCombination"/>s of the solver, as <c>solve_trade_off</c> returns them.</param>
    /// <param name="Points">The <see cref="TradeOffPoint"/> of every combination, or only those of the best when there are too many.</param>
    /// <param name="PointsComplete">Whether <paramref name="Points"/> holds every combination.</param>
    /// <param name="Conflicts">The constraints and rules that conflict when no combination exists.</param>
    public sealed record TradeOffStudy(string Title, TradeOffRoot Root, TradeOffStatus Status, TradeOffRequest Request, IReadOnlyList<TradeOffAttribute> Attributes,
        IReadOnlyList<TradeOffStudyChoice> Choices, IReadOnlyList<TradeOffObjective> Objectives, IReadOnlyList<TradeOffStudyConstraint> Constraints, IReadOnlyList<string> Rules,
        TradeOffCombination Current, IReadOnlyList<TradeOffCombination> Best, IReadOnlyList<TradeOffPoint> Points, bool PointsComplete, IReadOnlyList<string> Conflicts);
}
