// ------------------------------------------------------------------------------------------------
//  <copyright file="BehaviourChangesHelper.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.Systems.States;

    /// <summary>
    /// Gives a batch of changes that adds behaviour to the <c>Satellite.json</c> test model, once the ports of
    /// <see cref="ConnectionChangesHelper"/> are there: the operating modes of <c>eosat1</c>, as
    /// <c>state modes : OperatingModes { state launch; state safe { entry action pointToSun; } state nominal { do imaging; exit
    /// action stopImaging; } transition first launch accept SeparationDetected then safe; transition safeToNominal first safe
    /// if batteryCharge &gt;= 80 [%] do deploySolarArrays then nominal; transition first nominal accept LowBattery if
    /// batteryCharge &lt; powerSubsystem.battery.minimumCharge then safe; }</c>, the functions <c>action def AcquireImage { out image : Image; }</c>
    /// and <c>action def StoreImage { in image : Image; }</c>, the action <c>imaging</c> that links them by
    /// <c>flow of Image from acquire.image to store.image</c>, and a flow between the ports of the camera and of the on-board
    /// computer.
    /// </summary>
    internal static class BehaviourChangesHelper
    {
        /// <summary>
        /// The <c>Id</c> of the <c>battery</c> part of the power subsystem in <c>Satellite.json</c>.
        /// </summary>
        public const string BatteryId = "75297346-35de-bb2f-d9a1-5abdaa5c6955";

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
                new ModelChange { Identity = "Image", Payload = new ElementPayload { Type = "ItemDefinition", Owner = packageId, Name = "Image" } },
                new ModelChange { Identity = "SeparationDetected", Payload = new ElementPayload { Type = "ItemDefinition", Owner = packageId, Name = "SeparationDetected" } },
                new ModelChange { Identity = "LowBattery", Payload = new ElementPayload { Type = "AttributeDefinition", Owner = packageId, Name = "LowBattery" } },
                new ModelChange { Identity = "AcquireImage", Payload = new ElementPayload { Type = "ActionDefinition", Owner = packageId, Name = "AcquireImage" } },
                new ModelChange { Payload = new ElementPayload { Type = "ReferenceUsage", Owner = "AcquireImage", Name = "image", Definition = "Image", Direction = FeatureDirectionKind.Out } },
                new ModelChange { Identity = "StoreImage", Payload = new ElementPayload { Type = "ActionDefinition", Owner = packageId, Name = "StoreImage" } },
                new ModelChange { Payload = new ElementPayload { Type = "ItemUsage", Owner = "StoreImage", Name = "image", Definition = "Image", Direction = FeatureDirectionKind.In } },
                new ModelChange { Identity = "imaging", Payload = new ElementPayload { Type = "ActionUsage", Owner = eosat1, Name = "imaging" } },
                new ModelChange { Payload = new ElementPayload { Type = "ActionUsage", Owner = "imaging", Name = "acquire", Definition = "AcquireImage" } },
                new ModelChange { Payload = new ElementPayload { Type = "ActionUsage", Owner = "imaging", Name = "store", Definition = "StoreImage" } },
                new ModelChange { Payload = new ElementPayload { Type = "FlowUsage", Owner = "imaging", Item = "Image", Ends = ["acquire.image", "store.image"] } },
                new ModelChange
                {
                    Payload = new ElementPayload
                    {
                        Type = "FlowUsage", Owner = eosat1, Name = "imageFlow", Item = "Image", Ends = [ConnectionChangesHelper.CameraDataOut, ConnectionChangesHelper.ComputerDataIn], Text = "The images go to the computer."
                    }
                },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = eosat1, Name = "batteryCharge", Value = 100, Unit = "%" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = BatteryId, Name = "minimumCharge", Value = 30, Unit = "%" } },
                new ModelChange { Identity = "deploy", Payload = new ElementPayload { Type = "ActionUsage", Owner = eosat1, Name = "deploySolarArrays" } },
                new ModelChange { Identity = "OperatingModes", Payload = new ElementPayload { Type = "StateDefinition", Owner = packageId, Name = "OperatingModes" } },
                new ModelChange { Identity = "modes", Payload = new ElementPayload { Type = "StateUsage", Owner = eosat1, Name = "modes", Definition = "OperatingModes" } },
                new ModelChange { Payload = new ElementPayload { Type = "StateUsage", Owner = "modes", Name = "launch" } },
                new ModelChange { Identity = "safe", Payload = new ElementPayload { Type = "StateUsage", Owner = "modes", Name = "safe" } },
                new ModelChange { Identity = "nominal", Payload = new ElementPayload { Type = "StateUsage", Owner = "modes", Name = "nominal" } },
                new ModelChange { Payload = new ElementPayload { Type = "ActionUsage", Owner = "safe", Name = "pointToSun", StateAction = StateSubactionKind.Entry } },
                new ModelChange { Payload = new ElementPayload { Type = "PerformActionUsage", Owner = "nominal", Performed = "imaging", StateAction = StateSubactionKind.Do } },
                new ModelChange { Payload = new ElementPayload { Type = "ActionUsage", Owner = "nominal", Name = "stopImaging", StateAction = StateSubactionKind.Exit, Text = "The camera stops." } },
                new ModelChange { Payload = new ElementPayload { Type = "TransitionUsage", Owner = "modes", Ends = ["launch", "safe"], Trigger = "SeparationDetected" } },
                new ModelChange
                {
                    Payload = new ElementPayload
                    {
                        Type = "TransitionUsage", Owner = "modes", Name = "safeToNominal", Ends = ["safe", "nominal"], Guard = new GuardPayload { Attribute = "batteryCharge", Operator = ">=", Limit = 80, Unit = "%" },
                        Effect = "deploy", Text = "Once the battery is charged."
                    }
                },
                new ModelChange
                {
                    Payload = new ElementPayload
                    {
                        Type = "TransitionUsage", Owner = "modes", Ends = ["nominal", "safe"], Trigger = "LowBattery",
                        Guard = new GuardPayload { Attribute = "batteryCharge", Operator = "<", LimitAttribute = "powerSubsystem.battery.minimumCharge" }
                    }
                }
            ];
        }
    }
}
