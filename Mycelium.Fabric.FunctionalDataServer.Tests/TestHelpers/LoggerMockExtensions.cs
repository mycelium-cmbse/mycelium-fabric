// ------------------------------------------------------------------------------------------------
//  <copyright file="LoggerMockExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Tests.TestHelpers
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using Microsoft.Extensions.Logging;

    using Moq;

    public static class LoggerMockExtensions
    {
        extension<TLogger>(Mock<TLogger> logger)
            where TLogger : class, ILogger
        {
            [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Moq expression tree in unit test.")]
            public void VerifyLogged(LogLevel level, string messageFragment, Times times)
            {
                logger.Verify(log => log.Log(level, It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains(messageFragment)), It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()), times);
            }
        }
    }
}
