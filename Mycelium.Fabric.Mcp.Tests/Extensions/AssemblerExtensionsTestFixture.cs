// ------------------------------------------------------------------------------------------------
//  <copyright file="AssemblerExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Dal;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoPartDefinition = SysML2.NET.Core.DTO.Systems.Parts.PartDefinition;
    using PocoPartDefinition = SysML2.NET.Core.POCO.Systems.Parts.PartDefinition;

    /// <summary>
    /// Suite of tests for the <see cref="AssemblerExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class AssemblerExtensionsTestFixture
    {
        [Test]
        public void VerifyAddElements()
        {
            var libraryPart = new PocoPartDefinition { Id = Guid.NewGuid(), DeclaredName = "LibraryPart" };
            var replacedPart = new PocoPartDefinition { Id = Guid.NewGuid(), DeclaredName = "ReplacedPart" };
            List<IElement> elements = [libraryPart, replacedPart];
            List<DtoElement> dtos = [new DtoPartDefinition { Id = replacedPart.Id, DeclaredName = "ModelPart" }];

            var assembler = new Assembler();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((Assembler)null).AddElements(elements, dtos), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => assembler.AddElements(null, dtos), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => assembler.AddElements(elements, null), Throws.TypeOf<ArgumentNullException>());
            }

            assembler.AddElements(elements, dtos);
            assembler.Synchronize(dtos);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(assembler.Cache, Has.Count.EqualTo(2));
                Assert.That(assembler.Cache[libraryPart.Id].Value, Is.SameAs(libraryPart));
                Assert.That(assembler.Cache[replacedPart.Id].Value, Is.Not.SameAs(replacedPart));
                Assert.That(assembler.Cache[replacedPart.Id].Value.DeclaredName, Is.EqualTo("ModelPart"));
                Assert.That(replacedPart.DeclaredName, Is.EqualTo("ReplacedPart"));
            }
        }
    }
}
