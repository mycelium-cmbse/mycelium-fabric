// ------------------------------------------------------------------------------------------------
//  <copyright file="PayloadValueJsonConverter.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads a <see cref="PayloadValue"/> from a JSON number, Boolean or string, and writes it back the same way.
    /// </summary>
    public sealed class PayloadValueJsonConverter : JsonConverter<PayloadValue>
    {
        /// <summary>
        /// Reads a value from a JSON number, Boolean or string.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">The serializer options.</param>
        /// <returns>The <see cref="PayloadValue"/>.</returns>
        /// <exception cref="JsonException">Thrown when the JSON value is not a number, a Boolean or a string.</exception>
        public override PayloadValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => new PayloadValue { Number = reader.GetDouble() },
                JsonTokenType.True or JsonTokenType.False => new PayloadValue { Boolean = reader.GetBoolean() },
                JsonTokenType.String => new PayloadValue { Text = reader.GetString() },
                _ => throw new JsonException("A value must be a number, a Boolean or a text.")
            };
        }

        /// <summary>
        /// Writes a value as a JSON number, Boolean or string.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The serializer options.</param>
        public override void Write(Utf8JsonWriter writer, PayloadValue value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            if (value.Number != null)
            {
                writer.WriteNumberValue(value.Number.Value);
            }
            else if (value.Boolean != null)
            {
                writer.WriteBooleanValue(value.Boolean.Value);
            }
            else
            {
                writer.WriteStringValue(value.Text);
            }
        }
    }
}
