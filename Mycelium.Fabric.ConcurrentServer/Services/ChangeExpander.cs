// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeExpander.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Computes the effects of the elements of a change with the core-data service of the exact runtime type of each.
    /// </summary>
    public class ChangeExpander : IChangeExpander
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeExpander"/> class.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="ICoreDataServiceProvider"/> that resolves the core-data service of a data type.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the versions the effects add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> or <paramref name="logger"/> is <c>null</c>.</exception>
        public ChangeExpander(ICoreDataServiceProvider serviceProvider, ILogger<ChangeExpander> logger)
        {
            this.ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the <see cref="ILogger{TCategoryName}"/> that records the versions the effects add.
        /// </summary>
        private ILogger<ChangeExpander> Logger { get; }

        /// <summary>
        /// Gets the <see cref="ICoreDataServiceProvider"/> that resolves the core-data service of a data type.
        /// </summary>
        private ICoreDataServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Computes the versions that the given versions of a change entail, with the core-data service of the metaclass of each element.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change, which holds the given versions.</param>
        /// <param name="versions">The versions whose effects are computed.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The versions to add to the change, or the errors of every element together.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="versions"/> is <c>null</c>.</exception>
        /// <remarks>
        /// A deleted element is handed over as it stands at the previous commits; data that is not an element has no effect.
        /// </remarks>
        public async Task<ErrorOr<IReadOnlyList<DataVersion>>> ComputeEffectsAsync(IChangeContext context, IReadOnlyList<DataVersion> versions,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(versions);

            List<DataVersion> effects = [];
            List<Error> errors = [];

            foreach (var (element, changeType) in versions.Select(version => Classify(context, version)).Where(changed => changed.Element != null))
            {
                var outcome = await this.ServiceProvider.Resolve(element.GetType()).ComputeEffectsAsync(context, element, changeType, cancellationToken);

                if (outcome.IsError)
                {
                    errors.AddRange(outcome.Errors);
                }
                else
                {
                    effects.AddRange(outcome.Value);
                }
            }

            if (effects.Count + errors.Count > 0 && this.Logger.IsEnabled(LogLevel.Debug))
            {
                this.Logger.LogDebug("The effects of {VersionCount} versions add {EffectCount} versions to the change, with {ErrorCount} errors", versions.Count, effects.Count,
                    errors.Count);
            }

            return errors.Count == 0 ? await ErrorOrFactory.FromAsync<IReadOnlyList<DataVersion>>(effects) : errors;
        }

        /// <summary>
        /// Gives the element a version of a change names and how the change affects it.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="version">The version.</param>
        /// <returns>The element, <c>null</c> when the data is not an element, and its <see cref="ChangeType"/>.</returns>
        private static (IElement Element, ChangeType ChangeType) Classify(IChangeContext context, DataVersion version)
        {
            var previous = context.PreviousState.GetValueOrDefault(version.Identity.Id)?.Payload;

            return version.Payload switch
            {
                null => (previous as IElement, ChangeType.DELETED),
                _ when previous != null => (version.Payload as IElement, ChangeType.UPDATED),
                _ => (version.Payload as IElement, ChangeType.CREATED)
            };
        }
    }
}
