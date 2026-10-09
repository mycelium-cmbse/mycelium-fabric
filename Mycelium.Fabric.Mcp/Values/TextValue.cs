// ------------------------------------------------------------------------------------------------
//  <copyright file="TextValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    /// <summary>
    /// A text, for example <c>"S-band"</c>.
    /// </summary>
    /// <param name="Text">The text.</param>
    public sealed record TextValue(string Text) : ModelValue
    {
        /// <inheritdoc/>
        public override string KindName => "a text";

        /// <summary>
        /// Writes the text between double quotes, as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The text, for example <c>"S-band"</c>.</returns>
        public override string ToString()
        {
            return $"\"{this.Text}\"";
        }
    }
}
