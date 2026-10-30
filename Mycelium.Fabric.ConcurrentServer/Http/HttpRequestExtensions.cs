// ------------------------------------------------------------------------------------------------
//  <copyright file="HttpRequestExtensions.cs" company="Starion Group S.A.">
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
    using System.Linq;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;

    using SysML2.NET.PIM;
    using SysML2.NET.PSM;

    /// <summary>
    /// Reads the REST/HTTP PSM query parameters, reporting a malformed value as a validation error.
    /// </summary>
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// The query parameter carrying the page size.
        /// </summary>
        public const string PageSizeParameter = "page[size]";

        /// <summary>
        /// The query parameter carrying the cursor of the next page.
        /// </summary>
        public const string PageAfterParameter = "page[after]";

        /// <summary>
        /// The query parameter carrying the cursor of the previous page.
        /// </summary>
        public const string PageBeforeParameter = "page[before]";

        /// <summary>
        /// The query parameter carrying the direction of a relationship query.
        /// </summary>
        public const string DirectionParameter = "direction";

        /// <summary>
        /// The RFC 7240 §2 request header carrying the client preferences.
        /// </summary>
        public const string PreferHeader = "Prefer";

        /// <summary>
        /// The RFC 7240 preference asking for a response without a representation of the resource.
        /// </summary>
        public const string ReturnMinimalPreference = "return=minimal";

        /// <param name="request">The request being handled.</param>
        extension(HttpRequest request)
        {
            /// <summary>
            /// Reads the §8.1.3 pagination parameters; an absent <c>page[size]</c> is left at zero for the service to choose.
            /// </summary>
            /// <returns>The pagination parameters, or the errors that prevented reading them.</returns>
            public ErrorOr<QueryParameters> ReadQueryParameters()
            {
                ArgumentNullException.ThrowIfNull(request);

                var queryParameters = new QueryParameters();

                if (request.Query.TryGetValue(PageSizeParameter, out var pageSize))
                {
                    if (!int.TryParse(pageSize.ToString(), out var size) || size < 1)
                    {
                        return Error.Validation("page.size.invalid", $"The '{PageSizeParameter}' query parameter must be a positive integer.");
                    }

                    queryParameters.PageSize = size;
                }

                if (request.Query.TryGetValue(PageAfterParameter, out var pageAfter))
                {
                    if (!PageCursor.TryDecode(pageAfter.ToString(), out var after))
                    {
                        return Error.Validation("page.after.invalid", $"The '{PageAfterParameter}' query parameter is not a well-formed page cursor.");
                    }

                    queryParameters.PageAfter = after;
                }

                if (!request.Query.TryGetValue(PageBeforeParameter, out var pageBefore))
                {
                    return queryParameters;
                }

                if (!PageCursor.TryDecode(pageBefore.ToString(), out var before))
                {
                    return Error.Validation("page.before.invalid", $"The '{PageBeforeParameter}' query parameter is not a well-formed page cursor.");
                }

                queryParameters.PageBefore = before;

                return queryParameters;
            }

            /// <summary>
            /// Reads an optional identifier.
            /// </summary>
            /// <param name="name">The query parameter.</param>
            /// <returns>The identifier, <c>null</c> when absent, or the read errors.</returns>
            public ErrorOr<Guid?> ReadOptionalId(string name)
            {
                ArgumentNullException.ThrowIfNull(request);

                if (!request.Query.TryGetValue(name, out var value))
                {
                    return (Guid?)null;
                }

                return Guid.TryParse(value.ToString(), out var identifier)
                    ? identifier
                    : Error.Validation($"{name}.invalid", $"The '{name}' query parameter is not a well-formed identifier.");
            }

            /// <summary>
            /// Reads a required identifier.
            /// </summary>
            /// <param name="name">The query parameter.</param>
            /// <returns>The identifier, or the read errors.</returns>
            public ErrorOr<Guid> ReadRequiredId(string name)
            {
                ArgumentNullException.ThrowIfNull(request);

                if (!request.Query.TryGetValue(name, out var value))
                {
                    return Error.Validation($"{name}.missing", $"The '{name}' query parameter is required.");
                }

                return Guid.TryParse(value.ToString(), out var identifier)
                    ? identifier
                    : Error.Validation($"{name}.invalid", $"The '{name}' query parameter is not a well-formed identifier.");
            }

            /// <summary>
            /// Reads a repeatable identifier, in the order supplied.
            /// </summary>
            /// <param name="name">The query parameter.</param>
            /// <returns>The identifiers, empty when absent, or the read errors.</returns>
            public ErrorOr<List<Guid>> ReadIds(string name)
            {
                ArgumentNullException.ThrowIfNull(request);

                if (!request.Query.TryGetValue(name, out var values))
                {
                    return new List<Guid>();
                }

                List<Guid> identifiers = [];

                foreach (var value in values.SelectMany(Split))
                {
                    if (!Guid.TryParse(value, out var identifier))
                    {
                        return Error.Validation($"{name}.invalid", $"The '{name}' query parameter carries a value that is not a well-formed identifier.");
                    }

                    identifiers.Add(identifier);
                }

                return identifiers;
            }

            /// <summary>
            /// Reads the relationship direction, defaulting to <see cref="Direction.both"/> when absent.
            /// </summary>
            /// <returns>The direction, or the read errors.</returns>
            public ErrorOr<Direction> ReadDirection()
            {
                ArgumentNullException.ThrowIfNull(request);

                if (!request.Query.TryGetValue(DirectionParameter, out var value))
                {
                    return Direction.both;
                }

                return TryParseName<Direction>(value.ToString(), out var direction)
                    ? direction
                    : Error.Validation("direction.invalid", $"The '{DirectionParameter}' query parameter must be one of 'in', 'out' or 'both'.");
            }

            /// <summary>
            /// Reads a repeatable change type; an empty result means no filter (§7.2.3).
            /// </summary>
            /// <param name="name">The query parameter.</param>
            /// <returns>The change types, empty when absent, or the read errors.</returns>
            public ErrorOr<List<ChangeType>> ReadChangeTypes(string name)
            {
                ArgumentNullException.ThrowIfNull(request);

                if (!request.Query.TryGetValue(name, out var values))
                {
                    return new List<ChangeType>();
                }

                List<ChangeType> changeTypes = [];

                foreach (var value in values.SelectMany(Split))
                {
                    if (!TryParseName<ChangeType>(value, out var changeType))
                    {
                        return Error.Validation($"{name}.invalid", $"The '{name}' query parameter must be one of 'CREATED', 'UPDATED' or 'DELETED'.");
                    }

                    changeTypes.Add(changeType);
                }

                return changeTypes;
            }

            /// <summary>
            /// Tells whether the <c>Prefer</c> header carries the RFC 7240 §4.2 <c>return=minimal</c> preference.
            /// </summary>
            /// <returns><c>true</c> when the client prefers a response without the representation of the resource.</returns>
            public bool PrefersMinimalReturn()
            {
                ArgumentNullException.ThrowIfNull(request);

                return request.Headers[PreferHeader].SelectMany(Split).Select(Preference)
                    .Any(preference => preference.Equals(ReturnMinimalPreference, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Reduces one RFC 7240 preference to its unquoted <c>token=value</c> form, dropping its parameters.
        /// </summary>
        /// <param name="preference">The raw preference.</param>
        /// <returns>The normalised preference.</returns>
        private static string Preference(string preference)
        {
            var parts = preference.Split(';')[0].Split('=', 2, StringSplitOptions.TrimEntries);

            return parts.Length == 1 ? parts[0] : $"{parts[0]}={parts[1].Trim('"')}";
        }

        /// <summary>
        /// Parses an enumeration literal by name only, so that a numeric value is rejected.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration.</typeparam>
        /// <param name="value">The raw value.</param>
        /// <param name="literal">The literal named by <paramref name="value"/>.</param>
        /// <returns><c>true</c> when <paramref name="value"/> names a literal of <typeparamref name="TEnum"/>.</returns>
        private static bool TryParseName<TEnum>(string value, out TEnum literal)
            where TEnum : struct, Enum
        {
            var name = Enum.GetNames<TEnum>().FirstOrDefault(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));

            literal = name == null ? default : Enum.Parse<TEnum>(name);

            return name != null;
        }

        /// <summary>
        /// Splits one occurrence of a repeatable query parameter on commas.
        /// </summary>
        /// <param name="value">The raw value.</param>
        /// <returns>The non-empty values.</returns>
        private static IEnumerable<string> Split(string value)
        {
            return (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
