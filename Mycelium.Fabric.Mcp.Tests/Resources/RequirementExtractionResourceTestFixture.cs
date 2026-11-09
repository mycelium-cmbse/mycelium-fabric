// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementExtractionResourceTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="RequirementExtractionResource"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementExtractionResourceTestFixture
    {
        [Test]
        public void VerifyGetPage()
        {
            var page = RequirementExtractionResource.GetPage();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page, Does.StartWith("<!doctype html>").IgnoreCase);
                Assert.That(page, Does.Contain("ui/initialize"));
                Assert.That(page, Does.Contain("import_requirements").And.Contain("save_requirement_list"));
                Assert.That(page, Does.Contain(".g-ext"));
                Assert.That(page, Does.Not.Contain("<link rel=\"stylesheet\""));
                Assert.That(page, Does.Not.Contain("<script src="));
                Assert.That(RequirementExtractionResource.GetPage(), Is.SameAs(page));
                Assert.That(RequirementExtractionResource.Uri, Does.StartWith("ui://"));
                Assert.That(RequirementExtractionResource.Uri, Is.Not.EqualTo(BatchReviewResource.Uri).And.Not.EqualTo(TradeOffResource.Uri).And.Not.EqualTo(ModelHealthResource.Uri));
            }
        }
    }
}
