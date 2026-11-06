// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementImport.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// The result of the import of the requirements chosen by the user, sent to the view of the extraction.
    /// </summary>
    /// <param name="Message">
    /// The message for the AI assistant: the view writes it in the conversation, so that the assistant goes on.
    /// </param>
    /// <param name="Result">The <see cref="ApplyChangesResult"/> of the batch built from the choices of the user.</param>
    public sealed record RequirementImport(string Message, ApplyChangesResult Result)
    {
        /// <summary>
        /// Gets a value indicating whether the requirements have been imported, in one commit.
        /// </summary>
        public bool Imported => this.Result.Applied;
    }
}
