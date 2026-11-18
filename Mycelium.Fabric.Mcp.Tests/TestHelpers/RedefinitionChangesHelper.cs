// ------------------------------------------------------------------------------------------------
//  <copyright file="RedefinitionChangesHelper.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Builds a small rover whose definitions give the typical values of their usages, which a usage can redefine:
    /// <c>Instrument</c> (10 kg), <c>Camera :&gt; Instrument</c> (5 W), <c>Wheel</c> (2 kg), <c>Engine</c> (30 kg),
    /// <c>BigEngine :&gt; Engine</c> (50 kg, a redefinition of the mass of <c>Engine</c>), <c>Battery</c> (8 kg and a fixed
    /// capacity of 100), and <c>Rover</c> (<c>wheels : Wheel[4]</c>, <c>engine : Engine</c>, a <c>height</c> without value).
    /// The <c>rover : Rover</c> also holds a <c>camera</c>, a <c>spareCamera</c>, a <c>battery</c> and a fixed <c>length</c>
    /// of 3 m: 8 + 30 + 10 + 10 + 8 = 66 kg.
    /// </summary>
    internal static class RedefinitionChangesHelper
    {
        /// <summary>
        /// The mass of the rover, in kilograms.
        /// </summary>
        public const double RoverMass = 66;

        /// <summary>
        /// Loads an empty model and applies the changes that build the rover. Their temporary names stay valid in the next
        /// batches of the provider.
        /// </summary>
        /// <returns>The <see cref="InMemoryModelProvider"/> that holds the model.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the changes are refused.</exception>
        public static InMemoryModelProvider CreateModel()
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer(), StandardLibraryHelper.Empty), new DeSerializer(), StandardLibraryHelper.Empty);
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var result = modelProvider.ApplyChanges(CreateChanges());

            return result.Applied ? modelProvider : throw new InvalidOperationException(string.Join(" ", result.Problems));
        }

        /// <summary>
        /// Creates the changes that build the rover.
        /// </summary>
        /// <returns>The <see cref="ModelChange"/>s of one batch.</returns>
        public static List<ModelChange> CreateChanges()
        {
            return
            [
                new ModelChange { Identity = "Garage", Payload = new ElementPayload { Type = "Package", Name = "Garage" } },
                new ModelChange { Identity = "Instrument", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "Instrument" } },
                new ModelChange { Identity = "instrumentMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Instrument", Name = "mass", Value = 10, Unit = "kg" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "Camera", Specializes = "Instrument" } },
                new ModelChange { Identity = "cameraPower", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "power", Value = 5, Unit = "W" } },
                new ModelChange { Identity = "Wheel", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "Wheel" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Wheel", Name = "mass", Value = 2, Unit = "kg" } },
                new ModelChange { Identity = "Engine", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "Engine" } },
                new ModelChange { Identity = "engineMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Engine", Name = "mass", Value = 30, Unit = "kg" } },
                new ModelChange { Identity = "BigEngine", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "BigEngine", Specializes = "Engine" } },
                new ModelChange { Identity = "bigEngineMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "BigEngine", Name = "mass", Value = 50, Unit = "kg" } },
                new ModelChange { Identity = "Battery", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "Battery" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Battery", Name = "mass", Value = 8, Unit = "kg" } },
                new ModelChange { Identity = "batteryCapacity", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Battery", Name = "capacity", Value = 100, IsDefault = false } },
                new ModelChange { Identity = "Rover", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "Rover" } },
                new ModelChange { Identity = "wheels", Payload = new ElementPayload { Type = "PartUsage", Owner = "Rover", Name = "wheels", Definition = "Wheel", Multiplicity = "4" } },
                new ModelChange { Identity = "engine", Payload = new ElementPayload { Type = "PartUsage", Owner = "Rover", Name = "engine", Definition = "Engine" } },
                new ModelChange { Identity = "height", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Rover", Name = "height" } },
                new ModelChange { Identity = "rover", Payload = new ElementPayload { Type = "PartUsage", Owner = "Garage", Name = "rover", Definition = "Rover" } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "PartUsage", Owner = "rover", Name = "camera", Definition = "Camera" } },
                new ModelChange { Identity = "spareCamera", Payload = new ElementPayload { Type = "PartUsage", Owner = "rover", Name = "spareCamera", Definition = "Camera" } },
                new ModelChange { Identity = "battery", Payload = new ElementPayload { Type = "PartUsage", Owner = "rover", Name = "battery", Definition = "Battery" } },
                new ModelChange { Identity = "length", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "rover", Name = "length", Value = 3, Unit = "m" } }
            ];
        }
    }
}
