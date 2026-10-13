// ------------------------------------------------------------------------------------------------
//  <copyright file="CommitRequestHelper.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.PSM.DTO;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Applies a <see cref="CommitRequest"/> to the DTOs of a model, as a commit does, so that the tests can check the model
    /// that a batch of changes produces.
    /// </summary>
    internal static class CommitRequestHelper
    {
        /// <summary>
        /// Applies the change of a <see cref="CommitRequest"/> to the DTOs of a model: a payload adds or replaces the element
        /// of its identity, no payload removes it.
        /// </summary>
        /// <param name="model">The DTOs of the model before the commit, which are left in place.</param>
        /// <param name="commitRequest">The <see cref="CommitRequest"/> to apply.</param>
        /// <returns>The DTOs of the model after the commit.</returns>
        public static List<DtoElement> Apply(IEnumerable<DtoElement> model, CommitRequest commitRequest)
        {
            var dtosById = model.ToDictionary(dto => dto.Id);

            foreach (var dataVersion in commitRequest.Change)
            {
                if (dataVersion.Payload is DtoElement element)
                {
                    dtosById[element.Id] = element;
                }
                else
                {
                    dtosById.Remove(dataVersion.Identity.Id);
                }
            }

            return [.. dtosById.Values];
        }
    }
}
