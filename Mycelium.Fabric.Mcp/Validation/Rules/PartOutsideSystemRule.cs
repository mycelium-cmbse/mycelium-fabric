// ------------------------------------------------------------------------------------------------
//  <copyright file="PartOutsideSystemRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// The rule that finds the parts of a system left next to each other instead of inside the part that holds the system: a
    /// part that its package owns directly, that has parts or values of its own, next to another part that satisfies
    /// requirements. The budgets and the requirement checks of a part do not count the parts next to it, so a requirement may
    /// be reported as satisfied while it is not, for example when the subsystems are next to the spacecraft.
    /// </summary>
    public class PartOutsideSystemRule : BaseRule
    {
        /// <summary>
        /// The number of parts named in a problem, the others being counted.
        /// </summary>
        private const int NamedPartCount = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartOutsideSystemRule"/> class.
        /// </summary>
        public PartOutsideSystemRule() : base("part-outside-system", ValidationSeverity.Warning,
            "A part with parts or values is next to a part that satisfies requirements, in the same package, instead of inside the part that holds the system, so the budgets and checks of one do not count the other.")
        {
        }

        /// <summary>
        /// Checks that a part that its package owns, and that has parts or values, is not next to a part that satisfies
        /// requirements.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the part, or <c>null</c> when it is not such a part.</returns>
        protected override string FindProblemCore(IElement element, ValidationContext context)
        {
            if (element is not IPartUsage part || !HasPartsOrValues(part))
            {
                return null;
            }

            var satisfyingParts = part.GetPartsBeside().Where(context.SatisfiesRequirements).ToList();

            if (satisfyingParts.Count == 0)
            {
                return null;
            }

            var names = string.Join(", ", satisfyingParts.Take(NamedPartCount).Select(satisfyingPart => $"'{satisfyingPart.DeclaredName}'"));
            names = satisfyingParts.Count > NamedPartCount ? $"{names} and {satisfyingParts.Count - NamedPartCount} more" : names;

            return $"The part is next to {names}, which {(satisfyingParts.Count == 1 ? "satisfies" : "satisfy")} requirements, in the same package instead of inside the part that "
                + "holds the system: the budgets and checks of a part do not count the parts next to it. If they form one system, put its parts inside the part that holds it.";
        }

        /// <summary>
        /// Tells whether a part has parts or numeric values of its own, owned or inherited from its types.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns><c>true</c> when the part has a sub-part or an attribute with a numeric value.</returns>
        private static bool HasPartsOrValues(IPartUsage part)
        {
            return (((IType)part).feature ?? []).OfType<IPartUsage>().Any() || part.GetAttributeUsages().Any(attribute => attribute.GetNumericValue() != null);
        }
    }
}
