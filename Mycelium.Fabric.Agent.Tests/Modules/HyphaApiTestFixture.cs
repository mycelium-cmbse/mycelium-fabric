// ------------------------------------------------------------------------------------------------
//  <copyright file="HyphaApiTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.Agent.Modules;
    using Mycelium.Fabric.Agent.Tests.TestHelpers;

    [TestFixture]
    public class HyphaApiTestFixture
    {
        private const string SatelliteId = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";

        private const string Conversation = "user-1:project-1";

        [Test]
        public void VerifyAddRoutes()
        {
            Assert.That(() => new HyphaApi().AddRoutes(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task VerifyRunAgent()
        {
            using var application = new AgentApplicationFactory();
            using var client = application.CreateClient();

            using var notJson = await client.PostAsync(HyphaApi.RunRoute, new StringContent("not json", Encoding.UTF8, "application/json"));
            using var withoutThread = await PostRunAsync(client, " ", "run-1", "Hello.");
            using var withoutMessage = await PostRunAsync(client, Conversation, "run-1", null);
            using var withoutMessages = await client.PostAsync(HyphaApi.RunRoute, new StringContent("""{"threadId":"thread-1","runId":"run-1"}""", Encoding.UTF8, "application/json"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notJson.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(await withoutThread.Content.ReadAsStringAsync(), Is.EqualTo("The run has no threadId."));
                Assert.That(await withoutMessage.Content.ReadAsStringAsync(), Is.EqualTo("The run has no message of the user."));
                Assert.That(withoutMessages.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(await withoutMessages.Content.ReadAsStringAsync(), Is.EqualTo("The run has no message of the user."));
                Assert.That(application.ApiLogger.LatestRecord.Level, Is.EqualTo(LogLevel.Warning));
            }

            application.ChatClient.SetupAnswers(
                ChatClientMockExtensions.ToolCall("call-1", "sum_attribute", new Dictionary<string, object> { ["elementId"] = SatelliteId, ["attributeName"] = "mass" }),
                ChatClientMockExtensions.Text("The mass is ", "known."));

            using var answered = await PostRunAsync(client, Conversation, "run-2", "Give me the mass of the satellite.");
            var events = await ReadEventsAsync(answered);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(answered.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
                Assert.That(events[0], Is.EqualTo("RUN_STARTED"));
                Assert.That(events[^1], Is.EqualTo("RUN_FINISHED"));
                Assert.That(events, Does.Contain("TOOL_CALL_START").And.Contain("TOOL_CALL_RESULT").And.Contain("TEXT_MESSAGE_CONTENT"));
                Assert.That(events.IndexOf("TOOL_CALL_RESULT"), Is.LessThan(events.IndexOf("TEXT_MESSAGE_CONTENT")));
                Assert.That(application.ApiLogger.LatestRecord.Level, Is.EqualTo(LogLevel.Information));
                Assert.That(application.ApiLogger.LatestRecord.Message, Does.Contain("run-2"));
            }

            application.ChatClient.Setup(chatClient => chatClient.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new HttpRequestException("No connection could be made to the LLM."));

            using var failed = await PostRunAsync(client, Conversation, "run-3", "Hello.");
            var failedEvents = await ReadEventsAsync(failed);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failedEvents[^1], Is.EqualTo("RUN_ERROR"));
                Assert.That(await LastEventMessageAsync(failed), Does.Contain("No connection could be made to the LLM."));
                Assert.That(application.ApiLogger.Collector.GetSnapshot().Select(record => record.Level), Does.Contain(LogLevel.Error));
            }
        }

        [Test]
        public async Task VerifyGetThread()
        {
            using var application = new AgentApplicationFactory();
            using var client = application.CreateClient();
            var threadRoute = HyphaApi.ThreadRoute.Replace("{threadId}", Uri.EscapeDataString(Conversation), StringComparison.Ordinal);

            Assert.That(await client.GetStringAsync(threadRoute), Is.EqualTo("[]"));

            application.ChatClient.SetupAnswers(ChatClientMockExtensions.Text("I am Hypha."));

            using var answered = await PostRunAsync(client, Conversation, "run-1", "Who are you?");
            await ReadEventsAsync(answered);

            using var thread = JsonDocument.Parse(await client.GetStringAsync(threadRoute));
            var messages = thread.RootElement.EnumerateArray().ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(messages.Select(message => message.GetProperty("role").GetString()), Is.EqualTo(["user", "assistant"]));
                Assert.That(messages[^1].GetProperty("content").GetString(), Is.EqualTo("I am Hypha."));
            }
        }

        private static Task<HttpResponseMessage> PostRunAsync(HttpClient client, string threadId, string runId, string text)
        {
            object[] messages = text is null ? [] : [new { id = "message-1", role = "user", content = text }];
            var input = new
            {
                threadId,
                runId,
                messages,
                tools = Array.Empty<object>(),
                context = new[] { new { description = "project", value = "the satellite" } },
                state = new { },
                forwardedProps = new { }
            };

            return client.PostAsync(HyphaApi.RunRoute, new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json"));
        }

        private static async Task<List<string>> ReadEventsAsync(HttpResponseMessage response)
        {
            var events = await ReadEventDocumentsAsync(response);

            return [.. events.Select(document => document.RootElement.GetProperty("type").GetString())];
        }

        private static async Task<string> LastEventMessageAsync(HttpResponseMessage response)
        {
            var events = await ReadEventDocumentsAsync(response);

            return events[^1].RootElement.GetProperty("message").GetString();
        }

        private static async Task<List<JsonDocument>> ReadEventDocumentsAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            return [.. body.Split('\n').Where(line => line.StartsWith("data:", StringComparison.Ordinal)).Select(line => JsonDocument.Parse(line["data:".Length..]))];
        }
    }
}
