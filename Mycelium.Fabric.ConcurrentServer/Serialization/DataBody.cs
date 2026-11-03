// ------------------------------------------------------------------------------------------------
//  <copyright file="DataBody.cs" company="Starion Group S.A.">
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
    using System.Reflection;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using SysML2.NET.Common;

    /// <summary>
    /// A handler parameter bound to the optional collection of Data the request body carries.
    /// </summary>
    /// <remarks>
    /// Binding never fails, so that the handler reports a rejected body with its own operation id.
    /// </remarks>
    public sealed class DataBody : IBindableFromHttpContext<DataBody>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataBody"/> class.
        /// </summary>
        /// <param name="content">The Data, or the error that prevented reading them.</param>
        public DataBody(ErrorOr<IReadOnlyList<IData>> content)
        {
            this.Content = content;
        }

        /// <summary>
        /// Gets the Data the body carries, empty when there is no body, or the error that prevented reading them.
        /// </summary>
        public ErrorOr<IReadOnlyList<IData>> Content { get; }

        /// <summary>
        /// Binds the parameter by reading the request body through the registered <see cref="IRequestBodyReader"/>.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being bound.</param>
        /// <param name="parameter">The handler parameter being bound.</param>
        /// <returns>The bound <see cref="DataBody"/>.</returns>
        public static async ValueTask<DataBody> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            ArgumentNullException.ThrowIfNull(context);

            var reader = context.RequestServices.GetRequiredService<IRequestBodyReader>();

            return new DataBody(await reader.ReadDataAsync(context.Request));
        }
    }
}
