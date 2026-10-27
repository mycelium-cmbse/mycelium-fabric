// ------------------------------------------------------------------------------------------------
//  <copyright file="MissingValueTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// Suite of tests for the <see cref="MissingValue"/> class.
    /// </summary>
    [TestFixture]
    public class MissingValueTestFixture
    {
        [Test]
        public void VerifyDescribe()
        {
            var missingValues = Enumerable.Range(1, 7).Select(index => new MissingValue(Guid.NewGuid(), $"Demo::part{index}", [])).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => MissingValue.Describe(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(MissingValue.Describe([]), Is.Empty);
                Assert.That(MissingValue.Describe(missingValues[..2]), Is.EqualTo("Demo::part1, Demo::part2"));
                Assert.That(MissingValue.Describe(missingValues), Is.EqualTo("Demo::part1, Demo::part2, Demo::part3, Demo::part4, Demo::part5 and 2 more"));
            }
        }
    }
}
