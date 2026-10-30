// ------------------------------------------------------------------------------------------------
//  <copyright file="LibraryUnits.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;

    /// <summary>
    /// The units that the libraries of a model declare, found from their short name (<c>gr</c>), name (<c>grain</c>) or
    /// qualified name (<c>ProjectUnits::grain</c>). A unit of a library is an attribute of a library package that converts
    /// to another unit, as <c>attribute &lt;gr&gt; grain : MassUnit { :&gt;&gt; unitConversion: ConversionByConvention {
    /// :&gt;&gt; referenceUnit = g; :&gt;&gt; conversionFactor = 6.479891E-02; } }</c> or a <c>ConversionByPrefix</c> do,
    /// or whose value is a product, quotient or power of units (<c>= kg*m/s^2</c>). The units of the <c>SI</c> and
    /// <c>USCustomaryUnits</c> packages that <see cref="UnitCatalog"/> knows keep the definition of the catalog. The units
    /// are read from the elements given each time, so that a unit added to a library is known in the next batch.
    /// </summary>
    public sealed class LibraryUnits
    {
        /// <summary>
        /// The units by short name, name and qualified name, with the attribute that declares them. A name that two libraries
        /// give to different units is left out: their qualified names tell them apart. A name that they give to the same unit
        /// designates the declaration that comes first by qualified name.
        /// </summary>
        private readonly Dictionary<string, (Unit Unit, IAttributeUsage Declaration)> unitsByName;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryUnits"/> class.
        /// </summary>
        /// <param name="unitsByName">The units by short name, name and qualified name, with the attribute that declares them.</param>
        private LibraryUnits(Dictionary<string, (Unit Unit, IAttributeUsage Declaration)> unitsByName)
        {
            this.unitsByName = unitsByName;
        }

        /// <summary>
        /// Reads the units that the libraries of a model declare. A declaration that cannot be read as a unit is left out.
        /// </summary>
        /// <param name="elements">The elements of the model.</param>
        /// <returns>The <see cref="LibraryUnits"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is <c>null</c>.</exception>
        public static LibraryUnits Read(IEnumerable<IElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            var unitsByName = elements
                .OfType<IAttributeUsage>()
                .Where(IsDeclaredInLibrary)
                .Select(declaration => (Declaration: declaration, Unit: ReadDeclaration(declaration, [])))
                .Where(entry => !entry.Unit.IsError)
                .OrderBy(entry => entry.Declaration.qualifiedName, StringComparer.Ordinal)
                .SelectMany(entry => GetNames(entry.Declaration).Select(name => (Name: name, Unit: entry.Unit.Value, entry.Declaration)))
                .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                .Where(group => group.Select(entry => (entry.Unit.Dimension, entry.Unit.Factor, entry.Unit.Offset)).Distinct().Count() == 1)
                .ToDictionary(group => group.Key, group => (group.First().Unit, group.First().Declaration), StringComparer.Ordinal);

            return new LibraryUnits(unitsByName);
        }

        /// <summary>
        /// Reads the unit that an attribute of a library declares.
        /// </summary>
        /// <param name="unit">The attribute that declares the unit.</param>
        /// <returns>The <see cref="Unit"/>, named by the short name of the attribute, or an error that tells why it is not a unit.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/> is <c>null</c>.</exception>
        public static ErrorOr<Unit> ReadDeclaration(IFeature unit)
        {
            return ReadDeclaration(unit, []);
        }

        /// <summary>
        /// Finds a unit of the model from its symbol or name, simple or compound: a unit of <see cref="UnitCatalog"/> or a
        /// unit of a library of the model, for example <c>kg</c>, <c>gr</c> or <c>gr/s</c>.
        /// </summary>
        /// <param name="text">The symbol or name.</param>
        /// <param name="unit">The unit, or <c>null</c> when it is not known.</param>
        /// <returns><c>true</c> when the unit is known.</returns>
        public bool TryParse(string text, out Unit unit)
        {
            return UnitCatalog.TryParse(text, this.Find, out unit);
        }

        /// <summary>
        /// Finds the attribute of a library that declares a simple unit.
        /// </summary>
        /// <param name="text">The short name, name or qualified name of the unit.</param>
        /// <returns>The attribute, or <c>null</c> when no library of the model declares the unit.</returns>
        public IAttributeUsage FindDeclaration(string text)
        {
            return text != null && this.unitsByName.TryGetValue(text.Trim(), out var entry) ? entry.Declaration : null;
        }

        /// <summary>
        /// Reads the unit that an attribute of a library declares.
        /// </summary>
        /// <param name="unit">The attribute that declares the unit.</param>
        /// <param name="declarations">The <c>Id</c> of the units being read, to detect a unit defined from itself.</param>
        /// <returns>The <see cref="Unit"/>, named by the short name of the attribute, or an error that tells why it is not a unit.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/> is <c>null</c>.</exception>
        internal static ErrorOr<Unit> ReadDeclaration(IFeature unit, HashSet<Guid> declarations)
        {
            ArgumentNullException.ThrowIfNull(unit);

            var symbol = unit.DeclaredShortName ?? unit.DeclaredName;

            if (!IsDeclaredInLibrary(unit))
            {
                return Error.Validation(description: $"'{unit.qualifiedName ?? symbol}' is not a unit: a unit is declared in a library package, such as SI.");
            }

            if (IsDeclaredInCatalogPackage(unit) && UnitCatalog.TryParse(symbol, out var catalogUnit))
            {
                return catalogUnit;
            }

            if (!declarations.Add(unit.Id))
            {
                return Error.Validation(description: $"The unit '{symbol}' is defined from itself.");
            }

            var definition = TermReader.GetValueExpression(unit) is { } value ? TermReader.ReadUnit(value, declarations) : ReadConversion(unit, symbol, declarations);
            declarations.Remove(unit.Id);

            return definition.Then(definedUnit => definedUnit with { Symbol = symbol ?? definedUnit.Symbol });
        }

        /// <summary>
        /// Reads the conversion of a unit to another unit: a feature of the unit (its <c>unitConversion</c>) whose features
        /// give the reference unit, and a conversion factor or a prefix. The features are recognized by their values, since
        /// the features of the library that they redefine, which give their names, are often not in the model.
        /// </summary>
        /// <param name="unit">The attribute that declares the unit.</param>
        /// <param name="symbol">The short name of the unit, for the messages.</param>
        /// <param name="declarations">The <c>Id</c> of the units being read.</param>
        /// <returns>The <see cref="Unit"/>, or an error when the unit has no conversion to a known unit.</returns>
        private static ErrorOr<Unit> ReadConversion(IFeature unit, string symbol, HashSet<Guid> declarations)
        {
            List<Error> referenceErrors = null;

            foreach (var conversion in unit.ownedFeature ?? [])
            {
                Unit referenceUnit = null;
                var factor = 1.0;

                foreach (var value in (conversion.ownedFeature ?? []).Select(TermReader.GetValueExpression).Where(value => value != null))
                {
                    if (TryGetPrefixFactor(value, out var prefixFactor))
                    {
                        factor *= prefixFactor;
                        continue;
                    }

                    var valueUnit = TermReader.ReadUnit(value, declarations);

                    if (!valueUnit.IsError)
                    {
                        referenceUnit = valueUnit.Value;
                    }
                    else if (TermReader.Read(value).Then(term => term.Evaluate(ConstantEvaluationContext.Instance)) is { IsError: false, Value: NumberValue { Unit: null } number })
                    {
                        factor *= number.Number;
                    }
                    else if (value is IFeatureReferenceExpression)
                    {
                        referenceErrors = valueUnit.Errors;
                    }
                }

                if (referenceUnit != null)
                {
                    return referenceUnit with { Factor = referenceUnit.Factor * factor };
                }
            }

            return referenceErrors
                ?? [Error.Validation(description: $"The unit '{symbol}' has no conversion to a known unit: give it a unitConversion with a referenceUnit and a conversionFactor.")];
        }

        /// <summary>
        /// Reads a reference to an SI prefix, as <c>prefix = kilo</c> gives it in a <c>ConversionByPrefix</c>.
        /// </summary>
        /// <param name="value">The value of a feature of a conversion.</param>
        /// <param name="factor">The factor of the prefix.</param>
        /// <returns><c>true</c> when the value is a reference to a known prefix.</returns>
        private static bool TryGetPrefixFactor(IElement value, out double factor)
        {
            factor = 1;

            if (value is not IFeatureReferenceExpression reference)
            {
                return false;
            }

            var name = reference.referent?.DeclaredName ?? TermReader.GetMemberName(reference);

            return name != null && UnitCatalog.TryGetPrefixFactor(name, out factor);
        }

        /// <summary>
        /// Tells whether an element is declared in a library package, at any depth, or in a package of the library that
        /// <see cref="UnitCatalog"/> knows (<c>SI</c>, <c>USCustomaryUnits</c>).
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns><c>true</c> when the element is declared in a library.</returns>
        private static bool IsDeclaredInLibrary(IElement element)
        {
            for (var owner = element.owningNamespace; owner != null; owner = owner.owningNamespace)
            {
                if (owner is ILibraryPackage)
                {
                    return true;
                }
            }

            return IsDeclaredInCatalogPackage(element);
        }

        /// <summary>
        /// Tells whether an element is declared in a package of the library that <see cref="UnitCatalog"/> knows. The
        /// declared name is checked first, so that the qualified name is computed only for the elements of these packages.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns><c>true</c> when the element is declared in <c>SI</c> or <c>USCustomaryUnits</c>.</returns>
        private static bool IsDeclaredInCatalogPackage(IElement element)
        {
            return element.owningNamespace is { } owner && UnitCatalog.LibraryPackages.Contains(owner.DeclaredName) && UnitCatalog.LibraryPackages.Contains(owner.qualifiedName);
        }

        /// <summary>
        /// Gets the names under which a unit is found: its short name, its name and its qualified name.
        /// </summary>
        /// <param name="declaration">The attribute that declares the unit.</param>
        /// <returns>The names that are given.</returns>
        private static IEnumerable<string> GetNames(IAttributeUsage declaration)
        {
            return new[] { declaration.DeclaredShortName, declaration.DeclaredName, declaration.qualifiedName }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal);
        }

        /// <summary>
        /// Finds a simple unit of a library from its short name, name or qualified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The unit, or <c>null</c>.</returns>
        private Unit Find(string name)
        {
            return this.unitsByName.TryGetValue(name, out var entry) ? entry.Unit : null;
        }
    }
}
