// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Multiplicities.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.Multiplicities;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Actions;
    using SysML2.NET.Core.DTO.Systems.Connections;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.DTO.Systems.States;
    using SysML2.NET.Core.Root.Namespaces;

    /// <content>
    /// The multiplicity of a usage, which builds, as the textual notation does, <c>[4]</c> in <c>part wheels : Wheel[4];</c>:
    /// a <c>MultiplicityRange</c> owned by the usage, whose bounds are a <c>LiteralInteger</c> or a <c>LiteralInfinity</c>
    /// (<c>*</c>).
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The text of a multiplicity: an upper bound alone (<c>4</c>, <c>*</c>), or a lower bound and an upper bound
        /// (<c>1..4</c>, <c>0..*</c>), between optional brackets.
        /// </summary>
        private static readonly Regex MultiplicityPattern = new(@"^\[?\s*(?:(?<lower>\d+)\s*\.\.\s*)?(?<upper>\d+|\*)\s*\]?$", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Checks the multiplicity of a payload before it is built.
        /// </summary>
        /// <param name="element">The created or updated element.</param>
        /// <param name="multiplicity">The text of the multiplicity.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the element cannot have a multiplicity, or when the text is not a multiplicity.
        /// </exception>
        private static void CheckMultiplicity(IElement element, string multiplicity)
        {
            if (element is not IUsage || element is IConnectorAsUsage or ITransitionUsage or IPerformActionUsage or ISatisfyRequirementUsage)
            {
                throw new InvalidChangeException($"Only a usage, for example a part, a port, an item or an attribute, has a multiplicity, not the {Describe(element)}.");
            }

            ParseMultiplicity(multiplicity);
        }

        /// <summary>
        /// Reads the bounds of a multiplicity.
        /// </summary>
        /// <param name="multiplicity">The text of the multiplicity, for example <c>4</c>, <c>1..4</c> or <c>0..*</c>.</param>
        /// <returns>
        /// The lower bound, or <c>null</c> when only the upper bound is given, and the upper bound, or <c>null</c> for <c>*</c>.
        /// </returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the text is not a multiplicity, or when its lower bound is greater than its upper bound.
        /// </exception>
        private static (int? Lower, int? Upper) ParseMultiplicity(string multiplicity)
        {
            var match = MultiplicityPattern.Match(multiplicity ?? string.Empty);

            if (!match.Success || !TryParseBound(match.Groups["lower"], out var lower) || !TryParseBound(match.Groups["upper"], out var upper))
            {
                throw new InvalidChangeException($"The multiplicity '{multiplicity}' is not valid: write a number of units, such as '4', or a range, such as '1..4' or '0..*'.");
            }

            if (lower > upper)
            {
                throw new InvalidChangeException($"The multiplicity '{multiplicity}' is not valid: its lower bound is greater than its upper bound.");
            }

            return (lower, upper);
        }

        /// <summary>
        /// Reads a bound of a multiplicity.
        /// </summary>
        /// <param name="group">The group of the bound in <see cref="MultiplicityPattern"/>.</param>
        /// <param name="bound">The bound, or <c>null</c> when the group is empty or is <c>*</c>.</param>
        /// <returns><c>false</c> when the bound is too large.</returns>
        private static bool TryParseBound(Group group, out int? bound)
        {
            bound = null;

            if (!group.Success || group.Value == "*")
            {
                return true;
            }

            var isNumber = int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number);
            bound = number;

            return isNumber;
        }

        /// <summary>
        /// Gives a usage a multiplicity, as <c>[4]</c> or <c>[0..*]</c> does: an owned <c>MultiplicityRange</c> whose bounds,
        /// the lower one first when it is given, are owned <c>LiteralInteger</c>s, or a <c>LiteralInfinity</c> for <c>*</c>.
        /// The current multiplicity of the usage is replaced.
        /// </summary>
        /// <param name="usage">The usage.</param>
        /// <param name="multiplicity">The text of the multiplicity, already checked by <see cref="CheckMultiplicity"/>.</param>
        private void SetMultiplicity(IElement usage, string multiplicity)
        {
            var (lower, upper) = ParseMultiplicity(multiplicity);

            var currentMemberships = this.GetElements(usage.OwnedRelationship)
                .OfType<IRelationship>()
                .Where(relationship => this.GetElements(relationship.OwnedRelatedElement).OfType<IMultiplicity>().Any())
                .ToList();

            foreach (var membership in currentMemberships)
            {
                this.RemoveTree(membership);
            }

            var range = this.Add(new MultiplicityRange());
            this.AddOwnedRelationship(usage, new OwningMembership { Visibility = VisibilityKind.Public }, range);

            if (lower != null)
            {
                this.AddOwnedRelationship(range, new OwningMembership { Visibility = VisibilityKind.Public }, this.Add(new LiteralInteger { Value = lower.Value }));
            }

            IElement upperBound = upper == null ? new LiteralInfinity() : new LiteralInteger { Value = upper.Value };
            this.AddOwnedRelationship(range, new OwningMembership { Visibility = VisibilityKind.Public }, this.Add(upperBound));
        }
    }
}
