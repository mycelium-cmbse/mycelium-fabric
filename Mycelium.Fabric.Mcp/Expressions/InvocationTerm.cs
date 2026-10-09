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
    /// functions (<c>abs sqrt floor ceil round min max sum product</c>) and the sequence functions
    /// (<c>size isEmpty notEmpty</c>) are evaluated; a sequence argument, such as <c>(a, b, c)</c>, gives its items.
    /// </summary>
    /// <param name="Function">The name of the function, for example <c>max</c>.</param>
    /// <param name="Arguments">The arguments.</param>
    public sealed record InvocationTerm(string Function, IReadOnlyList<Term> Arguments) : Term
    {
        /// <summary>
        /// The names of the functions that the server evaluates.
        /// </summary>
        public static readonly IReadOnlyList<string> Functions = ["abs", "sqrt", "floor", "ceil", "round", "min", "max", "sum", "product", "size", "isEmpty", "notEmpty"];

        /// <summary>
        /// Evaluates the function on the values of its arguments.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The result, or an error when an argument cannot be evaluated or the function does not apply to it.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

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
                _ when !Functions.Contains(this.Function) => Error.Validation(description: $"The function '{this.Function}' is not supported. Supported functions: {string.Join(", ", Functions)}."),
                _ when items.Count == 0 || items.Any(item => item is not NumberValue) =>
                    Error.Validation(description: $"The function '{this.Function}' needs numbers, not ({string.Join(", ", items)})."),
                _ => this.Calculate(items.Cast<NumberValue>().ToList())
            };
        }

        /// <summary>
        /// Writes the call as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The call, for example <c>max(subj.mass, 100)</c>.</returns>
        public override string ToString()
        {
            return $"{this.Function}({string.Join(", ", this.Arguments)})";
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
                "ceil" => first with { Number = Math.Ceiling(first.Number) },
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
