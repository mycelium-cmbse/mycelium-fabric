// ------------------------------------------------------------------------------------------------
//  <copyright file="ErrorTypeExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Http
{
    using ErrorOr;

    using Microsoft.AspNetCore.Http;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;

    [TestFixture]
    public class ErrorTypeExtensionsTestFixture
    {
        [Test]
        public void VerifyStatusCode()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(ErrorType.Validation.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(ErrorType.Unauthorized.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
                Assert.That(ErrorType.Forbidden.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
                Assert.That(ErrorType.NotFound.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
                Assert.That(ErrorType.Conflict.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
                Assert.That(((ErrorType)RequestBodyReader.UnsupportedMediaType).StatusCode, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));
                Assert.That(ErrorType.Failure.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
                Assert.That(ErrorType.Unexpected.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            }
        }
    }
}
