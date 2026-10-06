// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeConstraintTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Requirements
{
    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="AttributeConstraint"/> class.
    /// </summary>
    [TestFixture]
    public class AttributeConstraintTestFixture
    {
        [Test]
        public void VerifyWithMargin()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(AttributeConstraint.Operators, Is.EqualTo(["<", "<=", ">", ">=", "=="]));
                Assert.That(AttributeConstraint.WithMargin("subj", "mass", "<=", 150, 20), Is.EqualTo(new AttributeConstraint("subj", "mass", 1.2, "<=", 150, null)));
                Assert.That(AttributeConstraint.WithMargin("subj", "power", "<", 70, 15), Is.EqualTo(new AttributeConstraint("subj", "power", 1.15, "<", 70, null)));
                Assert.That(AttributeConstraint.WithMargin("subj", "capacity", ">=", 300, 20), Is.EqualTo(new AttributeConstraint("subj", "capacity", null, ">=", 300, 1.2)));
                Assert.That(AttributeConstraint.WithMargin("subj", "dataRate", ">", 150, 10), Is.EqualTo(new AttributeConstraint("subj", "dataRate", null, ">", 150, 1.1)));
                Assert.That(AttributeConstraint.WithMargin("subj", "mass", "<=", 150, 0), Is.EqualTo(new AttributeConstraint("subj", "mass", null, "<=", 150, null)));
                Assert.That(AttributeConstraint.WithMargin("subj", "wheelCount", "==", 4, 0), Is.EqualTo(new AttributeConstraint("subj", "wheelCount", null, "==", 4, null)));
            }
        }

        [Test]
        public void VerifyEvaluate()
        {
            var massBudget = AttributeConstraint.WithMargin("subj", "mass", "<=", 150, 20);
            var payloadPower = AttributeConstraint.WithMargin("subj", "power", "<", 70, 0);
            var eclipseEnergy = AttributeConstraint.WithMargin("subj", "capacity", ">=", 300, 20);
            var downlinkRate = AttributeConstraint.WithMargin("subj", "dataRate", ">", 150, 0);
            var wheelCount = AttributeConstraint.WithMargin("subj", "wheelCount", "==", 4, 0);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(massBudget.Evaluate(125.8), Is.EqualTo(new ConstraintEvaluation(false, -0.96, "150.96 > 150")));
                Assert.That(massBudget.Evaluate(125), Is.EqualTo(new ConstraintEvaluation(true, 0, "150 <= 150")));
                Assert.That(payloadPower.Evaluate(64), Is.EqualTo(new ConstraintEvaluation(true, 6, "64 < 70")));
                Assert.That(payloadPower.Evaluate(70), Is.EqualTo(new ConstraintEvaluation(false, 0, "70 >= 70")));
                Assert.That(eclipseEnergy.Evaluate(420), Is.EqualTo(new ConstraintEvaluation(true, 60, "420 >= 360")));
                Assert.That(eclipseEnergy.Evaluate(350), Is.EqualTo(new ConstraintEvaluation(false, -10, "350 < 360")));
                Assert.That(downlinkRate.Evaluate(151.5), Is.EqualTo(new ConstraintEvaluation(true, 1.5, "151.5 > 150")));
                Assert.That(downlinkRate.Evaluate(150), Is.EqualTo(new ConstraintEvaluation(false, 0, "150 <= 150")));
                Assert.That(wheelCount.Evaluate(4), Is.EqualTo(new ConstraintEvaluation(true, 0, "4 == 4")));
                Assert.That(wheelCount.Evaluate(3), Is.EqualTo(new ConstraintEvaluation(false, -1, "3 != 4")));
            }
        }

        [Test]
        public void VerifyToString()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(AttributeConstraint.WithMargin("subj", "mass", "<=", 150, 20).ToString(), Is.EqualTo("subj.mass * 1.2 <= 150"));
                Assert.That(AttributeConstraint.WithMargin("subj", "capacity", ">=", 300, 20).ToString(), Is.EqualTo("subj.capacity >= 300 * 1.2"));
                Assert.That(new AttributeConstraint(null, "mass", null, "<", 1.5, null).ToString(), Is.EqualTo("mass < 1.5"));
            }
        }
    }
}
