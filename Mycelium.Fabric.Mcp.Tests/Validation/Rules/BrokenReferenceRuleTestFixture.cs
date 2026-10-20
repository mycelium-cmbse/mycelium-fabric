// ------------------------------------------------------------------------------------------------
//  <copyright file="BrokenReferenceRuleTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Validation.Rules
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    using SysML2.NET.Core.DTO.Core.Classifiers;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Connectors;
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Connections;
    using SysML2.NET.Core.DTO.Systems.Parts;

    using DtoRelationship = SysML2.NET.Core.DTO.Root.Elements.IRelationship;

    /// <summary>
    /// Suite of tests for the <see cref="BrokenReferenceRule"/> class.
    /// </summary>
    [TestFixture]
    public class BrokenReferenceRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new BrokenReferenceRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("broken-reference"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(rule.Description, Does.StartWith("A relationship misses an element that the metamodel requires"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // The definition of the camera is removed: the typing of the camera loses its type, and the membership of the
            // Components package loses its owned element. The members of the definition lose their owner.
            var builder = DtoModelBuilder.FromSatellite();
            var opticalCamera = builder.Get("OpticalCamera");
            var camera = builder.Get("camera");
            var cameraTyping = builder.Dtos.OfType<FeatureTyping>().Single(typing => typing.TypedFeature == camera.Id);
            var satelliteTyping = builder.Dtos.OfType<FeatureTyping>().Single(typing => typing.TypedFeature == builder.Get("eosat1").Id);
            var orphanMembershipId = opticalCamera.OwnedRelationship[0];

            builder.Remove(opticalCamera);

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new BrokenReferenceRule();

            string FindProblem(Guid? elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(cameraTyping.Id), Is.EqualTo("The FeatureTyping has no type, which the metamodel requires: the element it refers to has been removed, or it was never loaded."));
                Assert.That(FindProblem(opticalCamera.OwningRelationship), Does.StartWith("The OwningMembership refers to an element that is not in the model"));
                Assert.That(FindProblem(satelliteTyping.Id), Is.Null);
                Assert.That(FindProblem(orphanMembershipId), Is.Null);
                Assert.That(FindProblem(camera.Id), Is.Null);
            }

            // A connection and a binding connector without ends, whose ends are optional, a dependency between two definitions,
            // a dependency whose supplier is not in the model, an import of the package, and a
            // relationship of each other metaclass that has required ends.
            var linkBuilder = new DtoModelBuilder();
            var package = linkBuilder.AddMember(linkBuilder.RootNamespace, new Package { DeclaredName = "Links" });
            var antenna = linkBuilder.AddMember(package, new PartDefinition { DeclaredName = "Antenna" });
            var radio = linkBuilder.AddMember(package, new PartDefinition { DeclaredName = "Radio" });
            var antennaPart = linkBuilder.AddMember(package, new PartUsage { DeclaredName = "antenna" });
            var connection = linkBuilder.AddRelationship(antennaPart, new ConnectionUsage { DeclaredName = "rfLink" });
            var bindingConnector = linkBuilder.AddRelationship(antennaPart, new BindingConnector());
            var dependency = linkBuilder.AddMember(package, new Dependency { DeclaredName = "needsRadio", Client = [antenna.Id], Supplier = [radio.Id] });
            var brokenDependency = linkBuilder.AddMember(package, new Dependency { DeclaredName = "needsOldRadio", Client = [antenna.Id], Supplier = [Guid.NewGuid()] });
            var import = linkBuilder.AddRelationship(package, new NamespaceImport { ImportedNamespace = package.Id });

            var comment = linkBuilder.AddMember(package, new Comment { Body = "The antenna." });

            // The references of a feature reference expression to a unit of the SI library, which the model does not contain,
            // by short name and by name, then a name that is not a unit, and a unit name outside a feature reference expression.
            var massLimit = linkBuilder.AddMember(package, new FeatureReferenceExpression());
            var kilogram = linkBuilder.AddRelationship(massLimit, new Membership { MemberElement = Guid.NewGuid(), MemberName = "kg", MemberShortName = "kg" });
            var watt = linkBuilder.AddRelationship(massLimit, new Membership { MemberElement = Guid.NewGuid(), MemberName = "watt" });
            var notUnit = linkBuilder.AddRelationship(massLimit, new Membership { MemberElement = Guid.NewGuid(), MemberName = "camera", MemberShortName = "cam" });
            var removedUnit = linkBuilder.AddRelationship(package, new Membership { MemberElement = Guid.NewGuid(), MemberName = "kg", MemberShortName = "kg" });

            DtoRelationship[] validRelationships =
            [
                linkBuilder.AddRelationship(package, new Membership { MemberElement = radio.Id, MemberName = "receiver" }),
                linkBuilder.AddRelationship(package, new MembershipImport { ImportedMembership = radio.OwningRelationship.Value }),
                linkBuilder.AddRelationship(antennaPart, new ReferenceSubsetting { ReferencedFeature = antennaPart.Id }),
                linkBuilder.AddRelationship(radio, new Subclassification { Subclassifier = radio.Id, Superclassifier = antenna.Id }),
                linkBuilder.AddRelationship(radio, new Conjugation { ConjugatedType = radio.Id, OriginalType = antenna.Id }),
                linkBuilder.AddRelationship(comment, new Annotation { AnnotatedElement = antenna.Id }),
                linkBuilder.AddRelationship(radio, new Disjoining { TypeDisjoined = radio.Id, DisjoiningType = antenna.Id }),
                linkBuilder.AddRelationship(radio, new Differencing { DifferencingType = antenna.Id }),
                linkBuilder.AddRelationship(radio, new Intersecting { IntersectingType = antenna.Id }),
                linkBuilder.AddRelationship(radio, new Unioning { UnioningType = antenna.Id }),
                linkBuilder.AddRelationship(antennaPart, new FeatureChaining { ChainingFeature = antennaPart.Id }),
                linkBuilder.AddRelationship(antennaPart, new FeatureInverting { FeatureInverted = antennaPart.Id, InvertingFeature = antennaPart.Id }),
                linkBuilder.AddRelationship(antennaPart, new TypeFeaturing { FeatureOfType = antennaPart.Id, FeaturingType = antenna.Id }),
            ];

            var linkElements = linkBuilder.Build();
            var linkContext = new ValidationContext(linkElements);

            string FindLinkProblem(Guid elementId) => rule.FindProblem(linkElements.Single(element => element.Id == elementId), linkContext);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(FindLinkProblem(connection.Id), Is.Null);
                Assert.That(FindLinkProblem(bindingConnector.Id), Is.Null);
                Assert.That(FindLinkProblem(dependency.Id), Is.Null);
                Assert.That(FindLinkProblem(brokenDependency.Id), Is.EqualTo("The Dependency has no supplier, which the metamodel requires: the element it refers to has been removed, or it was never loaded."));
                Assert.That(FindLinkProblem(import.Id), Is.Null);
                Assert.That(validRelationships.Select(relationship => FindLinkProblem(relationship.Id)), Is.All.Null);
                Assert.That(FindLinkProblem(kilogram.Id), Is.Null);
                Assert.That(FindLinkProblem(watt.Id), Is.Null);
                Assert.That(FindLinkProblem(notUnit.Id), Does.StartWith("The Membership has no memberElement"));
                Assert.That(FindLinkProblem(removedUnit.Id), Does.StartWith("The Membership has no memberElement"));
            }
        }
    }
}
