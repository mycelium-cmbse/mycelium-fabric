// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeSchema.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json.Nodes;

    using Microsoft.Extensions.AI;

    using Mycelium.Fabric.Mcp.Extensions;

    /// <summary>
    /// The options that build the input schema of apply_changes as a strict schema, which a client that supports structured
    /// outputs can impose on the AI assistant: every object refuses the properties that it does not declare, a value is a
    /// number, a Boolean or a text, the type of an element and the operator of a constraint or a guard list the values that the
    /// server accepts, and the attribute and the operator of a constraint or a guard are required.
    /// </summary>
    /// <remarks>
    /// Every other property accepts <c>null</c>, which means that it is not given, so that a client whose strict mode requires
    /// every property, as the one of OpenAI does, keeps the meaning of the schema. The server still checks everything: the
    /// schema helps the AI assistant, it does not replace the checks. A unit is not listed: the server reads compound units
    /// (<c>km/h</c>, <c>m/s^2</c>) and the units that the libraries of the model declare, which no list can hold.
    /// </remarks>
    public static class ModelChangeSchema
    {
        /// <summary>
        /// The options of the input schema of apply_changes, given to the SDK when the tool is created.
        /// </summary>
        public static readonly AIJsonSchemaCreateOptions CreateOptions = new()
        {
            TransformSchemaNode = TransformNode,
            TransformOptions = new AIJsonSchemaTransformOptions { DisallowAdditionalProperties = true }
        };

        /// <summary>
        /// The values that a text property accepts, indexed by the type that declares the property and the name of the property.
        /// </summary>
        private static readonly Dictionary<(Type DeclaringType, string PropertyName), IReadOnlyList<string>> AllowedValues = new()
        {
            [(typeof(ElementPayload), nameof(ElementPayload.Type))] = ModelChangeBatch.CreatableMetaclassNames,
            [(typeof(ConstraintPayload), nameof(ConstraintPayload.Operator))] = ModelChangeBatch.ComparisonOperators,
            [(typeof(GuardPayload), nameof(GuardPayload.Operator))] = ModelChangeBatch.ComparisonOperators
        };

        /// <summary>
        /// Completes the schema that the SDK builds for a type or a property: the types of a <see cref="PayloadValue"/>, which a
        /// converter reads, a required property that cannot be <c>null</c>, and the <see cref="AllowedValues"/>.
        /// </summary>
        /// <param name="context">The <see cref="AIJsonSchemaCreateContext"/> that tells which type or property the schema describes.</param>
        /// <param name="schema">The schema built by the SDK.</param>
        /// <returns>The completed schema.</returns>
        private static JsonNode TransformNode(AIJsonSchemaCreateContext context, JsonNode schema)
        {
            if (schema is not JsonObject node)
            {
                return schema;
            }

            var nullableType = Nullable.GetUnderlyingType(context.TypeInfo.Type);

            if ((nullableType ?? context.TypeInfo.Type) == typeof(PayloadValue))
            {
                node["anyOf"] = CreateValueChoices(nullableType != null);
                return node;
            }

            if (context.PropertyInfo is not { } property)
            {
                return node;
            }

            if (property.IsRequired && node["type"] is JsonArray types)
            {
                node["type"] = types.GetTypesWithoutNull();
            }

            if (context.PropertyAttributeProvider is MemberInfo member && AllowedValues.TryGetValue((context.DeclaringType, member.Name), out var values))
            {
                var allowedValues = new JsonArray([.. values.Select(value => (JsonNode)value)]);

                if (!property.IsRequired)
                {
                    allowedValues.Add(null);
                }

                node["enum"] = allowedValues;
            }

            return node;
        }

        /// <summary>
        /// Creates the choices of the schema of a <see cref="PayloadValue"/>: a number, a Boolean or a text.
        /// </summary>
        /// <param name="isNullable">Whether the value can be <c>null</c>, when it is not given.</param>
        /// <returns>The choices, for the <c>anyOf</c> keyword.</returns>
        private static JsonArray CreateValueChoices(bool isNullable)
        {
            string[] valueTypes = isNullable ? ["number", "boolean", "string", "null"] : ["number", "boolean", "string"];

            return new JsonArray([.. valueTypes.Select(valueType => (JsonNode)new JsonObject { ["type"] = valueType })]);
        }
    }
}
