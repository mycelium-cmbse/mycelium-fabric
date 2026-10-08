// ------------------------------------------------------------------------------------------------
//  <copyright file="InvalidChangeException.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;

    /// <summary>
    /// The exception thrown when a <see cref="ModelChange"/> cannot be applied. Its message, written for the AI assistant,
    /// becomes one of the problems of the <see cref="ApplyChangesResult"/>.
    /// </summary>
    public sealed class InvalidChangeException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidChangeException"/> class.
        /// </summary>
        /// <param name="message">The message that explains why the change cannot be applied.</param>
        public InvalidChangeException(string message) : base(message)
        {
        }
    }
}
