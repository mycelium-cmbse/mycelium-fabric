// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationContextTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Validation
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Validation;

    using SysML2.NET.Core.DTO.Core.Classifiers;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;

    using PocoRequirementUsage = SysML2.NET.Core.POCO.Systems.Requirements.IRequirementUsage;

    /// <summary>
    /// Suite of tests for the <see cref="ValidationContext"/> class.
    /// </summary>
    [TestFixture]
    public class ValidationContextTestFixture
    {
        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ValidationContext(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ValidationContext([]), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyIsSpecialized()
        {
            // A definition that types a part, a definition that specializes it, and a definition that nothing uses.
            var builder = new DtoModelBuilder();
            var package = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Model" });
            var camera = builder.AddMember(package, new PartDefinition { DeclaredName = "Camera" });
            var opticalCamera = builder.AddMember(package, new PartDefinition { DeclaredName = "OpticalCamera" });
            var unused = builder.AddMember(package, new PartDefinition { DeclaredName = "Unused" });
            var part = builder.AddMember(package, new PartUsage { DeclaredName = "camera" });

            builder.AddRelationship(part, new FeatureTyping { TypedFeature = part.Id, Type = opticalCamera.Id });
            builder.AddRelationship(opticalCamera, new Subclassification { Subclassifier = opticalCamera.Id, Superclassifier = camera.Id });

            var elements = builder.Build();
            var context = new ValidationContext(elements);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.IsSpecialized(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(context.IsSpecialized(elements.Single(element => element.Id == camera.Id)), Is.True);
                Assert.That(context.IsSpecialized(elements.Single(element => element.Id == opticalCamera.Id)), Is.True);
                Assert.That(context.IsSpecialized(elements.Single(element => element.Id == unused.Id)), Is.False);
            }
        }

        [Test]
        public void VerifyIsSatisfied()
        {
            // A satisfied requirement, a group whose satisfy link covers its nested requirement, a requirement whose only satisfy
            // link is negated, a requirement without satisfy link, and a satisfy link to a requirement that is not in the model.
            var builder = new DtoModelBuilder();
            var package = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Requirements" });
            var satisfied = builder.AddMember(package, new RequirementUsage { DeclaredName = "satisfied" });
            var group = builder.AddMember(package, new RequirementUsage { DeclaredName = "group" });
            var nested = builder.AddMember(group, new RequirementUsage { DeclaredName = "nested" }, new FeatureMembership());
            var negated = builder.AddMember(package, new RequirementUsage { DeclaredName = "negated" });
            var alone = builder.AddMember(package, new RequirementUsage { DeclaredName = "alone" });

            builder.AddSatisfy(package, satisfied.Id);
            builder.AddSatisfy(package, group.Id);
            builder.AddSatisfy(package, negated.Id, true);
            builder.AddSatisfy(package, Guid.NewGuid());

            var elements = builder.Build();
            var context = new ValidationContext(elements);

            PocoRequirementUsage Get(Guid id) => (PocoRequirementUsage)elements.Single(element => element.Id == id);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.IsSatisfied(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(context.IsSatisfied(Get(satisfied.Id)), Is.True);
                Assert.That(context.IsSatisfied(Get(group.Id)), Is.True);
                Assert.That(context.IsSatisfied(Get(nested.Id)), Is.True);
                Assert.That(context.IsSatisfied(Get(negated.Id)), Is.False);
                Assert.That(context.IsSatisfied(Get(alone.Id)), Is.False);
            }
        }
    }
}
