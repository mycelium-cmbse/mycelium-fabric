// ------------------------------------------------------------------------------------------------
//  <copyright file="ApiBase.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Carter.Response;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Http;

    using SysML2.NET.PSM;
    using SysML2.NET.PSM.DTO;

    using Error = ErrorOr.Error;

    /// <summary>
    /// Base class of the Carter modules, carrying the behaviour shared by the request handlers.
    /// </summary>
    /// <remarks>
    /// Modules are singletons, so handlers take their services as <c>[FromServices]</c> parameters, bound as <c>null</c> when none is registered.
    /// </remarks>
    public abstract class ApiBase
    {
        /// <summary>
        /// The RFC 7240 §3 header acknowledging the preferences honoured.
        /// </summary>
        private const string PreferenceAppliedHeader = "Preference-Applied";

        /// <summary>
        /// Writes the <c>500</c> response of a route whose service is not registered yet.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task NotYetImplemented(HttpContext context, string operationId)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Response.NegotiateErrorAsync(StatusCodes.Status500InternalServerError,
                $"{operationId}: the operation is registered but is not yet implemented by this server.");
        }

        /// <summary>
        /// Writes the response reporting an exception a handler caught: the status of a <see cref="BadHttpRequestException" />, <c>501</c>, or a logged <c>500</c>.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="exception">The exception the handler caught.</param>
        /// <param name="logger">The logger the failure is recorded with.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task Failure(HttpContext context, string operationId, Exception exception, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(exception);

            if (context.Response.HasStarted)
            {
                logger?.LogError(exception, "{OperationId} failed after its response had started", operationId);

                return Task.CompletedTask;
            }

            switch (exception)
            {
                case OperationCanceledException when context.RequestAborted.IsCancellationRequested:

                    return Task.CompletedTask;

                case BadHttpRequestException badRequest:

                    return context.Response.NegotiateErrorAsync(badRequest.StatusCode, $"{operationId}: {badRequest.Message}");

                case NotSupportedException:

                    return NotImplemented(context, operationId, exception.Message);

                default:
                    logger?.LogError(exception, "{OperationId} failed unexpectedly", operationId);

                    return Failed(context, operationId, [Error.Unexpected($"{operationId}.failed", "The server did not complete the operation.")]);
            }
        }

        /// <summary>
        /// Writes the outcome of an operation, mapping its PIM record onto the PSM response model.
        /// </summary>
        /// <typeparam name="TResponse">The PSM response model.</typeparam>
        /// <typeparam name="TRecord">The PIM record.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="outcome">The outcome the service reported.</param>
        /// <param name="statusCode">The status code of a successful outcome.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task WriteAsync<TResponse, TRecord>(HttpContext context, string operationId, ErrorOr<TRecord> outcome, int statusCode)
            where TResponse : class, IPimMappable<TRecord>, new()
            where TRecord : class
        {
            return WriteAsync(context, operationId, outcome, statusCode, PimMapper.Map<TResponse, TRecord>);
        }

        /// <summary>
        /// Writes the outcome of an operation whose model is written as is.
        /// </summary>
        /// <typeparam name="TModel">The model the service returned.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="outcome">The outcome the service reported.</param>
        /// <param name="statusCode">The status code of a successful outcome.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task WriteAsync<TModel>(HttpContext context, string operationId, ErrorOr<TModel> outcome, int statusCode)
        {
            return WriteAsync(context, operationId, outcome, statusCode, model => model);
        }

        /// <summary>
        /// Writes the outcome of an operation through a mapping.
        /// </summary>
        /// <typeparam name="TRecord">The record the service returned.</typeparam>
        /// <typeparam name="TModel">The model written.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="outcome">The outcome the service reported.</param>
        /// <param name="statusCode">The status code of a successful outcome.</param>
        /// <param name="map">The mapping from the record onto the model.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task WriteAsync<TRecord, TModel>(HttpContext context, string operationId, ErrorOr<TRecord> outcome, int statusCode, Func<TRecord, TModel> map)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(map);

            return outcome.IsError ? Failed(context, operationId, outcome.Errors) : Negotiated(context, map(outcome.Value), statusCode);
        }

        /// <summary>
        /// Writes the outcome of a deletion: <c>200</c> with the deleted record, or <c>204</c> when the client prefers <c>return=minimal</c> (RFC 7240 §4.2).
        /// </summary>
        /// <typeparam name="TResponse">The PSM response model.</typeparam>
        /// <typeparam name="TRecord">The PIM record.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="outcome">The outcome the service reported.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task DeletedAsync<TResponse, TRecord>(HttpContext context, string operationId, ErrorOr<TRecord> outcome)
            where TResponse : class, IPimMappable<TRecord>, new()
            where TRecord : class
        {
            ArgumentNullException.ThrowIfNull(context);

            if (outcome.IsError || !context.Request.PrefersMinimalReturn())
            {
                return WriteAsync<TResponse, TRecord>(context, operationId, outcome, StatusCodes.Status200OK);
            }

            context.Response.StatusCode = StatusCodes.Status204NoContent;
            context.Response.Headers[PreferenceAppliedHeader] = HttpRequestExtensions.ReturnMinimalPreference;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Runs a paged operation and writes its PIM records mapped onto PSM response models.
        /// </summary>
        /// <typeparam name="TResponse">The PSM response model.</typeparam>
        /// <typeparam name="TRecord">The PIM record.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="operation">The operation, given the pagination parameters of the request.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task PagedAsync<TResponse, TRecord>(HttpContext context, string operationId, Func<QueryParameters, Task<ErrorOr<PagedResult<TRecord>>>> operation)
            where TResponse : class, IPimMappable<TRecord>, new()
            where TRecord : class
        {
            return PagedAsync(context, operationId, operation, PimMapper.Map<TResponse, TRecord>);
        }

        /// <summary>
        /// Runs a paged operation and writes its records as is.
        /// </summary>
        /// <typeparam name="TModel">The model the page carries.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="operation">The operation, given the pagination parameters of the request.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task PagedAsync<TModel>(HttpContext context, string operationId, Func<QueryParameters, Task<ErrorOr<PagedResult<TModel>>>> operation)
        {
            return PagedAsync(context, operationId, operation, model => model);
        }

        /// <summary>
        /// Runs a paged operation and writes its records through a mapping, with the §8.1.3 <c>Link</c> header.
        /// </summary>
        /// <typeparam name="TRecord">The record the page carries.</typeparam>
        /// <typeparam name="TModel">The model written.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="operation">The operation, given the pagination parameters of the request.</param>
        /// <param name="map">The mapping from a record onto the model.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static async Task PagedAsync<TRecord, TModel>(HttpContext context, string operationId, Func<QueryParameters, Task<ErrorOr<PagedResult<TRecord>>>> operation,
            Func<TRecord, TModel> map)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(map);

            var queryParameters = context.Request.ReadQueryParameters();

            if (queryParameters.IsError)
            {
                await Failed(context, operationId, queryParameters.Errors);

                return;
            }

            var outcome = await operation(queryParameters.Value);

            if (outcome.IsError)
            {
                await Failed(context, operationId, outcome.Errors);

                return;
            }

            context.Response.AddPaginationLinks(outcome.Value, queryParameters.Value.PageSize);

            List<TModel> page = [.. (outcome.Value.Records ?? []).Select(map)];

            await Negotiated(context, page, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Writes a model through Carter content negotiation.
        /// </summary>
        /// <typeparam name="TResponse">The model written.</typeparam>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="model">The model to write.</param>
        /// <param name="statusCode">The status code of the response.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task Negotiated<TResponse>(HttpContext context, TResponse model, int statusCode)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.Response.StatusCode = statusCode;

            return context.Response.Negotiate(model, context.RequestAborted);
        }

        /// <summary>
        /// Writes the response reporting the errors of an operation, with the status code of the first one.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="errors">The errors that prevented the operation.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task Failed(HttpContext context, string operationId, List<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(errors);

            var statusCode = errors.Count > 0 ? errors[0].Type.StatusCode : StatusCodes.Status500InternalServerError;

            return context.Response.NegotiateErrorAsync(statusCode, $"{operationId}: {string.Join(" ", errors.Select(error => error.Description))}");
        }

        /// <summary>
        /// Writes the <c>400</c> response for a request the OpenAPI document does not accept.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">What is invalid.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        protected static Task Invalid(HttpContext context, string operationId, string code, string detail)
        {
            return Failed(context, operationId, [Error.Validation(code, detail)]);
        }

        /// <summary>
        /// Writes the <c>501</c> response for a well-formed request the server cannot represent.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext" /> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="detail">What the server cannot act on.</param>
        /// <returns>A <see cref="Task" /> that completes once the response is written.</returns>
        private static Task NotImplemented(HttpContext context, string operationId, string detail)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Response.NegotiateErrorAsync(StatusCodes.Status501NotImplemented, $"{operationId}: {detail}");
        }
    }
}
