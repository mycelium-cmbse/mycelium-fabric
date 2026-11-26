// ------------------------------------------------------------------------------------------------
//  <copyright file="HyphaApi.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    using AGUI.Abstractions;
    using AGUI.Formatting;
    using AGUI.Server;

    using Carter;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.Agent.Hypha;

    /// <summary>
    /// The routes of Hypha that Bloom calls: the AG-UI endpoint, which answers a message as a stream of events, and the
    /// history of a conversation. Bloom sends only the new message of a conversation: Fabric keeps the conversation.
    /// </summary>
    public class HyphaApi : ICarterModule
    {
        /// <summary>
        /// The route of the AG-UI endpoint.
        /// </summary>
        public const string RunRoute = "/hypha/agui";

        /// <summary>
        /// The route of the history of a conversation.
        /// </summary>
        public const string ThreadRoute = "/hypha/threads/{threadId}";

        /// <summary>
        /// The log template of an answer that has been streamed to its end.
        /// </summary>
        private const string RunCompletedLogTemplate = "Hypha answered run {RunId} of thread {ThreadId}";

        /// <summary>
        /// The JSON options of the AG-UI types and of the contents of Microsoft.Extensions.AI.
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        /// <summary>
        /// Writes the events as Server-Sent Events, the transport of AG-UI over HTTP.
        /// </summary>
        private static readonly SseEventStreamFormatter Formatter = new();

        /// <summary>
        /// Writes every message of a conversation, as AG-UI messages, so that Bloom shows the conversation again after a reload.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request, whose route gives the identifier of the conversation.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
        public static Task GetThread(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var threadId = context.Request.RouteValues["threadId"] as string;
            var history = context.RequestServices.GetRequiredService<IHyphaAgent>().GetHistory(threadId);
            List<AGUIMessage> messages = [.. history.AsAGUIMessages(SerializerOptions)];

            return context.Response.WriteAsJsonAsync(messages, SerializerOptions, context.RequestAborted);
        }

        /// <summary>
        /// Answers the last message of the user of an AG-UI <see cref="RunAgentInput"/>, with the project, the selection and the
        /// user given as context, as a stream of AG-UI events. A failure of the answer ends the stream with a <see cref="RunErrorEvent"/>;
        /// the client cancels the answer by closing the stream.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request, whose body is the <see cref="RunAgentInput"/>.</param>
        /// <returns>A <see cref="Task"/> that completes once the stream is over.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
        public static async Task RunAgent(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var logger = context.RequestServices.GetRequiredService<ILogger<HyphaApi>>();
            var (input, problem) = await ReadInputAsync(context.Request);
            var request = input?.ToChatRequestContext(SerializerOptions);
            var message = request?.Messages.LastOrDefault(chatMessage => chatMessage.Role == ChatRole.User);

            if (message is null)
            {
                problem ??= "The run has no message of the user.";

                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Hypha refused a run: {Problem}", problem);
                }

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(problem, context.RequestAborted);

                return;
            }

            // The AG-UI conversion ends a failed answer with a RunErrorEvent of its own, which the failure then explains.
            var failure = new StrongBox<Exception>();
            var agent = context.RequestServices.GetRequiredService<IHyphaAgent>();
            var updates = RecordFailure(agent.RunAsync(input.ThreadId, message, DescribeContext(input.Context), context.RequestAborted), failure, input, logger, context.RequestAborted);
            var events = updates.AsAGUIEventStreamAsync(request, context.RequestAborted).Select(runEvent => runEvent is RunErrorEvent ? ExplainFailure(failure.Value) ?? runEvent : runEvent);

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = Formatter.MediaType;
            context.Response.Headers.CacheControl = "no-cache,no-store";
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            try
            {
                await Formatter.WriteAsync(events, context.Response.Body, context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
            catch (OperationCanceledException exception) when (context.RequestAborted.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(exception, "Hypha stopped run {RunId} of thread {ThreadId}: the client closed the stream", input.RunId, input.ThreadId);
                }

                return;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(RunCompletedLogTemplate, input.RunId, input.ThreadId);
            }
        }

        /// <summary>
        /// Registers the routes of Hypha.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> of the host.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <c>null</c>.</exception>
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.MapPost(RunRoute, RunAgent);
            app.MapGet(ThreadRoute, GetThread);
        }

        /// <summary>
        /// Creates the JSON options of the AG-UI types, which come first, and of the contents of Microsoft.Extensions.AI.
        /// </summary>
        /// <returns>The read-only <see cref="JsonSerializerOptions"/>.</returns>
        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.TypeInfoResolverChain.Add(AGUIJsonUtilities.DefaultTypeInfoResolver);
            options.TypeInfoResolverChain.Add(AIJsonUtilities.DefaultOptions.TypeInfoResolver);
            options.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
            AGUIJsonUtilities.RegisterInterruptContentTypes(options);
            options.MakeReadOnly();

            return options;
        }

        /// <summary>
        /// Writes the context of an AG-UI run as instructions for the LLM, one line per piece of context.
        /// </summary>
        /// <param name="context">The pieces of context, such as the project, the selected elements and the user.</param>
        /// <returns>The instructions, or <c>null</c> when the run has no context.</returns>
        private static string DescribeContext(IEnumerable<AGUIContext> context)
        {
            var lines = context?.Select(piece => $"- {piece.Description}: {piece.Value}").ToList() ?? [];

            return lines.Count == 0 ? null : $"The user works in this context:\n{string.Join('\n', lines)}";
        }

        /// <summary>
        /// Explains to the user the failures whose cause is known: the silence of the LLM, or an LLM that cannot be reached.
        /// </summary>
        /// <param name="failure">The failure of the answer, or <c>null</c> when none was recorded.</param>
        /// <returns>The <see cref="RunErrorEvent"/> that explains the failure, or <c>null</c> when its cause is not known.</returns>
        private static RunErrorEvent ExplainFailure(Exception failure)
        {
            var unreachable = failure;

            while (unreachable is not null and not HttpRequestException)
            {
                unreachable = unreachable.InnerException;
            }

            return failure switch
            {
                TimeoutException => new RunErrorEvent { Code = "silence", Message = failure.Message },
                not null when unreachable is not null => new RunErrorEvent { Code = "llm_unreachable", Message = $"The LLM cannot be reached: {unreachable.Message}" },
                _ => null
            };
        }

        /// <summary>
        /// Waits for the next part of an answer, and records and logs its failure before passing it on.
        /// </summary>
        /// <param name="updates">The parts of the answer.</param>
        /// <param name="failure">Receives the failure of the answer.</param>
        /// <param name="input">The <see cref="RunAgentInput"/> of the run.</param>
        /// <param name="logger">The <see cref="ILogger"/> that logs the failure.</param>
        /// <param name="cancellationToken">The cancellation asked by the client, which is not a failure.</param>
        /// <returns><c>true</c> when a part has come, <c>false</c> at the end of the answer.</returns>
        private static async Task<bool> MoveNextAsync(IAsyncEnumerator<ChatResponseUpdate> updates, StrongBox<Exception> failure, RunAgentInput input, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                return await updates.MoveNextAsync();
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                failure.Value = exception;

                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(exception, "Hypha failed run {RunId} of thread {ThreadId}", input.RunId, input.ThreadId);
                }

                throw;
            }
        }

        /// <summary>
        /// Reads the <see cref="RunAgentInput"/> of a request and checks that it names its conversation and its run.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/>, whose body is JSON.</param>
        /// <returns>The input, or <c>null</c> with the problem that a person reads when the body is not a valid input.</returns>
        private static async Task<(RunAgentInput Input, string Problem)> ReadInputAsync(HttpRequest request)
        {
            RunAgentInput input;

            try
            {
                input = await JsonSerializer.DeserializeAsync<RunAgentInput>(request.Body, SerializerOptions, request.HttpContext.RequestAborted);
            }
            catch (JsonException exception)
            {
                return (null, $"The body is not an AG-UI RunAgentInput: {exception.Message}");
            }

            return input switch
            {
                null => (null, "The body is empty."),
                { ThreadId: var threadId } when string.IsNullOrWhiteSpace(threadId) => (null, "The run has no threadId."),
                { RunId: var runId } when string.IsNullOrWhiteSpace(runId) => (null, "The run has no runId."),
                _ => (input, null)
            };
        }

        /// <summary>
        /// Passes on the parts of an answer, and records and logs its failure, if any.
        /// </summary>
        /// <param name="updates">The parts of the answer.</param>
        /// <param name="failure">Receives the failure of the answer.</param>
        /// <param name="input">The <see cref="RunAgentInput"/> of the run.</param>
        /// <param name="logger">The <see cref="ILogger"/> that logs the failure.</param>
        /// <param name="cancellationToken">The cancellation asked by the client.</param>
        /// <returns>The parts of the answer.</returns>
        private static async IAsyncEnumerable<ChatResponseUpdate> RecordFailure(IAsyncEnumerable<ChatResponseUpdate> updates, StrongBox<Exception> failure, RunAgentInput input, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var enumerator = updates.GetAsyncEnumerator(cancellationToken);

            while (await MoveNextAsync(enumerator, failure, input, logger, cancellationToken))
            {
                yield return enumerator.Current;
            }
        }
    }
}
