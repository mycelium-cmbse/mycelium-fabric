// ------------------------------------------------------------------------------------------------
//  <copyright file="ErrorTypeExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Http
{
    using ErrorOr;

    using Microsoft.AspNetCore.Http;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    /// <summary>
    /// Maps the <see cref="ErrorType"/> of a failed operation onto the HTTP response that reports it.
    /// </summary>
    public static class ErrorTypeExtensions
    {
        /// <param name="errorType">The type of the error an operation reported.</param>
        extension(ErrorType errorType)
        {
            /// <summary>
            /// Gets the status code the response reporting the error answers with.
            /// </summary>
            public int StatusCode => errorType switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                (ErrorType)RequestBodyReader.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
                _ => StatusCodes.Status500InternalServerError
            };
        }
    }
}
