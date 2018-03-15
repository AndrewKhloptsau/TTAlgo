﻿using Microsoft.AspNetCore.Hosting;
using System.ServiceProcess;

namespace TickTrader.BotAgent
{
    public static class WebHostServiceExtensions
    {
        public static void RunAsCustomService(this IWebHost host)
        {
            var webHostService = new BAHostService(host);
            ServiceBase.Run(webHostService);
        }
    }
}