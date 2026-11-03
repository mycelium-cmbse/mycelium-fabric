// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonRequestBodyDeserializer.cs" company="Starion Group S.A.">
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
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Net.Http.Headers;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Reads JSON request bodies of the REST/HTTP PSM through the SysML2.NET <see cref="IDeSerializer"/>.
    /// </summary>
    public class JsonRequestBodyDeserializer : IRequestBodyDeserializer
    {
        /// <summary>
        /// The (injected) <see cref="IDeSerializer"/> that reads the JSON of the REST/HTTP PSM.
        /// </summary>
        private readonly IDeSerializer deSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestBodyDeserializer"/> class.
        /// </summary>
        /// <param name="deSerializer">The (injected) <see cref="IDeSerializer"/>.</param>
        public JsonRequestBodyDeserializer(IDeSerializer deSerializer)
        {
            ArgumentNullException.ThrowIfNull(deSerializer);

            this.deSerializer = deSerializer;
        }

        /// <summary>
        /// Asserts whether this deserializer reads bodies of the given media type.
        /// </summary>
        /// <param name="contentType">The <c>Content-Type</c> of the request.</param>
        /// <returns>True when <paramref name="contentType"/> is <see cref="JsonResponseNegotiator.MediaType"/>.</returns>
        public bool CanRead(MediaTypeHeaderValue contentType)
        {
            ArgumentNullException.ThrowIfNull(contentType);

            return contentType.MediaType.Equals(JsonResponseNegotiator.MediaType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads a body carrying a single request model.
        /// </summary>
        /// <typeparam name="TRequest">The request model the body is expected to carry.</typeparam>
        /// <param name="body">The request body.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the read.</param>
        /// <returns>The request model the body carries.</returns>
        /// <exception cref="InvalidDataException">Thrown when the body is not a well-formed <typeparamref name="TRequest"/>.</exception>
        public async Task<TRequest> ReadRequestAsync<TRequest>(Stream body, CancellationToken cancellationToken)
            where TRequest : class, IRequest
        {
            ArgumentNullException.ThrowIfNull(body);

            try
            {
                return await this.deSerializer.DeSerializeRequestAsync<TRequest>(body, SerializationModeKind.JSON, false, cancellationToken)
                       ?? throw new InvalidDataException($"The request body does not carry a {typeof(TRequest).Name}.");
            }
            catch (Exception exception) when (exception is JsonException or SerializationException or InvalidOperationException or FormatException)
            {
                throw new InvalidDataException($"The request body is not a well-formed {typeof(TRequest).Name}: {exception.Message}", exception);
            }
        }

        /// <summary>
        /// Reads a body carrying a collection of Data.
        /// </summary>
        /// <param name="body">The request body.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the read.</param>
        /// <returns>The Data the body carries, empty for a JSON <c>null</c>, which the OpenAPI document allows in place of the array.</returns>
        /// <exception cref="InvalidDataException">Thrown when the body is not a well-formed collection of Data.</exception>
        public async Task<IReadOnlyList<IData>> ReadDataAsync(Stream body, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(body);

            using var buffer = new MemoryStream();

            await body.CopyToAsync(buffer, cancellationToken);

            try
            {
                if (IsNull(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
                {
                    return [];
                }

                buffer.Position = 0;

                var data = await this.deSerializer.DeSerializeAsync(buffer, SerializationModeKind.JSON, SerializationTargetKind.PSM, false, cancellationToken);

                return [.. data.OfType<IData>()];
            }
            catch (Exception exception) when (exception is JsonException or SerializationException or InvalidOperationException or FormatException)
            {
                throw new InvalidDataException($"The request body is not a well-formed collection of Data: {exception.Message}", exception);
            }
        }

        /// <summary>
        /// Tells whether a JSON document is the <c>null</c> literal alone.
        /// </summary>
        /// <param name="json">The UTF-8 JSON document.</param>
        /// <returns><c>true</c> when <paramref name="json"/> is <c>null</c>.</returns>
        private static bool IsNull(ReadOnlySpan<byte> json)
        {
            return json.Trim(" \t\r\n"u8).SequenceEqual("null"u8);
        }
    }
}

