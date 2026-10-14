// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementPromptsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Prompts
{
    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Prompts;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementPrompts"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementPromptsTestFixture
    {
        [Test]
        public void VerifyExtractRequirements()
        {
            var message = RequirementPrompts.ExtractRequirements(" EOSat1-requirements.md ");
            var scopedMessage = RequirementPrompts.ExtractRequirements("SSS.md", " 5.2.2 ");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => RequirementPrompts.ExtractRequirements(null), Throws.TypeOf<McpException>().With.Message.Contains("empty"));
                Assert.That(() => RequirementPrompts.ExtractRequirements(" "), Throws.TypeOf<McpException>().With.Message.Contains("empty"));

                Assert.That(message, Does.StartWith("Model the requirements of a document"));
                Assert.That(message, Does.Contain("read_document").And.Contain("idPattern").And.Contain("apply_changes").And.Contain("requirements.missing"));
                Assert.That(message, Does.Contain("Never create a requirement whose identifier is"));
                Assert.That(message, Does.Contain("Document: 'EOSat1-requirements.md' in the document folder. Scope: the whole document."));
                Assert.That(message, Does.EndWith("without calling apply_changes."));
                Assert.That(scopedMessage, Does.Contain("Document: 'SSS.md' in the document folder. Scope: the sections '5.2.2' and their subsections."));
            }
        }
    }
}
