// ------------------------------------------------------------------------------------------------
//  <copyright file="CoreDataService.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Computes the effects and validates the elements of one metaclass in a change, with the <see cref="IElementEffect{TElement}"/>s and
    /// <see cref="IElementRule{TElement}"/>s that apply to it.
    /// </summary>
    /// <typeparam name="TElement">The type of element the service handles.</typeparam>
    public sealed class CoreDataService<TElement> : ICoreDataService<TElement> where TElement : class, IElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoreDataService{TElement}"/> class.
        /// </summary>
        /// <param name="rules">The registered <see cref="IElementRule"/>s, of which those that apply to <typeparamref name="TElement"/> are kept.</param>
        /// <param name="effects">The registered <see cref="IElementEffect"/>s, of which those that apply to <typeparamref name="TElement"/> are kept.</param>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> or <paramref name="effects"/> is <c>null</c>.</exception>
        public CoreDataService(IEnumerable<IElementRule> rules, IEnumerable<IElementEffect> effects)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(effects);

            this.Rules = [.. rules.OfType<IElementRule<TElement>>()];
            this.Effects = [.. effects.OfType<IElementEffect<TElement>>()];
        }

        /// <summary>
        /// Gets the <see cref="IElementEffect{TElement}"/>s that apply to <typeparamref name="TElement"/>, in the order they are registered.
        /// </summary>
        private IReadOnlyList<IElementEffect<TElement>> Effects { get; }

        /// <summary>
        /// Gets the <see cref="IElementRule{TElement}"/>s that apply to <typeparamref name="TElement"/>, in the order they are registered.
        /// </summary>
        private IReadOnlyList<IElementRule<TElement>> Rules { get; }

        /// <summary>
        /// Computes the versions that a change of an element entails, with every effect that applies to it.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The element, as it stands before the change when the change deletes it.</param>
        /// <param name="changeType">The <see cref="ChangeType"/> of the element in the change.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The versions of every effect together, or the errors of every effect together.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="element"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="element"/> is not a <typeparamref name="TElement"/>.</exception>
        public async Task<ErrorOr<IReadOnlyList<DataVersion>>> ComputeEffectsAsync(IChangeContext context, IElement element, ChangeType changeType,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            var handled = AsHandled(element);
            List<DataVersion> versions = [];
            List<Error> errors = [];

            foreach (var effect in this.Effects)
            {
                var outcome = await effect.ComputeAsync(context, handled, changeType, cancellationToken);

                if (outcome.IsError)
                {
                    errors.AddRange(outcome.Errors);
                }
                else
                {
                    versions.AddRange(outcome.Value);
                }
            }

            return errors.Count == 0 ? await ErrorOrFactory.FromAsync<IReadOnlyList<DataVersion>>(versions) : errors;
        }

        /// <summary>
        /// Validates an element that a change creates or updates, with every rule that applies to it.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The element.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or the errors of every rule together.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="element"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="element"/> is not a <typeparamref name="TElement"/>.</exception>
        public async Task<ErrorOr<Success>> ValidateAsync(IChangeContext context, IElement element, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            var handled = AsHandled(element);
            List<Error> errors = [];

            foreach (var rule in this.Rules)
            {
                var outcome = await rule.ValidateAsync(context, handled, cancellationToken);

                if (outcome.IsError)
                {
                    errors.AddRange(outcome.Errors);
                }
            }

            return errors.Count == 0 ? Result.Success : errors;
        }

        /// <summary>
        /// Gives an element as the type of element the service handles.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The element, typed as a <typeparamref name="TElement"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="element"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="element"/> is not a <typeparamref name="TElement"/>.</exception>
        private static TElement AsHandled(IElement element)
        {
            return element switch
            {
                null => throw new ArgumentNullException(nameof(element)),
                TElement typedElement => typedElement,
                _ => throw new ArgumentException($"The {element.GetType().Name} {element.Id} is not a {typeof(TElement).Name}.", nameof(element))
            };
        }
    }
}
