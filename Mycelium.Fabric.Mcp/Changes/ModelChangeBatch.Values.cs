// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Values.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Enumerations;
    using SysML2.NET.Core.Root.Namespaces;

    /// <content>
    /// The values that the changes write in the model: numbers with or without unit (<c>38 [kg]</c>), Booleans, texts and
    /// references to enumeration values, for the value of an attribute or the limit of a constraint.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The operator of a number with a unit, such as <c>38 [kg]</c>.
        /// </summary>
        private const string QuantityOperator = "[";

        /// <summary>
        /// The separator between the name of an enumeration definition and the name of one of its values, as in
        /// <c>OrbitKind::sunSynchronous</c>.
        /// </summary>
        private const string QualifiedNameSeparator = "::";

        /// <summary>
        /// Checks a value and its unit before they are written.
        /// </summary>
        /// <param name="value">The value, or <c>null</c>.</param>
        /// <param name="unit">The unit of the value, or <c>null</c>.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when a unit is given without a numeric value, when the unit is not known, or when a text value is empty.
        /// </exception>
        private static void CheckValue(PayloadValue? value, string unit)
        {
            if (unit != null)
            {
                if (value?.Number == null)
                {
                    throw new InvalidChangeException($"The unit '{unit}' only applies to a numeric value.");
                }

                ParseUnit(unit);
            }

            if (value is { Number: null, Boolean: null } && string.IsNullOrWhiteSpace(value.Value.Text))
            {
                throw new InvalidChangeException("The value is empty: give a number, a Boolean or a text.");
            }
        }

        /// <summary>
        /// Finds a unit from its symbol or name.
        /// </summary>
        /// <param name="unit">The symbol or name of the unit, for example <c>kg</c>.</param>
        /// <returns>The <see cref="Unit"/>.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the unit is not known.</exception>
        private static Unit ParseUnit(string unit)
        {
            return UnitCatalog.TryParse(unit, out var parsedUnit)
                ? parsedUnit
                : throw new InvalidChangeException($"The unit '{unit}' is not known. Use a symbol such as kg, W, km/h, m/s^2 or arcsec, or a name such as kilogram.");
        }

        /// <summary>
        /// Creates an identifier for a unit of the SI library that is not in the model, from its symbol, so that the same
        /// unit always gets the same identifier.
        /// </summary>
        /// <param name="symbol">The symbol of the unit.</param>
        /// <returns>The identifier.</returns>
        private static Guid CreateUnitId(string symbol)
        {
            return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"SI{QualifiedNameSeparator}{symbol}"))[..16]);
        }

        /// <summary>
        /// Binds an attribute to a value, as <c>mass = 38 [kg]</c> does: a <c>FeatureValue</c> that owns the expression of the
        /// value.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <param name="value">The value, already checked by <see cref="CheckValue"/>.</param>
        /// <param name="unit">The unit of a numeric value, or <c>null</c>.</param>
        /// <exception cref="InvalidChangeException">Thrown when a text value names several enumeration values.</exception>
        private void AddValue(IElement attribute, PayloadValue value, string unit)
        {
            this.AddOwnedRelationship(attribute, new FeatureValue { Visibility = VisibilityKind.Public }, this.CreateValueExpression(value, unit));
        }

        /// <summary>
        /// Creates the expression of a value: a <c>LiteralRational</c>, a number with a unit (<c>38 [kg]</c>), a
        /// <c>LiteralBoolean</c>, a reference to the enumeration value that a text names, or else a <c>LiteralString</c>.
        /// </summary>
        /// <param name="value">The value, already checked by <see cref="CheckValue"/>.</param>
        /// <param name="unit">The unit of a numeric value, or <c>null</c>.</param>
        /// <returns>The new expression.</returns>
        /// <exception cref="InvalidChangeException">Thrown when a text names several enumeration values.</exception>
        private IElement CreateValueExpression(PayloadValue value, string unit)
        {
            if (value.Number is { } number)
            {
                var literal = this.Add(new LiteralRational { Value = number });

                return unit == null ? literal : this.CreateOperation(QuantityOperator, literal, this.CreateUnitReference(ParseUnit(unit)));
            }

            if (value.Boolean is { } boolean)
            {
                return this.Add(new LiteralBoolean { Value = boolean });
            }

            var enumerationValue = this.FindEnumerationValue(value.Text);

            return enumerationValue == null ? this.Add(new LiteralString { Value = value.Text }) : this.CreateReferenceOperand(enumerationValue);
        }

        /// <summary>
        /// Creates a reference to a unit, as <c>kg</c> in <c>38 [kg]</c>: a <c>FeatureReferenceExpression</c> whose membership
        /// names the unit. It designates the unit of the model that has this short name, or else the unit of the SI library,
        /// which the model does not contain, by a stable identifier.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <returns>The new expression.</returns>
        private FeatureReferenceExpression CreateUnitReference(Unit unit)
        {
            var unitElement = this.elementsById.Values
                .OfType<IAttributeUsage>()
                .FirstOrDefault(candidate => candidate.DeclaredShortName == unit.Symbol && !this.IsMarkedForDeletion(candidate));

            var reference = this.Add(new FeatureReferenceExpression());
            var unitId = unitElement?.Id ?? CreateUnitId(unit.Symbol);
            var membership = new Membership { MemberElement = unitId, MemberName = unit.Symbol, MemberShortName = unit.Symbol, Visibility = VisibilityKind.Public };
            this.AddOwnedRelationship(reference, membership);
            this.AddEmptyResult(reference);

            return reference;
        }

        /// <summary>
        /// Finds the enumeration value that a text names, such as <c>sunSynchronous</c> or <c>OrbitKind::sunSynchronous</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The enumeration value, or <c>null</c> when the text names none.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the text names several enumeration values.</exception>
        private IElement FindEnumerationValue(string text)
        {
            var names = text.Split(QualifiedNameSeparator);
            var valueName = names[^1];
            var definitionName = names.Length > 1 ? names[^2] : null;

            var enumerationValues = this.elementsById.Values
                .OfType<IEnumerationUsage>()
                .Where(candidate => candidate.DeclaredName == valueName && !this.IsMarkedForDeletion(candidate))
                .Where(candidate => this.GetOwner(candidate) is IEnumerationDefinition definition && (definitionName == null || definition.DeclaredName == definitionName))
                .ToList();

            if (enumerationValues.Count > 1)
            {
                var qualifiedNames = enumerationValues.Select(candidate => $"'{this.GetOwner(candidate).DeclaredName}{QualifiedNameSeparator}{valueName}'");

                throw new InvalidChangeException($"'{text}' names several enumeration values: write {string.Join(" or ", qualifiedNames)}.");
            }

            return enumerationValues.FirstOrDefault();
        }
    }
}
