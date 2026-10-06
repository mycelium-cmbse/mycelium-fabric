// ------------------------------------------------------------------------------------------------
//  <copyright file="GenerationPromptsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Prompts
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Prompts;

    /// <summary>
    /// Suite of tests for the <see cref="GenerationPrompts"/> class.
    /// </summary>
    [TestFixture]
    public class GenerationPromptsTestFixture
    {
        private string specification;

        [SetUp]
        public void SetUp()
        {
            this.specification = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "EOSat1.specification.md"));
        }

        [Test]
        public void VerifyGenerateModelFromSpec()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => GenerationPrompts.GenerateModelFromSpec(null), Throws.TypeOf<McpException>().With.Message.Contains("empty"));
                Assert.That(() => GenerationPrompts.GenerateModelFromSpec(" \r\n "), Throws.TypeOf<McpException>().With.Message.Contains("empty"));
            }

            var message = GenerationPrompts.GenerateModelFromSpec(this.specification);
            var procedure = message[..message.IndexOf("<specification>", StringComparison.Ordinal)];
            var reminder = message[message.IndexOf("</specification>", StringComparison.Ordinal)..];

            var toolNames = typeof(GenerationPrompts).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods())
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
                .Where(name => name != null)
                .ToList();

            var namesInProcedure = procedure.Split([' ', '\r', '\n', ',', '.', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Contains('_'))
                .Distinct()
                .ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(message, Does.StartWith("Build a SysML v2 model from the specification"));
                Assert.That(message, Does.Contain(this.specification.Trim()));
                Assert.That(procedure, Does.Contain("never call apply_changes before the user has approved your plan"));
                Assert.That(reminder, Does.Contain("Start with phase 1"));
                Assert.That(namesInProcedure, Does.Contain("apply_changes").And.Contain("check_requirements"));
                Assert.That(namesInProcedure, Is.SubsetOf(toolNames), "The procedure names a tool that the server does not have.");
            }
        }
    }
}
