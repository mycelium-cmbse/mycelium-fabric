// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffOperatorExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffOperatorExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffOperatorExtensionsTestFixture
    {
        /// <summary>
        /// Every operator, in the order of the enumeration.
        /// </summary>
        private static readonly TradeOffOperator[] Operators = Enum.GetValues<TradeOffOperator>();

        [Test]
        public void VerifyToSymbol()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Operators.Select(comparison => comparison.ToSymbol()), Is.EqualTo(["<", "<=", ">", ">=", "=="]));
                Assert.That(() => ((TradeOffOperator)99).ToSymbol(), Throws.TypeOf<ArgumentOutOfRangeException>());
            }
        }

        [Test]
        public void VerifyToOppositeSymbol()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Operators.Select(comparison => comparison.ToOppositeSymbol()), Is.EqualTo([">=", ">", "<=", "<", "!="]));
                Assert.That(() => ((TradeOffOperator)99).ToOppositeSymbol(), Throws.TypeOf<ArgumentOutOfRangeException>());
            }
        }

        [Test]
        public void VerifyToSwapped()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(TradeOffOperator.LessThan.ToSwapped(), Is.EqualTo(TradeOffOperator.GreaterThan));
                Assert.That(TradeOffOperator.LessThanOrEqual.ToSwapped(), Is.EqualTo(TradeOffOperator.GreaterThanOrEqual));
                Assert.That(TradeOffOperator.GreaterThan.ToSwapped(), Is.EqualTo(TradeOffOperator.LessThan));
                Assert.That(TradeOffOperator.GreaterThanOrEqual.ToSwapped(), Is.EqualTo(TradeOffOperator.LessThanOrEqual));
                Assert.That(TradeOffOperator.Equal.ToSwapped(), Is.EqualTo(TradeOffOperator.Equal));
            }
        }
    }
}
