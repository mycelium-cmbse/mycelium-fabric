// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelHealthResourceTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="ModelHealthResource"/> class.
    /// </summary>
    [TestFixture]
    public class ModelHealthResourceTestFixture
    {
        [Test]
        public void VerifyGetPage()
        {
            var page = ModelHealthResource.GetPage();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page, Does.StartWith("<!doctype html>").IgnoreCase);
                Assert.That(page, Does.Contain("ui/initialize"));
                Assert.That(page, Does.Contain(".kpis"));
                Assert.That(page, Does.Not.Contain("<link rel=\"stylesheet\""));
                Assert.That(page, Does.Not.Contain("<script src="));
                Assert.That(ModelHealthResource.GetPage(), Is.SameAs(page));
                Assert.That(ModelHealthResource.Uri, Does.StartWith("ui://"));
                Assert.That(ModelHealthResource.Uri, Is.Not.EqualTo(BatchReviewResource.Uri).And.Not.EqualTo(TradeOffResource.Uri));
            }
        }
    }
}
