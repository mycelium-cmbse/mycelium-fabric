// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffResourceTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="TradeOffResource"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffResourceTestFixture
    {
        [Test]
        public void VerifyGetPage()
        {
            var page = TradeOffResource.GetPage();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page, Does.StartWith("<!doctype html>").IgnoreCase);
                Assert.That(page, Does.Contain("ui/initialize"));
                Assert.That(page, Does.Not.Contain("<link rel=\"stylesheet\""));
                Assert.That(page, Does.Not.Contain("<script src="));
                Assert.That(TradeOffResource.GetPage(), Is.SameAs(page));
                Assert.That(TradeOffResource.Uri, Does.StartWith("ui://"));
                Assert.That(TradeOffResource.Uri, Is.Not.EqualTo(BatchReviewResource.Uri));
            }
        }
    }
}
