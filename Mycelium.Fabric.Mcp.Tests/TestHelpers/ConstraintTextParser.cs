// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintTextParser.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Builds, with a <see cref="ConstraintDtoBuilder"/>, the DTOs of an expression written in the SysML v2 textual notation,
    /// such as <c>subj.mass * 1.2 &lt;= 150 [kg] and not subj.isRedundant</c>, so that the tests read like the models they
    /// check. A name is resolved by a function given by the test; a name that it does not resolve, a unit and the target of
    /// a feature chain are referenced by their name only, as when they are not in the model.
    /// </summary>
    internal sealed class ConstraintTextParser
    {
        /// <summary>
        /// The operators of two characters or more, longest first.
        /// </summary>
        private static readonly string[] LongOperators = ["===", "!==", "**", "==", "!=", "<=", ">=", "??", "::"];

        /// <summary>
        /// The builder of the DTOs.
        /// </summary>
        private readonly ConstraintDtoBuilder builder;

        /// <summary>
        /// Gives the <c>Id</c> of the feature that a name designates, or <c>null</c>.
        /// </summary>
        private readonly Func<string, Guid?> resolve;

        /// <summary>
        /// The tokens of the parsed text.
        /// </summary>
        private List<string> tokens = [];

        /// <summary>
        /// The position of the next token.
        /// </summary>
        private int position;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstraintTextParser"/> class.
        /// </summary>
        /// <param name="builder">The builder of the DTOs.</param>
        /// <param name="resolve">Gives the <c>Id</c> of the feature that a name designates, or <c>null</c>.</param>
        public ConstraintTextParser(ConstraintDtoBuilder builder, Func<string, Guid?> resolve)
        {
            this.builder = builder;
            this.resolve = resolve;
        }

        /// <summary>
        /// Builds the DTOs of an expression.
        /// </summary>
        /// <param name="text">The expression in the textual notation.</param>
        /// <returns>The root DTO of the expression.</returns>
        /// <exception cref="FormatException">Thrown when the text is not an expression.</exception>
        public IElement Parse(string text)
        {
            this.tokens = Tokenize(text);
            this.position = 0;

            var expression = this.ParseConditional();

            return this.position == this.tokens.Count ? expression : throw new FormatException($"Unexpected '{this.tokens[this.position]}' in '{text}'.");
        }

        /// <summary>
        /// Splits a text into tokens: numbers, texts, names, units between brackets and operators.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The tokens.</returns>
        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var index = 0;

            while (index < text.Length)
            {
                var character = text[index];

                if (char.IsWhiteSpace(character))
                {
                    index++;
                    continue;
                }

                var start = index;

                if (char.IsDigit(character))
                {
                    index = SkipNumber(text, index);
                }
                else if (char.IsLetter(character) || character == '_')
                {
                    while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
                    {
                        index++;
                    }
                }
                else if (character == '"')
                {
                    index = text.IndexOf('"', index + 1) + 1;
                }
                else if (character == '[')
                {
                    index = text.IndexOf(']', index) + 1;
                }
                else
                {
                    index += LongOperators.FirstOrDefault(longOperator => string.CompareOrdinal(text, index, longOperator, 0, longOperator.Length) == 0)?.Length ?? 1;
                }

                tokens.Add(text[start..index]);
            }

            return tokens;
        }

        /// <summary>
        /// Skips a number, such as <c>150</c>, <c>1.2</c> or <c>1.5e-3</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="index">The position of the first digit.</param>
        /// <returns>The position after the number.</returns>
        private static int SkipNumber(string text, int index)
        {
            while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.'))
            {
                index++;
            }

            if (index < text.Length && text[index] is 'e' or 'E')
            {
                index++;

                if (index < text.Length && text[index] is '+' or '-')
                {
                    index++;
                }

                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            return index;
        }

        /// <summary>
        /// Gets the next token without consuming it.
        /// </summary>
        /// <returns>The next token, or <c>null</c> at the end.</returns>
        private string Peek()
        {
            return this.position < this.tokens.Count ? this.tokens[this.position] : null;
        }

        /// <summary>
        /// Consumes the next token, which must be the expected one.
        /// </summary>
        /// <param name="expected">The expected token.</param>
        /// <exception cref="FormatException">Thrown when the next token is another one.</exception>
        private void Expect(string expected)
        {
            if (this.Peek() != expected)
            {
                throw new FormatException($"Expected '{expected}' instead of '{this.Peek()}'.");
            }

            this.position++;
        }

        /// <summary>
        /// Parses a binary operation whose operators are given, with operands parsed by a function, from left to right.
        /// </summary>
        /// <param name="operators">The operators of this level of precedence.</param>
        /// <param name="parseOperand">Parses an operand, of the next level of precedence.</param>
        /// <returns>The DTO of the expression.</returns>
        private IElement ParseBinary(string[] operators, Func<IElement> parseOperand)
        {
            var expression = parseOperand();

            while (operators.Contains(this.Peek()))
            {
                var operatorSymbol = this.tokens[this.position++];
                expression = this.builder.Operation(operatorSymbol, expression, parseOperand());
            }

            return expression;
        }

        /// <summary>
        /// Parses <c>if c ? a else b</c>, or an expression of higher precedence.
        /// </summary>
        /// <returns>The DTO of the expression.</returns>
        private IElement ParseConditional()
        {
            if (this.Peek() != "if")
            {
                return this.ParseBinary(["??"], () => this.ParseBinary(["implies"], () => this.ParseBinary(["or", "|"], () => this.ParseBinary(["xor"],
                    () => this.ParseBinary(["and", "&"], () => this.ParseBinary(["==", "!=", "===", "!=="], () => this.ParseBinary(["<", "<=", ">", ">="],
                        () => this.ParseBinary(["+", "-"], () => this.ParseBinary(["*", "/", "%"], () => this.ParseBinary(["**", "^"], this.ParseUnary))))))))));
            }

            this.position++;
            var condition = this.ParseConditional();
            this.Expect("?");
            var then = this.ParseConditional();
            this.Expect("else");

            return this.builder.Operation("if", condition, then, this.ParseConditional());
        }

        /// <summary>
        /// Parses a unary operation, such as <c>-20</c> or <c>not x</c>, or a postfix expression.
        /// </summary>
        /// <returns>The DTO of the expression.</returns>
        private IElement ParseUnary()
        {
            var operatorSymbol = this.Peek();

            if (operatorSymbol is "-" or "+" or "not" or "~")
            {
                this.position++;

                return this.builder.Operation(operatorSymbol, this.ParseUnary());
            }

            return this.ParsePostfix();
        }

        /// <summary>
        /// Parses a primary expression followed by feature chain steps (<c>.mass</c>), units (<c>[kg]</c>) and indexes
        /// (<c>#(1)</c>).
        /// </summary>
        /// <returns>The DTO of the expression.</returns>
        private IElement ParsePostfix()
        {
            var expression = this.ParsePrimary();

            while (true)
            {
                var token = this.Peek();

                if (token == ".")
                {
                    this.position++;
                    expression = this.builder.NamedChain(expression, this.tokens[this.position++]);
                }
                else if (token != null && token.StartsWith('['))
                {
                    this.position++;
                    expression = this.builder.Quantity(expression, this.builder.NamedReference(token[1..^1].Trim()));
                }
                else if (token == "#")
                {
                    this.position++;
                    this.Expect("(");
                    var index = this.ParseConditional();
                    this.Expect(")");
                    expression = this.builder.Operation("#", expression, index);
                }
                else
                {
                    return expression;
                }
            }
        }

        /// <summary>
        /// Parses a literal, a name, a call of a function or an expression between parentheses.
        /// </summary>
        /// <returns>The DTO of the expression.</returns>
        /// <exception cref="FormatException">Thrown when the token cannot start an expression.</exception>
        private IElement ParsePrimary()
        {
            var token = this.Peek() ?? throw new FormatException("The expression ends too early.");
            this.position++;

            if (char.IsDigit(token[0]))
            {
                return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)
                    ? this.builder.Integer(integer)
                    : this.builder.Literal(double.Parse(token, CultureInfo.InvariantCulture));
            }

            if (token[0] == '"')
            {
                return this.builder.Text(token[1..^1]);
            }

            if (token == "(")
            {
                var items = new List<IElement> { this.ParseConditional() };

                while (this.Peek() == ",")
                {
                    this.position++;
                    items.Add(this.ParseConditional());
                }

                this.Expect(")");

                return items.Count == 1 ? items[0] : this.builder.Operation(",", [.. items]);
            }

            return token switch
            {
                "true" or "false" => this.builder.Boolean(token == "true"),
                "null" => this.builder.Null(),
                "*" => this.builder.Infinity(),
                _ when char.IsLetter(token[0]) || token[0] == '_' => this.ParseName(token),
                _ => throw new FormatException($"Unexpected '{token}'.")
            };
        }

        /// <summary>
        /// Parses a name, qualified or not (<c>OrbitKind::polar</c>), as a reference, or as a call when parentheses follow.
        /// </summary>
        /// <param name="firstName">The first name.</param>
        /// <returns>The DTO of the expression.</returns>
        private IElement ParseName(string firstName)
        {
            var name = firstName;

            while (this.Peek() == "::")
            {
                this.position++;
                name = $"{name}::{this.tokens[this.position++]}";
            }

            if (this.Peek() != "(")
            {
                return this.resolve(name) is { } id ? this.builder.Reference(id) : this.builder.NamedReference(name);
            }

            this.position++;
            var arguments = new List<IElement>();

            while (this.Peek() != ")")
            {
                arguments.Add(this.ParseConditional());

                if (this.Peek() == ",")
                {
                    this.position++;
                }
            }

            this.position++;

            return this.builder.Invocation(name, [.. arguments]);
        }
    }
}
