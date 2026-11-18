// ------------------------------------------------------------------------------------------------
//  <copyright file="StandardLibraryTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.IO;

    using Moq;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Serializer.Xmi;

    /// <summary>
    /// Suite of tests for the <see cref="StandardLibrary"/> class.
    /// </summary>
    [TestFixture]
    public class StandardLibraryTestFixture
    {
        private Uri missingDirectory;

        [SetUp]
        public void SetUp()
        {
            this.missingDirectory = new Uri(Path.Combine(Path.GetTempPath(), $"missing-library-{Guid.NewGuid()}"));
        }

        [Test]
        public void VerifyConstructor()
        {
            var libraryLoader = new Mock<IModelLibraryLoader>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new StandardLibrary(null, this.missingDirectory), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new StandardLibrary(libraryLoader.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new StandardLibrary(libraryLoader.Object, new Uri("StandardLibrary", UriKind.Relative)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => new StandardLibrary(libraryLoader.Object, new Uri("https://example.com/StandardLibrary")), Throws.TypeOf<ArgumentException>());
                Assert.That(new StandardLibrary(libraryLoader.Object, this.missingDirectory).LibraryDirectory, Is.EqualTo(this.missingDirectory));
            }

            // The library is only read when it is needed.
            libraryLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void VerifyElements()
        {
            var elements = StandardLibraryHelper.Loaded.Elements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(elements, Has.Count.GreaterThan(100000));
                Assert.That(elements[StandardLibraryHelper.RefinementId].qualifiedName, Is.EqualTo("ModelingMetadata::Refinement"));
                Assert.That(elements[StandardLibraryHelper.MassValueId].qualifiedName, Is.EqualTo("ISQBase::MassValue"));
                Assert.That(new StandardLibrary(new ModelLibraryLoader(StandardLibraryHelper.CreateLoggerFactory().Object), this.missingDirectory).Elements, Is.Empty);
            }
        }

        [Test]
        public void VerifyNamedElements()
        {
            var library = StandardLibraryHelper.Loaded;
            var namedElements = library.NamedElements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(namedElements, Has.Count.LessThan(library.Elements.Count / 10));
                Assert.That(namedElements.Values, Has.All.Matches<SysML2.NET.Core.POCO.Root.Elements.IElement>(element => element.DeclaredName != null || element.DeclaredShortName != null));
                Assert.That(namedElements[StandardLibraryHelper.RefinementId], Is.SameAs(library.Elements[StandardLibraryHelper.RefinementId]));
                Assert.That(namedElements[StandardLibraryHelper.MassValueId], Is.SameAs(library.Elements[StandardLibraryHelper.MassValueId]));
                Assert.That(namedElements, Does.Not.ContainKey(library.Elements[StandardLibraryHelper.MassValueId].OwningRelationship.Id));
                Assert.That(new StandardLibrary(new ModelLibraryLoader(StandardLibraryHelper.CreateLoggerFactory().Object), this.missingDirectory).NamedElements, Is.Empty);
            }
        }

        [Test]
        public void VerifyProblems()
        {
            var missingLibrary = new StandardLibrary(new ModelLibraryLoader(StandardLibraryHelper.CreateLoggerFactory().Object), this.missingDirectory);

            // A library whose units cannot be read keeps its elements: here a package MeasurementReferences that fails.
            var measurementReferences = new Mock<INamespace>();
            measurementReferences.Setup(package => package.DeclaredName).Returns("MeasurementReferences");
            measurementReferences.Setup(package => package.Resolve("MeasurementUnit")).Throws(new InvalidOperationException("The type cannot be computed."));

            var root = new Mock<INamespace>();
            root.Setup(rootNamespace => rootNamespace.OwnedRelationship).Returns([]);
            root.Setup(rootNamespace => rootNamespace.ownedMember).Returns([measurementReferences.Object]);

            var libraryLoader = new Mock<IModelLibraryLoader>();
            libraryLoader.Setup(loader => loader.Load(this.missingDirectory.LocalPath)).Returns([root.Object]);

            var libraryWithoutUnits = new StandardLibrary(libraryLoader.Object, this.missingDirectory);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(StandardLibraryHelper.Loaded.Problems, Is.Empty);
                Assert.That(missingLibrary.Problems, Has.Count.EqualTo(1));
                Assert.That(missingLibrary.Problems[0], Does.Contain(this.missingDirectory.LocalPath));
                Assert.That(libraryWithoutUnits.Problems, Is.EqualTo(new[] { "The units of the standard library could not be read: The type cannot be computed." }));
                Assert.That(libraryWithoutUnits.Elements.Values, Is.EqualTo(new[] { root.Object }));
                Assert.That(libraryWithoutUnits.Resolve("MeasurementReferences"), Is.SameAs(measurementReferences.Object));
                Assert.That(libraryWithoutUnits.Units.FindDeclaration("kg"), Is.Null);
            }

            libraryLoader.Verify(loader => loader.Load(this.missingDirectory.LocalPath), Times.Once);
        }

        [Test]
        public void VerifyUnits()
        {
            var units = StandardLibraryHelper.Loaded.Units;
            var missingLibrary = new StandardLibrary(new ModelLibraryLoader(StandardLibraryHelper.CreateLoggerFactory().Object), this.missingDirectory);

            Unit Parse(string text) => units.TryParse(text, out var unit) ? unit : null;
            Unit ReadDeclaration(string name) => LibraryUnits.ReadDeclaration(units.FindDeclaration(name)).Value;

            using (Assert.EnterMultipleScope())
            {
                // Units that only the library knows, alone or in a compound unit.
                Assert.That(Parse("hp").Factor, Is.EqualTo(745.6999).Within(1e-9));
                Assert.That(Parse("hp").Dimension, Is.EqualTo(new Dimension { Length = 2, Mass = 1, Time = -3 }));
                Assert.That(Parse("lbf").Factor, Is.EqualTo(4.448222).Within(1e-9));
                Assert.That(Parse("Å").Factor, Is.EqualTo(1e-10).Within(1e-22));
                Assert.That(Parse("kip/in²").Dimension, Is.EqualTo(new Dimension { Length = -1, Mass = 1, Time = -2 }));
                Assert.That(Parse("hp/s").Dimension, Is.EqualTo(new Dimension { Length = 2, Mass = 1, Time = -4 }));
                Assert.That(units.FindDeclaration("hp").qualifiedName, Is.EqualTo("USCustomaryUnits::'horsepower (550 ft*lbf/s)'"));
                Assert.That(units.FindDeclaration("kg").qualifiedName, Is.EqualTo("SI::kilogram"));

                // The units that the catalog knows keep its definition, which some declarations of the library contradict.
                Assert.That(ReadDeclaration("tonne").Factor, Is.EqualTo(1000));
                Assert.That(ReadDeclaration("ua").Factor, Is.EqualTo(1.495978707e11));
                Assert.That(ReadDeclaration("ua").Symbol, Is.EqualTo("ua"));
                Assert.That(ReadDeclaration("°C").Offset, Is.EqualTo(273.15));
                Assert.That(ReadDeclaration("B").Factor, Is.EqualTo(8));

                // A symbol that the catalog reads as another quantity (a picotonne) keeps the definition of the library, a
                // point, which is found by its name; the symbol itself stays a unit of the catalog.
                Assert.That(Parse("point (computer) (1/72 in)").Factor, Is.EqualTo(0.0254 / 72).Within(1e-9));
                Assert.That(Parse("pt").Dimension, Is.EqualTo(new Dimension { Mass = 1 }));
                Assert.That(units.FindDeclaration("pt"), Is.Null);
                Assert.That(units.FindDeclaration("point (computer) (1/72 in)"), Is.Not.Null);

                // A scale and a quantity are not units.
                Assert.That(units.FindDeclaration("°C_abs"), Is.Null);
                Assert.That(units.FindDeclaration("MassValue"), Is.Null);

                Assert.That(missingLibrary.Units.TryParse("hp", out _), Is.False);
                Assert.That(missingLibrary.Units.FindDeclaration("kg"), Is.Null);
            }
        }

        [Test]
        public void VerifyResolve()
        {
            var library = StandardLibraryHelper.Loaded;
            var missingLibrary = new StandardLibrary(new ModelLibraryLoader(StandardLibraryHelper.CreateLoggerFactory().Object), this.missingDirectory);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(library.Resolve("ISQ::MassValue")?.Id, Is.EqualTo(StandardLibraryHelper.MassValueId));
                Assert.That(library.Resolve("ISQBase::MassValue")?.Id, Is.EqualTo(StandardLibraryHelper.MassValueId));
                Assert.That(library.Resolve("ModelingMetadata::Refinement")?.Id, Is.EqualTo(StandardLibraryHelper.RefinementId));
                Assert.That(library.Resolve("SI::kg")?.qualifiedName, Is.EqualTo("SI::kilogram"));
                Assert.That(library.Resolve("ISQ")?.qualifiedName, Is.EqualTo("ISQ"));
                Assert.That(library.Resolve("ISQ::UnknownQuantity"), Is.Null);
                Assert.That(library.Resolve("UnknownPackage::MassValue"), Is.Null);
                Assert.That(library.Resolve("ISQ::"), Is.Null);
                Assert.That(library.Resolve(" "), Is.Null);
                Assert.That(library.Resolve(null), Is.Null);
                Assert.That(missingLibrary.Resolve("ISQ::MassValue"), Is.Null);
            }
        }

        [Test]
        public void VerifyStartLoading()
        {
            var libraryLoader = new Mock<IModelLibraryLoader>();
            libraryLoader.Setup(loader => loader.Load(It.IsAny<string>())).Returns([]);

            var library = new StandardLibrary(libraryLoader.Object, this.missingDirectory);
            library.StartLoading();
            library.StartLoading();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(library.Elements, Is.Empty);
                Assert.That(library.Problems, Is.Empty);
            }

            libraryLoader.Verify(loader => loader.Load(this.missingDirectory.LocalPath), Times.Once);
        }
    }
}
