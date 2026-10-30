// ------------------------------------------------------------------------------------------------
//  <copyright file="IRequestBodyReader.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Serialization
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Reads the body of a request through the <see cref="IRequestBodyDeserializer"/> matching its <c>Content-Type</c>.
    /// </summary>
    public interface IRequestBodyReader
    {
        /// <summary>
        /// Reads a body that must carry a single request model.
        /// </summary>
        /// <typeparam name="TRequest">The request model the body is expected to carry.</typeparam>
        /// <param name="request">The <see cref="HttpRequest"/> whose body is read.</param>
        /// <returns>The request model, or the error that prevented reading it.</returns>
        Task<ErrorOr<TRequest>> ReadRequestAsync<TRequest>(HttpRequest request)
            where TRequest : class, IRequest;

        /// <summary>
        /// Reads an optional body carrying a collection of Data.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> whose body is read.</param>
        /// <returns>The Data the body carries, empty when there is no body, or the error that prevented reading it.</returns>
        Task<ErrorOr<IReadOnlyList<IData>>> ReadDataAsync(HttpRequest request);
    }
}
