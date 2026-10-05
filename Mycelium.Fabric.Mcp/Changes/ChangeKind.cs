// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeKind.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The kinds of <see cref="ModelChange"/> that can be applied to the model. They are written as text in JSON (for
    /// example <c>"CreatePart"</c>), so that the AI assistant reads and writes names rather than numbers.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ChangeKind>))]
    public enum ChangeKind
    {
        /// <summary>
        /// Creates a package, in another package or at the top level of the model.
        /// </summary>
        CreatePackage,

        /// <summary>
        /// Creates a part definition (<c>part def</c>) in a package.
        /// </summary>
        CreatePartDefinition,

        /// <summary>
        /// Creates a part (<c>part</c>), optionally typed by a part definition, in a package, a part definition or a part.
        /// </summary>
        CreatePart,

        /// <summary>
        /// Creates an attribute, optionally bound to a numeric value, in a part definition or a part.
        /// </summary>
        CreateAttribute,

        /// <summary>
        /// Creates a requirement with its text in a package.
        /// </summary>
        CreateRequirement,

        /// <summary>
        /// Renames an element.
        /// </summary>
        Rename,

        /// <summary>
        /// Replaces the value of an attribute.
        /// </summary>
        SetValue,

        /// <summary>
        /// Replaces the definition that types a part.
        /// </summary>
        SetDefinition,

        /// <summary>
        /// Deletes an element and everything it owns.
        /// </summary>
        Delete
    }
}
