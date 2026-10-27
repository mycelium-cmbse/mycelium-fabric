// ------------------------------------------------------------------------------------------------
//  <copyright file="AppHostDetectorTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System.Collections.Generic;

    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Protocol;

    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="AppHostDetector"/> class.
    /// </summary>
    [TestFixture]
    public class AppHostDetectorTestFixture
    {
        [Test]
        public void VerifyShowsApps()
        {
            var detector = new AppHostDetector();

            var withApps = new ClientCapabilities
            {
                Extensions = new Dictionary<string, object> { [McpApps.ExtensionId] = new McpUiClientCapabilities { MimeTypes = [McpApps.HtmlMimeType] } }
            };

            var withOtherMimeType = new ClientCapabilities
            {
                Extensions = new Dictionary<string, object> { [McpApps.ExtensionId] = new McpUiClientCapabilities { MimeTypes = ["text/plain"] } }
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(detector.ShowsApps(null), Is.False);
                Assert.That(detector.ShowsApps(new ClientCapabilities()), Is.False);
                Assert.That(detector.ShowsApps(withOtherMimeType), Is.False);
                Assert.That(detector.ShowsApps(withApps), Is.True);
            }
        }
    }
}
