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
    using SysML2.NET.Core.DTO.Root.Namespaces;
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
        public void VerifyGetQualifiedName()
        {
            // A package owns two parts and an anonymous part. When the camera is removed, the qualified name of the lens cannot be
            // computed.
            var builder = new DtoModelBuilder();
            var package = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Model" });
            var camera = builder.AddMember(package, new PartUsage { DeclaredName = "camera" });
            var anonymousPart = builder.AddMember(package, new PartUsage());
            var lens = builder.AddMember(package, new PartUsage { DeclaredName = "lens" });
            var elements = builder.Build();

            builder.Remove(camera);
            var elementsWithoutCamera = builder.Build();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ValidationContext.GetQualifiedName(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(ValidationContext.GetQualifiedName(elements.Single(element => element.Id == lens.Id)), Is.EqualTo("Model::lens"));
                Assert.That(ValidationContext.GetQualifiedName(elements.Single(element => element.Id == anonymousPart.Id)), Is.Null);
                Assert.That(ValidationContext.GetQualifiedName(elementsWithoutCamera.Single(element => element.Id == lens.Id)), Is.Null);
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

        [Test]
        public void VerifySatisfiesRequirements()
        {
            // The spacecraft satisfies the mass requirement; its wheel and the solar array next to it do not.
            var elements = BudgetChangesHelper.CreateModel(withMissingValues: false, withPartOutside: true).Elements;
            var context = new ValidationContext(elements);

            bool SatisfiesRequirements(string name) => context.SatisfiesRequirements(elements.Single(element => element.DeclaredName == name));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.SatisfiesRequirements(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(SatisfiesRequirements(BudgetChangesHelper.SpacecraftName), Is.True);
                Assert.That(SatisfiesRequirements("wheel1"), Is.False);
                Assert.That(SatisfiesRequirements(BudgetChangesHelper.SolarArrayName), Is.False);
            }
        }

        [Test]
        public void VerifyCountElementsNamed()
        {
            // Two root namespaces declare a Payload package with a camera part, and an Aocs package owns a camera too.
            var builder = new DtoModelBuilder();
            var payload = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Payload" });
            var otherPayload = builder.AddMember(builder.Add(new Namespace()), new Package { DeclaredName = "Payload" });
            var aocs = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Aocs" });
            builder.AddMember(payload, new PartUsage { DeclaredName = "camera" });
            builder.AddMember(otherPayload, new PartUsage { DeclaredName = "camera" });
            builder.AddMember(aocs, new PartUsage { DeclaredName = "camera" });

            var context = new ValidationContext(builder.Build());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.CountElementsNamed(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(context.CountElementsNamed("Payload::camera"), Is.EqualTo(2));
                Assert.That(context.CountElementsNamed("Aocs::camera"), Is.EqualTo(1));
                Assert.That(context.CountElementsNamed("Payload::antenna"), Is.Zero);
            }
        }
    }
}
