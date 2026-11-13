// ------------------------------------------------------------------------------------------------
//  <copyright file="MultiplicityExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Multiplicities;

    /// <summary>
    /// Extension methods that read, from the <see cref="IMultiplicity"/>s of a SysML v2 model, the information used by the
    /// MCP tools.
    /// </summary>
    public static class MultiplicityExtensions
    {
        /// <param name="multiplicity">The <see cref="IMultiplicity"/> to read.</param>
        extension(IMultiplicity multiplicity)
        {
            /// <summary>
            /// Gets the range of the number of units that the multiplicity allows: <c>(1, 4)</c> for <c>[1..4]</c>,
            /// <c>(4, 4)</c> for <c>[4]</c>, and no largest number for <c>[0..*]</c>.
            /// </summary>
            /// <returns>The range, or <c>null</c> when the bounds of the multiplicity are not numbers.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="multiplicity"/> is <c>null</c>.
            /// </exception>
            public (int Minimum, int? Maximum)? GetUnitRange()
            {
                ArgumentNullException.ThrowIfNull(multiplicity);

                return multiplicity switch
                {
                    IMultiplicityRange { lowerBound: null, upperBound: ILiteralInteger upper } => (upper.Value, upper.Value),
                    IMultiplicityRange { lowerBound: null, upperBound: ILiteralInfinity } => (0, null),
                    IMultiplicityRange { lowerBound: ILiteralInteger lower, upperBound: ILiteralInteger upper } => (lower.Value, upper.Value),
                    IMultiplicityRange { lowerBound: ILiteralInteger lower, upperBound: ILiteralInfinity } => (lower.Value, null),
                    _ => null
                };
            }
        }
    }
}
