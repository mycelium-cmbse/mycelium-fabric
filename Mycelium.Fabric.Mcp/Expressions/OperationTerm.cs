// ------------------------------------------------------------------------------------------------
//  <copyright file="OperationTerm.cs" company="Starion Group S.A.">
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
    /// An operation of the SysML v2 expression language on its operands: arithmetic (<c>+ - * / % **</c>), comparison
    /// (<c>&lt; &lt;= &gt; &gt;= == !=</c>), logic (<c>and or xor not implies</c>), condition (<c>if c ? a else b</c>),
    /// <c>??</c>, sequence (<c>(a, b)</c>) and index (<c>#</c>). The logical operators follow a logic with three values:
    /// <c>false and x</c> is false and <c>true or x</c> is true even when <c>x</c> cannot be evaluated.
    /// </summary>
    /// <param name="Operator">The operator, for example <c>&lt;=</c>.</param>
    /// <param name="Operands">The operands, one for a unary operator, three for <c>if</c>.</param>
    public sealed record OperationTerm(string Operator, IReadOnlyList<Term> Operands) : Term
    {
        /// <summary>
        /// The arithmetic operators.
        /// </summary>
        private static readonly string[] ArithmeticOperators = ["+", "-", "*", "/", "%", "**", "^"];

        /// <summary>
        /// The comparison operators, with the operator they stand for.
        /// </summary>
        private static readonly Dictionary<string, string> ComparisonOperators = new()
        {
            ["<"] = "<", ["<="] = "<=", [">"] = ">", [">="] = ">=", ["=="] = "==", ["==="] = "==", ["!="] = "!=", ["!=="] = "!="
        };

        /// <inheritdoc/>
        public override int Precedence => this.Operator switch
        {
            "," => 0,
            "if" => 1,
            "??" => 2,
            "implies" => 3,
            "or" or "|" => 4,
            "xor" => 5,
            "and" or "&" => 6,
            "==" or "!=" or "===" or "!==" => 7,
            "<" or "<=" or ">" or ">=" => 9,
            "+" or "-" when this.Operands.Count == 2 => 11,
            "*" or "/" or "%" => 12,
            "**" or "^" => 13,
            _ when this.Operands.Count == 1 => 14,
            "#" => 15,
            _ => 8
        };

        /// <summary>
        /// Evaluates the operation.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The value of the operation, or an error that tells why it cannot be evaluated.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return this.Operator switch
            {
                "and" or "&" when this.Operands.Count > 1 => this.EvaluateLogical(context, true),
                "or" or "|" when this.Operands.Count > 1 => this.EvaluateLogical(context, false),
                "implies" when this.Operands.Count == 2 => this.EvaluateImplication(context),
                "if" when this.Operands.Count == 3 => this.EvaluateCondition(context),
                "??" when this.Operands.Count == 2 => this.EvaluateNullCoalescing(context),
                _ => this.EvaluateOperands(context).Then(this.Apply)
            };
        }

        /// <summary>
        /// Writes the operation as in the SysML v2 textual notation, with parentheses where the precedence needs them.
        /// </summary>
        /// <returns>The operation, for example <c>subj.mass * 1.2 &lt;= 150</c>.</returns>
        public override string ToString()
        {
            var operands = this.Operands.Select(this.Enclose).ToList();

            return (this.Operator, operands) switch
            {
                (",", _) => string.Join(", ", this.Operands),
                ("if", [var condition, var then, var otherwise]) => $"if {condition} ? {then} else {otherwise}",
                ("#", [var sequence, _]) => $"{sequence}#({this.Operands[1]})",
                ("not" or "~", [var operand]) => $"{this.Operator} {operand}",
                (_, [var operand]) => $"{this.Operator}{operand}",
                (_, [var left, var right]) => $"{left} {this.Operator} {right}",
                _ => $"{this.Operator}({string.Join(", ", this.Operands)})"
            };
        }

        /// <summary>
        /// Writes why a Boolean operand has its value.
        /// </summary>
        /// <param name="operand">The operand.</param>
        /// <param name="value">Its value.</param>
        /// <returns>The explanation of the value, for example <c>150.96 &gt; 150</c> or <c>subj.isRedundant is true</c>.</returns>
        private static string Explain(Term operand, BooleanValue value)
        {
            return value.Explanation ?? $"{operand} is {value}";
        }

        /// <summary>
        /// Evaluates an operand that must give a Boolean.
        /// </summary>
        /// <param name="operand">The operand.</param>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>The Boolean, or an error when the operand cannot be evaluated or gives another kind of value.</returns>
        private static ErrorOr<BooleanValue> EvaluateBoolean(Term operand, IEvaluationContext context)
        {
            return operand.Evaluate(context).Then(value => value is BooleanValue boolean
                ? ErrorOrFactory.From(boolean)
                : Error.Validation(description: $"'{operand}' gives {value.KindName} ({value}), not a Boolean."));
        }

        /// <summary>
        /// Flattens values into the items of a sequence: a sequence gives its items.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>The items.</returns>
        private static List<ModelValue> Flatten(IEnumerable<ModelValue> values)
        {
            return values.SelectMany(value => value is SequenceValue sequence ? sequence.Items : [value]).ToList();
        }

        /// <summary>
        /// Evaluates <c>and</c> or <c>or</c>: an operand that decides the result (false for <c>and</c>, true for <c>or</c>)
        /// gives it even when another operand cannot be evaluated.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <param name="isConjunction"><c>true</c> for <c>and</c>, <c>false</c> for <c>or</c>.</param>
        /// <returns>The Boolean, with the explanation of the operands that give it, or the error of an operand.</returns>
        private ErrorOr<ModelValue> EvaluateLogical(IEvaluationContext context, bool isConjunction)
        {
            var outcomes = this.Operands.Select(operand => (Operand: operand, Result: EvaluateBoolean(operand, context))).ToList();

            var decidingOutcomes = outcomes.Where(outcome => !outcome.Result.IsError && outcome.Result.Value.Value != isConjunction).ToList();

            if (decidingOutcomes.Count > 0)
            {
                return new BooleanValue(!isConjunction) { Explanation = string.Join($" {this.Operator} ", decidingOutcomes.Select(outcome => Explain(outcome.Operand, outcome.Result.Value))) };
            }

            var failedOutcome = outcomes.Find(outcome => outcome.Result.IsError);

            if (failedOutcome.Operand != null)
            {
                return failedOutcome.Result.Errors;
            }

            return new BooleanValue(isConjunction) { Explanation = string.Join($" {this.Operator} ", outcomes.Select(outcome => Explain(outcome.Operand, outcome.Result.Value))) };
        }

        /// <summary>
        /// Evaluates <c>a implies b</c>: true when <c>a</c> is false or <c>b</c> is true, even when the other one cannot be
        /// evaluated.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>The Boolean, or the error of an operand.</returns>
        private ErrorOr<ModelValue> EvaluateImplication(IEvaluationContext context)
        {
            var premise = EvaluateBoolean(this.Operands[0], context);
            var conclusion = EvaluateBoolean(this.Operands[1], context);

            if (!premise.IsError && !premise.Value.Value)
            {
                return new BooleanValue(true) { Explanation = Explain(this.Operands[0], premise.Value) };
            }

            if (!conclusion.IsError && conclusion.Value.Value)
            {
                return new BooleanValue(true) { Explanation = Explain(this.Operands[1], conclusion.Value) };
            }

            if (premise.IsError || conclusion.IsError)
            {
                return premise.IsError ? premise.Errors : conclusion.Errors;
            }

            return new BooleanValue(false) { Explanation = $"{Explain(this.Operands[0], premise.Value)} but {Explain(this.Operands[1], conclusion.Value)}" };
        }

        /// <summary>
        /// Evaluates <c>if c ? a else b</c>: the operand that the condition chooses.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>The value of the chosen operand, or an error when the condition cannot be evaluated.</returns>
        private ErrorOr<ModelValue> EvaluateCondition(IEvaluationContext context)
        {
            return EvaluateBoolean(this.Operands[0], context).Then(condition => this.Operands[condition.Value ? 1 : 2].Evaluate(context));
        }

        /// <summary>
        /// Evaluates <c>a ?? b</c>: <c>b</c> when <c>a</c> is <c>null</c>, otherwise <c>a</c>.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>The value, or an error when an operand cannot be evaluated.</returns>
        private ErrorOr<ModelValue> EvaluateNullCoalescing(IEvaluationContext context)
        {
            return this.Operands[0].Evaluate(context).Then(value => value is SequenceValue { Items.Count: 0 } ? this.Operands[1].Evaluate(context) : value);
        }

        /// <summary>
        /// Evaluates every operand.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/>.</param>
        /// <returns>The values of the operands, or the error of the first one that cannot be evaluated.</returns>
        private ErrorOr<List<ModelValue>> EvaluateOperands(IEvaluationContext context)
        {
            var values = new List<ModelValue>();

            foreach (var operand in this.Operands)
            {
                var value = operand.Evaluate(context);

                if (value.IsError)
                {
                    return value.Errors;
                }

                values.Add(value.Value);
            }

            return values;
        }

        /// <summary>
        /// Applies the operator to the values of the operands.
        /// </summary>
        /// <param name="values">The values of the operands.</param>
        /// <returns>The value of the operation, or an error when the operator does not apply to these values.</returns>
        private ErrorOr<ModelValue> Apply(List<ModelValue> values)
        {
            return (this.Operator, values) switch
            {
                (",", _) => new SequenceValue(Flatten(values)),
                ("not" or "~", [BooleanValue operand]) => new BooleanValue(!operand.Value) { Explanation = Explain(this.Operands[0], operand) },
                ("xor", [BooleanValue left, BooleanValue right]) => new BooleanValue(left.Value != right.Value)
                {
                    Explanation = $"{Explain(this.Operands[0], left)} xor {Explain(this.Operands[1], right)}"
                },
                ("-", [NumberValue operand]) => operand.Negate(),
                ("+", [NumberValue operand]) => operand,
                ("+", [TextValue left, TextValue right]) => new TextValue(left.Text + right.Text),
                ("#", [var sequence, NumberValue index]) => Index(sequence, index),
                (_, [NumberValue left, NumberValue right]) when ArithmeticOperators.Contains(this.Operator) => this.Calculate(left, right),
                (_, [var left, var right]) when ComparisonOperators.TryGetValue(this.Operator, out var comparison) => Compare(left, right, comparison),
                _ => Error.Validation(description: $"The operator '{this.Operator}' does not apply to {string.Join(" and ", values.Select(value => $"{value.KindName} ({value})"))}.")
            };
        }

        /// <summary>
        /// Gets an item of a sequence, from its index, which starts at 1 as in SysML v2.
        /// </summary>
        /// <param name="sequence">The sequence, or a single value, which is a sequence of one item.</param>
        /// <param name="index">The index.</param>
        /// <returns>The item, or an error when the index is out of the sequence.</returns>
        private static ErrorOr<ModelValue> Index(ModelValue sequence, NumberValue index)
        {
            var items = Flatten([sequence]);
            var position = (int)index.Number;

            return position >= 1 && position <= items.Count && position == index.Number
                ? items[position - 1]
                : Error.Validation(description: $"The index {index} is out of the sequence {sequence}.");
        }

        /// <summary>
        /// Calculates an arithmetic operation on two numbers.
        /// </summary>
        /// <param name="left">The left number.</param>
        /// <param name="right">The right number.</param>
        /// <returns>The result, or an error when the units do not allow it or the operation divides by zero.</returns>
        private ErrorOr<ModelValue> Calculate(NumberValue left, NumberValue right)
        {
            var result = this.Operator switch
            {
                "+" => left.Add(right),
                "-" => left.Subtract(right),
                "*" => left.Multiply(right),
                "/" => left.Divide(right),
                "%" => left.Remainder(right),
                _ => left.Power(right)
            };

            return result.Then(ModelValue (number) => number);
        }

        /// <summary>
        /// Compares two values: numbers can be ordered and tested for equality, the other values can only be tested for
        /// equality.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <param name="comparisonOperator">The comparison operator: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>==</c> or <c>!=</c>.</param>
        /// <returns>The <see cref="BooleanValue"/> of the comparison, or an error when the values cannot be compared.</returns>
        private static ErrorOr<ModelValue> Compare(ModelValue left, ModelValue right, string comparisonOperator)
        {
            if (left is NumberValue leftNumber && right is NumberValue rightNumber)
            {
                return leftNumber.Compare(rightNumber, comparisonOperator).Then(ModelValue (result) => result);
            }

            if (comparisonOperator is not ("==" or "!="))
            {
                return Error.Validation(description: $"{left.KindName} ({left}) and {right.KindName} ({right}) cannot be ordered: only numbers can.");
            }

            var areEqual = AreEqual(left, right);

            if (areEqual == null)
            {
                return Error.Validation(description: $"{left.KindName} ({left}) cannot be compared with {right.KindName} ({right}).");
            }

            var holds = areEqual.Value == (comparisonOperator == "==");
            var shownOperator = areEqual.Value ? "==" : "!=";

            return new BooleanValue(holds) { Explanation = $"{left} {shownOperator} {right}" };
        }

        /// <summary>
        /// Tells whether two values that are not both numbers are equal.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>Whether the values are equal, or <c>null</c> when they cannot be compared.</returns>
        private static bool? AreEqual(ModelValue left, ModelValue right)
        {
            return (left, right) switch
            {
                (BooleanValue leftBoolean, BooleanValue rightBoolean) => leftBoolean.Value == rightBoolean.Value,
                (TextValue leftText, TextValue rightText) => leftText.Text == rightText.Text,
                (EnumValue leftEnum, EnumValue rightEnum) => leftEnum.Name == rightEnum.Name,
                (EnumValue leftEnum, TextValue rightText) => leftEnum.Name == rightText.Text,
                (TextValue leftText, EnumValue rightEnum) => leftText.Text == rightEnum.Name,
                (PartValue leftPart, PartValue rightPart) => leftPart.Feature.Id == rightPart.Feature.Id,
                (SequenceValue { Items.Count: 0 }, SequenceValue { Items.Count: 0 }) => true,
                (SequenceValue { Items.Count: 0 }, _) or (_, SequenceValue { Items.Count: 0 }) => false,
                _ => null
            };
        }
    }
}
