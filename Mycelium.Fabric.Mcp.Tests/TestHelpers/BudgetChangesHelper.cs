// ------------------------------------------------------------------------------------------------
//  <copyright file="BudgetChangesHelper.cs" company="Starion Group S.A.">
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
    /// Builds a small spacecraft whose mass budget has a margin, can have values still to be defined, and a part left next to
    /// the spacecraft instead of inside it, as an LLM may build it. The spacecraft satisfies <c>REQ-MAS-001</c> (mass at most
    /// 20 kg) and holds <c>wheel1 : Wheel</c> (5 kg, margin 10 %), <c>wheel2 : Wheel</c> (5 kg), <c>computer</c> (3 kg), and
    /// <c>harness</c>, without mass: 13 kg, 13.5 kg with margins.
    /// </summary>
    internal static class BudgetChangesHelper
    {
        /// <summary>
        /// The name of the part that holds the system.
        /// </summary>
        public const string SpacecraftName = "spacecraft";

        /// <summary>
        /// The name of the part left next to the spacecraft, with 4 kg.
        /// </summary>
        public const string SolarArrayName = "solarArray";

        /// <summary>
        /// Loads an empty model and applies the changes that build the spacecraft.
        /// </summary>
        /// <param name="withMissingValues">
        /// Whether two values are still to be defined: <c>wheel2</c> has no mass, as <c>wheel1</c> of the same type has one,
        /// and a <c>battery</c> declares its mass without value.
        /// </param>
        /// <param name="withPartOutside">Whether the <c>solarArray</c> (4 kg) is left next to the spacecraft.</param>
        /// <returns>The <see cref="InMemoryModelProvider"/> that holds the model.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the changes are refused.</exception>
        public static InMemoryModelProvider CreateModel(bool withMissingValues, bool withPartOutside)
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var result = modelProvider.ApplyChanges(CreateChanges(withMissingValues, withPartOutside));

            return result.Applied ? modelProvider : throw new InvalidOperationException(string.Join(" ", result.Problems));
        }

        /// <summary>
        /// Creates the changes that build the spacecraft.
        /// </summary>
        /// <param name="withMissingValues">Whether two values are still to be defined.</param>
        /// <param name="withPartOutside">Whether the <c>solarArray</c> is left next to the spacecraft.</param>
        /// <returns>The <see cref="ModelChange"/>s of one batch.</returns>
        private static List<ModelChange> CreateChanges(bool withMissingValues, bool withPartOutside)
        {
            List<ModelChange> changes =
            [
                new ModelChange { Identity = "Budget", Payload = new ElementPayload { Type = "Package", Name = "Budget" } },
                new ModelChange { Identity = "Wheel", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Budget", Name = "Wheel" } },
                new ModelChange { Identity = SpacecraftName, Payload = new ElementPayload { Type = "PartUsage", Owner = "Budget", Name = SpacecraftName } },
                new ModelChange { Identity = "wheel1", Payload = new ElementPayload { Type = "PartUsage", Owner = SpacecraftName, Name = "wheel1", Definition = "Wheel" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "wheel1", Name = "mass", Value = 5, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "wheel1", Name = "massMargin", Value = 10 } },
                new ModelChange { Identity = "wheel2", Payload = new ElementPayload { Type = "PartUsage", Owner = SpacecraftName, Name = "wheel2", Definition = "Wheel" } },
                new ModelChange { Identity = "computer", Payload = new ElementPayload { Type = "PartUsage", Owner = SpacecraftName, Name = "computer" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "computer", Name = "mass", Value = 3, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = SpacecraftName, Name = "harness" } },
                new ModelChange
                {
                    Identity = "massLimit",
                    Payload = new ElementPayload
                    {
                        Type = "RequirementUsage", Owner = "Budget", Name = "massLimit", ReqId = "REQ-MAS-001", Text = "The spacecraft shall weigh at most 20 kg.",
                        Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 20, Unit = "kg" }
                    }
                },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = "massLimit", SatisfyingPart = SpacecraftName } }
            ];

            if (withMissingValues)
            {
                changes.Add(new ModelChange { Identity = "battery", Payload = new ElementPayload { Type = "PartUsage", Owner = SpacecraftName, Name = "battery" } });
                changes.Add(new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "battery", Name = "mass" } });
            }
            else
            {
                changes.Add(new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "wheel2", Name = "mass", Value = 5, Unit = "kg" } });
            }

            if (withPartOutside)
            {
                changes.Add(new ModelChange { Identity = SolarArrayName, Payload = new ElementPayload { Type = "PartUsage", Owner = "Budget", Name = SolarArrayName } });
                changes.Add(new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = SolarArrayName, Name = "mass", Value = 4, Unit = "kg" } });
            }

            return changes;
        }
    }
}
