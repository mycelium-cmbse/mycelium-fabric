// ------------------------------------------------------------------------------------------------
//  <copyright file="TypeExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Classifiers;
    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Extension methods that read the specializations (<c>:></c>) of an <see cref="IType"/>, such as a definition or a usage.
    /// </summary>
    public static class TypeExtensions
    {
        /// <param name="type">The <see cref="IType"/> to read.</param>
        extension(IType type)
        {
            /// <summary>
            /// Gets the elements that the type specializes, as <c>part def Camera :> Equipment</c> or <c>part camera :> sensor</c>
            /// write them: the superclassifiers of its owned subclassifications for a classifier, the subsetted features of its
            /// owned subsettings for a feature. The reference subsettings, redefinitions and cross subsettings are left out.
            /// </summary>
            /// <returns>The specialized elements, in order.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="type"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<IElement> GetSpecializedElements()
            {
                ArgumentNullException.ThrowIfNull(type);

                IEnumerable<IElement> superclassifiers = (type as IClassifier)?.ownedSubclassification?.Select(subclassification => subclassification.Superclassifier) ?? [];

                IEnumerable<IElement> subsettedFeatures = (type as IFeature)?.ownedSubsetting?
                    .Where(subsetting => subsetting is not (IReferenceSubsetting or IRedefinition or ICrossSubsetting))
                    .Select(subsetting => subsetting.SubsettedFeature) ?? [];

                return [.. superclassifiers.Concat(subsettedFeatures).Where(general => general != null)];
            }
        }
    }
}
