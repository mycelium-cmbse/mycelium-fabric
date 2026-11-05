// ------------------------------------------------------------------------------------------------
//  <copyright file="ToolArgumentCheckerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// Suite of tests for the <see cref="ToolArgumentChecker"/> class.
    /// </summary>
    [TestFixture]
    public class ToolArgumentCheckerTestFixture
    {
        /// <summary>
        /// The problems found when no argument is given.
        /// </summary>
        private static readonly string[] NoArgumentProblems = ["arguments: the required property 'changes' is missing."];

        /// <summary>
        /// The problems found when the name of the argument has another case.
        /// </summary>
        private static readonly string[] OtherCaseArgumentProblems =
        [
            "arguments: there is no property 'Changes'; the properties are changes.",
            "arguments: the required property 'changes' is missing."
        ];

        /// <summary>
        /// The problems found when the changes are sent as a text.
        /// </summary>
        private static readonly string[] TextChangesProblems = ["changes: an array is expected, not \"[{\"payload\": null}]\"."];

        /// <summary>
        /// The problems found with the other types and choices that a strict schema can use.
        /// </summary>
        private static readonly string[] OtherSchemaProblems =
        [
            "count: an integer is expected, not 1.5.",
            "options: an object is expected, not \"high\".",
            "level: a value that the schema describes is expected, not 4."
        ];

        private McpServerTool applyChanges;

        private McpServerTool defaultApplyChanges;

        [SetUp]
        public void SetUp()
        {
            var method = typeof(ConstructionTools).GetMethod(nameof(ConstructionTools.ApplyChanges));

            this.applyChanges = McpServerTool.Create(method, _ => null, new McpServerToolCreateOptions { SchemaCreateOptions = ModelChangeSchema.CreateOptions });
            this.defaultApplyChanges = McpServerTool.Create(method, _ => null);
        }

        [Test]
        public void VerifyFindProblems()
        {
            var schema = this.applyChanges.ProtocolTool.InputSchema;
            var payloadProperties = schema.GetProperty("properties").GetProperty("changes").GetProperty("items").GetProperty("properties").GetProperty("payload").GetProperty("properties");
            var metaclassCount = payloadProperties.GetProperty("type").GetProperty("enum").GetArrayLength() - 1;

            const string validBatch = """
                [
                  { "identity": "spacecraft", "payload": { "type": "PartUsage", "name": "spacecraft", "multiplicity": "1", "value": null } },
                  { "payload": { "type": "AttributeUsage", "owner": "spacecraft", "name": "mass", "value": 38, "unit": "kg" } },
                  { "payload": { "type": "RequirementUsage", "name": "massLimit", "text": "The mass shall not exceed 150 kg.",
                                 "constraint": { "attribute": "mass", "operator": "<=", "limit": 150, "unit": "kg", "margin": 20 } } },
                  { "payload": { "type": "TransitionUsage", "owner": "modes", "ends": ["safe", "nominal"], "guard": { "attribute": "charge", "operator": ">=", "limit": 80 } } },
                  { "identity": "f2b0b48c-0000-4000-8000-000000000000", "payload": { "name": "satellite", "conjugated": false } },
                  { "identity": "f2b0b48c-0000-4000-8000-000000000001" }
                ]
                """;

            const string otherCaseBatch = """
                [{ "Identity": "port", "Payload": { "Type": "portdefinition", "Name": "DataPort", "Direction": "oUT" } }]
                """;

            const string wrongBatch = """
                [
                  { "payload": { "type": "Paquet", "name": "P", "colour": "red" } },
                  { "payload": { "type": "PartUsage", "name": "wheels", "multiplicity": 4, "value": [1, 2] } },
                  { "payload": { "type": "RequirementUsage", "name": "r", "constraint": { "attribute": "mass", "op": "<=" } } },
                  { "payload": { "type": "AttributeUsage", "name": "a", "direction": "input", "ends": ["a", 3] } },
                  { "payload": { "type": "TransitionUsage", "guard": { "attribute": null, "operator": "=" } } }
                ]
                """;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ToolArgumentChecker.FindProblems(CreateArguments(validBatch), schema), Is.Empty);
                Assert.That(ToolArgumentChecker.FindProblems(CreateArguments(otherCaseBatch), schema), Is.Empty);

                Assert.That(ToolArgumentChecker.FindProblems(null, schema), Is.EqualTo(NoArgumentProblems));

                // The SDK binds the arguments by their exact name.
                Assert.That(ToolArgumentChecker.FindProblems(new Dictionary<string, JsonElement> { ["Changes"] = JsonDocument.Parse("[]").RootElement }, schema),
                    Is.EqualTo(OtherCaseArgumentProblems));

                Assert.That(ToolArgumentChecker.FindProblems(CreateArguments("\"[{\\\"payload\\\": null}]\""), schema), Is.EqualTo(TextChangesProblems));

                Assert.That(ToolArgumentChecker.FindProblems(CreateArguments(wrongBatch), schema), Is.EqualTo(new[]
                {
                    $"changes[0].payload.type: \"Paquet\" is not one of the {metaclassCount} values that the schema lists: Create only: the SysML v2 metaclass of the new element, "
                        + "for example Package, PartDefinition, PartUsage, AttributeUsage or RequirementUsage. Relationships are not created directly: the server builds them from owner, "
                        + "definition and value.",
                    "changes[0].payload: there is no property 'colour'; the properties are type, owner, name, definition, value, unit, text, reqId, constraint, constraints, multiplicity, "
                        + "satisfiedRequirement, satisfyingPart, ends, specializes, performed, direction, conjugated, isEnd, trigger, guard, effect, stateAction, item.",
                    "changes[1].payload.multiplicity: a text is expected, not 4.",
                    "changes[1].payload.value: a number, a Boolean or a text is expected, not [1, 2].",
                    "changes[2].payload.constraint: there is no property 'op'; the properties are kind, attribute, operator, limit, limitAttribute, unit, margin.",
                    "changes[2].payload.constraint: the required property 'operator' is missing.",
                    "changes[3].payload.direction: one of 'In', 'Inout', 'Out' is expected, not \"input\".",
                    "changes[3].payload.ends[1]: a text is expected, not 3.",
                    "changes[4].payload.guard.attribute: a text is expected, not null.",
                    "changes[4].payload.guard.operator: one of '<', '<=', '>', '>=', '==', '!=' is expected, not \"=\"."
                }));

                // A schema that is not strict accepts the properties that it does not declare, and no schema accepts anything.
                Assert.That(ToolArgumentChecker.FindProblems(CreateArguments(wrongBatch), this.defaultApplyChanges.ProtocolTool.InputSchema), Has.Count.EqualTo(4));
                Assert.That(ToolArgumentChecker.FindProblems(CreateArguments(wrongBatch), default), Is.Empty);
            }

            // The other types and choices that a strict schema can use.
            const string otherSchema = """
                {
                  "type": "object", "additionalProperties": false,
                  "properties": {
                    "count": { "type": "integer" },
                    "options": { "type": ["object", "null"], "properties": { "level": { "type": "number" } } },
                    "level": { "anyOf": [{ "enum": [1, 2] }, { "enum": [3] }] }
                  }
                }
                """;

            var otherArguments = new Dictionary<string, JsonElement>
            {
                ["count"] = JsonDocument.Parse("1.5").RootElement,
                ["options"] = JsonDocument.Parse("\"high\"").RootElement,
                ["level"] = JsonDocument.Parse("4").RootElement
            };

            var validArguments = new Dictionary<string, JsonElement>
            {
                ["count"] = JsonDocument.Parse("2").RootElement,
                ["options"] = JsonDocument.Parse("{ \"level\": 0.5, \"other\": true }").RootElement,
                ["level"] = JsonDocument.Parse("3").RootElement
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ToolArgumentChecker.FindProblems(otherArguments, JsonDocument.Parse(otherSchema).RootElement), Is.EqualTo(OtherSchemaProblems));

                Assert.That(ToolArgumentChecker.FindProblems(validArguments, JsonDocument.Parse(otherSchema).RootElement), Is.Empty);
            }
        }

        [Test]
        public async Task VerifyCheckArguments()
        {
            Assert.That(() => ToolArgumentChecker.CheckArguments(null), Throws.TypeOf<ArgumentNullException>());

            var result = new CallToolResult();
            var next = new Mock<McpRequestHandler<CallToolRequestParams, CallToolResult>>();
            next.Setup(handler => handler(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>())).Returns(new ValueTask<CallToolResult>(result));

            var handler = ToolArgumentChecker.CheckArguments(next.Object);
            var server = new Mock<McpServer>();

            var parameters = new CallToolRequestParams { Name = "apply_changes", Arguments = CreateArguments("[{ \"payload\": { \"type\": \"Package\", \"direction\": \"input\" } }]") };

            var context = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, parameters)
            {
                MatchedPrimitive = this.applyChanges
            };

            Assert.That(async () => await handler(context, CancellationToken.None), Throws.TypeOf<McpException>().With.Message.EqualTo(
                "The arguments do not follow the input schema of apply_changes, and nothing has been done:\n"
                + "- changes[0].payload.direction: one of 'In', 'Inout', 'Out' is expected, not \"input\".\n"
                + "Correct them all and call apply_changes again."));

            next.Verify(nextHandler => nextHandler(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>()), Times.Never);

            // Beyond 20 problems, the others are counted.
            var manyChanges = $"[{string.Join(", ", Enumerable.Repeat("{ \"payload\": { \"type\": 1 } }", 25))}]";
            context.Params = new CallToolRequestParams { Name = "apply_changes", Arguments = CreateArguments(manyChanges) };

            Assert.That(async () => await handler(context, CancellationToken.None), Throws.TypeOf<McpException>().With.Message.Contains("- changes[19].payload.type")
                .And.Message.Not.Contains("changes[20]").And.Message.Contains("- and 5 other problems."));

            context.Params = new CallToolRequestParams { Name = "apply_changes", Arguments = CreateArguments("[{ \"payload\": { \"type\": \"Package\", \"direction\": \"In\" } }]") };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await handler(context, CancellationToken.None), Is.SameAs(result));

                // A tool whose schema is not strict, or no tool, is called without a check.
                context.MatchedPrimitive = this.defaultApplyChanges;
                context.Params = new CallToolRequestParams { Name = "apply_changes", Arguments = CreateArguments("[{ \"payload\": { \"colour\": \"red\" } }]") };
                Assert.That(await handler(context, CancellationToken.None), Is.SameAs(result));

                context.MatchedPrimitive = null;
                Assert.That(await handler(context, CancellationToken.None), Is.SameAs(result));

                next.Verify(nextHandler => nextHandler(context, CancellationToken.None), Times.Exactly(3));
            }
        }

        /// <summary>
        /// Creates the arguments of apply_changes.
        /// </summary>
        /// <param name="changes">The JSON of the changes argument.</param>
        /// <returns>The arguments, by name.</returns>
        private static Dictionary<string, JsonElement> CreateArguments(string changes)
        {
            return new Dictionary<string, JsonElement> { ["changes"] = JsonDocument.Parse(changes).RootElement };
        }
    }
}
