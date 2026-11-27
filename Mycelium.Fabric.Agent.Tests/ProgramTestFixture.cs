// ------------------------------------------------------------------------------------------------
//  <copyright file="ProgramTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;

    using ModelContextProtocol.Client;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Agent.Extensions;
    using Mycelium.Fabric.Agent.Hypha;
    using Mycelium.Fabric.Agent.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Resources;

    [TestFixture]
    public class ProgramTestFixture
    {
        [Test]
        public async Task VerifyMain()
        {
            using (var unconfiguredApplication = new WebApplicationFactory<HyphaOptions>())
            {
                Assert.That(() => unconfiguredApplication.Services, Throws.InstanceOf<InvalidOperationException>().With.Message.Contains(ServiceCollectionExtensions.ModelPathKey));
            }

            using var application = new AgentApplicationFactory();
            await using var client = await application.CreateMcpClientAsync();

            var tools = await client.ListToolsAsync();
            var resources = await client.ListResourcesAsync();
            var registeredTools = application.Services.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(registeredTools, Does.Contain("sum_attribute").And.Contain("apply_changes"));
                Assert.That(tools.Select(tool => tool.Name), Is.EquivalentTo(registeredTools));
                Assert.That(resources.Select(resource => resource.Uri), Is.EquivalentTo([BatchReviewResource.Uri, TradeOffResource.Uri, ModelHealthResource.Uri, RequirementExtractionResource.Uri]));
            }
        }
    }
}
