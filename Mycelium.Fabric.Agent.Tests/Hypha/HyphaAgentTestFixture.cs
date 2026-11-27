// ------------------------------------------------------------------------------------------------
//  <copyright file="HyphaAgentTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.Hypha
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Mycelium.Fabric.Agent.Hypha;
    using Mycelium.Fabric.Agent.Tests.TestHelpers;

    [TestFixture]
    public class HyphaAgentTestFixture
    {
        private const string SatelliteId = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";

        private const string Conversation = "user-1:project-1";

        private const string Context = "The user works in this context:\n- project: the satellite";

        [Test]
        public void VerifyConstructor()
        {
            var chatClient = new Mock<IChatClient>();
            var toolbox = new Mock<IFabricToolbox>();
            var options = Options.Create(new HyphaOptions());
            var optionsWithoutRoom = Options.Create(new HyphaOptions { ContextSize = 16384, ReservedTokens = 20000 });
            var loggerFactory = new Mock<ILoggerFactory>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new HyphaAgent(null, toolbox.Object, options, loggerFactory.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new HyphaAgent(chatClient.Object, null, options, loggerFactory.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new HyphaAgent(chatClient.Object, toolbox.Object, null, loggerFactory.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new HyphaAgent(chatClient.Object, toolbox.Object, options, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new HyphaAgent(chatClient.Object, toolbox.Object, optionsWithoutRoom, loggerFactory.Object), Throws.TypeOf<ArgumentException>().With.Message.Contains(nameof(HyphaOptions.ReservedTokens)));
            }
        }

        [Test]
        public async Task VerifyRunAsync()
        {
            using var application = new AgentApplicationFactory();
            var calls = application.ChatClient.SetupAnswers(
                ChatClientMockExtensions.ToolCall("call-1", "sum_attribute", new Dictionary<string, object> { ["elementId"] = SatelliteId, ["attributeName"] = "mass" }),
                ChatClientMockExtensions.Text("The mass is ", "known."),
                ChatClientMockExtensions.Text("You are welcome."),
                ChatClientMockExtensions.Text("I am Hypha."));
            var agent = application.Services.GetRequiredService<IHyphaAgent>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => agent.RunAsync(" ", new ChatMessage(ChatRole.User, "Hello."), null, CancellationToken.None).ToListAsync().AsTask(), Throws.TypeOf<ArgumentException>());
                Assert.That(() => agent.RunAsync(Conversation, null, null, CancellationToken.None).ToListAsync().AsTask(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(calls, Has.Count.EqualTo(0));
            }

            var answer = await agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "Give me the mass of the satellite."), Context, CancellationToken.None).ToListAsync();
            var results = answer.SelectMany(update => update.Contents).OfType<FunctionResultContent>().ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(string.Concat(answer.Select(update => update.Text)), Is.EqualTo("The mass is known."));
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].CallId, Is.EqualTo("call-1"));
                Assert.That(results[0].Result?.ToString(), Does.Contain("total"));
                Assert.That(calls, Has.Count.EqualTo(2));
                Assert.That(Instructions(calls[0]), Is.EqualTo(HyphaInstructions.System));
                Assert.That(UserTexts(calls[0])[^1], Does.StartWith(Context).And.EndWith("Give me the mass of the satellite."));
                Assert.That(calls[0].Options.Tools.Select(tool => tool.Name), Does.Contain("sum_attribute").And.Not.Contain("decide_batch"));
                Assert.That(calls[1].Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToList(), Has.Count.EqualTo(1));
            }

            await agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "Thanks."), null, CancellationToken.None).ToListAsync();
            await agent.RunAsync("user-2:project-1", new ChatMessage(ChatRole.User, "Who are you?"), null, CancellationToken.None).ToListAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(UserTexts(calls[2]), Is.EqualTo([UserTexts(calls[0])[^1], "Thanks."]));
                Assert.That(Instructions(calls[2]), Is.EqualTo(HyphaInstructions.System));
                Assert.That(UserTexts(calls[3]), Is.EqualTo(["Who are you?"]));
            }
        }

        [Test]
        public async Task VerifyGetHistory()
        {
            using var application = new AgentApplicationFactory();
            application.ChatClient.SetupAnswers(
                ChatClientMockExtensions.ToolCall("call-1", "sum_attribute", new Dictionary<string, object> { ["elementId"] = SatelliteId, ["attributeName"] = "mass" }),
                ChatClientMockExtensions.Text("The mass is known."));
            var agent = application.Services.GetRequiredService<IHyphaAgent>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(agent.GetHistory(null), Is.Empty);
                Assert.That(agent.GetHistory(Conversation), Is.Empty);
            }

            await agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "Give me the mass of the satellite."), Context, CancellationToken.None).ToListAsync();

            var history = agent.GetHistory(Conversation);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(history.Select(message => message.Role), Is.EqualTo([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant]));
                Assert.That(history[0].Text, Is.EqualTo("Give me the mass of the satellite."));
                Assert.That(history[^1].Text, Is.EqualTo("The mass is known."));
                Assert.That(agent.GetHistory("user-2:project-1"), Is.Empty);
            }
        }

        [Test]
        public async Task RunAsync_WithALongConversation_SummarizesTheOlderMessagesForTheLlmOnly()
        {
            using var application = new AgentApplicationFactory(TimeSpan.FromMinutes(1), 400);
            var longText = string.Concat(Enumerable.Repeat("A long answer about the satellite. ", 20));
            var calls = application.ChatClient.SetupAnswers([.. Enumerable.Range(0, 14).Select(_ => ChatClientMockExtensions.Text(longText))]);

            application.ChatClient.Setup(client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "The summary of the conversation.")));

            var agent = application.Services.GetRequiredService<IHyphaAgent>();

            foreach (var index in Enumerable.Range(0, 14))
            {
                await agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, $"Question {index}."), null, CancellationToken.None).ToListAsync();
            }

            var lastCallTexts = calls[^1].Messages.Select(message => message.Text).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(lastCallTexts, Does.Not.Contain("Question 0."));
                Assert.That(lastCallTexts, Does.Contain("Question 13."));
                Assert.That(string.Concat(lastCallTexts), Does.Contain("The summary of the conversation."));
                Assert.That(agent.GetHistory(Conversation), Has.Count.EqualTo(28));
                Assert.That(agent.GetHistory(Conversation)[0].Text, Is.EqualTo("Question 0."));
            }

            application.ChatClient.Verify(client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task RunAsync_WithASilentLlm_FailsAfterTheSilenceTimeout()
        {
            using var application = new AgentApplicationFactory(TimeSpan.FromMilliseconds(300), 32768);

            application.ChatClient.Setup(client => client.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> _, ChatOptions _, CancellationToken cancellationToken) => Silent(cancellationToken));

            var agent = application.Services.GetRequiredService<IHyphaAgent>();

            Assert.That(() => agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "Hello."), null, CancellationToken.None).ToListAsync().AsTask(), Throws.TypeOf<TimeoutException>());

            var calls = application.ChatClient.SetupAnswers(ChatClientMockExtensions.Text("Hello."));

            var answer = await agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "Hello again."), null, CancellationToken.None).ToListAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(string.Concat(answer.Select(update => update.Text)), Is.EqualTo("Hello."));
                Assert.That(UserTexts(calls[0]), Is.EqualTo(["Hello.", "Hello again."]));
            }
        }

        [Test]
        public async Task RunAsync_WithTwoMessagesOfOneConversation_AnswersThemInTurn()
        {
            using var application = new AgentApplicationFactory();
            var callCount = 0;
            List<ChatMessage> secondCallMessages = null;

            application.ChatClient.Setup(client => client.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> messages, ChatOptions _, CancellationToken cancellationToken) =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        return Silent(cancellationToken);
                    }

                    secondCallMessages = [.. messages];

                    return ChatClientMockExtensions.Stream(ChatClientMockExtensions.Text("Second answer."));
                });

            var agent = application.Services.GetRequiredService<IHyphaAgent>();
            using var firstCancellation = new CancellationTokenSource();

            var firstAnswer = agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "First question."), null, firstCancellation.Token).ToListAsync().AsTask();
            await WaitUntil(() => Volatile.Read(ref callCount) == 1);
            var secondAnswer = agent.RunAsync(Conversation, new ChatMessage(ChatRole.User, "Second question."), null, CancellationToken.None).ToListAsync().AsTask();
            await Task.Delay(200);

            // The second message waits for the end of the first answer.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Volatile.Read(ref callCount), Is.EqualTo(1));
                Assert.That(secondAnswer.IsCompleted, Is.False);
            }

            // Sent at once: the first answer is cancelled, and the second message goes.
            await firstCancellation.CancelAsync();

            Assert.That(() => firstAnswer, Throws.InstanceOf<OperationCanceledException>());

            var answer = await secondAnswer;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(string.Concat(answer.Select(update => update.Text)), Is.EqualTo("Second answer."));
                Assert.That(callCount, Is.EqualTo(2));
                Assert.That(agent.GetHistory(Conversation).Select(message => message.Text), Is.EqualTo(["First question.", "Second question.", "Second answer."]));
                Assert.That(secondCallMessages.Where(message => message.Role != ChatRole.System).Select(message => message.Text), Is.EqualTo(["First question.", "(This answer stopped before its end, before any tool was called.)", "Second question."]));
            }
        }

        private static string Instructions(ChatCall call)
        {
            return string.Join('\n', call.Messages.Where(message => message.Role == ChatRole.System).Select(message => message.Text).Append(call.Options?.Instructions).Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        private static List<string> UserTexts(ChatCall call)
        {
            return [.. call.Messages.Where(message => message.Role == ChatRole.User).Select(message => message.Text)];
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> Silent([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            yield break;
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!condition())
            {
                await Task.Delay(10, timeout.Token);
            }
        }
    }
}
