// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeValidator.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Validates the elements of a change with the core-data service of the exact runtime type of each.
    /// </summary>
    public class ChangeValidator : IChangeValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeValidator"/> class.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="ICoreDataServiceProvider"/> that resolves the core-data service of a data type.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the outcome of each validation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> or <paramref name="logger"/> is <c>null</c>.</exception>
        public ChangeValidator(ICoreDataServiceProvider serviceProvider, ILogger<ChangeValidator> logger)
        {
            this.ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the <see cref="ILogger{TCategoryName}"/> that records the outcome of each validation.
        /// </summary>
        private ILogger<ChangeValidator> Logger { get; }

        /// <summary>
        /// Gets the <see cref="ICoreDataServiceProvider"/> that resolves the core-data service of a data type.
        /// </summary>
        private ICoreDataServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Validates every element a change creates or updates, with the core-data service of its metaclass.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or the errors of every element together.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
        public async Task<ErrorOr<Success>> ValidateAsync(IChangeContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            List<IElement> elements = [.. context.Change.Select(version => version.Payload).OfType<IElement>()];
            List<Error> errors = [];

            foreach (var element in elements)
            {
                var outcome = await this.ServiceProvider.Resolve(element.GetType()).ValidateAsync(context, element, cancellationToken);

                if (outcome.IsError)
                {
                    errors.AddRange(outcome.Errors);
                }
            }

            if (this.Logger.IsEnabled(LogLevel.Debug))
            {
                this.Logger.LogDebug("Validated {ElementCount} elements of a change of {VersionCount} versions, with {ErrorCount} errors", elements.Count,
                    context.Change.Count, errors.Count);
            }

            return errors.Count == 0 ? Result.Success : errors;
        }
    }
}
