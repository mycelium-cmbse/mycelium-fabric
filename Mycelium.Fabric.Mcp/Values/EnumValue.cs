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
    using System;

    /// <summary>
    /// A value of an enumeration, for example <c>sunSynchronous</c> of <c>enum def OrbitKind { enum sunSynchronous; enum polar; }</c>.
    /// Two values are the same when they have the same qualified name: <c>polar</c> of two enumerations are different values.
    /// </summary>
    /// <param name="Name">The name of the value, for example <c>sunSynchronous</c>.</param>
    /// <param name="QualifiedName">The qualified name of the value, for example <c>EOSat1::OrbitKind::sunSynchronous</c>.</param>
    public sealed record EnumValue(string Name, string QualifiedName) : ModelValue
    {
        /// <summary>
        /// Gets the name of the kind of value, used in the messages: <c>an enumeration value</c>.
        /// </summary>
        public override string KindName => "an enumeration value";

        /// <summary>
        /// Tells whether a text names the value: its name, or the end of its qualified name, such as <c>OrbitKind::polar</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><c>true</c> when the text names the value.</returns>
        public bool IsNamedBy(string text)
        {
            return text == this.Name || text == this.QualifiedName || (this.QualifiedName?.EndsWith($"::{text}", StringComparison.Ordinal) ?? false);
        }

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
