﻿using Caliburn.Micro;
using System;
using System.Reflection;
using TickTrader.Algo.Core.Lib;

namespace TickTrader.BotTerminal
{
    class AboutDialogViewModel : Screen
    {
        private static readonly string _version;
        private static readonly string _buildDate;


        public string Version { get; private set; }


        static AboutDialogViewModel()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            _version = entryAssembly.GetName().Version.ToString();
            _buildDate = entryAssembly.GetLinkerTime().ToString("yyyy.MM.dd");
        }

        public AboutDialogViewModel()
        {
            DisplayName = $"About - {EnvService.Instance.ApplicationName}";
            Version = $"Version: {_version} ({_buildDate})";

//#if DEBUG
//            for (var i = 0; i < 5; i++)
//                GC.Collect(2, GCCollectionMode.Forced, true);
//#endif
        }


        public void Ok()
        {
            TryCloseAsync();
        }
    }
}
