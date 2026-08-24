// ------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer
{
    using Carter;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Entry class of this application
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry method of this application
        /// </summary>
        /// <param name="args">The launch arguments</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddLogging();
            builder.Services.AddCarter();

            var app = builder.Build();

            app.MapCarter();

            app.Run();
        }
    }
}
