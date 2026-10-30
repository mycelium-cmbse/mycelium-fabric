// ------------------------------------------------------------------------------------------------
//  <copyright file="LibraryUnitsTestFixture.cs" company="Starion Group S.A.">
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
    using System.IO;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.TradeOff;
    using Mycelium.Fabric.Mcp.Values;

    using NUnit.Framework;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Suite of tests for the <see cref="LibraryUnits"/> class.
    /// </summary>
    [TestFixture]
    public class LibraryUnitsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the payload subsystem of EOSat-1, which holds the camera (38) and the mass memory (1.5), without unit.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the camera of EOSat-1, whose definition gives its mass.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the mass memory of EOSat-1, the other part of the payload subsystem.
        /// </summary>
        private static readonly Guid MassMemoryId = Guid.Parse("8f7fcbf6-8c6a-5546-1eb6-2e5adfe7a3d4");

        /// <summary>
        /// The <c>Id</c> of the satellite EOSat-1.
        /// </summary>
        private const string Eosat1Id = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";

        /// <summary>
        /// The <c>Id</c> of the mass budget requirement of EOSat-1, whose subject is the satellite.
        /// </summary>
        private static readonly Guid MassBudgetId = Guid.Parse("6b93c533-0395-7e18-1bf6-4475deb47ba5");

        /// <summary>
        /// The conversion factor of the slug to the kilogram.
        /// </summary>
        private const double SlugFactor = 14.5939;

        [Test]
        public void VerifyRead()
        {
            var dtos = new List<DtoElement>();
            var builder = new ConstraintDtoBuilder(dtos);
            var projectUnits = builder.Library("ProjectUnits");
            var otherUnits = builder.Library("OtherUnits");

            var slug = builder.UnitIn(projectUnits.Id, "slug", "slug", builder.NamedReference("kg"), builder.Literal(SlugFactor), builder.Boolean(false));
            var kiloslug = builder.UnitIn(projectUnits.Id, "kiloslug", "kslug", builder.NamedReference("kilo"), builder.Reference(slug.Id));
            var poundFoot = builder.UnitIn(projectUnits.Id, "pound foot", "lbft");
            builder.SetValue(poundFoot, builder.Operation("*", builder.NamedReference("lb"), builder.NamedReference("ft")));
            builder.UnitIn(projectUnits.Id, "shot", "sh", builder.NamedReference("kg"), builder.Integer(1));
            builder.UnitIn(otherUnits.Id, "shot", "sh", builder.NamedReference("g"), builder.Integer(1));
            var otherSlug = builder.UnitIn(otherUnits.Id, "slug", "slug", builder.NamedReference("kg"), builder.Literal(SlugFactor));
            builder.UnitIn(projectUnits.Id, "undefined", "undef");
            builder.UnitIn(projectUnits.Id, "furlong of the project", "fur", builder.NamedReference("furlong"), builder.Integer(1));
            builder.UnitIn(ModelDtoHelper.AddPackage(dtos, "Units").Id, "grain", "gr", builder.NamedReference("mg"), builder.Literal(64.79891));
            var kilogram = builder.Unit("SI", "kilogram", "kg");

            var units = LibraryUnits.Read(ModelDtoHelper.Assemble(dtos));

            Unit Parse(string text) => units.TryParse(text, out var unit) ? unit : null;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => LibraryUnits.Read(null), Throws.ArgumentNullException);

                // Units of a library, from their short name, name or qualified name, alone or in a compound unit.
                Assert.That(Parse("slug").Factor, Is.EqualTo(SlugFactor).Within(1e-12));
                Assert.That(Parse("slug").Dimension, Is.EqualTo(new Dimension { Mass = 1 }));
                Assert.That(Parse("ProjectUnits::slug").Factor, Is.EqualTo(SlugFactor).Within(1e-12));
                Assert.That(Parse("kslug").Factor, Is.EqualTo(SlugFactor * 1000).Within(1e-9));
                Assert.That(Parse("kiloslug").Symbol, Is.EqualTo("kslug"));
                Assert.That(Parse("slug/s").Dimension, Is.EqualTo(new Dimension { Mass = 1, Time = -1 }));
                Assert.That(Parse("lbft").Factor, Is.EqualTo(0.45359237 * 0.3048).Within(1e-12));
                Assert.That(Parse("kg").Factor, Is.EqualTo(1));

                // A short name that two libraries give to different units is found only by its qualified name.
                Assert.That(Parse("sh"), Is.Null);
                Assert.That(Parse("ProjectUnits::shot").Factor, Is.EqualTo(1));
                Assert.That(Parse("OtherUnits::shot").Factor, Is.EqualTo(1e-3).Within(1e-15));

                // Declarations that are not units of a library.
                Assert.That(Parse("undef"), Is.Null);
                Assert.That(Parse("fur"), Is.Null);
                Assert.That(Parse("gr"), Is.Null);

                // The same unit declared by two libraries: the first by qualified name.
                Assert.That(units.FindDeclaration("slug").Id, Is.EqualTo(otherSlug.Id));
                Assert.That(units.FindDeclaration("ProjectUnits::slug").Id, Is.EqualTo(slug.Id));
                Assert.That(units.FindDeclaration(" kslug ").Id, Is.EqualTo(kiloslug.Id));
                Assert.That(units.FindDeclaration("kg").Id, Is.EqualTo(kilogram.Id));
                Assert.That(units.FindDeclaration("slug/s"), Is.Null);
                Assert.That(units.FindDeclaration(null), Is.Null);
            }
        }

        [Test]
        public void VerifyReadDeclaration()
        {
            var dtos = new List<DtoElement>();
            var builder = new ConstraintDtoBuilder(dtos);
            var projectUnits = builder.Library("ProjectUnits");

            var slug = builder.UnitIn(projectUnits.Id, "slug", "slug", builder.NamedReference("kg"), builder.Literal(SlugFactor));
            var cycle = builder.UnitIn(projectUnits.Id, "cycle", "cyc");
            builder.Attribute(builder.Attribute(cycle.Id, null).Id, null, builder.Reference(cycle.Id));
            var undefined = builder.UnitIn(projectUnits.Id, "undefined", "undef");
            var unknownReference = builder.UnitIn(projectUnits.Id, "furlong of the project", "fur", builder.NamedReference("furlong"), builder.Integer(1));
            var userUnit = builder.UnitIn(ModelDtoHelper.AddPackage(dtos, "Units").Id, "grain", "gr", builder.NamedReference("mg"), builder.Literal(64.79891));
            var kilogram = builder.Unit("SI", "kilogram", "kg");
            var inchOfMercury = builder.UnitIn(builder.LibraryPackage("SI").Id, "inch of mercury", "inHg", builder.NamedReference("Pa"), builder.Literal(3386.389));
            var footOfMercury = builder.Unit("SI", "foot of mercury", "ftHg");

            var elements = ModelDtoHelper.Assemble(dtos).OfType<IFeature>().ToDictionary(element => element.Id);

            string Read(Guid unitId)
            {
                var unit = LibraryUnits.ReadDeclaration(elements[unitId]);

                return unit.IsError ? $"error: {unit.FirstError.Description}" : unit.Value.Symbol;
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => LibraryUnits.ReadDeclaration(null), Throws.ArgumentNullException);
                Assert.That(Read(slug.Id), Is.EqualTo("slug"));
                Assert.That(Read(kilogram.Id), Is.EqualTo("kg"));
                Assert.That(Read(inchOfMercury.Id), Is.EqualTo("inHg"));
                Assert.That(LibraryUnits.ReadDeclaration(elements[inchOfMercury.Id]).Value.Factor, Is.EqualTo(3386.389).Within(1e-9));
                Assert.That(Read(cycle.Id), Is.EqualTo("error: The unit 'cyc' is defined from itself."));
                Assert.That(Read(undefined.Id), Does.StartWith("error: The unit 'undef' has no conversion to a known unit"));
                Assert.That(Read(footOfMercury.Id), Does.StartWith("error: The unit 'ftHg' has no conversion to a known unit"));
                Assert.That(Read(unknownReference.Id), Is.EqualTo("error: The unit 'furlong' is not known."));
                Assert.That(Read(userUnit.Id), Is.EqualTo("error: 'grain' is not a unit: a unit is declared in a library package, such as SI."));
            }
        }

        [Test]
        public void Tools_WithAUnitOfALibrary_ConvertTheValues()
        {
            var provider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            provider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            // A library of the project, added to the model, that declares the slug.
            var dtos = new List<DtoElement>();
            var builder = new ConstraintDtoBuilder(dtos);
            builder.UnitIn(builder.Library("ProjectUnits").Id, "slug", "slug", builder.NamedReference("kg"), builder.Literal(SlugFactor));
            var commitRequest = new CommitRequest();
            commitRequest.Change.AddRange(dtos.Select(dto => new DataVersionRequest { Identity = new DataIdentityRequest { Id = dto.Id }, Payload = dto }));
            provider.CreateCommit(commitRequest);

            IAttributeUsage MassOf(Guid partId) =>
                ((IFeature)provider.GetRequiredElementById(partId)).type
                    .SelectMany(type => type.ownedFeature)
                    .OfType<IAttributeUsage>()
                    .Single(attribute => attribute.DeclaredName == "mass");

            ModelChange MassChange(Guid partId, double value, string unit) =>
                new() { Identity = MassOf(partId).Id.ToString(), Payload = new ElementPayload { Value = value, Unit = unit } };

            ModelChange LimitChange(double limit) =>
                new()
                {
                    Identity = MassBudgetId.ToString(),
                    Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "payloadSubsystem.camera.mass", Operator = "<=", Limit = limit, Unit = "kg" } }
                };

            RequirementStatus CheckMassBudget() => new RequirementTools(provider).CheckRequirements().Requirements.Single(check => check.Id == MassBudgetId).Status;

            // The camera weighs 2 slug (29.1878 kg) and the mass memory 1500 g; the satellite satisfies the mass budget.
            var satisfyLink = new ModelChange
            {
                Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = MassBudgetId.ToString(), SatisfyingPart = Eosat1Id }
            };
            var refused = provider.ApplyChanges([MassChange(CameraId, 2, "slug/s")]);
            var applied = provider.ApplyChanges([MassChange(CameraId, 2, "slug"), MassChange(MassMemoryId, 1500, "g"), LimitChange(30), satisfyLink]);
            var payloadMass = new BudgetTools(provider).SumAttribute(PayloadSubsystemId, "mass");
            var statusUnderLimit = CheckMassBudget();
            provider.ApplyChanges([LimitChange(29)]);
            var statusOverLimit = CheckMassBudget();

            var variant = new TradeOffVariant
            {
                Name = "light", Values = new Dictionary<string, double> { ["mass"] = 20 }, Units = new Dictionary<string, string> { ["mass"] = "kg" }
            };

            var choice = new TradeOffChoice { PartId = CameraId, Variants = [variant] };
            var request = new TradeOffRequest(PayloadSubsystemId, [choice], [new TradeOffObjective { Attribute = "mass" }], null, null, 1, null);
            var problem = TradeOffProblemBuilder.Build(provider, request);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refused.Applied, Is.False);
                Assert.That(refused.Problems, Has.Some.Contains("combines a unit of a library of the model with other units"));
                Assert.That(applied.Applied, Is.True);
                Assert.That(payloadMass.Unit.Symbol, Is.EqualTo("slug"));
                Assert.That(payloadMass.Total, Is.EqualTo(2 + (1.5 / SlugFactor)).Within(1e-3));
                Assert.That(statusUnderLimit, Is.EqualTo(RequirementStatus.Satisfied));
                Assert.That(statusOverLimit, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(problem.GetUnit("mass").Symbol, Is.EqualTo("slug"));
                Assert.That(problem.Decisions[0].Options.Single(option => option.Variant == "light").Values["mass"] * SlugFactor, Is.EqualTo(20).Within(1e-9));
            }
        }
    }
}
