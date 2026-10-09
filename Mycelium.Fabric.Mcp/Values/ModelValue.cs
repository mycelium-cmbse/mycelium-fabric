// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    /// <summary>
    /// A value that an expression of the model gives: a <see cref="NumberValue"/> (with or without unit), a
    /// <see cref="BooleanValue"/>, a <see cref="TextValue"/>, an <see cref="EnumValue"/>, a <see cref="PartValue"/> or a
    /// <see cref="SequenceValue"/>.
    /// </summary>
    public abstract record ModelValue
    {
        /// <summary>
        /// Gets the name of the kind of value, used in the messages, for example <c>a number</c>.
        /// </summary>
        public abstract string KindName { get; }
    }
}
