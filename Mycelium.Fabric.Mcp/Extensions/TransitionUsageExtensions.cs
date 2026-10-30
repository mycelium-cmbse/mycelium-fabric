// ------------------------------------------------------------------------------------------------
//  <copyright file="TransitionUsageExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Systems.States;

    /// <summary>
    /// Extension methods that read the source and the target of an <see cref="ITransitionUsage"/>.
    /// </summary>
    public static class TransitionUsageExtensions
    {
        /// <param name="transition">The <see cref="ITransitionUsage"/> to read.</param>
        extension(ITransitionUsage transition)
        {
            /// <summary>
            /// Describes the ends of the transition by their names, as they are written in
            /// <c>transition first launch then safe</c>: its derived <c>source</c>, then its derived <c>target</c>.
            /// </summary>
            /// <returns>The name of the source and of the target, without the one that the transition does not have.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="transition"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<string> GetEndPaths()
            {
                ArgumentNullException.ThrowIfNull(transition);

                return [.. new[] { transition.source, transition.target }.Where(end => end != null).Select(end => end.name ?? end.Id.ToString())];
            }
        }
    }
}
