// ------------------------------------------------------------------------------------------------
//  <copyright file="PartValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// A part (or another feature that is not an attribute) of the model, for example the part that satisfies a requirement,
    /// whose features an expression can navigate, as <c>subj.camera.mass</c> does.
    /// </summary>
    /// <param name="Feature">The feature of the model.</param>
    public sealed record PartValue(IFeature Feature) : ModelValue
    {
        /// <inheritdoc/>
        public override string KindName => "a part";

        /// <summary>
        /// Writes the qualified name of the part.
        /// </summary>
        /// <returns>The qualified name, for example <c>EOSat1::Architecture::eosat1</c>.</returns>
        public override string ToString()
        {
            return this.Feature.qualifiedName ?? this.Feature.DeclaredName;
        }
    }
}
