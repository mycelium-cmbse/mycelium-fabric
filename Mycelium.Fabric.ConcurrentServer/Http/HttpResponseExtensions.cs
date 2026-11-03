// ------------------------------------------------------------------------------------------------
//  <copyright file="HttpResponseExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Http
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Carter.Response;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Net.Http.Headers;

    using SysML2.NET.PSM;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Writes the REST/HTTP PSM pagination metadata and error model onto a response.
    /// </summary>
    public static class HttpResponseExtensions
    {
        /// <summary>
        /// The query parameters a pagination link sets itself rather than carrying over from the request.
        /// </summary>
        private static readonly HashSet<string> PaginationParameters =
            new([HttpRequestExtensions.PageSizeParameter, HttpRequestExtensions.PageAfterParameter, HttpRequestExtensions.PageBeforeParameter], StringComparer.OrdinalIgnoreCase);

        /// <param name="response">The response the metadata is written to.</param>
        extension(HttpResponse response)
        {
            /// <summary>
            /// Adds the §8.1.3 <c>Link</c> header with the <c>prev</c> and <c>next</c> pages, if any.
            /// </summary>
            /// <typeparam name="T">The record type.</typeparam>
            /// <param name="pagedResult">The page the service returned.</param>
            /// <param name="pageSize">The requested page size, or zero.</param>
            public void AddPaginationLinks<T>(PagedResult<T> pagedResult, int pageSize)
            {
                ArgumentNullException.ThrowIfNull(response);
                ArgumentNullException.ThrowIfNull(pagedResult);

                List<string> links = [];

                if (pagedResult.Next.HasValue)
                {
                    links.Add(Link(response.HttpContext.Request, HttpRequestExtensions.PageAfterParameter, pagedResult.Next.Value, pageSize, "next"));
                }

                if (pagedResult.Previous.HasValue)
                {
                    links.Add(Link(response.HttpContext.Request, HttpRequestExtensions.PageBeforeParameter, pagedResult.Previous.Value, pageSize, "prev"));
                }

                if (links.Count == 0)
                {
                    return;
                }

                response.Headers[HeaderNames.Link] = string.Join(", ", links);
            }

            /// <summary>
            /// Writes the OpenAPI <c>Error</c> model through content negotiation, as every error response of the REST/HTTP PSM is.
            /// </summary>
            /// <param name="statusCode">The status code of the response.</param>
            /// <param name="description">The description of the error.</param>
            /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
            public Task NegotiateErrorAsync(int statusCode, string description)
            {
                ArgumentNullException.ThrowIfNull(response);

                response.StatusCode = statusCode;

                return response.Negotiate(new Error { Description = description }, response.HttpContext.RequestAborted);
            }
        }

        /// <summary>
        /// Builds one <c>Link</c> entry that keeps the non-pagination query parameters of the request, escaping only values so <c>page[…]</c> names stay literal.
        /// </summary>
        /// <param name="request">The request being answered.</param>
        /// <param name="cursorParameter">The cursor query parameter.</param>
        /// <param name="page">The page addressed.</param>
        /// <param name="pageSize">The page size to carry over, or zero.</param>
        /// <param name="relation">The <c>rel</c> value.</param>
        /// <returns>The header entry.</returns>
        private static string Link(HttpRequest request, string cursorParameter, Page page, int pageSize, string relation)
        {
            var url = new StringBuilder(UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path));

            url.Append('?')
                .Append(cursorParameter)
                .Append('=')
                .Append(Uri.EscapeDataString(PageCursor.Encode(page)));

            if (pageSize > 0)
            {
                url.Append('&')
                    .Append(HttpRequestExtensions.PageSizeParameter)
                    .Append('=')
                    .Append(pageSize.ToString(CultureInfo.InvariantCulture));
            }

            var retained = request.Query.Where(parameter => !PaginationParameters.Contains(parameter.Key))
                .SelectMany(parameter => parameter.Value.Select(value => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(value ?? string.Empty)}"));

            foreach (var parameter in retained)
            {
                url.Append('&').Append(parameter);
            }

            return $"<{url}>; rel=\"{relation}\"";
        }
    }
}
