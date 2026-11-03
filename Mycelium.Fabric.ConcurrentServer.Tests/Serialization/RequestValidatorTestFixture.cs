// ------------------------------------------------------------------------------------------------
//  <copyright file="RequestValidatorTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Serialization
{
    using System;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;

    [TestFixture]
    public class RequestValidatorTestFixture
    {
        [Test]
        public void VerifyValidate()
        {
            Assert.That(() => RequestValidator.Validate<ProjectRequest>(null), Throws.TypeOf<ArgumentNullException>());

            var project = new ProjectRequest { Name = "Rover" };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(RequestValidator.Validate(project).Value, Is.SameAs(project));
                Assert.That(RequestValidator.Validate(new ProjectRequest { Name = " " }).FirstError.Code, Is.EqualTo("body.name.missing"));
                Assert.That(RequestValidator.Validate(new QueryRequest()).FirstError.Code, Is.EqualTo("body.name.missing"));
                Assert.That(RequestValidator.Validate(new QueryRequest { Name = "Parts" }).IsError, Is.False);
            }

            var head = Guid.NewGuid();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(RequestValidator.Validate(new BranchRequest { Name = "feature", Head = head }).IsError, Is.False);
                Assert.That(RequestValidator.Validate(new BranchRequest { Name = "feature" }).FirstError.Code, Is.EqualTo("body.head.missing"));

                // Every missing member is reported, not only the first.
                Assert.That(RequestValidator.Validate(new BranchRequest()).Errors, Has.Count.EqualTo(2));

                Assert.That(RequestValidator.Validate(new TagRequest { Name = "v1", TaggedCommit = head }).IsError, Is.False);
                Assert.That(RequestValidator.Validate(new TagRequest { Name = "v1" }).FirstError.Code, Is.EqualTo("body.taggedCommit.missing"));
            }

            var creation = new DataVersionRequest { Payload = Mock.Of<IDataRequest>() };
            var deletion = new DataVersionRequest { Identity = new DataIdentityRequest { Id = head } };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(RequestValidator.Validate(new CommitRequest { Change = [creation, deletion] }).IsError, Is.False);
                Assert.That(RequestValidator.Validate(new CommitRequest { Change = [] }).FirstError.Code, Is.EqualTo("body.change.missing"));
                Assert.That(RequestValidator.Validate(new CommitRequest { Change = null }).FirstError.Code, Is.EqualTo("body.change.missing"));
                Assert.That(RequestValidator.Validate(new CommitRequest { Change = [new DataVersionRequest()] }).FirstError.Code, Is.EqualTo("body.change.identity.missing"));

                Assert.That(RequestValidator.Validate(new CommitRequest { Change = [new DataVersionRequest { Identity = new DataIdentityRequest() }] }).FirstError.Code,
                    Is.EqualTo("body.change.identity.invalid"));
            }
        }
    }
}
