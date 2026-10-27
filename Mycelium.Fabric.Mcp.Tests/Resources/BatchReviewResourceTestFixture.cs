// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchReviewResourceTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Resources
{
    using System;

    using Mycelium.Fabric.Mcp.Resources;

    /// <summary>
    /// Suite of tests for the <see cref="BatchReviewResource"/> class.
    /// </summary>
    [TestFixture]
    public class BatchReviewResourceTestFixture
    {
        [Test]
        public void VerifyGetPage()
        {
            var page = BatchReviewResource.GetPage();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page, Does.StartWith("<!doctype html>").IgnoreCase);
                Assert.That(page, Does.Contain("ui/initialize"));
                Assert.That(page, Does.Contain(".ap-h"));
                Assert.That(page, Does.Not.Contain("href=\"style.css\""));
                Assert.That(page, Does.Not.Contain("src=\"script.js\""));
                Assert.That(page.IndexOf("<script>", StringComparison.Ordinal), Is.GreaterThan(page.IndexOf("id=\"note\"", StringComparison.Ordinal)));
                Assert.That(BatchReviewResource.GetPage(), Is.SameAs(page));
                Assert.That(BatchReviewResource.Uri, Does.StartWith("ui://"));
            }
        }
    }
}
