// ------------------------------------------------------------------------------------------------
//  <copyright file="IFabricToolbox.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Hypha
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Gives the LLM the tools of the Fabric MCP server.
    /// </summary>
    public interface IFabricToolbox
    {
        /// <summary>
        /// Opens a session with the Fabric MCP server and lists the tools that the LLM may call.
        /// </summary>
        /// <param name="cancellationToken">Cancels the opening of the session.</param>
        /// <returns>The <see cref="FabricToolSession"/>, which the caller disposes once the answer is over.</returns>
        Task<FabricToolSession> OpenAsync(CancellationToken cancellationToken);
    }
}
