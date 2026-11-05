// ------------------------------------------------------------------------------------------------
//  <copyright file="PlannedElement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Plans
{
    using System.ComponentModel;

    /// <summary>
    /// An element that a section of a <see cref="ConstructionPlan"/> creates, designated by its name and its metaclass.
    /// </summary>
    public sealed record PlannedElement
    {
        /// <summary>
        /// Gets the name of the element, or the end of its qualified name when the name repeats in the model, for example
        /// <c>camera::mass</c>.
        /// </summary>
        [Description("The name of the element, as in the payload of the change that creates it ('camera'), or the end of its qualified name when the name repeats ('camera::mass').")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the metaclass of the element, for example <c>PartUsage</c>.
        /// </summary>
        [Description("The metaclass of the element, as in the payload of the change that creates it (PartUsage, RequirementUsage...).")]
        public string Type { get; init; }
    }
}
