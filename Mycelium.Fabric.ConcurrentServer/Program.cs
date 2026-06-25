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
    using Microsoft.Extensions.Hosting;

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
            var builder = Host.CreateApplicationBuilder(args);

            var host = builder.Build();
            host.Run();
        }
    }
}
