// ------------------------------------------------------------------------------------------------
//  <copyright file="FabricToolboxTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.Hypha
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using ModelContextProtocol.Server;

    using Moq;

    using Mycelium.Fabric.Agent.Hypha;
    using Mycelium.Fabric.Agent.Tests.TestHelpers;

    [TestFixture]
    public class FabricToolboxTestFixture
    {
        private static readonly string[] ToolsOfTheViews = ["decide_batch", "explore_trade_off", "import_requirements", "save_requirement_list"];

        [Test]
        public async Task VerifyOpenAsync()
        {
            var httpClientFactory = new Mock<IHttpClientFactory>();
            var loggerFactory = new Mock<ILoggerFactory>();
            var options = Options.Create(new HyphaOptions());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new FabricToolbox(null, options, loggerFactory.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new FabricToolbox(httpClientFactory.Object, null, loggerFactory.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new FabricToolbox(httpClientFactory.Object, options, null), Throws.TypeOf<ArgumentNullException>());
            }

            var toolboxWithoutEndpoint = new FabricToolbox(httpClientFactory.Object, options, loggerFactory.Object);

            Assert.That(() => toolboxWithoutEndpoint.OpenAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>().With.Message.Contains(nameof(HyphaOptions.McpEndpoint)));

            httpClientFactory.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);

            using var application = new AgentApplicationFactory();
            var registeredTools = application.Services.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToList();

            await using var session = await application.Services.GetRequiredService<IFabricToolbox>().OpenAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(registeredTools, Is.SupersetOf(ToolsOfTheViews));
                Assert.That(session.Tools.Select(tool => tool.Name), Is.EquivalentTo(registeredTools.Except(ToolsOfTheViews)));
            }
        }
    }
}
