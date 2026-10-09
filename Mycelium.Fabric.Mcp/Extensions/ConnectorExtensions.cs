// ------------------------------------------------------------------------------------------------
//  <copyright file="ConnectorExtensions.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.Connectors;
    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Extension methods that read the ends of an <see cref="IConnector"/>, such as a connection or an interface.
    /// </summary>
    public static class ConnectorExtensions
    {
        /// <param name="connector">The <see cref="IConnector"/> to read.</param>
        extension(IConnector connector)
        {
            /// <summary>
            /// Describes the ends of the connector as paths of names, as they are written in
            /// <c>connect payloadSubsystem.camera.dataOut to dataHandlingSubsystem.obc.dataIn</c>.
            /// </summary>
            /// <returns>The path of each end, from the owner of the connector.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="connector"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<string> GetEndPaths()
            {
                ArgumentNullException.ThrowIfNull(connector);

                return [.. connector.GetConnectedPaths().Select(path => string.Join(".", path.Select(feature => feature.name ?? feature.Id.ToString())))];
            }

            /// <summary>
            /// Tells whether the connector connects an element: an end goes through the element, or connects one of its own
            /// features, such as a port of a part.
            /// </summary>
            /// <param name="element">The element.</param>
            /// <returns><c>true</c> when the connector connects the element.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="connector"/> is <c>null</c>.
            /// </exception>
            public bool Connects(IElement element)
            {
                ArgumentNullException.ThrowIfNull(connector);

                return element != connector && connector.GetConnectedPaths()
                    .SelectMany(path => path)
                    .Any(feature => feature == element || feature.owner == element);
            }

            /// <summary>
            /// Gets, for each end of the connector, the features of its path: the chaining features of a feature chain such as
            /// <c>camera.dataOut</c>, or the connected feature alone.
            /// </summary>
            /// <returns>The features of the path of each end.</returns>
            private IEnumerable<IReadOnlyList<IFeature>> GetConnectedPaths()
            {
                return (connector.relatedFeature ?? [])
                    .Where(feature => feature != null)
                    .Select(feature => feature.chainingFeature is { Count: > 0 } chainingFeatures ? chainingFeatures : [feature]);
            }
        }
    }
}
