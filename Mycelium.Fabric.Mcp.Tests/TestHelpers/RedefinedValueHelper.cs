// ------------------------------------------------------------------------------------------------
//  <copyright file="RedefinedValueHelper.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Builds EOSat-1 (<c>Satellite.json</c>) where the camera has a mass of its own, as the SysML textual notation writes it:
    /// <c>part camera : OpticalCamera { :&gt;&gt; mass = 35; }</c>. The attribute of the camera has no declared name and
    /// redefines the <c>mass</c> of <c>OpticalCamera</c>, whose default value is 38.
    /// </summary>
    internal static class RedefinedValueHelper
    {
        /// <summary>
        /// The mass that the camera gives itself.
        /// </summary>
        public const double CameraMass = 35;

        /// <summary>
        /// The mass of <c>OpticalCamera</c>, which the camera redefines.
        /// </summary>
        public const double DefinitionMass = 38;

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        public static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>, which holds the camera.
        /// </summary>
        public static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        public static readonly Guid Eosat1Id = Guid.Parse("5d06ef4d-4489-500c-e94f-a8cf563d3fc1");

        /// <summary>
        /// Builds the DTOs of the model.
        /// </summary>
        /// <returns>The DTOs of EOSat-1, where the camera redefines its mass.</returns>
        public static List<DtoElement> CreateDtos()
        {
            var dtos = ModelDtoHelper.ReadSatelliteDtos();
            var builder = new ConstraintDtoBuilder(dtos);
            var parser = new ConstraintTextParser(builder, _ => null);

            var opticalCamera = dtos.Single(dto => dto.DeclaredName == "OpticalCamera");

            var definitionMass = dtos.OfType<FeatureMembership>()
                .Where(membership => membership.OwningRelatedElement == opticalCamera.Id)
                .SelectMany(membership => membership.OwnedRelatedElement)
                .Select(memberId => dtos.Single(dto => dto.Id == memberId))
                .OfType<AttributeUsage>()
                .Single(attribute => attribute.DeclaredName == "mass");

            var cameraMass = builder.Attribute(CameraId, null, parser.Parse(CameraMass.ToString(CultureInfo.InvariantCulture)));
            builder.Redefinition(cameraMass.Id, definitionMass.Id);

            return dtos;
        }

        /// <summary>
        /// Loads the model in an <see cref="InMemoryModelProvider"/>, which can also apply and preview changes.
        /// </summary>
        /// <returns>The <see cref="InMemoryModelProvider"/> that holds the model.</returns>
        public static InMemoryModelProvider CreateModel()
        {
            var path = Path.Combine(Path.GetTempPath(), $"redefined-value-{Guid.NewGuid()}.json");

            try
            {
                using (var stream = File.Create(path))
                {
                    new Serializer().Serialize(CreateDtos(), SerializationModeKind.JSON, false, stream, new JsonWriterOptions());
                }

                var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer(), StandardLibraryHelper.Empty), new DeSerializer(), StandardLibraryHelper.Empty);
                modelProvider.LoadModel(new Uri(path));

                return modelProvider;
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
