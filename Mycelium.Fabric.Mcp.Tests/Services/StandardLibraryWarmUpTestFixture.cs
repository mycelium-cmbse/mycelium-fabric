// ------------------------------------------------------------------------------------------------
//  <copyright file="StandardLibraryWarmUpTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.Threading;

    using Moq;

    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="StandardLibraryWarmUp"/> class.
    /// </summary>
    [TestFixture]
    public class StandardLibraryWarmUpTestFixture
    {
        [Test]
        public void VerifyConstructor()
        {
            Assert.That(() => new StandardLibraryWarmUp(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void VerifyStartAsync()
        {
            var library = new Mock<IStandardLibrary>();
            var warmUp = new StandardLibraryWarmUp(library.Object);

            Assert.That(warmUp.StartAsync(CancellationToken.None).IsCompletedSuccessfully, Is.True);

            library.Verify(standardLibrary => standardLibrary.StartLoading(), Times.Once);
        }

        [Test]
        public void VerifyStopAsync()
        {
            var library = new Mock<IStandardLibrary>();
            var warmUp = new StandardLibraryWarmUp(library.Object);

            Assert.That(warmUp.StopAsync(CancellationToken.None).IsCompletedSuccessfully, Is.True);

            library.Verify(standardLibrary => standardLibrary.StartLoading(), Times.Never);
        }
    }
}
