// ------------------------------------------------------------------------------------------------
//  <copyright file="ThemeFiles.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// The theme sources of this repository, read by the tests that need no Keycloak.
    /// </summary>
    internal static class ThemeFiles
    {
        /// <summary>
        /// Gets the docker/keycloak/themes/ directory of the repository the tests run from.
        /// </summary>
        public static string ThemesDirectory { get; } = FindThemesDirectory();

        /// <summary>
        /// Reads one property of a theme.properties file.
        /// </summary>
        /// <param name="relativePath">The file, relative to themes/ (for example <c>mycelium/login/theme.properties</c>).</param>
        /// <param name="key">The property name.</param>
        /// <returns>The value.</returns>
        /// <exception cref="AssertionException">When the property is missing.</exception>
        public static string Property(string relativePath, string key)
        {
            return Properties(relativePath).TryGetValue(key, out var value) ? value : throw new AssertionException($"no {key} in {relativePath}");
        }

        /// <summary>
        /// Reads every property of a theme.properties file; comments and blank lines are skipped.
        /// </summary>
        /// <param name="relativePath">The file, relative to themes/.</param>
        /// <returns>The values by property name, the first declaration winning.</returns>
        public static IReadOnlyDictionary<string, string> Properties(string relativePath)
        {
            return File.ReadAllLines(Path.Combine(ThemesDirectory, relativePath))
                .Where(line => !line.StartsWith('#') && line.Contains('='))
                .Select(line => line.Split('=', 2))
                .DistinctBy(pair => pair[0].Trim())
                .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim());
        }

        /// <summary>
        /// Walks up from the test output directory to the first directory holding docker/keycloak/themes/.
        /// </summary>
        /// <returns>The full path of themes/.</returns>
        /// <exception cref="DirectoryNotFoundException">When no parent directory holds docker/keycloak/themes/.</exception>
        private static string FindThemesDirectory()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docker", "keycloak", "themes")))
            {
                directory = directory.Parent;
            }

            return directory is null
                ? throw new DirectoryNotFoundException("no docker/keycloak/themes/ above the test directory")
                : Path.Combine(directory.FullName, "docker", "keycloak", "themes");
        }
    }
}
