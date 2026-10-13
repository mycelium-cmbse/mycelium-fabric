// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelDtoHelper.cs" company="Starion Group S.A.">
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
    using System.IO;
    using System.Linq;

    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using PocoElement = SysML2.NET.Core.POCO.Root.Elements.IElement;

    /// <summary>
    /// Reads the DTOs of the test model and turns DTOs into POCOs, so that the tests can change a model at the DTO level and
    /// read it as the tools do.
    /// </summary>
    internal static class ModelDtoHelper
    {
        /// <summary>
        /// Reads the DTOs of <c>Data/Satellite.json</c>.
        /// </summary>
        /// <returns>The DTOs of the model.</returns>
        public static List<DtoElement> ReadSatelliteDtos()
        {
            using var stream = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json"));

            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();
        }

        /// <summary>
        /// Adds a package at the top level of a model, to own the elements built by a test.
        /// </summary>
        /// <param name="dtos">The DTOs of the model.</param>
        /// <param name="name">The name of the package.</param>
        /// <returns>The new package.</returns>
        public static Package AddPackage(List<DtoElement> dtos, string name)
        {
            var package = new Package { Id = Guid.NewGuid(), DeclaredName = name };
            package.ElementId = package.Id.ToString();
            dtos.Add(package);

            return package;
        }

        /// <summary>
        /// Turns DTOs into POCOs, whose derived properties can then be read.
        /// </summary>
        /// <param name="dtos">The DTOs of the model.</param>
        /// <returns>The POCOs of the model.</returns>
        public static List<PocoElement> Assemble(IEnumerable<DtoElement> dtos)
        {
            var assembler = new Assembler();
            assembler.Synchronize(dtos);

            return assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToList();
        }
    }
}
