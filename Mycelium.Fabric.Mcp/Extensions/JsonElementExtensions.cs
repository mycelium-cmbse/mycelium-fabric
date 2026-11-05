// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonElementExtensions.cs" company="Starion Group S.A.">
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
    using System.Text.Json;

    /// <summary>
    /// Extension methods that read a <see cref="JsonElement"/>: a value given to a tool, or a part of the JSON schema of a tool.
    /// </summary>
    public static class JsonElementExtensions
    {
        /// <param name="element">The <see cref="JsonElement"/> to read.</param>
        extension(JsonElement element)
        {
            /// <summary>
            /// Tells whether the element is equal to another value, two texts being compared without case, as the server reads
            /// the values that a schema lists.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns><c>true</c> when the values are equal.</returns>
            public bool EqualsIgnoringCase(JsonElement other)
            {
                return element.ValueKind == JsonValueKind.String && other.ValueKind == JsonValueKind.String
                    ? string.Equals(element.GetString(), other.GetString(), StringComparison.OrdinalIgnoreCase)
                    : JsonElement.DeepEquals(element, other);
            }

            /// <summary>
            /// Gets the names of the types that the element, the <c>type</c> keyword of a schema, gives.
            /// </summary>
            /// <returns>The names of the types: one for a type, such as <c>"string"</c>, several for an array of types.</returns>
            public IEnumerable<string> GetTypeNames()
            {
                return element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().Select(typeName => typeName.GetString()) : [element.GetString()];
            }

            /// <summary>
            /// Tells whether the element, a value, has one of the types that the <c>type</c> keyword of a schema gives.
            /// </summary>
            /// <param name="type">The <c>type</c> keyword of the schema: a type, such as <c>"string"</c>, or an array of types.</param>
            /// <returns><c>true</c> when the value has one of the types; a type that JSON Schema does not define accepts any value.</returns>
            public bool HasSchemaType(JsonElement type)
            {
                var valueKind = element.ValueKind;
                var isInteger = valueKind == JsonValueKind.Number && element.TryGetInt64(out _);

                return type.GetTypeNames().Any(typeName => typeName switch
                {
                    "string" => valueKind == JsonValueKind.String,
                    "number" => valueKind == JsonValueKind.Number,
                    "integer" => isInteger,
                    "boolean" => valueKind is JsonValueKind.True or JsonValueKind.False,
                    "array" => valueKind == JsonValueKind.Array,
                    "object" => valueKind == JsonValueKind.Object,
                    "null" => valueKind == JsonValueKind.Null,
                    _ => true
                });
            }

            /// <summary>
            /// Tells whether the element is a strict schema: an object that refuses the properties that it does not declare.
            /// </summary>
            /// <returns><c>true</c> when the element is a strict schema.</returns>
            public bool IsStrictSchema()
            {
                return element.ValueKind == JsonValueKind.Object && element.TryGetProperty("additionalProperties", out var additionalProperties)
                    && additionalProperties.ValueKind == JsonValueKind.False;
            }

            /// <summary>
            /// Writes the element as in JSON, a text between double quotes, shortened when it is long.
            /// </summary>
            /// <param name="maximumLength">The maximum length of the text, without the ellipsis that ends a shortened text.</param>
            /// <returns>The value, for example <c>"input"</c> or <c>4</c>.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumLength"/> is negative.</exception>
            public string ToShortText(int maximumLength)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);

                var text = element.ValueKind == JsonValueKind.String ? $"\"{element.GetString()}\"" : element.GetRawText();

                return text.Length <= maximumLength ? text : $"{text[..maximumLength]}…";
            }
        }
    }
}
