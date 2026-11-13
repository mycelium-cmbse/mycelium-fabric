// ------------------------------------------------------------------------------------------------
//  <copyright file="FunctionalDataInitializationServiceTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Tests.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    using NUnit.Framework;

    [TestFixture]
    public class FunctionalDataInitializationServiceTestFixture
    {
        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new FunctionalDataInitializationService(null), Throws.ArgumentNullException);
                Assert.That(() => new FunctionalDataInitializationService(CreateInitializer().Object), Throws.Nothing);
            }
        }

        [Test]
        public async Task VerifyStartAsync()
        {
            var initializer = CreateInitializer();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cancellation = new CancellationTokenSource();

            initializer.Setup(database => database.InitializeAsync(cancellation.Token)).Returns(completion.Task);

            var service = new FunctionalDataInitializationService(initializer.Object);
            var startup = service.StartAsync(cancellation.Token);

            Assert.That(startup.IsCompleted, Is.False);

            completion.SetResult();

            await startup;

            initializer.Verify(database => database.InitializeAsync(cancellation.Token), Times.Once);

            initializer.Setup(database => database.InitializeAsync(cancellation.Token)).ThrowsAsync(new InvalidOperationException("Initialization failed."));

            Assert.That(() => service.StartAsync(cancellation.Token), Throws.TypeOf<InvalidOperationException>());

            await cancellation.CancelAsync();

            initializer.Setup(database => database.InitializeAsync(cancellation.Token)).Returns(Task.FromCanceled(cancellation.Token));

            Assert.That(() => service.StartAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

            initializer.Verify(database => database.InitializeAsync(cancellation.Token), Times.Exactly(3));
            initializer.VerifyNoOtherCalls();
        }

        [Test]
        public async Task VerifyStopAsync()
        {
            var initializer = CreateInitializer();
            var service = new FunctionalDataInitializationService(initializer.Object);

            await service.StopAsync(CancellationToken.None);

            using var cancellation = new CancellationTokenSource();

            await cancellation.CancelAsync();
            await service.StopAsync(cancellation.Token);

            initializer.Verify(database => database.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
            initializer.VerifyNoOtherCalls();
        }

        private static Mock<IFunctionalDataInitializer> CreateInitializer()
        {
            return new Mock<IFunctionalDataInitializer>();
        }
    }
}
