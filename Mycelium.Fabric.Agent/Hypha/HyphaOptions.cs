// ------------------------------------------------------------------------------------------------
//  <copyright file="HyphaOptions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Hypha
{
    using System;

    /// <summary>
    /// The settings of Hypha, the assistant of Bloom, read from the <c>Hypha</c> section of the configuration.
    /// </summary>
    public class HyphaOptions
    {
        /// <summary>
        /// The name of the section of the configuration that holds these settings.
        /// </summary>
        public const string SectionName = "Hypha";

        /// <summary>
        /// Gets or sets the base address of the OpenAI-compatible API of the LLM, such as the <c>/v1</c> address of llama.cpp.
        /// </summary>
        public Uri Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the name of the model that the API serves.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the key of the API, or <c>null</c> when the API asks for none, as a local llama.cpp.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the size, in tokens, of the context window of the model: the older messages of a conversation are
        /// summarized once the conversation fills what <see cref="ReservedTokens"/> leaves of it.
        /// </summary>
        public int ContextSize { get; set; } = 32768;

        /// <summary>
        /// Gets or sets the tokens of the context window kept for the instructions, the description of the tools (about
        /// 15,000 tokens for the tools of Fabric) and the answer.
        /// </summary>
        public int ReservedTokens { get; set; } = 20000;

        /// <summary>
        /// Gets or sets how long an answer may stay silent, with no text and no tool call, before it fails.
        /// </summary>
        public TimeSpan SilenceTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the address of the Fabric MCP server whose tools the assistant calls.
        /// </summary>
        public Uri McpEndpoint { get; set; }
    }
}
