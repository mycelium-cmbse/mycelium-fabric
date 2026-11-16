// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Common.DependencyInjection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Registers families of implementations with the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The service collection the registrations are added to.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers every concrete class that implements <typeparamref name="TMarker"/> under the interfaces closest to its implementation.
            /// </summary>
            /// <typeparam name="TMarker">The interface the registered classes implement.</typeparam>
            /// <param name="lifetime">The <see cref="ServiceLifetime"/> of the registrations.</param>
            /// <param name="assemblies">The assemblies scanned, the one declaring <typeparamref name="TMarker"/> when none is given.</param>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <exception cref="ArgumentException"><typeparamref name="TMarker"/> is not an interface.</exception>
            /// <exception cref="InvalidOperationException">Two scanned classes would be registered under the same interface.</exception>
            /// <remarks>
            /// A class is registered under each interface it implements that extends <typeparamref name="TMarker"/> and that no other such interface
            /// extends, or under <typeparamref name="TMarker"/> itself when it implements none. A service type registered before the scan keeps its registration.
            /// </remarks>
            public IServiceCollection AddImplementationsOf<TMarker>(ServiceLifetime lifetime = ServiceLifetime.Scoped, params Assembly[] assemblies) where TMarker : class
            {
                ArgumentNullException.ThrowIfNull(services);

                var marker = typeof(TMarker);

                if (!marker.IsInterface)
                {
                    throw new ArgumentException($"{marker.Name} is not an interface.", nameof(TMarker));
                }

                var scannedAssemblies = assemblies is { Length: > 0 } ? assemblies : [marker.Assembly];

                var descriptors = scannedAssemblies.SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && marker.IsAssignableFrom(type))
                    .SelectMany(implementation => ClosestInterfaces(implementation, marker).Select(serviceType => new ServiceDescriptor(serviceType, implementation, lifetime)))
                    .ToList();

                var ambiguous = descriptors.GroupBy(descriptor => descriptor.ServiceType).FirstOrDefault(implementations => implementations.Count() > 1);

                if (ambiguous != null)
                {
                    throw new InvalidOperationException(
                        $"{ambiguous.Key.Name} is implemented by {string.Join(" and ", ambiguous.Select(descriptor => descriptor.ImplementationType?.Name))}; "
                        + "register one explicitly.");
                }

                services.TryAdd(descriptors);

                return services;
            }
        }

        /// <summary>
        /// Gives the interfaces of a class that extend a marker interface and that no other such interface of the class extends.
        /// </summary>
        /// <param name="implementation">The class that implements the marker.</param>
        /// <param name="marker">The marker interface.</param>
        /// <returns>The closest interfaces, or the marker itself when the class implements no interface extending it.</returns>
        private static List<Type> ClosestInterfaces(Type implementation, Type marker)
        {
            List<Type> candidates = [.. implementation.GetInterfaces().Where(candidate => candidate != marker && marker.IsAssignableFrom(candidate))];
            List<Type> closest = [.. candidates.Where(candidate => !candidates.Exists(other => other != candidate && candidate.IsAssignableFrom(other)))];

            return closest.Count > 0 ? closest : [marker];
        }
    }
}
