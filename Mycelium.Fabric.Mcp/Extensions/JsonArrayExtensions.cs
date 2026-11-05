// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonArrayExtensions.cs" company="Starion Group S.A.">
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
    using System.Text.Json.Nodes;

    /// <summary>
    /// Extension methods that read a <see cref="JsonArray"/> of the JSON schema of a tool.
    /// </summary>
    public static class JsonArrayExtensions
    {
        /// <param name="types">The <see cref="JsonArray"/> to read: the types of a property, for example <c>["string", "null"]</c>.</param>
        extension(JsonArray types)
        {
            /// <summary>
            /// Gets the types of the property without <c>null</c>, for a property that is required and cannot be <c>null</c>.
            /// </summary>
            /// <returns>The other type, or a new array of the other types when there are several.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is <c>null</c>.</exception>
            public JsonNode GetTypesWithoutNull()
            {
                ArgumentNullException.ThrowIfNull(types);

                var otherTypes = types.Select(type => type.GetValue<string>()).Where(type => type != "null").ToList();

                return otherTypes.Count == 1 ? JsonValue.Create(otherTypes[0]) : new JsonArray([.. otherTypes.Select(type => (JsonNode)type)]);
            }
        }
    }
}
