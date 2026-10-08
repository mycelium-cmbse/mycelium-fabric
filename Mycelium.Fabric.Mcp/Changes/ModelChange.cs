// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChange.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.ComponentModel;

    /// <summary>
    /// One change of a batch sent to the <c>apply_changes</c> tool, shaped like a <c>DataVersion</c> of a commit: an
    /// <see cref="Identity"/> and a <see cref="Payload"/>.
    /// </summary>
    /// <remarks>
    /// As for <c>createCommit</c> of the <c>ProjectDataVersioningService</c> (Systems Modeling API and Services 1.0 §7.2.3),
    /// the kind of change follows from these two properties: a payload with a <see cref="ElementPayload.Type"/> creates an
    /// element, a payload without type updates the element designated by the identity, and no payload deletes it. The
    /// identity holds either the <c>Id</c> of an element of the model, or the temporary name given to an element created by
    /// a previous change of the same batch, whose <c>Id</c> is not known yet.
    /// </remarks>
    public sealed record ModelChange
    {
        /// <summary>
        /// Gets the identity of the element: the element to update or delete, or the optional temporary name of the element
        /// to create.
        /// </summary>
        [Description("Update and delete: the element, as an identifier (Id) or the temporary name of an element created by a previous change. Create, optional: a temporary name for the new element (for example 'camera'), unique in the batch, that later changes of the same batch use in identity, owner or definition.")]
        public string Identity { get; init; }

        /// <summary>
        /// Gets the data of the element: everything that describes it for a creation, the properties to change for an
        /// update, or <c>null</c> for a deletion.
        /// </summary>
        [Description("The data of the element. Create: its type and its properties. Update: only the properties to change, without type. Delete: no payload.")]
        public ElementPayload Payload { get; init; }
    }
}
