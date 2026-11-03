// ------------------------------------------------------------------------------------------------
//  <copyright file="RequestBody.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Serialization
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// A handler parameter bound to the request model the request body carries.
    /// </summary>
    /// <typeparam name="TRequest">The request model the body is expected to carry.</typeparam>
    /// <remarks>
    /// Binding never fails, so that the handler reports a rejected body with its own operation id.
    /// </remarks>
    public sealed class RequestBody<TRequest> : IBindableFromHttpContext<RequestBody<TRequest>>
        where TRequest : class, IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestBody{TRequest}"/> class.
        /// </summary>
        /// <param name="content">The request model, or the error that prevented reading it.</param>
        public RequestBody(ErrorOr<TRequest> content)
        {
            this.Content = content;
        }

        /// <summary>
        /// Gets the request model, or the error that prevented reading it.
        /// </summary>
        public ErrorOr<TRequest> Content { get; }

        /// <summary>
        /// Binds the parameter by reading the request body through the registered <see cref="IRequestBodyReader"/> and validating it with <see cref="RequestValidator"/>.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being bound.</param>
        /// <param name="parameter">The handler parameter being bound.</param>
        /// <returns>The bound <see cref="RequestBody{TRequest}"/>.</returns>
        public static async ValueTask<RequestBody<TRequest>> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            ArgumentNullException.ThrowIfNull(context);

            var reader = context.RequestServices.GetRequiredService<IRequestBodyReader>();

            var content = await reader.ReadRequestAsync<TRequest>(context.Request);

            return new RequestBody<TRequest>(content.IsError ? content : RequestValidator.Validate(content.Value));
        }
    }
}
