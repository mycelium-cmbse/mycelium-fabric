// ------------------------------------------------------------------------------------------------
//  <copyright file="IPartSelection.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;
    using System.Collections.Generic;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// What some parts of the model hold in one combination that a trade-off studies, instead of what the model says: the
    /// variant that a part takes, its number of units, or other values. The <see cref="ModelEvaluationContext"/> reads the
    /// values through it, so that a requirement is checked on a combination exactly as it is checked on the model.
    /// </summary>
    public interface IPartSelection
    {
        /// <summary>
        /// Collects the values of an attribute over a part and its sub-parts in the combination, as <c>CollectValues</c>
        /// does on the model.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="readPartIds">Receives the <c>Id</c> of each part whose content the combination decides and that is read.</param>
        /// <returns>
        /// The <see cref="CollectedValues"/>, or an error when the part is not in the combination (it is removed, or it belongs
        /// to a part that the combination replaces).
        /// </returns>
        ErrorOr<CollectedValues> CollectValues(IFeature part, string attributeName, ISet<Guid> readPartIds);

        /// <summary>
        /// Gets the feature whose features stand for those of a part in the combination: the variant that the part takes,
        /// or the part itself.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="readPartIds">Receives the <c>Id</c> of the part when the combination decides its content.</param>
        /// <returns>The feature to navigate, or an error when the part is not in the combination.</returns>
        ErrorOr<IFeature> Resolve(IFeature part, ISet<Guid> readPartIds);

        /// <summary>
        /// Gets the value that the combination gives to an attribute of a part, instead of the values of the model.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="readPartIds">Receives the <c>Id</c> of the part when the combination decides its content.</param>
        /// <returns>The value, or <c>null</c> when the combination gives none.</returns>
        NumberValue GetGivenValue(IFeature part, string attributeName, ISet<Guid> readPartIds);
    }
}
