// ------------------------------------------------------------------------------------------------
//  <copyright file="ChatCall.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.TestHelpers
{
    using System.Collections.Generic;

    using Microsoft.Extensions.AI;

    /// <summary>
    /// A call to the mocked LLM: the messages and the options it received.
    /// </summary>
    public sealed record ChatCall(IReadOnlyList<ChatMessage> Messages, ChatOptions Options);
}
