// ------------------------------------------------------------------------------------------------
//  <copyright file="ToolArgumentChecker.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    using ModelContextProtocol;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Extensions;

    /// <summary>
    /// Checks the arguments of a tool whose input schema is strict, as the one of apply_changes is, against this schema before
    /// the SDK reads them, and returns every difference at once, as apply_changes returns every problem of a batch. Without
    /// it, the SDK ignores a property that it does not know and only answers "An error occurred invoking" when it cannot read
    /// a value, so that the AI assistant does not learn what to correct.
    /// </summary>
    /// <remarks>
    /// A schema is strict when it refuses the properties that it does not declare. The keywords of a strict schema are checked:
    /// <c>type</c>, <c>enum</c>, <c>anyOf</c>, <c>properties</c>, <c>required</c>, <c>additionalProperties</c> and
    /// <c>items</c>. The names of the arguments are compared with case, as the SDK binds them, and the names of the properties
    /// of a value and the listed texts without case, as the server reads them. The schemas that the SDK builds for the other
    /// tools are looser than what the server reads (a list that is optional accepts <c>null</c>, while its schema says an
    /// array), so they are not checked.
    /// </remarks>
    public static class ToolArgumentChecker
    {
        /// <summary>
        /// The maximum number of problems written in the message, the same as the new issues of a batch.
        /// </summary>
        private const int MaximumProblemCount = 20;

        /// <summary>
        /// The maximum number of values listed in a problem: a longer list is left to the schema, and the description of the
        /// property is given instead.
        /// </summary>
        private const int MaximumListedValueCount = 10;

        /// <summary>
        /// The maximum length of a wrong value written in a problem.
        /// </summary>
        private const int MaximumShownValueLength = 60;

        /// <summary>
        /// The filter of the calls of tools that checks the arguments of a tool whose input schema is strict before the tool
        /// is called.
        /// </summary>
        /// <param name="next">The next handler of the calls of tools.</param>
        /// <returns>The handler that checks the arguments, then calls <paramref name="next"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is <c>null</c>.</exception>
        /// <remarks>
        /// When the arguments do not follow the schema, the handler throws an <see cref="McpException"/>, whose message the SDK
        /// returns to the AI assistant as an error of the tool, and the tool is not called.
        /// </remarks>
        public static McpRequestHandler<CallToolRequestParams, CallToolResult> CheckArguments(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        {
            ArgumentNullException.ThrowIfNull(next);

            return (context, cancellationToken) =>
            {
                if (context.MatchedPrimitive is McpServerTool tool && tool.ProtocolTool.InputSchema.IsStrictSchema())
                {
                    var problems = FindProblems(context.Params?.Arguments, tool.ProtocolTool.InputSchema);

                    if (problems.Count > 0)
                    {
                        throw new McpException(DescribeProblems(tool.ProtocolTool.Name, problems));
                    }
                }

                return next(context, cancellationToken);
            };
        }

        /// <summary>
        /// Finds where the arguments of a tool do not follow its input schema.
        /// </summary>
        /// <param name="arguments">The arguments, by name, or <c>null</c> when there are none.</param>
        /// <param name="schema">The input schema of the tool.</param>
        /// <returns>
        /// The problems, each with the path of the value, for example
        /// <c>changes[2].payload.direction: one of 'In', 'Inout', 'Out' is expected, not "input".</c>, or none.
        /// </returns>
        public static IReadOnlyList<string> FindProblems(IDictionary<string, JsonElement> arguments, JsonElement schema)
        {
            var problems = new List<string>();

            if (schema.ValueKind == JsonValueKind.Object)
            {
                var givenArguments = arguments ?? new Dictionary<string, JsonElement>();
                CheckProperties(givenArguments.Select(argument => (argument.Key, argument.Value)), schema, string.Empty, problems);
            }

            return problems;
        }

        /// <summary>
        /// Checks a value against its schema.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="schema">The schema of the value; a schema that is not an object accepts any value.</param>
        /// <param name="path">The path of the value, for example <c>changes[2].payload</c>.</param>
        /// <param name="problems">The problems found so far, to which those of the value are added.</param>
        private static void CheckValue(JsonElement value, JsonElement schema, string path, List<string> problems)
        {
            if (schema.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (schema.TryGetProperty("anyOf", out var choices) && !choices.EnumerateArray().Any(choice => Accepts(choice, value, path)))
            {
                problems.Add($"{path}: {DescribeTypes(choices.EnumerateArray())} is expected, not {value.ToShortText(MaximumShownValueLength)}.");
                return;
            }

            if (schema.TryGetProperty("type", out var type) && !value.HasSchemaType(type))
            {
                problems.Add($"{path}: {DescribeTypes([schema])} is expected, not {value.ToShortText(MaximumShownValueLength)}.");
                return;
            }

            if (schema.TryGetProperty("enum", out var allowedValues) && !allowedValues.EnumerateArray().Any(allowedValue => allowedValue.EqualsIgnoringCase(value)))
            {
                problems.Add($"{path}: {DescribeAllowedValues(allowedValues, schema, value)}");
                return;
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                CheckProperties(value.EnumerateObject().Select(property => (property.Name, property.Value)), schema, path, problems);
            }
            else if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var itemSchema))
            {
                var index = 0;

                foreach (var item in value.EnumerateArray())
                {
                    CheckValue(item, itemSchema, $"{path}[{index++}]", problems);
                }
            }
        }

        /// <summary>
        /// Checks the properties of an object against the schema of the object: each property that it declares, the ones that
        /// it does not declare when it is strict, and the ones that it requires. The names of the arguments of the tool are
        /// compared with case, as the SDK binds them, and the names of the properties of a value without case, as the server
        /// reads them.
        /// </summary>
        /// <param name="properties">The properties of the object, by name.</param>
        /// <param name="schema">The schema of the object.</param>
        /// <param name="path">The path of the object, empty for the arguments of the tool.</param>
        /// <param name="problems">The problems found so far, to which those of the object are added.</param>
        private static void CheckProperties(IEnumerable<(string Name, JsonElement Value)> properties, JsonElement schema, string path, List<string> problems)
        {
            var location = path.Length == 0 ? "arguments" : path;
            var nameComparer = path.Length == 0 ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

            var declaredProperties = schema.TryGetProperty("properties", out var declared)
                ? declared.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, nameComparer)
                : new Dictionary<string, JsonElement>(nameComparer);

            var givenNames = new HashSet<string>(nameComparer);

            foreach (var (name, value) in properties)
            {
                givenNames.Add(name);

                if (declaredProperties.TryGetValue(name, out var propertySchema))
                {
                    CheckValue(value, propertySchema, path.Length == 0 ? name : $"{path}.{name}", problems);
                }
                else if (schema.IsStrictSchema())
                {
                    problems.Add($"{location}: there is no property '{name}'; the properties are {string.Join(", ", declaredProperties.Keys)}.");
                }
            }

            if (schema.TryGetProperty("required", out var required))
            {
                problems.AddRange(required.EnumerateArray()
                    .Select(requiredName => requiredName.GetString())
                    .Where(requiredName => !givenNames.Contains(requiredName))
                    .Select(requiredName => $"{location}: the required property '{requiredName}' is missing."));
            }
        }

        /// <summary>
        /// Tells whether a schema accepts a value.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// <param name="value">The value.</param>
        /// <param name="path">The path of the value.</param>
        /// <returns><c>true</c> when the value follows the schema.</returns>
        private static bool Accepts(JsonElement schema, JsonElement value, string path)
        {
            var problems = new List<string>();
            CheckValue(value, schema, path, problems);

            return problems.Count == 0;
        }

        /// <summary>
        /// Describes the types that a value must have, <c>null</c> being left out when there are others.
        /// </summary>
        /// <param name="schemas">The schema of the value, or the choices of its <c>anyOf</c>.</param>
        /// <returns>
        /// The description, for example <c>a number, a Boolean or a text</c>, or <c>a value that the schema describes</c> when
        /// the schemas give no type.
        /// </returns>
        private static string DescribeTypes(IEnumerable<JsonElement> schemas)
        {
            var typeNames = schemas
                .SelectMany(schema => schema.TryGetProperty("type", out var type) ? type.GetTypeNames() : [])
                .Distinct()
                .ToList();

            if (typeNames.Count > 1)
            {
                typeNames.Remove("null");
            }

            var descriptions = typeNames.Select(typeName => typeName switch
            {
                "string" => "a text",
                "number" => "a number",
                "integer" => "an integer",
                "boolean" => "a Boolean",
                "array" => "an array",
                "object" => "an object",
                _ => typeName
            }).ToList();

            return descriptions.Count switch
            {
                0 => "a value that the schema describes",
                1 => descriptions[0],
                _ => $"{string.Join(", ", descriptions.Take(descriptions.Count - 1))} or {descriptions[^1]}"
            };
        }

        /// <summary>
        /// Describes a value that is not one of the values that a schema lists: with these values, or, for a long list, with
        /// its length and the description of the property.
        /// </summary>
        /// <param name="allowedValues">The listed values.</param>
        /// <param name="schema">The schema, whose description explains a long list.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The description, for example <c>one of 'In', 'Inout', 'Out' is expected, not "input".</c>.
        /// </returns>
        private static string DescribeAllowedValues(JsonElement allowedValues, JsonElement schema, JsonElement value)
        {
            var listedValues = allowedValues.EnumerateArray().Where(allowedValue => allowedValue.ValueKind != JsonValueKind.Null).ToList();

            if (listedValues.Count <= MaximumListedValueCount)
            {
                return $"one of {string.Join(", ", listedValues.Select(listedValue => $"'{listedValue}'"))} is expected, not {value.ToShortText(MaximumShownValueLength)}.";
            }

            var description = schema.TryGetProperty("description", out var text) ? $": {text.GetString()}" : ".";

            return $"{value.ToShortText(MaximumShownValueLength)} is not one of the {listedValues.Count} values that the schema lists{description}";
        }

        /// <summary>
        /// Writes the message of the problems of the arguments of a tool, for the AI assistant.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="problems">The problems.</param>
        /// <returns>The message, with at most <see cref="MaximumProblemCount"/> problems.</returns>
        private static string DescribeProblems(string toolName, IReadOnlyList<string> problems)
        {
            var listedProblems = string.Join(string.Empty, problems.Take(MaximumProblemCount).Select(problem => $"\n- {problem}"));
            var otherProblems = problems.Count > MaximumProblemCount ? $"\n- and {problems.Count - MaximumProblemCount} other problems." : string.Empty;

            return $"The arguments do not follow the input schema of {toolName}, and nothing has been done:{listedProblems}{otherProblems}\n"
                + $"Correct them all and call {toolName} again.";
        }
    }
}
