// ------------------------------------------------------------------------------------------------
//  <copyright file="InvocationTerm.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// A call of a function of the SysML v2 library on its arguments, for example <c>max(subj.mass, 100)</c>. The numerical
    /// functions (<c>abs sqrt floor round min max sum product</c>), the sequence functions (<c>size isEmpty notEmpty</c>)
    /// and the control functions <c>select</c> and <c>collect</c>, whose second argument is a <see cref="BodyTerm"/>, are
    /// evaluated; a sequence argument, such as <c>(a, b, c)</c>, gives its items.
    /// </summary>
    /// <param name="Function">The name of the function, for example <c>max</c>.</param>
    /// <param name="Arguments">The arguments.</param>
    public sealed record InvocationTerm(string Function, IReadOnlyList<Term> Arguments) : Term
    {
        /// <summary>
        /// The package of the SysML v2 library that declares the functions on real numbers.
        /// </summary>
        private const string RealFunctions = "RealFunctions";

        /// <summary>
        /// The packages of the SysML v2 library that declare the functions on rational and real numbers.
        /// </summary>
        private static readonly string[] RealPackages = ["RationalFunctions", RealFunctions];

        /// <summary>
        /// The packages of the SysML v2 library that declare the functions on all numbers.
        /// </summary>
        private static readonly string[] NumericalPackages = ["NumericalFunctions", "IntegerFunctions", .. RealPackages];

        /// <summary>
        /// The packages of the SysML v2 library that declare the comparison functions <c>min</c> and <c>max</c>.
        /// </summary>
        private static readonly string[] ComparisonPackages = ["DataFunctions", "ScalarFunctions", "NaturalFunctions", .. NumericalPackages];

        /// <summary>
        /// The package of the SysML v2 library that declares the sequence functions.
        /// </summary>
        private static readonly string[] SequencePackages = ["SequenceFunctions"];

        /// <summary>
        /// The package of the SysML v2 library that declares the control functions.
        /// </summary>
        private static readonly string[] ControlPackages = ["ControlFunctions"];

        /// <summary>
        /// The functions that the server evaluates, by name, with the packages of the SysML v2 library that declare them: a
        /// function of the model is one of them only when its qualified name is one of these packages and its name, so that a
        /// function of the user that has the same name, such as <c>MyFunctions::max</c>, is not taken for it.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Functions = new Dictionary<string, string[]>
        {
            ["abs"] = NumericalPackages,
            ["sqrt"] = [RealFunctions],
            ["floor"] = RealPackages,
            ["round"] = RealPackages,
            ["min"] = ComparisonPackages,
            ["max"] = ComparisonPackages,
            ["sum"] = NumericalPackages,
            ["product"] = NumericalPackages,
            ["size"] = SequencePackages,
            ["isEmpty"] = SequencePackages,
            ["notEmpty"] = SequencePackages,
            ["select"] = ControlPackages,
            ["collect"] = ControlPackages
        };

        /// <summary>
        /// Gets the name of the function that the server evaluates for a function of the model, from its qualified name.
        /// </summary>
        /// <param name="qualifiedName">The qualified name of the function, for example <c>RealFunctions::max</c>.</param>
        /// <returns>The name of the function, for example <c>max</c>, or <c>null</c> when it is not a function of the library that the server evaluates.</returns>
        public static string GetFunctionName(string qualifiedName)
        {
            var separator = qualifiedName?.LastIndexOf("::", StringComparison.Ordinal) ?? -1;

            if (separator < 0)
            {
                return null;
            }

            var name = qualifiedName[(separator + 2)..];

            return Functions.TryGetValue(name, out var packages) && packages.Contains(qualifiedName[..separator]) ? name : null;
        }

        /// <summary>
        /// Evaluates the function on the values of its arguments.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The result, or an error when an argument cannot be evaluated or the function does not apply to it.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (this.Function is "select" or "collect")
            {
                return this.EvaluateIteration(context);
            }

            var items = new List<ModelValue>();

            foreach (var argument in this.Arguments)
            {
                var value = argument.Evaluate(context);

                if (value.IsError)
                {
                    return value.Errors;
                }

                items.AddRange(value.Value is SequenceValue sequence ? sequence.Items : [value.Value]);
            }

            return this.Function switch
            {
                "size" => new NumberValue(items.Count),
                "isEmpty" => new BooleanValue(items.Count == 0),
                "notEmpty" => new BooleanValue(items.Count > 0),
                _ when !Functions.ContainsKey(this.Function) => Error.Validation(description: $"The function '{this.Function}' is not supported. Supported functions: {string.Join(", ", Functions.Keys)}."),
                _ when items.Count == 0 || items.Any(item => item is not NumberValue) =>
                    Error.Validation(description: $"The function '{this.Function}' needs numbers, not ({string.Join(", ", items)})."),
                _ => this.Calculate(items.Cast<NumberValue>().ToList())
            };
        }

        /// <summary>
        /// Writes the call as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The call, for example <c>max(subj.mass, 100)</c> or <c>(a, b)-&gt;select {in p; p.mass &gt; 10}</c>.</returns>
        public override string ToString()
        {
            return this.Arguments is [var collection, BodyTerm body]
                ? $"{this.Enclose(collection)}->{this.Function} {body}"
                : $"{this.Function}({string.Join(", ", this.Arguments)})";
        }

        /// <summary>
        /// Evaluates <c>select</c>, which keeps the items of a sequence for which the body is true, or <c>collect</c>, which
        /// gives the values of the body for the items. A result of one item is that item, as in SysML v2.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>The result, or an error when the sequence or the body cannot be evaluated.</returns>
        private ErrorOr<ModelValue> EvaluateIteration(IEvaluationContext context)
        {
            if (this.Arguments is not [var collection, BodyTerm body])
            {
                return Error.Validation(description: $"The function '{this.Function}' needs a sequence and a body, as in (a, b)->{this.Function} {{in p; p.mass > 10}}.");
            }

            var sequence = collection.Evaluate(context);

            if (sequence.IsError)
            {
                return sequence.Errors;
            }

            var results = new List<ModelValue>();

            foreach (var item in sequence.Value is SequenceValue items ? items.Items : [sequence.Value])
            {
                var value = body.Apply(context, item);

                if (value.IsError)
                {
                    return value.Errors;
                }

                if (this.Function == "collect")
                {
                    results.AddRange(value.Value is SequenceValue values ? values.Items : [value.Value]);
                }
                else if (value.Value is BooleanValue isSelected)
                {
                    results.AddRange(isSelected.Value ? [item] : []);
                }
                else
                {
                    return Error.Validation(description: $"'{body}' gives {value.Value.Describe()} for {item}, not a Boolean.");
                }
            }

            return results.Count == 1 ? results[0] : new SequenceValue(results);
        }

        /// <summary>
        /// Calculates a numerical function.
        /// </summary>
        /// <param name="numbers">The numbers, at least one.</param>
        /// <returns>The result, or an error when the units do not allow it.</returns>
        private ErrorOr<ModelValue> Calculate(List<NumberValue> numbers)
        {
            var first = numbers[0];

            var result = this.Function switch
            {
                "abs" => first with { Number = Math.Abs(first.Number) },
                "floor" => first with { Number = Math.Floor(first.Number) },
                "round" => first with { Number = Math.Round(first.Number, MidpointRounding.AwayFromZero) },
                "sqrt" => first.Unit == null && first.Number >= 0 ? new NumberValue(Math.Sqrt(first.Number)) : (ErrorOr<NumberValue>)Error.Validation(description: $"sqrt does not apply to {first}."),
                "sum" => NumberValue.Sum(numbers),
                "product" => numbers.Skip(1).Aggregate(first, (product, number) => product.Multiply(number)),
                _ => this.SelectExtremum(numbers)
            };

            return result.Then(ModelValue (number) => number);
        }

        /// <summary>
        /// Selects the smallest (<c>min</c>) or the largest (<c>max</c>) number, comparing them in the unit of the first one.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The selected number, or an error when two units have different dimensions.</returns>
        private ErrorOr<NumberValue> SelectExtremum(List<NumberValue> numbers)
        {
            var selected = numbers[0];

            foreach (var number in numbers.Skip(1))
            {
                var converted = number.ConvertTo(selected.Unit);

                if (converted.IsError)
                {
                    return converted.Errors;
                }

                var isBetter = this.Function == "max" ? converted.Value.Number > selected.Number : converted.Value.Number < selected.Number;
                selected = isBetter ? converted.Value : selected;
            }

            return selected;
        }
    }
}
