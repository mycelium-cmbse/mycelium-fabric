// ------------------------------------------------------------------------------------------------
//  <copyright file="MissingValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A part that should add a value to a sum and has none yet, the value being "to be defined": the part declares the
    /// attribute without value, or it has the type of a part that has a value.
    /// </summary>
    /// <param name="Id">The identifier of the part.</param>
    /// <param name="QualifiedName">The qualified name of the part.</param>
    /// <param name="Types">The declared names of the types of the part, empty when it is not typed.</param>
    public sealed record MissingValue(Guid Id, string QualifiedName, IReadOnlyList<string> Types)
    {
        /// <summary>
        /// The number of parts named in a message, the others being counted.
        /// </summary>
        private const int NamedPartCount = 5;

        /// <summary>
        /// Writes, for a message, the parts whose value is missing: their qualified names, the first ones only when there
        /// are many.
        /// </summary>
        /// <param name="missingValues">The missing values.</param>
        /// <returns>The parts, for example <c>EOSat1::eosat1::camera, EOSat1::eosat1::radio and 3 more</c>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="missingValues"/> is <c>null</c>.
        /// </exception>
        public static string Describe(IReadOnlyList<MissingValue> missingValues)
        {
            ArgumentNullException.ThrowIfNull(missingValues);

            var names = string.Join(", ", missingValues.Take(NamedPartCount).Select(missingValue => missingValue.QualifiedName));

            return missingValues.Count > NamedPartCount ? $"{names} and {missingValues.Count - NamedPartCount} more" : names;
        }
    }
}
