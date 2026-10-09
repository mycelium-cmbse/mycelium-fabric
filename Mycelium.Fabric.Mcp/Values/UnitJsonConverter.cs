// ------------------------------------------------------------------------------------------------
//  <copyright file="UnitJsonConverter.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Writes a <see cref="Unit"/> as its symbol in JSON, for example <c>"kg"</c>, and reads it back from its symbol.
    /// </summary>
    public sealed class UnitJsonConverter : JsonConverter<Unit>
    {
        /// <summary>
        /// Reads a unit from its symbol.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">The serializer options.</param>
        /// <returns>The <see cref="Unit"/>.</returns>
        /// <exception cref="JsonException">Thrown when the symbol is not a known unit.</exception>
        public override Unit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var symbol = reader.GetString();

            return UnitCatalog.TryParse(symbol, out var unit) ? unit : throw new JsonException($"The unit '{symbol}' is not known.");
        }

        /// <summary>
        /// Writes the symbol of a unit.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The unit.</param>
        /// <param name="options">The serializer options.</param>
        public override void Write(Utf8JsonWriter writer, Unit value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStringValue(value.Symbol);
        }
    }
}
