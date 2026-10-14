// ------------------------------------------------------------------------------------------------
//  <copyright file="MarkdownTableTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Documents
{
    using System;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// Suite of tests for the <see cref="MarkdownTable"/> class.
    /// </summary>
    [TestFixture]
    public class MarkdownTableTestFixture
    {
        [Test]
        public void VerifyFormatRow()
        {
            string[] simpleRow = ["REQ-SYS-001", "The launch mass shall not exceed 150 kg."];
            string[] messyRow = ["x|y", "line one\r\nline two\n\n  line three  ", null, string.Empty];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => MarkdownTable.FormatRow(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(MarkdownTable.FormatRow(simpleRow), Is.EqualTo("| REQ-SYS-001 | The launch mass shall not exceed 150 kg. |"));
                Assert.That(MarkdownTable.FormatRow(messyRow), Is.EqualTo(@"| x\|y | line one line two line three |  |  |"));
                Assert.That(MarkdownTable.FormatRow([]), Is.EqualTo("|  |"));
            }
        }
    }
}
