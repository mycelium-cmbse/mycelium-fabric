// ------------------------------------------------------------------------------------------------
//  <copyright file="RequestBodyReader.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Net.Http.Headers;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;

    using Error = ErrorOr.Error;

    /// <summary>
    /// Reads the body of a request through the <see cref="IRequestBodyDeserializer"/> matching its <c>Content-Type</c>.
    /// </summary>
    public class RequestBodyReader : IRequestBodyReader
    {
        /// <summary>
        /// The <see cref="Error.NumericType"/> of the error reporting a body in a format no deserializer reads.
        /// </summary>
        public const int UnsupportedMediaType = StatusCodes.Status415UnsupportedMediaType;

        /// <summary>
        /// The (injected) deserializers, one per supported wire format.
        /// </summary>
        private readonly IReadOnlyList<IRequestBodyDeserializer> deserializers;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestBodyReader"/> class.
        /// </summary>
        /// <param name="deserializers">The (injected) deserializers, one per supported wire format.</param>
        public RequestBodyReader(IEnumerable<IRequestBodyDeserializer> deserializers)
        {
            ArgumentNullException.ThrowIfNull(deserializers);

            this.deserializers = [.. deserializers];
        }

        /// <summary>
        /// Reads a body that must carry a single request model.
        /// </summary>
        /// <typeparam name="TRequest">The request model the body is expected to carry.</typeparam>
        /// <param name="request">The <see cref="HttpRequest"/> whose body is read.</param>
        /// <returns>The request model, or the error that prevented reading it.</returns>
        public async Task<ErrorOr<TRequest>> ReadRequestAsync<TRequest>(HttpRequest request)
            where TRequest : class, IRequest
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!HasBody(request))
            {
                return Error.Validation("body.missing", $"The request carries no {typeof(TRequest).Name}.");
            }

            var deserializer = this.Select(request);

            if (deserializer.IsError)
            {
                return deserializer.Errors;
            }

            try
            {
                return await deserializer.Value.ReadRequestAsync<TRequest>(request.Body, request.HttpContext.RequestAborted);
            }
            catch (InvalidDataException exception)
            {
                return Error.Validation("body.invalid", exception.Message);
            }
        }

        /// <summary>
        /// Reads an optional body carrying a collection of Data.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> whose body is read.</param>
        /// <returns>The Data the body carries, empty when there is no body, or the error that prevented reading it.</returns>
        public async Task<ErrorOr<IReadOnlyList<IData>>> ReadDataAsync(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!HasBody(request))
            {
                IReadOnlyList<IData> none = [];

                return ErrorOrFactory.From(none);
            }

            var deserializer = this.Select(request);

            if (deserializer.IsError)
            {
                return deserializer.Errors;
            }

            try
            {
                return ErrorOrFactory.From(await deserializer.Value.ReadDataAsync(request.Body, request.HttpContext.RequestAborted));
            }
            catch (InvalidDataException exception)
            {
                return Error.Validation("body.invalid", exception.Message);
            }
        }

        /// <summary>
        /// Tells whether a request carries a body, including a chunked one that declares no <c>Content-Length</c>.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> inspected.</param>
        /// <returns><c>true</c> unless the request declares or is known to have no body.</returns>
        private static bool HasBody(HttpRequest request)
        {
            return request.ContentLength != 0 && request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>() is not { CanHaveBody: false };
        }

        /// <summary>
        /// Selects the deserializer that reads the <c>Content-Type</c> of the request.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequest"/> whose body is read.</param>
        /// <returns>The deserializer, or the <see cref="UnsupportedMediaType"/> error when none reads the body.</returns>
        private ErrorOr<IRequestBodyDeserializer> Select(HttpRequest request)
        {
            var deserializer = MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
                ? this.deserializers.FirstOrDefault(candidate => candidate.CanRead(contentType))
                : null;

            if (deserializer is null)
            {
                return Error.Custom(UnsupportedMediaType, "body.unsupportedMediaType", $"No request body of type '{request.ContentType}' can be read.");
            }

            return ErrorOrFactory.From(deserializer);
        }
    }
}
