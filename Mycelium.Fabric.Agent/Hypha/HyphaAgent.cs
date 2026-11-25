// ------------------------------------------------------------------------------------------------
//  <copyright file="HyphaAgent.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Hypha
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Agents.AI;
    using Microsoft.Agents.AI.Compaction;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Hypha, the assistant of Bloom: a <see cref="ChatClientAgent"/> of the Microsoft Agent Framework, which calls the tools of
    /// Fabric until the LLM gives its answer, keeps one session per conversation in memory, and has the LLM summarize the
    /// older messages of a conversation once it fills what <see cref="HyphaOptions.ReservedTokens"/> leaves of the context
    /// window. The instructions and the tools come first and stay the same from one answer to the next, so that the LLM
    /// server reads them once and keeps them in its cache: the context of an answer goes with the message of the user.
    /// </summary>
    public class HyphaAgent : IHyphaAgent
    {
        /// <summary>
        /// The conversations, by their identifier.
        /// </summary>
        private readonly ConcurrentDictionary<string, Conversation> conversations = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="HyphaAgent"/> class.
        /// </summary>
        /// <param name="chatClient">The <see cref="IChatClient"/> of the LLM, which also writes the summaries.</param>
        /// <param name="toolbox">The <see cref="IFabricToolbox"/> that gives the tools of Fabric.</param>
        /// <param name="options">The <see cref="HyphaOptions"/>.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> of the agent.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <see cref="HyphaOptions.ReservedTokens"/> leaves no room for the conversation.</exception>
        public HyphaAgent(IChatClient chatClient, IFabricToolbox toolbox, IOptions<HyphaOptions> options, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(chatClient);
            ArgumentNullException.ThrowIfNull(toolbox);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            this.Toolbox = toolbox;
            this.Options = options.Value;

            var conversationTokens = this.Options.ContextSize - this.Options.ReservedTokens;

            if (conversationTokens <= 0)
            {
                throw new ArgumentException($"{HyphaOptions.SectionName}:{nameof(HyphaOptions.ContextSize)} ({this.Options.ContextSize}) must be larger than {HyphaOptions.SectionName}:{nameof(HyphaOptions.ReservedTokens)} ({this.Options.ReservedTokens}).", nameof(options));
            }

            var compaction = new CompactionProvider(new SummarizationCompactionStrategy(chatClient, CompactionTriggers.TokensExceed(conversationTokens)), loggerFactory: loggerFactory);

            this.History = new InMemoryChatHistoryProvider();

            // The history is stored at the end of each answer: storing it after each call of the LLM would stop the compaction.
            this.Agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
            {
                Name = "Hypha",
                ChatOptions = new ChatOptions { Instructions = HyphaInstructions.System },
                ChatHistoryProvider = this.History,
                AIContextProviders = [compaction]
            }, loggerFactory);
        }

        /// <summary>
        /// Gets the agent that runs the LLM and the tools.
        /// </summary>
        private ChatClientAgent Agent { get; }

        /// <summary>
        /// Gets the history of the conversations that the agent gives to the LLM, kept in the sessions.
        /// </summary>
        private InMemoryChatHistoryProvider History { get; }

        /// <summary>
        /// Gets the <see cref="IFabricToolbox"/> that gives the tools of Fabric.
        /// </summary>
        private IFabricToolbox Toolbox { get; }

        /// <summary>
        /// Gets the <see cref="HyphaOptions"/>.
        /// </summary>
        private HyphaOptions Options { get; }

        /// <summary>
        /// Gets every message of a conversation, with the calls of tools and their results, even those already summarized for the LLM.
        /// </summary>
        /// <param name="threadId">The identifier of the conversation.</param>
        /// <returns>The messages in their order, or an empty list for a conversation that has none.</returns>
        public IReadOnlyList<ChatMessage> GetHistory(string threadId) => !string.IsNullOrWhiteSpace(threadId) && this.conversations.TryGetValue(threadId, out var conversation) ? conversation.GetMessages() : [];

        /// <summary>
        /// Answers a message of a conversation, once the previous answer of the same conversation is over: the messages of a
        /// conversation wait for their turn, in their order. The answer fails when the LLM stays silent for longer than
        /// <see cref="HyphaOptions.SilenceTimeout"/>; the time spent waiting for the turn does not count.
        /// </summary>
        /// <param name="threadId">The identifier of the conversation, one per user and project.</param>
        /// <param name="message">The message of the user.</param>
        /// <param name="context">What the user works on, such as the project and the selected elements, for this answer only; <c>null</c> when none.</param>
        /// <param name="cancellationToken">Cancels the answer, as when the user closes the stream or sends a message at once.</param>
        /// <returns>The parts of the answer as the LLM writes them, with the calls of tools and their results.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="threadId"/> is <c>null</c> or blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <c>null</c>.</exception>
        /// <exception cref="TimeoutException">Thrown when the LLM stays silent for longer than <see cref="HyphaOptions.SilenceTimeout"/>.</exception>
        public async IAsyncEnumerable<ChatResponseUpdate> RunAsync(string threadId, ChatMessage message, string context, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
            ArgumentNullException.ThrowIfNull(message);

            var conversation = this.conversations.GetOrAdd(threadId, static _ => new Conversation());
            await conversation.Turn.WaitAsync(cancellationToken);
            var request = string.IsNullOrWhiteSpace(context) ? message : new ChatMessage(ChatRole.User, [new TextContent($"{context}\n\n"), .. message.Contents]);
            var answer = new List<ChatResponseUpdate>();
            var isComplete = false;

            try
            {
                conversation.Session ??= await this.Agent.CreateSessionAsync(cancellationToken);

                await using var tools = await this.Toolbox.OpenAsync(cancellationToken);
                using var silence = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var runOptions = new ChatClientAgentRunOptions(new ChatOptions { Tools = tools.Tools });
                await using var updates = this.Agent.RunStreamingAsync(request, conversation.Session, runOptions, silence.Token).GetAsyncEnumerator(silence.Token);

                while (await this.MoveNextAsync(updates, silence, cancellationToken))
                {
                    var update = updates.Current.AsChatResponseUpdate();
                    answer.Add(update);

                    yield return update;
                }

                isComplete = true;
            }
            finally
            {
                if (!isComplete && conversation.Session is not null)
                {
                    this.RecordStoppedAnswer(conversation.Session, request, answer);
                }

                conversation.Record(message, answer);
                conversation.Turn.Release();
            }
        }

        /// <summary>
        /// Waits for the next part of an answer, for <see cref="HyphaOptions.SilenceTimeout"/> at most.
        /// </summary>
        /// <param name="updates">The parts of the answer.</param>
        /// <param name="silence">Cancels the answer once the LLM has been silent for too long.</param>
        /// <param name="cancellationToken">The cancellation asked by the caller.</param>
        /// <returns><c>true</c> when a part has come, <c>false</c> at the end of the answer.</returns>
        /// <exception cref="TimeoutException">Thrown when no part has come in time.</exception>
        private async Task<bool> MoveNextAsync(IAsyncEnumerator<AgentResponseUpdate> updates, CancellationTokenSource silence, CancellationToken cancellationToken)
        {
            silence.CancelAfter(this.Options.SilenceTimeout);

            try
            {
                return await updates.MoveNextAsync();
            }
            catch (OperationCanceledException exception) when (silence.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The LLM sent nothing for {this.Options.SilenceTimeout.TotalSeconds} s.", exception);
            }
        }

        /// <summary>
        /// Adds to the history that the LLM reads a message whose answer has stopped before its end, cancelled or failed, with
        /// the tools that the answer had already called: they may have changed the model.
        /// </summary>
        /// <param name="session">The session of the conversation.</param>
        /// <param name="message">The message of the user.</param>
        /// <param name="answer">The parts of the answer that came.</param>
        private void RecordStoppedAnswer(AgentSession session, ChatMessage message, IEnumerable<ChatResponseUpdate> answer)
        {
            var contents = answer.SelectMany(update => update.Contents).ToList();
            var resultIds = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();
            var calledTools = contents.OfType<FunctionCallContent>().Where(call => resultIds.Contains(call.CallId)).Select(call => call.Name).ToList();

            var note = calledTools.Count == 0
                ? "(This answer stopped before its end, before any tool was called.)"
                : $"(This answer stopped before its end, after calling: {string.Join(", ", calledTools)}.)";

            // A failed answer may already have stored its message: it is not added twice.
            var stored = this.History.GetMessages(session);
            List<ChatMessage> history = stored.Contains(message) ? [.. stored, new ChatMessage(ChatRole.Assistant, note)] : [.. stored, message, new ChatMessage(ChatRole.Assistant, note)];

            this.History.SetMessages(session, history);
        }

        /// <summary>
        /// A conversation: its session with the agent, the turn that its messages wait for, and every message it has had.
        /// </summary>
        private sealed class Conversation
        {
            /// <summary>
            /// Every message of the conversation, kept apart from the session, whose history the summaries shorten.
            /// </summary>
            private readonly List<ChatMessage> messages = [];

            /// <summary>
            /// Gets the turn of the conversation: one answer at a time.
            /// </summary>
            public SemaphoreSlim Turn { get; } = new(1, 1);

            /// <summary>
            /// Gets or sets the session of the conversation with the agent, created with its first answer.
            /// </summary>
            public AgentSession Session { get; set; }

            /// <summary>
            /// Gets a copy of every message of the conversation.
            /// </summary>
            /// <returns>The messages in their order.</returns>
            public IReadOnlyList<ChatMessage> GetMessages()
            {
                lock (this.messages)
                {
                    return [.. this.messages];
                }
            }

            /// <summary>
            /// Adds a message of the user and its answer, even partial, to the conversation.
            /// </summary>
            /// <param name="message">The message of the user.</param>
            /// <param name="answer">The parts of the answer that came.</param>
            public void Record(ChatMessage message, IEnumerable<ChatResponseUpdate> answer)
            {
                var response = answer.ToChatResponse();

                lock (this.messages)
                {
                    this.messages.Add(message);
                    this.messages.AddRange(response.Messages);
                }
            }
        }
    }
}
