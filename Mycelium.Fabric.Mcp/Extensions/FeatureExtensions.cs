// ------------------------------------------------------------------------------------------------
//  <copyright file="FeatureExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Extension methods that read, from the <see cref="IFeature"/>s of a SysML v2 model, the information used by the MCP
    /// tools.
    /// </summary>
    public static class FeatureExtensions
    {
        /// <param name="feature">The <see cref="IFeature"/> to read.</param>
        extension(IFeature feature)
        {
            /// <summary>
            /// Gets the parts next to the feature, outside it: the other parts that its package, or the root namespace, owns
            /// directly. A feature of a type, such as a part inside the system, has none, since the parts next to it belong
            /// to the same owner.
            /// </summary>
            /// <returns>The parts next to the feature, empty when its owner is a type or when it has no owner.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<IPartUsage> GetPartsBeside()
            {
                ArgumentNullException.ThrowIfNull(feature);

                return feature.owningNamespace is { } owner and not IType
                    ? [.. (owner.ownedElement ?? []).OfType<IPartUsage>().Where(part => part.Id != feature.Id)]
                    : [];
            }
        }
    }
}
