// ------------------------------------------------------------------------------------------------
//  <copyright file="TermReader.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Kernel.Functions;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;

    /// <summary>
    /// Reads an expression of the model (a tree of SysML2.NET POCOs) as a <see cref="Term"/> that the server can evaluate
    /// and write: literals, references, feature chains, numbers with a unit, operators and calls of library functions.
    /// </summary>
    public static class TermReader
    {
        /// <summary>
        /// The operator of a number with a unit, such as <c>150 [kg]</c>.
        /// </summary>
        private const string QuantityOperator = "[";

        /// <summary>
        /// Reads an expression as a <see cref="Term"/>.
        /// </summary>
        /// <param name="expression">The expression, or <c>null</c> when an operand has no value.</param>
        /// <returns>The <see cref="Term"/>, or an error that tells why the expression cannot be read.</returns>
        public static ErrorOr<Term> Read(IElement expression)
        {
            return expression switch
            {
                null => Error.Validation(description: "An operand of the expression has no value."),
                ILiteralBoolean literal => new LiteralTerm(new BooleanValue(literal.Value)),
                ILiteralInteger literal => new LiteralTerm(new NumberValue(literal.Value)),
                ILiteralRational literal => new LiteralTerm(new NumberValue(literal.Value)),
                ILiteralString literal => new LiteralTerm(new TextValue(literal.Value)),
                ILiteralInfinity => new LiteralTerm(new NumberValue(double.PositiveInfinity)),
                INullExpression => new LiteralTerm(SequenceValue.Empty),
                IFeatureChainExpression chain => ReadChain(chain),
                IOperatorExpression { Operator: QuantityOperator } quantity => ReadQuantity(quantity),
                IOperatorExpression operation => ReadAll(GetArguments(operation)).Then(Term (operands) => new OperationTerm(operation.Operator, operands)),
                IInvocationExpression invocation => ReadInvocation(invocation),
                IFeatureReferenceExpression reference => ReadReference(reference),
                _ => Error.Validation(description: $"The expression '{expression.GetType().Name}' is not supported.")
            };
        }

        /// <summary>
        /// Gets the expression of the value of a feature, as <c>= 150 [kg]</c> gives it.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <returns>The expression, or <c>null</c> when the feature has no value.</returns>
        public static IElement GetValueExpression(IFeature feature)
        {
            ArgumentNullException.ThrowIfNull(feature);

            return (feature.OwnedRelationship ?? [])
                .OfType<IFeatureValue>()
                .Select(featureValue => (IElement)featureValue.value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Reads expressions.
        /// </summary>
        /// <param name="expressions">The expressions.</param>
        /// <returns>The <see cref="Term"/>s, or the error of the first expression that cannot be read.</returns>
        private static ErrorOr<List<Term>> ReadAll(IEnumerable<IElement> expressions)
        {
            var terms = new List<Term>();

            foreach (var expression in expressions)
            {
                var term = Read(expression);

                if (term.IsError)
                {
                    return term.Errors;
                }

                terms.Add(term.Value);
            }

            return terms;
        }

        /// <summary>
        /// Reads a reference to a feature, such as <c>massLimit</c>.
        /// </summary>
        /// <param name="reference">The feature reference expression.</param>
        /// <returns>The <see cref="ReferenceTerm"/>, or an error when the reference does not resolve to an element of the model.</returns>
        private static ErrorOr<Term> ReadReference(IFeatureReferenceExpression reference)
        {
            if (reference.referent is { } feature)
            {
                // An enumeration value is written with its definition, as OrbitKind::polar.
                var name = feature.DeclaredName ?? feature.DeclaredShortName ?? feature.qualifiedName;

                return new ReferenceTerm(ModelEvaluationContext.IsEnumerationValue(feature) ? $"{feature.owningNamespace.DeclaredName}::{name}" : name, feature);
            }

            return Error.Validation(description: $"The reference to '{GetMemberName(reference) ?? "an element"}' does not resolve to an element of the model.");
        }

        /// <summary>
        /// Reads a feature chain, such as <c>subj.camera.mass</c>, as steps from its source.
        /// </summary>
        /// <param name="chain">The feature chain expression.</param>
        /// <returns>The <see cref="NavigationTerm"/>, or an error when the source or the target cannot be read.</returns>
        private static ErrorOr<Term> ReadChain(IFeatureChainExpression chain)
        {
            var arguments = GetArguments(chain);

            if (arguments.Count != 1)
            {
                return Error.Validation(description: "The feature chain has no source.");
            }

            var names = GetTargetNames(chain);

            if (names.Contains(null))
            {
                return Error.Validation(description: "The target of the feature chain has no name.");
            }

            return Read(arguments[0]).Then(source => names.Aggregate(source, (term, name) => new NavigationTerm(term, name)));
        }

        /// <summary>
        /// Reads a number with a unit, such as <c>150 [kg]</c>.
        /// </summary>
        /// <param name="quantity">The operator expression <c>[</c>.</param>
        /// <returns>The <see cref="QuantityTerm"/>, or an error when the number or the unit cannot be read.</returns>
        private static ErrorOr<Term> ReadQuantity(IOperatorExpression quantity)
        {
            var arguments = GetArguments(quantity);

            if (arguments.Count != 2)
            {
                return Error.Validation(description: "The number with a unit has no unit.");
            }

            var unit = ReadUnit(arguments[1]);

            return unit.IsError ? unit.Errors : Read(arguments[0]).Then(Term (value) => new QuantityTerm(value, unit.Value));
        }

        /// <summary>
        /// Reads a unit: a reference to a unit of the SI library, by its short name (<c>kg</c>) or name (<c>kilogram</c>),
        /// or a product, quotient or power of units (<c>m/s^2</c>). The reference is read from its name even when the
        /// library is not in the model.
        /// </summary>
        /// <param name="expression">The expression of the unit.</param>
        /// <returns>The <see cref="Unit"/>, or an error when the unit is not known.</returns>
        private static ErrorOr<Unit> ReadUnit(IElement expression)
        {
            if (expression is IFeatureReferenceExpression reference)
            {
                var name = reference.referent is { } unitFeature ? unitFeature.DeclaredShortName ?? unitFeature.DeclaredName : GetMemberShortName(reference) ?? GetMemberName(reference);

                return UnitCatalog.TryParse(name, out var unit) ? unit : Error.Validation(description: $"The unit '{name}' is not known.");
            }

            if (expression is IOperatorExpression { Operator: "*" or "/" or "**" or "^" } operation && GetArguments(operation) is [var left, var right])
            {
                var leftUnit = ReadUnit(left);

                if (leftUnit.IsError)
                {
                    return leftUnit.Errors;
                }

                if (operation.Operator is "**" or "^")
                {
                    return right is ILiteralInteger exponent ? leftUnit.Value.Power(exponent.Value) : Error.Validation(description: "The exponent of a unit must be an integer.");
                }

                return ReadUnit(right).Then(rightUnit => operation.Operator == "*" ? leftUnit.Value.Multiply(rightUnit) : leftUnit.Value.Divide(rightUnit));
            }

            return Error.Validation(description: "The unit of the number is not a reference to a unit.");
        }

        /// <summary>
        /// Reads a call of a library function, such as <c>max(a, b)</c>.
        /// </summary>
        /// <param name="invocation">The invocation expression.</param>
        /// <returns>The <see cref="InvocationTerm"/>, or an error when the function is not in the model or an argument cannot be read.</returns>
        private static ErrorOr<Term> ReadInvocation(IInvocationExpression invocation)
        {
            var functionName = invocation.function?.DeclaredName ?? invocation.type?.FirstOrDefault()?.DeclaredName;

            if (functionName == null)
            {
                return Error.Validation(description: "The function that the expression calls is not in the model.");
            }

            return ReadAll(GetArguments(invocation)).Then(Term (arguments) => new InvocationTerm(functionName, arguments));
        }

        /// <summary>
        /// Gets the arguments of an expression: the values of its input parameters, in order. They are read from the
        /// <see cref="IFeatureValue"/>s of the parameters, since SysML2.NET leaves the derived <c>argument</c> empty.
        /// </summary>
        /// <param name="expression">The expression, for example the <see cref="IOperatorExpression"/> of <c>a &lt;= b</c>.</param>
        /// <returns>The arguments of the expression, with <c>null</c> for an input parameter that has no value.</returns>
        private static List<IElement> GetArguments(IExpression expression)
        {
            return (expression.input ?? []).Select(GetValueExpression).ToList();
        }

        /// <summary>
        /// Gets the names of the features that a feature chain navigates from its source.
        /// </summary>
        /// <param name="chain">The feature chain expression, for example <c>subj.camera.mass</c>.</param>
        /// <returns>The names, for example <c>camera</c> and <c>mass</c>, with <c>null</c> for a feature that has no name.</returns>
        private static List<string> GetTargetNames(IFeatureChainExpression chain)
        {
            var target = chain.targetFeature;

            if (target == null)
            {
                return [GetMemberName(chain)];
            }

            // A target such as camera.mass is itself a chain of features.
            var features = target.chainingFeature is { Count: > 0 } chainingFeatures ? chainingFeatures : [target];

            return [.. features.Select(feature => feature.DeclaredName ?? feature.DeclaredShortName)];
        }

        /// <summary>
        /// Gets the name under which an expression references an element, which remains when the element is not in the
        /// model, such as a unit of the SI library.
        /// </summary>
        /// <param name="expression">The feature reference or feature chain expression.</param>
        /// <returns>The name, or <c>null</c>.</returns>
        private static string GetMemberName(IElement expression)
        {
            return GetReferenceMembership(expression)?.MemberName;
        }

        /// <summary>
        /// Gets the short name under which an expression references an element, such as <c>kg</c>.
        /// </summary>
        /// <param name="expression">The feature reference expression.</param>
        /// <returns>The short name, or <c>null</c>.</returns>
        private static string GetMemberShortName(IElement expression)
        {
            return GetReferenceMembership(expression)?.MemberShortName;
        }

        /// <summary>
        /// Gets the membership through which an expression references an element: its membership that is not a parameter.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The membership, or <c>null</c>.</returns>
        private static IMembership GetReferenceMembership(IElement expression)
        {
            return (expression.OwnedRelationship ?? [])
                .OfType<IMembership>()
                .FirstOrDefault(membership => membership is not IOwningMembership);
        }
    }
}
