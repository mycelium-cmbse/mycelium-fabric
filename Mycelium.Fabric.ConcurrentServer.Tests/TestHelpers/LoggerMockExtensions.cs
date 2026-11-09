// ------------------------------------------------------------------------------------------------
//  <copyright file="LoggerMockExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers
{
    using System;

    using Microsoft.Extensions.Logging;

    using Moq;

    public static class LoggerMockExtensions
    {
        extension<TLogger>(Mock<TLogger> logger)
            where TLogger : class, ILogger
        {
#pragma warning disable CA1873
            public void VerifyLogged(LogLevel level, string messageFragment, Times times)
            {
                logger.Verify(log => log.Log(level, It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains(messageFragment)), It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()), times);
            }
#pragma warning restore CA1873
        }
    }
}
