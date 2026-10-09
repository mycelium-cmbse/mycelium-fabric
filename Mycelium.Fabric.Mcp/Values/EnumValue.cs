// ------------------------------------------------------------------------------------------------
//  <copyright file="EnumValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    /// <summary>
    /// A value of an enumeration, for example <c>sunSynchronous</c> of <c>enum def OrbitKind { enum sunSynchronous; enum polar; }</c>.
    /// </summary>
    /// <param name="Name">The name of the value, for example <c>sunSynchronous</c>.</param>
    public sealed record EnumValue(string Name) : ModelValue
    {
        /// <inheritdoc/>
        public override string KindName => "an enumeration value";

        /// <summary>
        /// Writes the name of the value.
        /// </summary>
        /// <returns>The name, for example <c>sunSynchronous</c>.</returns>
        public override string ToString()
        {
            return this.Name;
        }
    }
}
