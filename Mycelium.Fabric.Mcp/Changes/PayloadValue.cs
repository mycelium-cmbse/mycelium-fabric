// ------------------------------------------------------------------------------------------------
//  <copyright file="PayloadValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Globalization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A value given in a payload: a number (<c>38</c>), a Boolean (<c>true</c>) or a text (<c>"S-band"</c>, or the name of
    /// an enumeration value such as <c>sunSynchronous</c>). It is written in JSON as the value itself.
    /// </summary>
    [JsonConverter(typeof(PayloadValueJsonConverter))]
    public readonly record struct PayloadValue
    {
        /// <summary>
        /// Gets the number, or <c>null</c> when the value is not a number.
        /// </summary>
        public double? Number { get; init; }

        /// <summary>
        /// Gets the Boolean, or <c>null</c> when the value is not a Boolean.
        /// </summary>
        public bool? Boolean { get; init; }

        /// <summary>
        /// Gets the text, or <c>null</c> when the value is not a text.
        /// </summary>
        public string Text { get; init; }

        /// <summary>
        /// Creates a numeric value.
        /// </summary>
        /// <param name="number">The number.</param>
        public static implicit operator PayloadValue(double number)
        {
            return new PayloadValue { Number = number };
        }

        /// <summary>
        /// Creates a Boolean value.
        /// </summary>
        /// <param name="boolean">The Boolean.</param>
        public static implicit operator PayloadValue(bool boolean)
        {
            return new PayloadValue { Boolean = boolean };
        }

        /// <summary>
        /// Creates a text value.
        /// </summary>
        /// <param name="text">The text.</param>
        public static implicit operator PayloadValue(string text)
        {
            return new PayloadValue { Text = text };
        }

        /// <summary>
        /// Writes the value as in JSON.
        /// </summary>
        /// <returns>The value, for example <c>38</c>, <c>true</c> or <c>"S-band"</c>.</returns>
        public override string ToString()
        {
            if (this.Number != null)
            {
                return this.Number.Value.ToString(CultureInfo.InvariantCulture);
            }

            return this.Boolean == null ? $"\"{this.Text}\"" : this.Boolean.Value.ToString().ToLowerInvariant();
        }
    }
}
