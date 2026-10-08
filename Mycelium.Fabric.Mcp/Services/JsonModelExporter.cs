// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonModelExporter.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    using ModelContextProtocol;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// The <see cref="IModelExporter"/> that writes a model to a JSON file of the Systems Modeling API, the format that the
    /// <see cref="InMemoryModelProvider"/> loads.
    /// </summary>
    /// <remarks>
    /// The elements of the model are POCOs: each one is turned back into its DTO by the <c>ToDto</c> extension method that
    /// SysML2.NET.Dal provides for its metaclass, then the DTOs are serialized by SysML2.NET.Serializer.Json.
    /// </remarks>
    public partial class JsonModelExporter : IModelExporter
    {
        /// <summary>
        /// The extension of the written files.
        /// </summary>
        private const string FileExtension = ".json";

        /// <summary>
        /// The <c>ToDto</c> methods of SysML2.NET.Dal, one per metaclass, indexed by the POCO class they convert.
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo> ToDtoMethods = typeof(Assembler).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true })
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name == "ToDto" && method.GetParameters().Length == 2)
            .ToDictionary(method => method.GetParameters()[0].ParameterType);

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonModelExporter"/> class.
        /// </summary>
        /// <param name="exportDirectory">The <see cref="Uri"/> of the folder in which the files are written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="exportDirectory"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="exportDirectory"/> is not an absolute file <see cref="Uri"/>.
        /// </exception>
        public JsonModelExporter(Uri exportDirectory)
        {
            ArgumentNullException.ThrowIfNull(exportDirectory);

            if (!exportDirectory.IsAbsoluteUri || !exportDirectory.IsFile)
            {
                throw new ArgumentException("The export directory must be an absolute file URI.", nameof(exportDirectory));
            }

            this.ExportDirectory = exportDirectory;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the folder in which the files are written.
        /// </summary>
        public Uri ExportDirectory { get; }

        /// <summary>
        /// Writes the given elements to a new JSON file of the export folder, which is created when it does not exist. An
        /// existing file is never replaced.
        /// </summary>
        /// <param name="elements">The elements of the model to write.</param>
        /// <param name="fileName">
        /// The simple name of the file, without folder, to which <c>.json</c> is added when it is missing, or <c>null</c>
        /// for <c>model-</c> followed by the current UTC date and time.
        /// </param>
        /// <returns>The full path of the written file.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is not a simple name, or when the export folder already has a file with
        /// this name. The message is sent back to the AI assistant.
        /// </exception>
        public string Export(IReadOnlyCollection<IElement> elements, string fileName)
        {
            ArgumentNullException.ThrowIfNull(elements);

            var path = Path.Combine(this.ExportDirectory.LocalPath, GetFileName(fileName));

            if (File.Exists(path))
            {
                throw new McpException($"The export folder already has a file named '{Path.GetFileName(path)}': choose another name.");
            }

            Directory.CreateDirectory(this.ExportDirectory.LocalPath);

            using var stream = new FileStream(path, FileMode.CreateNew);
            new Serializer().Serialize(elements.Select(CreateDto), SerializationModeKind.JSON, false, stream, new JsonWriterOptions { Indented = true });

            return path;
        }

        /// <summary>
        /// Gets the name of the file to write: the given simple name, with the <c>.json</c> extension, or a name made of the
        /// current UTC date and time when no name is given.
        /// </summary>
        /// <param name="fileName">The simple name of the file, or <c>null</c>.</param>
        /// <returns>The name of the file, with its extension.</returns>
        /// <exception cref="McpException">Thrown when <paramref name="fileName"/> is not a simple name.</exception>
        private static string GetFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return $"model-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}{FileExtension}";
            }

            if (!SimpleFileName().IsMatch(fileName))
            {
                throw new McpException($"The file name '{fileName}' is not a simple name: use at most 100 letters, digits, '-', '_' and '.', without folder, not starting with '.'.");
            }

            return fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + FileExtension;
        }

        /// <summary>
        /// Creates the DTO of an element with the <c>ToDto</c> method of its metaclass, without its derived properties, as
        /// in the files that the <see cref="InMemoryModelProvider"/> loads.
        /// </summary>
        /// <param name="element">The POCO of the element.</param>
        /// <returns>The DTO of the element.</returns>
        private static DtoElement CreateDto(IElement element)
        {
            return (DtoElement)ToDtoMethods[element.GetType()].Invoke(null, [element, false]);
        }

        /// <summary>
        /// Matches a simple file name: letters, digits, <c>-</c>, <c>_</c> and <c>.</c>, without folder separator, not
        /// starting with <c>.</c>, at most 100 characters.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a simple file name.</returns>
        [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,99}$", RegexOptions.None, 1000)]
        private static partial Regex SimpleFileName();
    }
}
