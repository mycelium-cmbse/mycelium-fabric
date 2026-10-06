// ------------------------------------------------------------------------------------------------
//  <copyright file="SatisfyRequirementUsageExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.IO;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Requirements;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="SatisfyRequirementUsageExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class SatisfyRequirementUsageExtensionsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>massBudget</c> requirement (REQ-SYS-001) in <c>Satellite.json</c>.
        /// </summary>
        private const string MassBudgetId = "6b93c533-0395-7e18-1bf6-4475deb47ba5";

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid Eosat1Id = Guid.Parse("5d06ef4d-4489-500c-e94f-a8cf563d3fc1");

        [Test]
        public void VerifyResolveSatisfyingFeature()
        {
            var modelProvider = new InMemoryModelProvider();
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            var satisfyId = modelProvider.ApplyChanges([new ModelChange { Kind = ChangeKind.Satisfy, Element = MassBudgetId, SatisfyingPart = Eosat1Id.ToString() }]).CreatedElements[0].Id;
            var builtSatisfy = (ISatisfyRequirementUsage)modelProvider.GetElementById(satisfyId);

            // A satisfy link whose subject is bound to a number rather than to a feature.
            var numberSatisfy = new SatisfyRequirementUsage();
            var subject = new ReferenceUsage { Direction = FeatureDirectionKind.In };
            numberSatisfy.AssignOwnership(new SubjectMembership(), subject);
            subject.AssignOwnership(new FeatureValue(), new LiteralRational { Value = 1 });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((ISatisfyRequirementUsage)null).ResolveSatisfyingFeature(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new SatisfyRequirementUsage().ResolveSatisfyingFeature(), Is.Null);
                Assert.That(numberSatisfy.ResolveSatisfyingFeature(), Is.Null);
                Assert.That(builtSatisfy.ResolveSatisfyingFeature(), Is.SameAs(modelProvider.GetElementById(Eosat1Id)));
            }
        }
    }
}
