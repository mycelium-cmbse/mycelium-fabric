// ------------------------------------------------------------------------------------------------
//  <copyright file="RelationshipChangesHelper.cs" company="Starion Group S.A.">
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
    /// Gives a batch of changes that relates elements of the <c>Satellite.json</c> test model: <c>OpticalCamera :> Equipment</c>,
    /// a part definition <c>Sensor :> Equipment</c>, <c>camera :> sensor</c>, and two actions of <c>eosat1</c>,
    /// <c>first acquireImage then downlinkImage</c>, the first one performed by the camera, allocated to it and satisfying
    /// REQ-SYS-003.
    /// </summary>
    internal static class RelationshipChangesHelper
    {
        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        public const string CameraId = "558a0ae4-8585-66a3-9bc6-52322650941c";

        /// <summary>
        /// The <c>Id</c> of the requirement REQ-SYS-003 in <c>Satellite.json</c>.
        /// </summary>
        public const string PointingAccuracyId = "24253a34-a8ba-fc0a-0dba-e5e83d42bd5f";

        /// <summary>
        /// Creates the changes.
        /// </summary>
        /// <returns>The changes, in order.</returns>
        public static IReadOnlyList<ModelChange> CreateChanges()
        {
            const string packageId = ConnectionChangesHelper.PackageId;
            const string eosat1 = ConnectionChangesHelper.Eosat1Id;

            return
            [
                new ModelChange { Identity = "equipment", Payload = new ElementPayload { Type = "PartDefinition", Owner = packageId, Name = "Equipment" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartDefinition", Owner = packageId, Name = "Sensor", Specializes = "equipment" } },
                new ModelChange { Identity = ConnectionChangesHelper.OpticalCameraId, Payload = new ElementPayload { Specializes = "equipment" } },
                new ModelChange { Identity = "sensor", Payload = new ElementPayload { Type = "PartUsage", Owner = ConnectionChangesHelper.PayloadSubsystemId, Name = "sensor" } },
                new ModelChange { Identity = CameraId, Payload = new ElementPayload { Specializes = "sensor" } },
                new ModelChange { Identity = "acquire", Payload = new ElementPayload { Type = "ActionUsage", Owner = eosat1, Name = "acquireImage" } },
                new ModelChange { Payload = new ElementPayload { Type = "ActionUsage", Owner = eosat1, Name = "downlinkImage" } },
                new ModelChange { Payload = new ElementPayload { Type = "SuccessionAsUsage", Owner = eosat1, Ends = ["acquireImage", "downlinkImage"] } },
                new ModelChange { Payload = new ElementPayload { Type = "PerformActionUsage", Owner = CameraId, Performed = "acquire", Text = "The camera takes the images." } },
                new ModelChange { Payload = new ElementPayload { Type = "AllocationUsage", Owner = eosat1, Name = "imagingAllocation", Ends = ["acquireImage", "payloadSubsystem.camera"] } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = PointingAccuracyId, SatisfyingPart = "acquire" } }
            ];
        }
    }
}
