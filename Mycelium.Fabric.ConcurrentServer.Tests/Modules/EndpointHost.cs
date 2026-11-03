// ------------------------------------------------------------------------------------------------
//  <copyright file="EndpointHost.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    using Carter;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Services;

    [SetUpFixture]
    [NonParallelizable]
    public class EndpointHost
    {
        private static readonly Dictionary<Type, Mock> Loggers = typeof(ApiBase).Assembly.GetTypes()
            .Where(type => typeof(ICarterModule).IsAssignableFrom(type) && !type.IsAbstract)
            .ToDictionary(type => type, type => (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(typeof(ILogger<>).MakeGenericType(type))));

        private static WebApplicationFactory<ApiBase> application;

        private static WebApplicationFactory<ApiBase> bareApplication;

        internal static Mock<IProjectDataVersioningService> VersioningService { get; } = new();

        internal static Mock<IElementNavigationService> NavigationService { get; } = new();

        internal static Mock<IProjectService> ProjectService { get; } = new();

        internal static Mock<IQueryService> QueryService { get; } = new();

        internal static HttpClient Client { get; private set; }

        internal static HttpClient WithoutServices { get; private set; }

        internal static Mock<ILogger<TModule>> Logger<TModule>()
        {
            return (Mock<ILogger<TModule>>)Loggers[typeof(TModule)];
        }

        internal static void ResetMocks()
        {
            Mock[] services = [VersioningService, NavigationService, ProjectService, QueryService];

            foreach (var mock in services.Concat(Loggers.Values))
            {
                mock.Reset();
            }

            foreach (var logger in Loggers.Values)
            {
                logger.As<ILogger>().Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            }
        }

        [OneTimeSetUp]
        public void Start()
        {
            application = new WebApplicationFactory<ApiBase>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                RegisterLoggers(services);
                services.AddSingleton(VersioningService.Object);
                services.AddSingleton(NavigationService.Object);
                services.AddSingleton(ProjectService.Object);
                services.AddSingleton(QueryService.Object);
            }));

            bareApplication = new WebApplicationFactory<ApiBase>().WithWebHostBuilder(builder => builder.ConfigureTestServices(RegisterLoggers));

            Client = application.CreateClient();
            WithoutServices = bareApplication.CreateClient();
        }

        [OneTimeTearDown]
        public void Stop()
        {
            Client.Dispose();
            WithoutServices.Dispose();
            application.Dispose();
            bareApplication.Dispose();
        }

        private static void RegisterLoggers(IServiceCollection services)
        {
            foreach (var (module, logger) in Loggers)
            {
                services.AddSingleton(typeof(ILogger<>).MakeGenericType(module), logger.Object);
            }
        }
    }
}
