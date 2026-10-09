// ------------------------------------------------------------------------------------------------
//  <copyright file="ReferenceTerm.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// A reference to a feature of the model by its name, for example the subject <c>subj</c>, an attribute with a value
    /// (<c>massLimit</c>), a part (<c>eosat1</c>) or an enumeration value (<c>OrbitKind::sunSynchronous</c>).
    /// </summary>
    /// <param name="Name">The name of the feature, as written in the expression.</param>
    /// <param name="Feature">The referenced feature.</param>
    public sealed record ReferenceTerm(string Name, IFeature Feature) : Term
    {
        /// <summary>
        /// Gets the value of the referenced feature.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The value of the feature, or an error that tells why it has none.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.GetValue(this.Feature);
        }

        /// <summary>
        /// Writes the name of the feature.
        /// </summary>
        /// <returns>The name, for example <c>subj</c>.</returns>
        public override string ToString()
        {
            return this.Name;
        }
    }
}
