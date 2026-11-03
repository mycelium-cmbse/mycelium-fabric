// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonResponseNegotiator.cs" company="Starion Group S.A.">
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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Carter;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Net.Http.Headers;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Writes responses as REST/HTTP PSM JSON through the SysML2.NET <see cref="ISerializer"/>.
    /// </summary>
    /// <remarks>
    /// Also serves wildcard <c>Accept</c> entries, which Carter's reflection-based default would otherwise answer.
    /// </remarks>
    public class JsonResponseNegotiator : IResponseNegotiator
    {
        /// <summary>
        /// The JSON media type.
        /// </summary>
        public const string MediaType = "application/json";

        /// <summary>
        /// The options every response is written with.
        /// </summary>
        private static readonly JsonWriterOptions JsonWriterOptions = new() { Indented = false, SkipValidation = false };

        /// <summary>
        /// The SysML2.NET JSON serializer.
        /// </summary>
        private readonly ISerializer serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonResponseNegotiator"/> class.
        /// </summary>
        /// <param name="serializer">The SysML2.NET JSON serializer.</param>
        public JsonResponseNegotiator(ISerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);

            this.serializer = serializer;
        }

        /// <summary>
        /// Asserts whether an <c>Accept</c> entry names or ranges over <see cref="MediaType"/>.
        /// </summary>
        /// <param name="accept">One <c>Accept</c> entry.</param>
        /// <returns>True when this negotiator serves the entry.</returns>
        public bool CanHandle(MediaTypeHeaderValue accept)
        {
            ArgumentNullException.ThrowIfNull(accept);

            return accept.MatchesAllTypes
                   || (accept.MatchesAllSubTypes && accept.Type.Equals("application", StringComparison.OrdinalIgnoreCase))
                   || accept.MediaType.Equals(MediaType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes an API response, a SysML Core element, or a collection of either.
        /// </summary>
        /// <typeparam name="T">The static type of the model.</typeparam>
        /// <param name="req">The request.</param>
        /// <param name="res">The response.</param>
        /// <param name="model">The model to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> that completes once the payload is written.</returns>
        /// <exception cref="NotSupportedException">Thrown when the model is not a REST/HTTP PSM payload.</exception>
        public Task Handle<T>(HttpRequest req, HttpResponse res, T model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(res);
            ArgumentNullException.ThrowIfNull(model);

            res.ContentType = MediaType;

            return model switch
            {
                IResponse response => this.serializer.SerializeResponseAsync(response, SerializationModeKind.JSON, false, res.Body, JsonWriterOptions, cancellationToken),
                IIdentified identified => this.serializer.SerializeAsync(identified, SerializationModeKind.JSON, false, res.Body, JsonWriterOptions, cancellationToken),
                IEnumerable<object> items => this.SerializeManyAsync(items, res.Body, cancellationToken),
                _ => throw new NotSupportedException($"A {model.GetType().Name} is not a payload of the REST/HTTP PSM.")
            };
        }

        /// <summary>
        /// Writes a collection of API responses and SysML Core elements, which a query result may mix (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="items">The records to write.</param>
        /// <param name="stream">The stream the payload is written to.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> that completes once the payload is written.</returns>
        /// <exception cref="NotSupportedException">Thrown when a record is neither an API response nor a SysML Core element.</exception>
        private async Task SerializeManyAsync(IEnumerable<object> items, Stream stream, CancellationToken cancellationToken)
        {
            var records = items as IReadOnlyList<object> ?? [.. items];

            if (records.Any(record => record is not (IResponse or IIdentified)))
            {
                throw new NotSupportedException("A collection carrying a record that is not a payload of the REST/HTTP PSM cannot be serialized.");
            }

            if (records.All(record => record is IResponse))
            {
                await this.serializer.SerializeResponseAsync(records.Cast<IResponse>(), SerializationModeKind.JSON, false, stream, JsonWriterOptions, cancellationToken);

                return;
            }

            if (records.All(record => record is not IResponse))
            {
                await this.serializer.SerializeAsync(records.Cast<IIdentified>(), SerializationModeKind.JSON, false, stream, JsonWriterOptions, cancellationToken);

                return;
            }

            await stream.WriteAsync("["u8.ToArray(), cancellationToken);

            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                if (recordIndex > 0)
                {
                    await stream.WriteAsync(","u8.ToArray(), cancellationToken);
                }

                await (records[recordIndex] is IResponse response
                    ? this.serializer.SerializeResponseAsync(response, SerializationModeKind.JSON, false, stream, JsonWriterOptions, cancellationToken)
                    : this.serializer.SerializeAsync((IIdentified)records[recordIndex], SerializationModeKind.JSON, false, stream, JsonWriterOptions, cancellationToken));
            }

            await stream.WriteAsync("]"u8.ToArray(), cancellationToken);
        }
    }
}
