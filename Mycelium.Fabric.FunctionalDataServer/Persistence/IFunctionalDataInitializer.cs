// ------------------------------------------------------------------------------------------------
//  <copyright file="IFunctionalDataInitializer.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Mycelium.SDK.Dal;

    /// <summary>
    /// Brings the Functional Data database up to date with the <see cref="SqlSchemaScripts"/> packaged in <c>Mycelium.SDK.Dal</c>.
    /// </summary>
    public interface IFunctionalDataInitializer
    {
        /// <summary>
        /// Applies, in name order and within one locked transaction, every packaged script the database has not recorded as applied.
        /// </summary>
        /// <param name="cancellationToken">The token canceling initialization.</param>
        /// <returns>A task that completes after the initialization transaction commits.</returns>
        /// <exception cref="InvalidOperationException">Configuration is invalid, or a script or its history cannot be applied.</exception>
        /// <exception cref="OperationCanceledException">Initialization is canceled or exceeds its configured deadline.</exception>
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
