// ------------------------------------------------------------------------------------------------
//  <copyright file="McpAppPageTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Resources
{
    using Mycelium.Fabric.Mcp.Resources;

    /// <summary>
    /// Suite of tests for the <see cref="McpAppPage"/> class.
    /// </summary>
    [TestFixture]
    public class McpAppPageTestFixture
    {
        [Test]
        public void VerifyRead()
        {
            var page = McpAppPage.Read("review-batch.html");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page, Does.Contain("<style>"));
                Assert.That(page, Does.Contain("<script>"));
                Assert.That(page, Does.Not.Contain("<link rel=\"stylesheet\""));
                Assert.That(page, Does.Not.Contain("<script src="));
                Assert.That(page, Does.Contain("${count}"));
                Assert.That(() => McpAppPage.Read("missing.html"), Throws.InvalidOperationException.With.Message.Contains("McpApp.missing.html"));
            }
        }
    }
}
