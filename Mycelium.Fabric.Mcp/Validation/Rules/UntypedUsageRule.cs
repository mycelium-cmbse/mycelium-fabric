// ------------------------------------------------------------------------------------------------
//  <copyright file="UntypedUsageRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System;
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Items;
    using SysML2.NET.Core.POCO.Systems.Ports;

    /// <summary>
    /// The rule that finds the parts, items and ports that nothing describes: they have no type and no feature of their own.
    /// An untyped part that owns other parts, such as a subsystem, is a usual grouping and is not reported. Attributes are
    /// left out, since the attributes of a model are often untyped.
    /// </summary>
    public class UntypedUsageRule : IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public string Name => "untyped-usage";

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public ValidationSeverity Severity => ValidationSeverity.Warning;

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public string Description => "A part, item or port has no type and no feature, so nothing tells what it is.";

        /// <summary>
        /// Checks that a part, an item or a port has a type or features.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the usage, or <c>null</c> when the element is not an empty untyped usage.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            if (element is not (IItemUsage or IPortUsage) || element is not IFeature feature)
            {
                return null;
            }

            var hasType = (feature.type ?? []).Any(type => type != null);
            var hasFeature = (((IType)feature).ownedFeature ?? []).Count > 0;

            return hasType || hasFeature
                ? null
                : $"The {element.GetType().Name} has no type and no feature: type it with a definition, for example camera : Camera, or give it features.";
        }
    }
}
