// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionPlanTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Plans
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Plans;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="ConstructionPlan"/> class.
    /// </summary>
    [TestFixture]
    public class ConstructionPlanTestFixture
    {
        private static readonly string[] DoneSectionNames = ["Architecture"];

        private static readonly int[] MissingElementCounts = [3, 25, 1];

        private static readonly string[] PlanProblems =
        [
            "Plan, section 1: the section has no name.",
            "Plan, section 2 ('Behaviour'): the section lists no element. List the elements it creates, each by its name and its type.",
            "Plan, section 3 ('behaviour'), element 1: the element has no name.",
            "Plan, section 3 ('behaviour'), element 2: the element has no name.",
            "Plan, section 3 ('behaviour'), element 3 ('launch'): the element has no type. Give its metaclass, as in the payload of the change that creates it (PartUsage, RequirementUsage...).",
            "Plan, section 3 ('behaviour'), element 4 ('launch'): 'Launch' is not a metaclass that apply_changes creates, for example Package, PartDefinition, PartUsage, AttributeUsage or RequirementUsage.",
            "Plan, section 4: the section has no name.",
            "Plan, section 4: the section lists no element. List the elements it creates, each by its name and its type.",
            "Plan: several sections are named 'Behaviour'. Give each section its own name."
        ];

        private static readonly string[] RemainingSectionNames = ["Launch", "Requirements", "Context"];

        private InMemoryModelProvider modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            this.modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var result = this.modelProvider.ApplyChanges(
            [
                new ModelChange { Identity = "mission", Payload = new ElementPayload { Type = "Package", Name = "Mission" } },
                new ModelChange { Identity = "Spacecraft", Payload = new ElementPayload { Type = "PartDefinition", Name = "Spacecraft", Owner = "mission" } },
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "PartUsage", Name = "spacecraft", Owner = "mission", Definition = "Spacecraft" } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "PartUsage", Name = "camera", Owner = "spacecraft" } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Type = "AttributeUsage", Name = "mass", Owner = "camera", Value = 38, Unit = "kg" } },
                new ModelChange { Identity = "launcher", Payload = new ElementPayload { Type = "PartUsage", Name = "SA-506", Owner = "mission" } }
            ]);

            Assert.That(result.Problems, Is.Empty);
        }

        [Test]
        public void VerifyCreate()
        {
            var noSection = ConstructionPlan.Create(null);
            var emptyPlan = ConstructionPlan.Create([]);

            var invalidPlan = ConstructionPlan.Create(
            [
                new PlanSection { Name = " ", Elements = [new PlannedElement { Name = "camera", Type = "PartUsage" }] },
                new PlanSection { Name = "Behaviour" },
                new PlanSection
                {
                    Name = "behaviour ",
                    Elements = [null, new PlannedElement { Name = "", Type = "PartUsage" }, new PlannedElement { Name = "launch" }, new PlannedElement { Name = "launch", Type = "Launch" }]
                },
                null
            ]);

            var validPlan = ConstructionPlan.Create([new PlanSection { Name = " Architecture ", Elements = [new PlannedElement { Name = " camera ", Type = "partusage" }] }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(noSection.IsError, Is.True);
                Assert.That(noSection.FirstError.Description, Does.Contain("no section"));
                Assert.That(emptyPlan.IsError, Is.True);

                Assert.That(invalidPlan.Errors.Select(error => error.Description), Is.EqualTo(PlanProblems));

                Assert.That(validPlan.IsError, Is.False);
                Assert.That(validPlan.Value.Sections.Single().Name, Is.EqualTo("Architecture"));
                Assert.That(validPlan.Value.Sections.Single().Elements, Is.EqualTo(new[] { new PlannedElement { Name = "camera", Type = "PartUsage" } }));
            }
        }

        [Test]
        public void VerifyGetProgress()
        {
            var architecture = new PlanSection
            {
                Name = "Architecture",
                Elements =
                [
                    new PlannedElement { Name = "Spacecraft", Type = "PartDefinition" },
                    new PlannedElement { Name = "spacecraft", Type = "PartUsage" },
                    new PlannedElement { Name = "camera", Type = "PartUsage" },
                    new PlannedElement { Name = "camera::mass", Type = "AttributeUsage" },
                    new PlannedElement { Name = "Mission::spacecraft::camera::mass", Type = "AttributeUsage" },
                    new PlannedElement { Name = "SA-506", Type = "PartUsage" },
                    new PlannedElement { Name = "Mission::'SA-506'", Type = "PartUsage" }
                ]
            };

            var launch = new PlanSection
            {
                Name = "Launch",
                Elements =
                [
                    new PlannedElement { Name = "launchPad", Type = "PartUsage" },
                    new PlannedElement { Name = "camera", Type = "PartDefinition" },
                    new PlannedElement { Name = "spacecraft::mass", Type = "AttributeUsage" },
                    new PlannedElement { Name = "SA-506", Type = "PartUsage" }
                ]
            };

            var requirements = new PlanSection
            {
                Name = "Requirements",
                Elements = [.. Enumerable.Range(1, 25).Select(number => new PlannedElement { Name = $"requirement{number}", Type = "RequirementUsage" })]
            };

            var context = new PlanSection { Name = "Context", Elements = [new PlannedElement { Name = "groundStation", Type = "PartUsage" }] };

            var progress = ConstructionPlan.Create([architecture, launch, requirements, context]).Value.GetProgress(this.modelProvider.Elements);
            var doneProgress = ConstructionPlan.Create([architecture]).Value.GetProgress(this.modelProvider.Elements);
            var emptyModelProgress = ConstructionPlan.Create([architecture]).Value.GetProgress([]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ConstructionPlan.Create([architecture]).Value.GetProgress(null), Throws.TypeOf<ArgumentNullException>());

                Assert.That(progress.DoneSections, Is.EqualTo(DoneSectionNames));
                Assert.That(progress.RemainingSections.Select(section => section.Name), Is.EqualTo(RemainingSectionNames));
                Assert.That(progress.RemainingSections.Select(section => section.MissingElementCount), Is.EqualTo(MissingElementCounts));
                Assert.That(progress.RemainingSections[0].MissingElements, Is.EqualTo(launch.Elements.Take(3)));
                Assert.That(progress.RemainingSections[1].MissingElements, Is.EqualTo(requirements.Elements.Take(17)));
                Assert.That(progress.RemainingSections[2].MissingElements, Is.Empty);
                Assert.That(progress.Reminder, Does.StartWith("Sections left: 3 of 4; missing elements: 29."));

                Assert.That(doneProgress.DoneSections, Is.EqualTo(DoneSectionNames));
                Assert.That(doneProgress.RemainingSections, Is.Empty);
                Assert.That(doneProgress.Reminder, Is.EqualTo("Every element of the plan is in the model."));

                Assert.That(emptyModelProgress.DoneSections, Is.Empty);
                Assert.That(emptyModelProgress.RemainingSections.Single().MissingElementCount, Is.EqualTo(7));
            }
        }
    }
}
