// ------------------------------------------------------------------------------------------------
//  <copyright file="MultiplicityChangesHelper.cs" company="Starion Group S.A.">
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
    /// Builds a small rover whose parts have multiplicities and whose requirement has two constraints. The <c>rover</c> holds
    /// <c>bogies : Bogie[2]</c>, each with a <c>chassis</c> (3 kg) and <c>wheels : Wheel[2]</c> (2 kg and 5 W each), and a
    /// <c>battery</c> (10 kg): 24 kg and 20 W. It satisfies <c>REQ-ROV-001</c>: mass at most 30 kg and power at most 15 W.
    /// </summary>
    internal static class MultiplicityChangesHelper
    {
        /// <summary>
        /// The name of the part that holds the rover.
        /// </summary>
        public const string RoverName = "rover";

        /// <summary>
        /// Loads an empty model and applies the changes that build the rover.
        /// </summary>
        /// <param name="withUncountedPart">
        /// Whether the rover also holds <c>heaters[1..4]</c> (0.5 kg and 10 W each), whose number of units is not fixed.
        /// </param>
        /// <returns>The <see cref="InMemoryModelProvider"/> that holds the model.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the changes are refused.</exception>
        public static InMemoryModelProvider CreateModel(bool withUncountedPart)
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var result = modelProvider.ApplyChanges(CreateChanges(withUncountedPart));

            return result.Applied ? modelProvider : throw new InvalidOperationException(string.Join(" ", result.Problems));
        }

        /// <summary>
        /// Creates the changes that build the rover.
        /// </summary>
        /// <param name="withUncountedPart">Whether the rover also holds <c>heaters[1..4]</c>.</param>
        /// <returns>The <see cref="ModelChange"/>s of one batch.</returns>
        public static List<ModelChange> CreateChanges(bool withUncountedPart)
        {
            List<ModelChange> changes =
            [
                new ModelChange { Identity = "Rover", Payload = new ElementPayload { Type = "Package", Name = "Rover" } },
                new ModelChange { Identity = "Wheel", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Rover", Name = "Wheel" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Wheel", Name = "mass", Value = 2, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Wheel", Name = "power", Value = 5, Unit = "W" } },
                new ModelChange { Identity = "Bogie", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Rover", Name = "Bogie" } },
                new ModelChange { Identity = "chassis", Payload = new ElementPayload { Type = "PartUsage", Owner = "Bogie", Name = "chassis" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "chassis", Name = "mass", Value = 3, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "Bogie", Name = "wheels", Definition = "Wheel", Multiplicity = "2" } },
                new ModelChange { Identity = RoverName, Payload = new ElementPayload { Type = "PartUsage", Owner = "Rover", Name = RoverName } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = RoverName, Name = "bogies", Definition = "Bogie", Multiplicity = "[2]" } },
                new ModelChange { Identity = "battery", Payload = new ElementPayload { Type = "PartUsage", Owner = RoverName, Name = "battery" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "battery", Name = "mass", Value = 10, Unit = "kg" } },
                new ModelChange
                {
                    Identity = "roverBudget",
                    Payload = new ElementPayload
                    {
                        Type = "RequirementUsage", Owner = "Rover", Name = "roverBudget", ReqId = "REQ-ROV-001", Text = "The rover shall weigh at most 30 kg and use at most 15 W.",
                        Constraints =
                        [
                            new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 30, Unit = "kg" },
                            new ConstraintPayload { Attribute = "power", Operator = "<=", Limit = 15, Unit = "W" }
                        ]
                    }
                },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = "roverBudget", SatisfyingPart = RoverName } }
            ];

            if (withUncountedPart)
            {
                changes.Add(new ModelChange { Identity = "heaters", Payload = new ElementPayload { Type = "PartUsage", Owner = RoverName, Name = "heaters", Multiplicity = "1..4" } });
                changes.Add(new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "heaters", Name = "mass", Value = 0.5, Unit = "kg" } });
                changes.Add(new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "heaters", Name = "power", Value = 10, Unit = "W" } });
            }

            return changes;
        }
    }
}
