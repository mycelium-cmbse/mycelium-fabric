// ------------------------------------------------------------------------------------------------
//  <copyright file="HttpResponseExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Http
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Carter;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Net.Http.Headers;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.PSM;
    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class HttpResponseExtensionsTestFixture
    {
        private static readonly Guid Identifier = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static HttpResponse Response()
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Scheme = "https",
                    Host = new HostString("fabric.test", 9000),
                    Path = "/projects"
                }
            };

            return context.Response;
        }

        [Test]
        public void VerifyAddPaginationLinks()
        {
            var response = Response();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => HttpResponseExtensions.AddPaginationLinks(null, new PagedResult<string>(), 3), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => Response().AddPaginationLinks<string>(null, 3), Throws.TypeOf<ArgumentNullException>());
            }

            // A single-page result addresses nothing either side of it, so no header is written.
            response.AddPaginationLinks(new PagedResult<string>(), 3);

            Assert.That(response.Headers[HeaderNames.Link].ToString(), Is.Empty);

            response.AddPaginationLinks(new PagedResult<string> { Next = new Page { Epoch = 7, Identifier = Identifier } }, 3);

            var link = response.Headers[HeaderNames.Link].ToString();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(link, Does.StartWith("<https://fabric.test:9000/projects?page[after]="));
                Assert.That(link, Does.Contain("page[size]=3"));
                Assert.That(link, Does.EndWith("; rel=\"next\""));
            }

            response = Response();

            response.AddPaginationLinks(
                new PagedResult<string>
                {
                    Next = new Page { Epoch = 7, Identifier = Identifier },
                    Previous = new Page { Epoch = 6, Identifier = Identifier }
                },
                0);

            link = response.Headers[HeaderNames.Link].ToString();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(link, Does.Contain("rel=\"next\""));
                Assert.That(link, Does.Contain("rel=\"prev\""));
                Assert.That(link, Does.Contain("page[before]="));

                // An unrequested page size is not carried over to the links.
                Assert.That(link, Does.Not.Contain("page[size]"));
            }

            // The other query parameters still apply to the next page, while the cursor and size of the request are replaced.
            response = Response();
            response.HttpContext.Request.QueryString = new QueryString("?baseCommitId=abc&changeTypes=CREATED,UPDATED&page[after]=old&page[size]=9");

            response.AddPaginationLinks(new PagedResult<string> { Next = new Page { Epoch = 7, Identifier = Identifier } }, 3);

            link = response.Headers[HeaderNames.Link].ToString();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(link, Does.Contain("&baseCommitId=abc"));
                Assert.That(link, Does.Contain("&changeTypes=CREATED%2CUPDATED"));
                Assert.That(link, Does.Not.Contain("old"));
                Assert.That(link, Does.Not.Contain("page[size]=9"));
            }
        }

        [Test]
        public async Task VerifyNegotiateErrorAsync()
        {
            Assert.That(() => HttpResponseExtensions.NegotiateErrorAsync(null, StatusCodes.Status404NotFound, "missing"), Throws.TypeOf<ArgumentNullException>());

            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton<IResponseNegotiator>(new JsonResponseNegotiator(new Serializer())).BuildServiceProvider(),
                Response =
                {
                    Body = new MemoryStream()
                }
            };

            context.Request.Headers.Accept = "application/json";

            await context.Response.NegotiateErrorAsync(StatusCodes.Status404NotFound, "getProjectById: No project with the given id exists.");

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var error = (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
                Assert.That(context.Response.ContentType, Is.EqualTo(JsonResponseNegotiator.MediaType));
                Assert.That(error.GetProperty("@type").GetString(), Is.EqualTo("Error"));
                Assert.That(error.GetProperty("description").GetString(), Is.EqualTo("getProjectById: No project with the given id exists."));
            }
        }
    }
}
