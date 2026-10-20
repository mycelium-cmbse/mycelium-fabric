// ------------------------------------------------------------------------------------------------
//  <copyright file="DefinitionPartsChangesHelper.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// Gives a batch of changes that builds, in the <c>Empty.json</c> test model, parts declared in part definitions:
    /// <c>part def Engine { attribute mass = 2 [kg]; }</c>, <c>part def Stage { part engine1 : Engine; part engine2 : Engine; }</c>,
    /// <c>part stageA : Stage;</c>, <c>part launcher { part first : Stage; part second : Stage; }</c>, a definition that
    /// contains a part of its own kind (<c>part def Node { part next : Node; }</c> and <c>part node : Node;</c>), and the
    /// requirement <c>REQ-STG-001</c> (a stage weighs at most 3 kg) satisfied by <c>stageA</c>.
    /// </summary>
    internal static class DefinitionPartsChangesHelper
    {
        /// <summary>
        /// The declared name of the part typed by <c>Stage</c>.
        /// </summary>
        public const string StageName = "stageA";

        /// <summary>
        /// The declared name of the part that owns two parts typed by <c>Stage</c>.
        /// </summary>
        public const string LauncherName = "launcher";

        /// <summary>
        /// The declared name of the part typed by <c>Node</c>, a definition that contains a part of its own kind.
        /// </summary>
        public const string NodeName = "node";

        /// <summary>
        /// Creates the changes that build the parts declared in part definitions in <c>Empty.json</c>.
        /// </summary>
        /// <returns>The changes, in order.</returns>
        public static IReadOnlyList<ModelChange> CreateChanges()
        {
            return
            [
                new ModelChange { Identity = "Demo", Payload = new ElementPayload { Type = "Package", Name = "Demo" } },
                new ModelChange { Identity = "Engine", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Demo", Name = "Engine" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Engine", Name = "mass", Value = 2, Unit = "kg" } },
                new ModelChange { Identity = "Stage", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Demo", Name = "Stage" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "Stage", Name = "engine1", Definition = "Engine" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "Stage", Name = "engine2", Definition = "Engine" } },
                new ModelChange { Identity = StageName, Payload = new ElementPayload { Type = "PartUsage", Owner = "Demo", Name = StageName, Definition = "Stage" } },
                new ModelChange { Identity = LauncherName, Payload = new ElementPayload { Type = "PartUsage", Owner = "Demo", Name = LauncherName } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = LauncherName, Name = "first", Definition = "Stage" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = LauncherName, Name = "second", Definition = "Stage" } },
                new ModelChange { Identity = "Node", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Demo", Name = "Node" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "Node", Name = "next", Definition = "Node" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "Demo", Name = NodeName, Definition = "Node" } },
                new ModelChange
                {
                    Identity = "stageMass",
                    Payload = new ElementPayload
                    {
                        Type = "RequirementUsage", Owner = "Demo", Name = "stageMass", ReqId = "REQ-STG-001", Text = "A stage shall weigh at most 3 kg.",
                        Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 3, Unit = "kg" }
                    }
                },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = "stageMass", SatisfyingPart = StageName } }
            ];
        }
    }
}
