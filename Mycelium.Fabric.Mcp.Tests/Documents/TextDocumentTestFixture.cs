// ------------------------------------------------------------------------------------------------
//  <copyright file="TextDocumentTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Documents
{
    using System;
    using System.Text.RegularExpressions;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// Suite of tests for the <see cref="TextDocument"/> record.
    /// </summary>
    [TestFixture]
    public class TextDocumentTestFixture
    {
        [Test]
        public void VerifyFindIdentifiers()
        {
            var document = new TextDocument("EOSat1-requirements.md",
            [
                new DocumentSection(1, 0, string.Empty, ["Identifiers follow the convention REQ-<area>-<number>, for example REQ-SYS-001."]),
                new DocumentSection(2, 1, "REQ-SYS-001 Launch mass", ["The launch mass shall not exceed 150 kg."]),
                new DocumentSection(3, 1, "Payload",
                [
                    "| REQ-PAY-001 | The camera mass shall not exceed 40 kg, as allocated by REQ-SYS-001 and REQ-GND-001. |",
                    "| REQ-PAY-002 | REQ-PAY-002 is listed twice, and cites REQ-OPS-009, which no section defines. |"
                ]),
                new DocumentSection(4, 1, "Ground", ["1. **REQ-GND-001**: the ground station shall track the satellite."]),
                new DocumentSection(5, 1, "Empty", [])
            ]);

            var pattern = new Regex(@"REQ-[A-Z]+-\d{3}", RegexOptions.None, TimeSpan.FromSeconds(1));
            var identifiers = document.FindIdentifiers(pattern);
            string[] systemIdentifiers = ["REQ-SYS-001"];
            string[] payloadIdentifiers = ["REQ-PAY-001", "REQ-PAY-002", "REQ-OPS-009"];
            string[] groundIdentifiers = ["REQ-GND-001"];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => document.FindIdentifiers(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(identifiers, Has.Count.EqualTo(5));
                Assert.That(identifiers[0], Is.Empty);
                Assert.That(identifiers[1], Is.EqualTo(systemIdentifiers));
                Assert.That(identifiers[2], Is.EqualTo(payloadIdentifiers));
                Assert.That(identifiers[3], Is.EqualTo(groundIdentifiers));
                Assert.That(identifiers[4], Is.Empty);
                Assert.That(document.FindIdentifiers(new Regex("z*", RegexOptions.None, TimeSpan.FromSeconds(1))), Has.All.Empty);
            }

            var slowPattern = new Regex("(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(1));
            var slowDocument = document with { Sections = [new DocumentSection(1, 1, "Slow", [new string('a', 5000) + "!"])] };

            Assert.That(() => slowDocument.FindIdentifiers(slowPattern), Throws.TypeOf<RegexMatchTimeoutException>());
        }
    }
}
