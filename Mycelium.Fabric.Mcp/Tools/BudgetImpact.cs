// ------------------------------------------------------------------------------------------------
//  <copyright file="BudgetImpact.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// The change that a batch would make to the total of a budget attribute, the mass or the power, over a top-level part of the
    /// model and its sub-parts, as sum_attribute computes it: <c>eosat1::mass 125.8 → 128.9 kg</c>.
    /// </summary>
    /// <param name="Part">The qualified name of the top-level part, for example <c>EOSat1::Architecture::eosat1</c>.</param>
    /// <param name="Attribute">The declared name of the attribute, <c>mass</c> or <c>power</c>.</param>
    /// <param name="Before">The total before the batch, 0 when no sub-part had the attribute.</param>
    /// <param name="After">The total after the batch, 0 when no sub-part has the attribute any more.</param>
    /// <param name="Unit">
    /// The symbol of the unit of the totals, read from the documentation of the attribute when the values have none, or
    /// <c>null</c>.
    /// </param>
    public sealed record BudgetImpact(string Part, string Attribute, double Before, double After, string Unit)
    {
        /// <summary>
        /// Compares the budget totals of the top-level parts, the parts that no definition or usage owns, between two versions
        /// of the model. A part that the batch creates or deletes has no impact: the view shows it among the changed elements.
        /// </summary>
        /// <param name="elementsBefore">The elements of the model before the batch.</param>
        /// <param name="elementsAfter">The elements of the model after the batch.</param>
        /// <returns>The totals that change, ordered by part then attribute.</returns>
        public static List<BudgetImpact> Compare(IReadOnlyList<IElement> elementsBefore, IReadOnlyList<IElement> elementsAfter)
        {
            var partsBefore = elementsBefore.OfType<IPartUsage>().Where(IsTopLevel).ToDictionary(part => part.Id);

            return elementsAfter.OfType<IPartUsage>()
                .Where(IsTopLevel)
                .Where(part => partsBefore.ContainsKey(part.Id))
                .SelectMany(part => CompareTotals(partsBefore[part.Id], part))
                .OrderBy(impact => impact.Part, StringComparer.Ordinal)
                .ThenBy(impact => impact.Attribute, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Compares the budget totals of a part before and after the batch.
        /// </summary>
        /// <param name="before">The part before the batch.</param>
        /// <param name="after">The part after the batch.</param>
        /// <returns>The totals that change.</returns>
        private static IEnumerable<BudgetImpact> CompareTotals(IPartUsage before, IPartUsage after)
        {
            var totalsBefore = PreviewElement.GetBudgetTotals(before);
            var totalsAfter = PreviewElement.GetBudgetTotals(after);

            foreach (var attribute in totalsBefore.Concat(totalsAfter).Select(budget => budget.Attribute).Distinct())
            {
                var totalBefore = totalsBefore.FirstOrDefault(budget => budget.Attribute == attribute);
                var totalAfter = totalsAfter.FirstOrDefault(budget => budget.Attribute == attribute);

                if (!NumberValue.AreEqual(totalBefore.Total, totalAfter.Total))
                {
                    yield return new BudgetImpact(after.qualifiedName, attribute, totalBefore.Total, totalAfter.Total, totalAfter.Unit ?? totalBefore.Unit);
                }
            }
        }

        /// <summary>
        /// Tells whether a part is a top-level part: no definition nor usage owns it, only packages.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns><c>true</c> when the part is a top-level part.</returns>
        private static bool IsTopLevel(IPartUsage part)
        {
            for (var owner = part.owner; owner != null; owner = owner.owner)
            {
                if (owner is IType)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
