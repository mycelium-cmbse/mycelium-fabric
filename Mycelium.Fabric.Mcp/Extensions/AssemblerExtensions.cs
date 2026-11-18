// ------------------------------------------------------------------------------------------------
//  <copyright file="AssemblerExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Dal;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Extension methods that prepare the SysML2.NET <see cref="Assembler"/> that builds the POCOs of a model.
    /// </summary>
    public static class AssemblerExtensions
    {
        /// <param name="assembler">The <see cref="Assembler"/> to prepare.</param>
        extension(Assembler assembler)
        {
            /// <summary>
            /// Adds POCOs that are already built, such as the named elements of the standard library, to the cache of the
            /// assembler, so that the DTOs synchronized next resolve their references to them. An element that has the
            /// <c>Id</c> of one of these DTOs is left out: the DTO would otherwise modify it.
            /// </summary>
            /// <param name="elements">The built elements.</param>
            /// <param name="dtos">The DTOs that will be synchronized next.</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="assembler"/>, <paramref name="elements"/> or <paramref name="dtos"/> is <c>null</c>.
            /// </exception>
            public void AddElements(IEnumerable<IElement> elements, IEnumerable<DtoElement> dtos)
            {
                ArgumentNullException.ThrowIfNull(assembler);
                ArgumentNullException.ThrowIfNull(elements);
                ArgumentNullException.ThrowIfNull(dtos);

                var dtoIds = dtos.Select(dto => dto.Id).ToHashSet();

                foreach (var element in elements.Where(element => !dtoIds.Contains(element.Id)))
                {
                    assembler.Cache.TryAdd(element.Id, new Lazy<IElement>(() => element));
                }
            }
        }
    }
}
