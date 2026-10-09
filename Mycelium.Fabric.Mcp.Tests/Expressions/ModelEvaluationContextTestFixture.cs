// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelEvaluationContextTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Expressions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Serializer.Json;

    using AttributeUsageDto = SysML2.NET.Core.DTO.Systems.Attributes.AttributeUsage;
    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoRelationship = SysML2.NET.Core.DTO.Root.Elements.IRelationship;
    using PocoElement = SysML2.NET.Core.POCO.Root.Elements.IElement;

    /// <summary>
    /// Suite of tests for the <see cref="ModelEvaluationContext"/> and <see cref="ConstantEvaluationContext"/> classes, on
    /// <c>Satellite.json</c> with a few added attributes.
    /// </summary>
    [TestFixture]
    public class ModelEvaluationContextTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>massBudget</c> requirement (REQ-SYS-001) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid MassBudgetId = Guid.Parse("6b93c533-0395-7e18-1bf6-4475deb47ba5");

        /// <summary>
        /// The <c>Id</c> of the <c>Requirements</c> package in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid RequirementsPackageId = Guid.Parse("896440c0-d061-7856-fb8f-fe278e26de93");

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid Eosat1Id = Guid.Parse("5d06ef4d-4489-500c-e94f-a8cf563d3fc1");

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The elements of the test model, by <c>Id</c>.
        /// </summary>
        private Dictionary<Guid, PocoElement> elements;

        /// <summary>
        /// The <c>Id</c>s of the elements added by the test, by name.
        /// </summary>
        private Dictionary<string, Guid> addedIds;

        [SetUp]
        public void SetUp()
        {
            var model = ModelDtoHelper.ReadSatelliteDtos();

            var result = new ModelChangeApplier(new Serializer(), new DeSerializer()).Apply(model,
                [new ModelChange { Identity = MassBudgetId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150 } } }]);

            var dtos = CommitRequestHelper.Apply(model, result.Value);
            var builder = new ConstraintDtoBuilder(dtos);
            var parser = new ConstraintTextParser(builder, name => this.addedIds.TryGetValue(name, out var id) ? id : null);
            var subject = builder.GetSubject(MassBudgetId);
            var orbitKinds = builder.Enumeration(RequirementsPackageId, "OrbitKind", "sunSynchronous", "polar");

            this.addedIds = new Dictionary<string, Guid>
            {
                ["subj"] = subject.Id,
                ["subjMass"] = dtos.OfType<AttributeUsageDto>().Single(attribute => attribute.DeclaredName == "mass" && IsOwnedBy(dtos, attribute.Id, subject.Id)).Id,
                ["sunSynchronous"] = orbitKinds[0].Id,
                ["first"] = builder.Attribute(RequirementsPackageId, "first").Id,
                ["noValue"] = builder.Attribute(RequirementsPackageId, "noValue").Id,
                ["payloadMass"] = builder.Attribute(PayloadSubsystemId, "mass").Id
            };

            this.addedIds["massLimit"] = builder.Attribute(RequirementsPackageId, "massLimit", parser.Parse("150 [kg]")).Id;
            this.addedIds["second"] = builder.Attribute(RequirementsPackageId, "second", parser.Parse("first")).Id;
            builder.SetValue(dtos.Single(dto => dto.Id == this.addedIds["first"]), parser.Parse("second"));

            builder.Attribute(CameraId, "isRedundant", parser.Parse("true"));
            builder.Attribute(CameraId, "orbit", parser.Parse("sunSynchronous"));
            builder.Attribute(CameraId, "dataVolume", parser.Parse("2 [GB]"));
            builder.Attribute(CameraId, "heat", parser.Parse("5 [W]"));
            builder.Attribute(CameraId, "margin", parser.Parse("subj"));

            var storage = dtos.Single(dto => dto.DeclaredName == "massMemory" && IsOwnedBy(dtos, dto.Id, PayloadSubsystemId));
            builder.Attribute(storage.Id, "dataVolume", parser.Parse("512 [MB]"));
            builder.Attribute(storage.Id, "heat", parser.Parse("2 [kg]"));

            this.elements = ModelDtoHelper.Assemble(dtos).ToDictionary(element => element.Id);
        }

        [Test]
        public void VerifyGetValue()
        {
            var context = this.CreateContext(this.elements[Eosat1Id]);
            var contextWithoutPart = this.CreateContext(null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.GetValue(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.Value(context, "subj"), Is.EqualTo("EOSat1::Architecture::eosat1"));
                Assert.That(this.Value(contextWithoutPart, "subj"), Is.EqualTo("error: No part satisfies the requirement, so its subject 'subj' has no value."));
                Assert.That(this.Value(context, "sunSynchronous"), Is.EqualTo("sunSynchronous"));
                Assert.That(this.Value(context, "massLimit"), Is.EqualTo("150 [kg]"));
                Assert.That(this.Value(context, "first"), Is.EqualTo("error: The value of 'first' depends on itself."));
                Assert.That(this.Value(context, "payloadMass"), Is.EqualTo("39.5"));
                Assert.That(this.Value(context, "subjMass"), Is.EqualTo("125.8"));
                Assert.That(this.Value(contextWithoutPart, "subjMass"), Is.EqualTo("error: No part satisfies the requirement, so its subject 'subj' has no value."));
                Assert.That(this.Value(context, "noValue"), Is.EqualTo("error: 'noValue' has no value."));
                Assert.That(context.GetValue((IFeature)this.elements[CameraId]).Value, Is.EqualTo(new PartValue((IFeature)this.elements[CameraId])));
            }
        }

        [Test]
        public void VerifyNavigate()
        {
            var satellite = new PartValue((IFeature)this.elements[Eosat1Id]);
            var payload = new PartValue((IFeature)this.elements[PayloadSubsystemId]);
            var camera = new PartValue((IFeature)this.elements[CameraId]);
            var context = this.CreateContext(satellite.Feature);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.Navigate(null, "mass"), Throws.TypeOf<ArgumentNullException>());
                Assert.That(context.Navigate(satellite, "payloadSubsystem").Value, Is.EqualTo(payload));
                Assert.That(context.Navigate(payload, "camera").Value, Is.EqualTo(camera));
                Assert.That(context.Navigate(payload, "mass").Value, Is.EqualTo(new NumberValue(39.5)));
                Assert.That(context.Navigate(camera, "isRedundant").Value, Is.EqualTo(new BooleanValue(true)));
                Assert.That(context.Navigate(camera, "orbit").Value, Is.EqualTo(new EnumValue("sunSynchronous")));
                Assert.That(context.Navigate(camera, "margin").Value, Is.EqualTo(satellite));
                Assert.That(context.Navigate(payload, "dataVolume").Value.ToString(), Is.EqualTo("2.512 [GB]"));
                Assert.That(context.Navigate(payload, "heat").FirstError.Description, Is.EqualTo("Cannot add 5 [W] and 2 [kg]: their units have different dimensions."));
                Assert.That(context.Navigate(satellite, "temperature").FirstError.Description,
                    Is.EqualTo("No part under 'EOSat1::Architecture::eosat1' has a numeric value for 'temperature'."));
                Assert.That(context.RollUps.Select(rollUp => $"{rollUp.Part.DeclaredName}.{rollUp.Attribute} = {rollUp.Total}"),
                    Is.EqualTo(["payloadSubsystem.mass = 39.5", "payloadSubsystem.dataVolume = 2.512 [GB]"]));
                Assert.That(context.RollUps[0].Contributions.Select(contribution => contribution.Value), Is.EquivalentTo([38, 1.5]));

                var simulation = new ModelEvaluationContext(null, satellite.Feature, new ValueOverride(CameraId, "mass", 30));
                Assert.That(simulation.Navigate(satellite, "mass").Value, Is.EqualTo(new NumberValue(117.8)));
                Assert.That(simulation.Navigate(satellite, "power").Value, Is.EqualTo(new NumberValue(227)));
            }
        }

        [Test]
        public void VerifyIsEnumerationValue()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ModelEvaluationContext.IsEnumerationValue(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(ModelEvaluationContext.IsEnumerationValue(this.Feature("sunSynchronous")), Is.True);
                Assert.That(ModelEvaluationContext.IsEnumerationValue(this.Feature("massLimit")), Is.False);
            }
        }

        [Test]
        public void VerifyConstantEvaluationContext()
        {
            var context = ConstantEvaluationContext.Instance;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => context.GetValue(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(context.GetValue(this.Feature("sunSynchronous")).Value, Is.EqualTo(new EnumValue("sunSynchronous")));
                Assert.That(context.GetValue(this.Feature("massLimit")).FirstError.Description, Is.EqualTo("'massLimit' is not a constant."));
                Assert.That(context.Navigate(new PartValue((IFeature)this.elements[CameraId]), "mass").FirstError.Description, Is.EqualTo("'mass' is not a constant."));
            }
        }

        /// <summary>
        /// Tells whether an element is owned by another one, through its owning relationship.
        /// </summary>
        /// <param name="dtos">The DTOs of the model.</param>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <param name="ownerId">The <c>Id</c> of the owner.</param>
        /// <returns><c>true</c> when the element is owned by the owner.</returns>
        private static bool IsOwnedBy(List<DtoElement> dtos, Guid elementId, Guid ownerId)
        {
            var element = dtos.Single(dto => dto.Id == elementId);

            return dtos.OfType<DtoRelationship>().Any(relationship => relationship.Id == element.OwningRelationship && relationship.OwningRelatedElement == ownerId);
        }

        /// <summary>
        /// Creates the context of the evaluation of the massBudget requirement.
        /// </summary>
        /// <param name="satisfyingFeature">The part that satisfies the requirement, or <c>null</c>.</param>
        /// <returns>The new <see cref="ModelEvaluationContext"/>.</returns>
        private ModelEvaluationContext CreateContext(PocoElement satisfyingFeature)
        {
            return new ModelEvaluationContext(this.Feature("subj"), (IFeature)satisfyingFeature);
        }

        /// <summary>
        /// Gets a feature added by the test.
        /// </summary>
        /// <param name="name">The name under which it was added.</param>
        /// <returns>The feature.</returns>
        private IFeature Feature(string name)
        {
            return (IFeature)this.elements[this.addedIds[name]];
        }

        /// <summary>
        /// Gets the value of a feature added by the test, or its error.
        /// </summary>
        /// <param name="context">The context of the evaluation.</param>
        /// <param name="name">The name under which the feature was added.</param>
        /// <returns>The value as text, or the error.</returns>
        private string Value(ModelEvaluationContext context, string name)
        {
            var value = context.GetValue(this.Feature(name));

            return value.IsError ? $"error: {value.FirstError.Description}" : value.Value.ToString();
        }
    }
}
