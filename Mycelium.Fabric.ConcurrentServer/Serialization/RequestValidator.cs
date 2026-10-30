// ------------------------------------------------------------------------------------------------
//  <copyright file="RequestValidator.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ErrorOr;

    using SysML2.NET.PSM.DTO;

    using Error = ErrorOr.Error;

    /// <summary>
    /// Checks a request model against the members the REST/HTTP PSM OpenAPI document requires, before it is mapped onto PIM operation arguments.
    /// </summary>
    public static class RequestValidator
    {
        /// <summary>
        /// Validates a request model.
        /// </summary>
        /// <typeparam name="TRequest">The request model.</typeparam>
        /// <param name="request">The request read from the body.</param>
        /// <returns>The <paramref name="request"/>, or the validation errors it carries.</returns>
        public static ErrorOr<TRequest> Validate<TRequest>(TRequest request)
            where TRequest : class, IRequest
        {
            ArgumentNullException.ThrowIfNull(request);

            List<Error> errors = request switch
            {
                ProjectRequest project => [.. RequiredName(project.Name)],
                BranchRequest branch => [.. RequiredName(branch.Name), .. Required(branch.Head.HasValue, "head")],
                TagRequest tag => [.. RequiredName(tag.Name), .. Required(tag.TaggedCommit != Guid.Empty, "taggedCommit")],
                QueryRequest query => [.. RequiredName(query.Name)],
                CommitRequest commit => [.. Change(commit.Change)],
                _ => []
            };

            return errors.Count == 0 ? request : errors;
        }

        /// <summary>
        /// Validates the <c>name</c> member every named request model requires.
        /// </summary>
        /// <param name="name">The name read from the body.</param>
        /// <returns>The error reporting a missing name, if any.</returns>
        private static IEnumerable<Error> RequiredName(string name)
        {
            return Required(!string.IsNullOrWhiteSpace(name), "name");
        }

        /// <summary>
        /// Reports a required member that is absent.
        /// </summary>
        /// <param name="isPresent">Whether the member is present.</param>
        /// <param name="member">The JSON name of the member.</param>
        /// <returns>The error reporting the absent member, if any.</returns>
        private static IEnumerable<Error> Required(bool isPresent, string member)
        {
            return isPresent ? [] : [Error.Validation($"body.{member}.missing", $"The request body requires a '{member}'.")];
        }

        /// <summary>
        /// Validates <c>CommitRequest.change</c>, which <c>createCommit</c> takes as <c>DataVersion [1..*]</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="change">The data versions read from the body.</param>
        /// <returns>The errors the change carries.</returns>
        /// <remarks>
        /// A data version without payload deletes the data identity it names, so it requires one (§8.1.3).
        /// </remarks>
        private static IEnumerable<Error> Change(IReadOnlyCollection<DataVersionRequest> change)
        {
            if (change is not { Count: > 0 })
            {
                return Required(false, "change");
            }

            if (change.Any(version => version.Payload == null && version.Identity == null))
            {
                return [Error.Validation("body.change.identity.missing", "A data version without payload deletes data, so it requires the 'identity' it deletes.")];
            }

            return change.Any(version => version.Identity?.Id == Guid.Empty)
                ? [Error.Validation("body.change.identity.invalid", "A data identity requires an '@id'.")]
                : [];
        }
    }
}
