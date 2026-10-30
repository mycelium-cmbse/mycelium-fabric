// ------------------------------------------------------------------------------------------------
//  <copyright file="IRequestBodyDeserializer.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Serialization
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Net.Http.Headers;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Reads request bodies of the REST/HTTP PSM written in one wire format.
    /// </summary>
    public interface IRequestBodyDeserializer
    {
        /// <summary>
        /// Asserts whether this deserializer reads bodies of the given media type.
        /// </summary>
        /// <param name="contentType">The <c>Content-Type</c> of the request.</param>
        /// <returns>True when the body can be read by this deserializer.</returns>
        bool CanRead(MediaTypeHeaderValue contentType);

        /// <summary>
        /// Reads a body carrying a single request model.
        /// </summary>
        /// <typeparam name="TRequest">The request model the body is expected to carry.</typeparam>
        /// <param name="body">The request body.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the read.</param>
        /// <returns>The request model the body carries.</returns>
        /// <exception cref="InvalidDataException">Thrown when the body is not a well-formed <typeparamref name="TRequest"/>.</exception>
        Task<TRequest> ReadRequestAsync<TRequest>(Stream body, CancellationToken cancellationToken)
            where TRequest : class, IRequest;

        /// <summary>
        /// Reads a body carrying a collection of Data.
        /// </summary>
        /// <param name="body">The request body.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the read.</param>
        /// <returns>The Data the body carries.</returns>
        /// <exception cref="InvalidDataException">Thrown when the body is not a well-formed collection of Data.</exception>
        Task<IReadOnlyList<IData>> ReadDataAsync(Stream body, CancellationToken cancellationToken);
    }
}
