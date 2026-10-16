// ------------------------------------------------------------------------------------------------
//  <copyright file="SectionListBuilderTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="SectionListBuilder"/> class.
    /// </summary>
    [TestFixture]
    public class SectionListBuilderTestFixture
    {
        private SectionListBuilder builder;

        [SetUp]
        public void SetUp()
        {
            this.builder = new SectionListBuilder();
        }

        [Test]
        public void VerifyStartSection()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.builder.StartSection(0, "Title"), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => this.builder.StartSection(7, "Title"), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => this.builder.StartSection(1, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.builder.StartSection(6, "  Level six  "), Throws.Nothing);
            }

            var sections = this.builder.Build();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0], Has.Property("Number").EqualTo(1).And.Property("Level").EqualTo(6).And.Property("Title").EqualTo("Level six"));
            }
        }

        [Test]
        public void VerifyAddLine()
        {
            Assert.That(() => this.builder.AddLine(null), Throws.TypeOf<ArgumentNullException>());

            this.builder.AddLine("Before the first heading.");

            var sections = this.builder.Build();
            string[] lines = ["Before the first heading."];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0], Has.Property("Level").EqualTo(0).And.Property("Title").Empty);
                Assert.That(sections[0].Lines, Is.EqualTo(lines));
            }
        }

        [Test]
        public void VerifyBuild()
        {
            Assert.That(this.builder.Build(), Is.Empty);

            this.builder.AddLine(" ");
            this.builder.StartSection(1, "A");
            this.builder.AddLine(string.Empty);
            this.builder.StartSection(2, "B");
            this.builder.AddLine(" ");
            this.builder.AddLine("b1");
            this.builder.AddLine(string.Empty);
            this.builder.AddLine("b2");
            this.builder.AddLine("  ");

            var sections = this.builder.Build();
            var secondBuild = this.builder.Build();
            string[] linesOfB = ["b1", string.Empty, "b2"];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sections, Has.Count.EqualTo(2));
                Assert.That(sections[0], Is.EqualTo(new DocumentSection(1, 1, "A", sections[0].Lines)));
                Assert.That(sections[0].Lines, Is.Empty);
                Assert.That(sections[1], Has.Property("Number").EqualTo(2).And.Property("Level").EqualTo(2).And.Property("Title").EqualTo("B"));
                Assert.That(sections[1].Lines, Is.EqualTo(linesOfB));
                Assert.That(secondBuild, Is.EqualTo(sections));
            }
        }
    }
}
