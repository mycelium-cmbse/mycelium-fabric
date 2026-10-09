// ------------------------------------------------------------------------------------------------
//  <copyright file="ConnectionChangesHelper.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Gives a batch of changes that adds ports and connectors to the <c>Satellite.json</c> test model, as
    /// <c>port def DataPort { out attribute rate; }</c>, <c>interface def DataInterface { end port source : DataPort; end port
    /// target : ~DataPort; }</c>, an interface from the camera to the on-board computer, a connection between the camera and
    /// the mass memory, and a binding of a port of the payload subsystem to the port of the camera.
    /// </summary>
    internal static class ConnectionChangesHelper
    {
        /// <summary>
        /// The <c>Id</c> of the <c>EOSat1</c> package in <c>Satellite.json</c>.
        /// </summary>
        public const string PackageId = "9d4b1232-b67b-5023-5163-7bdc6de8ff50";

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        public const string Eosat1Id = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        public const string PayloadSubsystemId = "95a8c184-a12e-1125-c0ee-bbe7a025de5b";

        /// <summary>
        /// The <c>Id</c> of the <c>OpticalCamera</c> part definition in <c>Satellite.json</c>, which types the camera.
        /// </summary>
        public const string OpticalCameraId = "85a46d8e-cb8e-0bf8-641e-760fdd358c66";

        /// <summary>
        /// The <c>Id</c> of the <c>onBoardComputer</c> part in <c>Satellite.json</c>.
        /// </summary>
        public const string OnBoardComputerId = "3d4c9630-97ec-0a3a-296a-742885df1bc0";

        /// <summary>
        /// The end of the interface at the camera, whose port is owned by its definition <c>OpticalCamera</c>.
        /// </summary>
        public const string CameraDataOut = "payloadSubsystem.camera.dataOut";

        /// <summary>
        /// The end of the interface at the on-board computer.
        /// </summary>
        public const string ComputerDataIn = "dataHandlingSubsystem.onBoardComputer.dataIn";

        /// <summary>
        /// Creates the changes that add the ports and connectors to <c>Satellite.json</c>.
        /// </summary>
        /// <returns>The changes, in order.</returns>
        public static IReadOnlyList<ModelChange> CreateChanges()
        {
            return
            [
                new ModelChange { Identity = "DataPort", Payload = new ElementPayload { Type = "PortDefinition", Owner = PackageId, Name = "DataPort", Text = "Image data." } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "DataPort", Name = "rate", Direction = FeatureDirectionKind.Out } },
                new ModelChange { Identity = "DataInterface", Payload = new ElementPayload { Type = "InterfaceDefinition", Owner = PackageId, Name = "DataInterface" } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = "DataInterface", Name = "source", Definition = "DataPort", IsEnd = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = "DataInterface", Name = "target", Definition = "DataPort", Conjugated = true, IsEnd = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = OpticalCameraId, Name = "dataOut", Definition = "DataPort" } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = OnBoardComputerId, Name = "dataIn", Definition = "DataPort", Conjugated = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = PayloadSubsystemId, Name = "imageOut", Definition = "DataPort" } },
                new ModelChange
                {
                    Payload = new ElementPayload
                    {
                        Type = "InterfaceUsage", Owner = Eosat1Id, Name = "imageLink", Definition = "DataInterface", Ends = [CameraDataOut, ComputerDataIn], Text = "Images to the on-board computer."
                    }
                },
                new ModelChange { Payload = new ElementPayload { Type = "ConnectionUsage", Owner = PayloadSubsystemId, Ends = ["camera", "massMemory"] } },
                new ModelChange { Payload = new ElementPayload { Type = "BindingConnectorAsUsage", Owner = PayloadSubsystemId, Ends = ["imageOut", "camera.dataOut"] } }
            ];
        }
    }
}
