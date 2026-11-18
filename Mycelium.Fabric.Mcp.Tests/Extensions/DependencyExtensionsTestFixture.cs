// ------------------------------------------------------------------------------------------------
//  <copyright file="DependencyExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Root.Dependencies;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="DependencyExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class DependencyExtensionsTestFixture
    {
        [Test]
        public void VerifyIsRefinement()
        {
            Assert.That(() => ((IDependency)null).IsRefinement(), Throws.TypeOf<ArgumentNullException>());

            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer(), StandardLibraryHelper.Loaded), new DeSerializer(), StandardLibraryHelper.Loaded);
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            modelProvider.ApplyChanges(
            [
                new ModelChange { Identity = "requirements", Payload = new ElementPayload { Type = "Package", Name = "Requirements" } },
                new ModelChange { Identity = "high", Payload = new ElementPayload { Type = "RequirementUsage", Owner = "requirements", Name = "high", Text = "The high-level requirement." } },
                new ModelChange { Identity = "low", Payload = new ElementPayload { Type = "RequirementUsage", Owner = "requirements", Name = "low", Text = "The low-level requirement." } },
                new ModelChange { Payload = new ElementPayload { Type = "Dependency", Name = "refinement", Clients = ["low"], Suppliers = ["high"], IsRefinement = true } },
                new ModelChange { Payload = new ElementPayload { Type = "Dependency", Name = "dependency", Clients = ["low"], Suppliers = ["high"] } }
            ]);

            // Without the standard library, the metadata of the refinement has no type: it is not taken for a refinement.
            var path = Path.Combine(Path.GetTempPath(), $"refinement-{Guid.NewGuid()}.json");
            var withoutLibrary = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer(), StandardLibraryHelper.Empty), new DeSerializer(), StandardLibraryHelper.Empty);

            try
            {
                using (var stream = File.Create(path))
                {
                    new Serializer().Serialize(modelProvider.ElementDtos, SerializationModeKind.JSON, false, stream, new JsonWriterOptions());
                }

                withoutLibrary.LoadModel(new Uri(path));
            }
            finally
            {
                File.Delete(path);
            }

            IDependency GetDependency(IModelProvider provider, string name) => provider.Elements.OfType<IDependency>().Single(dependency => dependency.DeclaredName == name);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(GetDependency(modelProvider, "refinement").IsRefinement(), Is.True);
                Assert.That(GetDependency(modelProvider, "dependency").IsRefinement(), Is.False);
                Assert.That(GetDependency(withoutLibrary, "refinement").IsRefinement(), Is.False);
            }
        }
    }
}
