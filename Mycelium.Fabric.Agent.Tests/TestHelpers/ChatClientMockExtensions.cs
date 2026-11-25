// ------------------------------------------------------------------------------------------------
//  <copyright file="ChatClientMockExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.AI;

    using Moq;

    /// <summary>
    /// Sets up the answers of a mocked LLM and builds them.
    /// </summary>
    public static class ChatClientMockExtensions
    {
        extension(Mock<IChatClient> chatClient)
        {
            /// <summary>
            /// Makes the mocked LLM give the answers in their order, one per streamed call, and records the calls.
            /// </summary>
            public List<ChatCall> SetupAnswers(params IReadOnlyList<ChatResponseUpdate>[] answers)
            {
                var calls = new List<ChatCall>();
                var remainingAnswers = new Queue<IReadOnlyList<ChatResponseUpdate>>(answers);

                chatClient.Setup(client => client.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
                    .Returns((IEnumerable<ChatMessage> messages, ChatOptions options, CancellationToken _) =>
                    {
                        lock (calls)
                        {
                            calls.Add(new ChatCall([.. messages], options));

                            return Stream(remainingAnswers.Dequeue());
                        }
                    });

                return calls;
            }
        }

        public static IReadOnlyList<ChatResponseUpdate> Text(params string[] parts)
        {
            var messageId = Guid.NewGuid().ToString();

            return [.. parts.Select(part => new ChatResponseUpdate(ChatRole.Assistant, part) { MessageId = messageId })];
        }

        public static IReadOnlyList<ChatResponseUpdate> ToolCall(string callId, string toolName, Dictionary<string, object> arguments)
        {
            return [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, toolName, arguments)]) { MessageId = Guid.NewGuid().ToString() }];
        }

        public static async IAsyncEnumerable<ChatResponseUpdate> Stream(IEnumerable<ChatResponseUpdate> updates)
        {
            foreach (var update in updates)
            {
                await Task.Yield();

                yield return update;
            }
        }
    }
}
