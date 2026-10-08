// ------------------------------------------------------------------------------------------------
//  <copyright file="IModelProvider.cs" company="Starion Group S.A.">
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

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Changes;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.PSM.DTO;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Provides access to the SysML v2 model that the MCP tools work on.
    /// </summary>
    /// <remarks>
    /// The read members mirror the <c>ElementNavigationService</c> of the Systems Modeling API and Services 1.0
    /// (§7.2.2): <see cref="Elements"/> for <c>getElements</c>, <see cref="RootElements"/> for
    /// <c>getRootElements</c> and <see cref="GetElementById"/> for <c>getElementById</c>. The project and commit
    /// scoping of the specification is left out for now, since a provider serves a single model at a time.
    /// <see cref="CreateCommit"/> plays the part of <c>POST /projects/{projectId}/commits</c>, the REST binding of
    /// <c>createCommit</c> of the <c>ProjectDataVersioningService</c> (§7.2.3, §8.1.3): it takes a <c>CommitRequest</c>,
    /// applies it as a whole or not at all, and returns the created <c>Commit</c>. <see cref="ApplyChanges"/> turns a batch
    /// of changes into such a request.
    /// </remarks>
    public interface IModelProvider
    {
        /// <summary>
        /// Gets all the elements of the loaded model.
        /// </summary>
        IReadOnlyList<IElement> Elements { get; }

        /// <summary>
        /// Gets the root elements of the loaded model, that is the elements that have no owner.
        /// </summary>
        IReadOnlyList<IElement> RootElements { get; }

        /// <summary>
        /// Gets the DTOs of all the elements of the loaded model, as a JSON file of the Systems Modeling API stores them and as
        /// the payloads of a <see cref="CommitRequest"/> carry them.
        /// </summary>
        IReadOnlyList<DtoElement> ElementDtos { get; }

        /// <summary>
        /// Loads the model stored at the given location, replacing the model loaded before.
        /// </summary>
        /// <param name="modelPath">The <see cref="Uri"/> of the JSON file that contains the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelPath"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="modelPath"/> is not an absolute file <see cref="Uri"/>.
        /// </exception>
        void LoadModel(Uri modelPath);

        /// <summary>
        /// Gets the element of the loaded model that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>
        /// The <see cref="IElement"/> that has the given identifier, or <c>null</c> when the loaded model contains no
        /// such element.
        /// </returns>
        IElement GetElementById(Guid elementId);

        /// <summary>
        /// Gets the element of the loaded model that has the given identifier, or fails with a message the AI assistant can
        /// act on.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>The <see cref="IElement"/> that has the given identifier.</returns>
        /// <exception cref="McpException">
        /// Thrown when the loaded model contains no such element. Unlike other exceptions, the message of an
        /// <see cref="McpException"/> is sent back to the AI assistant.
        /// </exception>
        IElement GetRequiredElementById(Guid elementId);

        /// <summary>
        /// Applies a batch of changes to the loaded model, all or nothing: the batch is turned into a <c>CommitRequest</c> by
        /// an <see cref="IModelChangeApplier"/>, then committed with <see cref="CreateCommit"/>. When one change is invalid,
        /// the model is left unchanged and the problems are returned.
        /// </summary>
        /// <param name="changes">The changes to apply, in order.</param>
        /// <returns>The <see cref="ApplyChangesResult"/> that tells whether the batch has been applied.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        ApplyChangesResult ApplyChanges(IReadOnlyList<ModelChange> changes);

        /// <summary>
        /// Creates a commit from the given request: each <see cref="DataVersionRequest"/> of its change with a payload adds or
        /// replaces the element that has its identity, and each one without payload removes it.
        /// </summary>
        /// <param name="commitRequest">The <see cref="CommitRequest"/> that describes the commit.</param>
        /// <returns>The created <see cref="Commit"/>, whose previous commit is the one created before it.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="commitRequest"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a <see cref="DataVersionRequest"/> has no identity, or a payload that is not the element of its
        /// identity. The model is then left unchanged.
        /// </exception>
        Commit CreateCommit(CommitRequest commitRequest);
    }
}