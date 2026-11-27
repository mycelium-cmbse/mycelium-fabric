// ------------------------------------------------------------------------------------------------
//  <copyright file="FabricToolSessionTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.Hypha
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Extensions.AI;

    using Moq;

    using Mycelium.Fabric.Agent.Hypha;

    [TestFixture]
    public class FabricToolSessionTestFixture
    {
        [Test]
        public async Task VerifyDisposeAsync()
        {
            Assert.That(() => new FabricToolSession(null, null), Throws.TypeOf<ArgumentNullException>());

            await using (var sessionWithoutClient = new FabricToolSession(null, []))
            {
                Assert.That(sessionWithoutClient.Tools, Is.Empty);
            }

            var client = new Mock<IAsyncDisposable>();
            var tool = new Mock<AITool>();
            var session = new FabricToolSession(client.Object, [tool.Object]);

            Assert.That(session.Tools, Is.EqualTo([tool.Object]));

            await session.DisposeAsync();

            client.Verify(disposable => disposable.DisposeAsync(), Times.Once);
        }
    }
}
