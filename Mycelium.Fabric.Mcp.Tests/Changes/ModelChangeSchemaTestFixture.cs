// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeSchemaTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Changes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    using Microsoft.Extensions.AI;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="ModelChangeSchema"/> class.
    /// </summary>
    [TestFixture]
    public class ModelChangeSchemaTestFixture
    {
        /// <summary>
        /// The keywords that the strict mode of OpenAI accepts, of which the schema uses a part.
        /// </summary>
        private static readonly string[] StrictKeywords = ["type", "description", "properties", "required", "additionalProperties", "items", "enum", "anyOf"];

        [Test]
        public void VerifyCreateOptions()
        {
            var applyChanges = typeof(ConstructionTools).GetMethod(nameof(ConstructionTools.ApplyChanges));
            var schema = McpServerTool.Create(applyChanges, _ => null, new McpServerToolCreateOptions { SchemaCreateOptions = ModelChangeSchema.CreateOptions }).ProtocolTool.InputSchema;
            var defaultSchema = McpServerTool.Create(applyChanges, _ => null).ProtocolTool.InputSchema;

            var payload = GetPayloadSchema(schema);
            var types = payload.GetProperty("properties").GetProperty("type").GetProperty("enum").EnumerateArray().ToList();
            var typeNames = types.Where(type => type.ValueKind == JsonValueKind.String).Select(type => type.GetString()).ToList();

            List<JsonElement> comparisons =
            [
                payload.GetProperty("properties").GetProperty("constraint"),
                payload.GetProperty("properties").GetProperty("constraints").GetProperty("items"),
                payload.GetProperty("properties").GetProperty("guard")
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(typeNames, Does.Contain("Package").And.Contain("PartUsage").And.Contain("InterfaceUsage").And.Contain("TransitionUsage"));
                Assert.That(typeNames, Does.Not.Contain("FeatureTyping").And.Not.Contain("ConjugatedPortDefinition").And.Not.Contain("AllocationDefinition"));
                Assert.That(typeNames, Is.Ordered.Using((IComparer<string>)StringComparer.Ordinal));
                Assert.That(types[^1].ValueKind, Is.EqualTo(JsonValueKind.Null));

                foreach (var comparison in comparisons)
                {
                    Assert.That(comparison.GetProperty("required").EnumerateArray().Select(name => name.GetString()), Is.EquivalentTo(new[] { "attribute", "operator" }));
                    Assert.That(comparison.GetProperty("properties").GetProperty("operator").GetProperty("type").GetString(), Is.EqualTo("string"));
                    Assert.That(comparison.GetProperty("properties").GetProperty("operator").GetProperty("enum").EnumerateArray().Select(value => value.GetString()),
                        Is.EqualTo(new[] { "<", "<=", ">", ">=", "==", "!=" }));
                    Assert.That(comparison.GetProperty("properties").GetProperty("attribute").GetProperty("type").GetString(), Is.EqualTo("string"));
                    Assert.That(GetChoiceTypes(comparison.GetProperty("properties").GetProperty("limit")), Is.EqualTo(new[] { "number", "boolean", "string", "null" }));
                }

                Assert.That(GetChoiceTypes(payload.GetProperty("properties").GetProperty("value")), Is.EqualTo(new[] { "number", "boolean", "string", "null" }));
                Assert.That(GetObjectSchemas(schema).Select(objectSchema => objectSchema.GetProperty("additionalProperties").ValueKind), Has.All.EqualTo(JsonValueKind.False));
                Assert.That(GetPropertySchemas(schema).Where(property => !property.TryGetProperty("type", out _) && !property.TryGetProperty("anyOf", out _)), Is.Empty);
                Assert.That(GetKeywords(schema).Except(StrictKeywords), Is.Empty);
                Assert.That(GetPayloadSchema(defaultSchema).GetProperty("properties").GetProperty("type").TryGetProperty("enum", out _), Is.False);
                Assert.That(GetPayloadSchema(defaultSchema).GetProperty("properties").GetProperty("value").TryGetProperty("anyOf", out _), Is.False);
                Assert.That(defaultSchema.TryGetProperty("additionalProperties", out _), Is.False);
            }

            // A strict client requires every property: each optional one must still accept null, which means that it is not given.
            var strictSchema = AIJsonUtilities.TransformSchema(schema, new AIJsonSchemaTransformOptions { RequireAllProperties = true, DisallowAdditionalProperties = true });

            var optionalProperties = GetObjectSchemas(schema)
                .SelectMany(objectSchema => objectSchema.GetProperty("properties").EnumerateObject()
                    .Where(property => !objectSchema.TryGetProperty("required", out var required) || !required.EnumerateArray().Any(name => name.GetString() == property.Name)))
                .Select(property => property.Value)
                .ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(GetObjectSchemas(strictSchema).Select(objectSchema => objectSchema.GetProperty("required").GetArrayLength()),
                    Is.EqualTo(GetObjectSchemas(strictSchema).Select(objectSchema => objectSchema.GetProperty("properties").EnumerateObject().Count())));
                Assert.That(optionalProperties, Has.Count.EqualTo(39));
                Assert.That(optionalProperties.Where(property => !AcceptsNull(property)), Is.Empty);
            }

            // The other types keep their schema, even with a property of the same name.
            var createdElementSchema = AIJsonUtilities.CreateJsonSchema(typeof(CreatedElement), serializerOptions: McpJsonUtilities.DefaultOptions,
                inferenceOptions: ModelChangeSchema.CreateOptions);

            Assert.That(createdElementSchema.GetProperty("properties").GetProperty("type").TryGetProperty("enum", out _), Is.False);

            // Each listed metaclass is one that a change can create, and a relationship is not.
            var applier = new ModelChangeApplier(new Serializer(), new DeSerializer());

            var notCreatableTypes = typeNames
                .Where(typeName => HasMetaclassProblem(applier, typeName))
                .ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notCreatableTypes, Is.Empty);
                Assert.That(HasMetaclassProblem(applier, "FeatureTyping"), Is.True);
            }
        }

        /// <summary>
        /// Gets the schema of the payload of a change in the input schema of apply_changes.
        /// </summary>
        /// <param name="schema">The input schema of apply_changes.</param>
        /// <returns>The schema of the payload.</returns>
        private static JsonElement GetPayloadSchema(JsonElement schema)
        {
            return schema.GetProperty("properties").GetProperty("changes").GetProperty("items").GetProperty("properties").GetProperty("payload");
        }

        /// <summary>
        /// Gets the types of the choices of a schema with <c>anyOf</c>.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <returns>The type of each choice.</returns>
        private static IEnumerable<string> GetChoiceTypes(JsonElement schema)
        {
            return schema.GetProperty("anyOf").EnumerateArray().Select(choice => choice.GetProperty("type").GetString());
        }

        /// <summary>
        /// Gets the schema and the schemas that it contains, at every depth.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <returns>The schemas.</returns>
        private static IEnumerable<JsonElement> GetSchemas(JsonElement schema)
        {
            yield return schema;

            var children = new List<JsonElement>();

            if (schema.TryGetProperty("properties", out var properties))
            {
                children.AddRange(properties.EnumerateObject().Select(property => property.Value));
            }

            if (schema.TryGetProperty("items", out var items))
            {
                children.Add(items);
            }

            if (schema.TryGetProperty("anyOf", out var choices))
            {
                children.AddRange(choices.EnumerateArray());
            }

            foreach (var child in children.SelectMany(GetSchemas))
            {
                yield return child;
            }
        }

        /// <summary>
        /// Gets the schemas of objects, at every depth.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <returns>The schemas that have properties.</returns>
        private static IEnumerable<JsonElement> GetObjectSchemas(JsonElement schema)
        {
            return GetSchemas(schema).Where(child => child.TryGetProperty("properties", out _));
        }

        /// <summary>
        /// Gets the schemas of the properties of the objects, at every depth.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <returns>The schemas of the properties.</returns>
        private static IEnumerable<JsonElement> GetPropertySchemas(JsonElement schema)
        {
            return GetObjectSchemas(schema).SelectMany(objectSchema => objectSchema.GetProperty("properties").EnumerateObject().Select(property => property.Value));
        }

        /// <summary>
        /// Gets the keywords of the schemas, at every depth.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <returns>The distinct keywords.</returns>
        private static IEnumerable<string> GetKeywords(JsonElement schema)
        {
            return GetSchemas(schema).SelectMany(child => child.EnumerateObject().Select(keyword => keyword.Name)).Distinct();
        }

        /// <summary>
        /// Tells whether a schema accepts <c>null</c>, by its type, its choices or its listed values.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <returns><c>true</c> when <c>null</c> follows the schema.</returns>
        private static bool AcceptsNull(JsonElement schema)
        {
            if (schema.TryGetProperty("anyOf", out var choices))
            {
                return choices.EnumerateArray().Any(AcceptsNull);
            }

            var typeAcceptsNull = schema.GetProperty("type") is var type && (type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Any(typeName => typeName.GetString() == "null")
                : type.GetString() == "null");

            return typeAcceptsNull && (!schema.TryGetProperty("enum", out var values) || values.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.Null));
        }

        /// <summary>
        /// Tells whether a change that creates an element of a metaclass is refused because of its metaclass.
        /// </summary>
        /// <param name="applier">The <see cref="ModelChangeApplier"/>.</param>
        /// <param name="typeName">The name of the metaclass.</param>
        /// <returns><c>true</c> when the server refuses the metaclass.</returns>
        private static bool HasMetaclassProblem(ModelChangeApplier applier, string typeName)
        {
            var result = applier.Apply([], [new ModelChange { Payload = new ElementPayload { Type = typeName, Name = "element" } }], []);

            return result.IsError && result.Errors.Any(error => error.Description.Contains("is not the metaclass"));
        }
    }
}
