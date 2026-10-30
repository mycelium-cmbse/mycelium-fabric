// ------------------------------------------------------------------------------------------------
//  <copyright file="HttpRequestExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Http
{
    using System;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;

    using Mycelium.Fabric.ConcurrentServer.Http;

    using SysML2.NET.PIM;
    using SysML2.NET.PSM;

    [TestFixture]
    public class HttpRequestExtensionsTestFixture
    {
        private static readonly Guid Identifier = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid OtherIdentifier = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static HttpRequest Request(string queryString = null)
        {
            var context = new DefaultHttpContext();

            if (!string.IsNullOrWhiteSpace(queryString))
            {
                context.Request.QueryString = new QueryString(queryString);
            }

            return context.Request;
        }

        [Test]
        public void VerifyReadQueryParameters()
        {
            Assert.That(() => HttpRequestExtensions.ReadQueryParameters(null), Throws.TypeOf<ArgumentNullException>());

            // An absent page[size] leaves the page size to the API provider, modelled as zero.
            var outcome = Request().ReadQueryParameters();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(outcome.IsError, Is.False);
                Assert.That(outcome.Value.PageSize, Is.EqualTo(0));
                Assert.That(outcome.Value.PageAfter, Is.Null);
                Assert.That(outcome.Value.PageBefore, Is.Null);
            }

            var cursor = PageCursor.Encode(new Page { Epoch = 7, Identifier = Identifier });

            outcome = Request($"?page[size]=3&page[after]={Uri.EscapeDataString(cursor)}").ReadQueryParameters();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(outcome.Value.PageSize, Is.EqualTo(3));
                Assert.That(outcome.Value.PageAfter!.Value.Identifier, Is.EqualTo(Identifier));
            }

            outcome = Request($"?page[before]={Uri.EscapeDataString(cursor)}").ReadQueryParameters();

            Assert.That(outcome.Value.PageBefore!.Value.Epoch, Is.EqualTo(7));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Request("?page[size]=0").ReadQueryParameters().FirstError.Type, Is.EqualTo(ErrorType.Validation));
                Assert.That(Request("?page[size]=-1").ReadQueryParameters().IsError, Is.True);
                Assert.That(Request("?page[size]=many").ReadQueryParameters().IsError, Is.True);
                Assert.That(Request("?page[after]=zzz").ReadQueryParameters().IsError, Is.True);
                Assert.That(Request("?page[before]=zzz").ReadQueryParameters().IsError, Is.True);
            }
        }

        [Test]
        public void VerifyReadOptionalId()
        {
            Assert.That(() => HttpRequestExtensions.ReadOptionalId(null, "branchId"), Throws.TypeOf<ArgumentNullException>());

            var outcome = Request().ReadOptionalId("branchId");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(outcome.IsError, Is.False);
                Assert.That(outcome.Value, Is.Null);
                Assert.That(Request($"?branchId={Identifier}").ReadOptionalId("branchId").Value, Is.EqualTo(Identifier));
                Assert.That(Request("?branchId=nope").ReadOptionalId("branchId").FirstError.Type, Is.EqualTo(ErrorType.Validation));
            }
        }

        [Test]
        public void VerifyReadRequiredId()
        {
            Assert.That(() => HttpRequestExtensions.ReadRequiredId(null, "baseCommitId"), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Request($"?baseCommitId={Identifier}").ReadRequiredId("baseCommitId").Value, Is.EqualTo(Identifier));
                Assert.That(Request().ReadRequiredId("baseCommitId").FirstError.Code, Is.EqualTo("baseCommitId.missing"));
                Assert.That(Request("?baseCommitId=nope").ReadRequiredId("baseCommitId").FirstError.Code, Is.EqualTo("baseCommitId.invalid"));
            }
        }

        [Test]
        public void VerifyReadIds()
        {
            Assert.That(() => HttpRequestExtensions.ReadIds(null, "commitId"), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Request().ReadIds("commitId").Value, Has.Count.EqualTo(0));

                // A repeatable parameter may be repeated, comma-separated, or both.
                Assert.That(Request($"?commitId={Identifier}&commitId={OtherIdentifier}").ReadIds("commitId").Value, Is.EqualTo([Identifier, OtherIdentifier]));
                Assert.That(Request($"?commitId={Identifier},{OtherIdentifier}").ReadIds("commitId").Value, Is.EqualTo([Identifier, OtherIdentifier]));
                Assert.That(Request($"?commitId={Identifier},nope").ReadIds("commitId").FirstError.Type, Is.EqualTo(ErrorType.Validation));
            }
        }

        [Test]
        public void VerifyReadDirection()
        {
            Assert.That(() => HttpRequestExtensions.ReadDirection(null), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                // §7.2.2 types direction as mandatory; both is the widest of the three allowable values.
                Assert.That(Request().ReadDirection().Value, Is.EqualTo(Direction.both));
                Assert.That(Request("?direction=in").ReadDirection().Value, Is.EqualTo(Direction.@in));
                Assert.That(Request("?direction=out").ReadDirection().Value, Is.EqualTo(Direction.@out));
                Assert.That(Request("?direction=BOTH").ReadDirection().Value, Is.EqualTo(Direction.both));
                Assert.That(Request("?direction=sideways").ReadDirection().FirstError.Type, Is.EqualTo(ErrorType.Validation));
                Assert.That(Request("?direction=1").ReadDirection().FirstError.Type, Is.EqualTo(ErrorType.Validation));
            }
        }

        [Test]
        public void VerifyReadChangeTypes()
        {
            Assert.That(() => HttpRequestExtensions.ReadChangeTypes(null, "changeTypes"), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                // §7.2.3 reads an empty collection as "the argument was not specified", not as "exclude all".
                Assert.That(Request().ReadChangeTypes("changeTypes").Value, Has.Count.EqualTo(0));

                Assert.That(Request("?changeTypes=CREATED,UPDATED&changeTypes=DELETED").ReadChangeTypes("changeTypes").Value,
                    Is.EqualTo([ChangeType.CREATED, ChangeType.UPDATED, ChangeType.DELETED]));

                Assert.That(Request("?changeTypes=RENAMED").ReadChangeTypes("changeTypes").FirstError.Type, Is.EqualTo(ErrorType.Validation));
                Assert.That(Request("?changeTypes=CREATED,7").ReadChangeTypes("changeTypes").FirstError.Type, Is.EqualTo(ErrorType.Validation));
            }
        }

        [Test]
        public void VerifyPrefersMinimalReturn()
        {
            Assert.That(() => HttpRequestExtensions.PrefersMinimalReturn(null), Throws.TypeOf<ArgumentNullException>());

            var request = Request();

            Assert.That(request.PrefersMinimalReturn(), Is.False);

            foreach (var prefer in new[] { "return=minimal", "Return = \"minimal\"", "respond-async, return=minimal; foo=bar" })
            {
                request.Headers[HttpRequestExtensions.PreferHeader] = prefer;

                Assert.That(request.PrefersMinimalReturn(), Is.True, prefer);
            }

            foreach (var prefer in new[] { "return=representation", "respond-async", "minimal" })
            {
                request.Headers[HttpRequestExtensions.PreferHeader] = prefer;

                Assert.That(request.PrefersMinimalReturn(), Is.False, prefer);
            }
        }
    }
}
