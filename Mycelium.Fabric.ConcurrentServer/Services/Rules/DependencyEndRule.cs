// ------------------------------------------------------------------------------------------------
//  <copyright file="DependencyEndRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Verifies that every client and supplier of a dependency is an element the change can reference once it is committed.
    /// </summary>
    /// <remarks>
    /// KerML 1.0 §8.3.2.2.2 makes <c>client</c> and <c>supplier</c> stored references to <c>Element</c>.
    /// </remarks>
    public class DependencyEndRule : IElementRule<IDependency>
    {
        /// <summary>
        /// Validates the clients and suppliers of a dependency that a change creates or updates.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The dependency.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, a <see cref="ErrorType.Validation"/> error per client or supplier that is not an element, or the errors of the lookup.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="element"/> is <c>null</c>.</exception>
        public async Task<ErrorOr<Success>> ValidateAsync(IChangeContext context, IDependency element, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(element);

            var ends = await context.GetDataAsync([.. element.Client, .. element.Supplier], cancellationToken);

            if (ends.IsError)
            {
                return ends.Errors;
            }

            List<Error> errors = [.. Dangling(ends.Value, element, element.Client, "client"), .. Dangling(ends.Value, element, element.Supplier, "supplier")];

            return errors.Count == 0 ? Result.Success : errors;
        }

        /// <summary>
        /// Gives an error for each end of a dependency that is not an element the change can reference.
        /// </summary>
        /// <param name="ends">The data of the ends that were found, by identity.</param>
        /// <param name="dependency">The dependency.</param>
        /// <param name="endIds">The identifiers of the clients or of the suppliers.</param>
        /// <param name="end">The name of the end, <c>client</c> or <c>supplier</c>.</param>
        /// <returns>The errors, none when every end is an element.</returns>
        private static IEnumerable<Error> Dangling(IReadOnlyDictionary<Guid, IData> ends, IDependency dependency, IEnumerable<Guid> endIds, string end)
        {
            return endIds.Where(endId => ends.GetValueOrDefault(endId) is not IElement)
                .Select(endId => Error.Validation($"dependency.{end}.dangling", $"The {end} {endId} of dependency {dependency.Id} is not an element once the change is committed."));
        }
    }
}
