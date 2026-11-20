// ------------------------------------------------------------------------------------------------
//  <copyright file="DependencyExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Linq;

    using SysML2.NET.Core.POCO.Root.Dependencies;
    using SysML2.NET.Core.POCO.Systems.Metadata;

    /// <summary>
    /// Extension methods that read, from the <see cref="IDependency"/>s of a SysML v2 model, the information used by the
    /// MCP tools.
    /// </summary>
    public static class DependencyExtensions
    {
        /// <summary>
        /// The qualified name of the metadata definition of the standard library that marks a dependency as a refinement.
        /// </summary>
        public const string RefinementName = "ModelingMetadata::Refinement";

        /// <param name="dependency">The <see cref="IDependency"/> to read.</param>
        extension(IDependency dependency)
        {
            /// <summary>
            /// Tells whether the dependency is a refinement, as <c>#refinement dependency 'FLR-R001' to 'HLR-R001';</c>: it is
            /// annotated by a metadata usage whose metadata definition is <c>ModelingMetadata::Refinement</c>. Without the
            /// standard library, the metadata definition is unknown and the dependency is not taken for a refinement.
            /// </summary>
            /// <returns><c>true</c> when the dependency is a refinement.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="dependency"/> is <c>null</c>.</exception>
            public bool IsRefinement()
            {
                ArgumentNullException.ThrowIfNull(dependency);

                return dependency.ownedAnnotation
                    .Select(annotation => annotation.annotatingElement)
                    .OfType<IMetadataUsage>()
                    .Any(metadata => metadata.metadataDefinition?.qualifiedName == RefinementName);
            }
        }
    }
}
