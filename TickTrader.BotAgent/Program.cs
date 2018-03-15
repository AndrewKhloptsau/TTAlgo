using System.IO;
using Microsoft.AspNetCore.Hosting;
using TickTrader.BotAgent.WebAdmin;
using Microsoft.Extensions.Configuration;
using TickTrader.BotAgent.BA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TickTrader.BotAgent.BA;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TickTrader.Algo.Core.Metadata;
using TickTrader.Algo.Api;
using TickTrader.Algo.Core;
using TickTrader.Algo.Common.Model.Config;
using TickTrader.BotAgent.BA.Info;
using System.Diagnostics;
using Newtonsoft.Json;
using TickTrader.BotAgent.WebAdmin.Server.Models;
using System.Security.Cryptography.X509Certificates;
using TickTrader.BotAgent.WebAdmin.Server.Extensions;
using TickTrader.BotAgent.Extensions;
using NLog;
using TickTrader.Algo.Common.Model.Interop;

namespace TickTrader.BotAgent
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = LogManager.GetLogger(nameof(Startup));
            try
            {
                bool isService = true;

                if (Debugger.IsAttached || args.Contains("console"))
                    isService = false;

                var pathToContentRoot = Directory.GetCurrentDirectory();

                if (isService)
                {
                    var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
                    pathToContentRoot = Path.GetDirectoryName(pathToExe);
                }

                var pathToWebRoot = Path.Combine(pathToContentRoot, @"WebAdmin\wwwroot");
                var pathToAppSettings = Path.Combine(pathToContentRoot, @"WebAdmin\appsettings.json");

                var config = EnsureDefaultConfiguration(pathToAppSettings);

                var cert = GetCertificate(config, logger, pathToContentRoot);

                var host = new WebHostBuilder()
                    .UseConfiguration(config)
                    .UseKestrel(options => options.UseHttps(cert))
                    .UseContentRoot(pathToContentRoot)
                    .UseWebRoot(pathToWebRoot)
                    .UseStartup<Startup>()
                    .Build();

                Console.WriteLine($"Web root path: {pathToWebRoot}");

                if (isService)
                    host.RunAsCustomService();
                else
                    host.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private static X509Certificate2 GetCertificate(IConfiguration config, Logger logger, string contentRoot)
        {
            var sslConf = config.GetSslSettings();

            ValidateSslConfiguration(sslConf, logger);

            var pfxFile = sslConf.File;

            if (!pfxFile.IsPathAbsolute())
                pfxFile = Path.Combine(contentRoot, pfxFile);

            return new X509Certificate2(pfxFile, sslConf.Password);
        }

        private static void ValidateSslConfiguration(SslSettings sslConf, Logger logger)
        {
            if (sslConf == null)
                throw new ArgumentException("SSL configuration not found");

            if(string.IsNullOrWhiteSpace(sslConf.File))
                throw new ArgumentException("Certificate file is not defined");
        }

        private static IConfiguration EnsureDefaultConfiguration(string configFile)
        {
            if (!System.IO.File.Exists(configFile))
            {
                CreateDefaultConfig(configFile);
            }

            var builder = new ConfigurationBuilder()
              .AddJsonFile(configFile, optional: false)
              .AddEnvironmentVariables();

            return builder.Build();
        }

        private static void CreateDefaultConfig(string configFile)
        {
            var appSettings = AppSettings.Default;
            SaveConfig(configFile, appSettings);
        }

        private static void SaveConfig(string configFile, AppSettings appSettings)
        {
            System.IO.File.WriteAllText(configFile, JsonConvert.SerializeObject(appSettings, Formatting.Indented));
        }

        private static void RunConsole()
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            var logFactory = serviceProvider.GetService<ILoggerFactory>();
            InitLogger(logFactory);
            var server = ServerModel.Load(logFactory);

            CommandUi cmdEngine = new CommandUi();
            cmdEngine.RegsiterCommand("info", () =>
            {
                lock (server.SyncObj)
                {
                    foreach (var acc in server.Accounts)
                    {
                        var model = acc;
                        Console.WriteLine(GetDisplayName(acc) + " - " + acc.ConnectionState);
                        foreach (var bot in acc.TradeBots)
                            Console.WriteLine("\t{0} - {1} ", bot.Id, bot.State);
                    }
                }
            });

            cmdEngine.RegsiterCommand("account", () =>
            {
                var cmd = CommandUi.Choose("cmd", "add", "remove", "change password", "test", "cancel", "info");

                IAccount acc;
                List<IAccount> accountsList;

                switch (cmd)
                {
                    case "add":
                        var newLogin = CommandUi.InputString("login");
                        var newPassword = CommandUi.InputString("password");
                        var newServer = CommandUi.InputString("server");
                        server.AddAccount(new AccountKey(newLogin, newServer), newPassword, false);
                        break;

                    case "remove":
                        lock (server.SyncObj)
                            accountsList = server.Accounts.ToList();
                        acc = CommandUi.Choose("account", accountsList, GetDisplayName);
                        server.RemoveAccount(new AccountKey(acc.Username, acc.Address));
                        break;

                    case "change password":
                        lock (server.SyncObj)
                            accountsList = server.Accounts.ToList();
                        acc = CommandUi.Choose("account", accountsList, GetDisplayName);
                        var chgPassword = CommandUi.InputString("new password");
                        server.ChangeAccountPassword(new AccountKey(acc.Username, acc.Address), chgPassword);
                        break;

                    case "test":
                        lock (server.SyncObj)
                            accountsList = server.Accounts.ToList();
                        acc = CommandUi.Choose("account", accountsList, GetDisplayName);
                        var result = acc.TestConnection().Result;
                        if (result.Code == ConnectionErrorCodes.None)
                            Console.WriteLine("Valid connection.");
                        else
                            Console.WriteLine("Error = " + acc.TestConnection().Result);
                        break;
                    case "info":
                        lock (server.SyncObj)
                            accountsList = server.Accounts.ToList();
                        acc = CommandUi.Choose("account", accountsList, GetDisplayName);
                        var accKey = new AccountKey(acc.Username, acc.Address);
                        ConnectionInfo info;
                        var getInfoError = server.GetAccountInfo(accKey, out info);
                        if (getInfoError == ConnectionErrorCodes.None)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Symbols:");
                            Console.WriteLine(string.Join(",", info.Symbols.Select(s => s.Name)));
                            Console.WriteLine();
                            Console.WriteLine("Currencies:");
                            Console.WriteLine(string.Join(",", info.Currencies.Select(s => s.Name)));
                        }
                        else
                            Console.WriteLine("Error = " + acc.TestConnection().Result);
                        break;
                }

            });

            cmdEngine.RegsiterCommand("trade bot", () =>
            {
                var cmd = CommandUi.Choose("cmd", "add", "remove", "start", "stop", "view status", "cancel");

                IAccount acc;
                List<IAccount> accountsList;
                ITradeBot[] bots;

                switch (cmd)
                {
                    case "add":

                        PluginInfo[] availableBots;

                        lock (server.SyncObj)
                        {
                            availableBots = server.GetPluginsByType(AlgoTypes.Robot);
                            accountsList = server.Accounts.ToList();
                        }

                        if (accountsList.Count == 0)
                            Console.WriteLine("Cannot add bot: no accounts!");
                        else if (availableBots.Length == 0)
                            Console.WriteLine("Cannot add bot: no bots in repository!");
                        else
                        {
                            if (accountsList.Count == 1)
                                acc = accountsList[0];
                            else
                                acc = CommandUi.Choose("account", accountsList, GetDisplayName);

                            var botToAdd = CommandUi.Choose("bot", availableBots, b => b.Descriptor.DisplayName);

                            if (botToAdd.Descriptor.IsValid)
                            {
                                var botConfig = SetupBot(botToAdd.Descriptor);
                                var botId = server.AutogenerateBotId(botToAdd.Descriptor.DisplayName);

                                TradeBotModelConfig botCfg = new TradeBotModelConfig
                                {
                                    InstanceId = botId,
                                    Plugin = botToAdd.Id,
                                    PluginConfig = botConfig,
                                    Isolated = false
                                };

                                acc.AddBot(botCfg);
                            }
                            else
                                Console.WriteLine("Cannot add bot: bot is invalid!");
                        }

                        break;

                    case "start":

                        lock (server.SyncObj)
                            bots = server.TradeBots.ToArray();

                        var botToStart = CommandUi.Choose("bot", bots, b => b.Id);

                        botToStart.Start();

                        break;

                    case "remove":

                        lock (server.SyncObj)
                            bots = server.TradeBots.ToArray();

                        var botToRemove = CommandUi.Choose("bot", bots, b => b.Id);

                        server.RemoveBot(botToRemove.Id);

                        break;

                    case "stop":

                        lock (server.SyncObj)
                            bots = server.TradeBots.ToArray();

                        var botToStop = CommandUi.Choose("bot", bots, b => b.Id);

                        botToStop.StopAsync().Wait();

                        break;

                    case "view status":

                        lock (server.SyncObj)
                            bots = server.TradeBots.ToArray();

                        var botToView = CommandUi.Choose("bot", bots, b => b.Id);

                        Action<string> printAction = st =>
                        {
                            Console.Clear();
                            Console.WriteLine(st);
                        };

                        lock (server.SyncObj)
                        {
                            printAction(botToView.Log.Status);
                            botToView.Log.StatusUpdated += printAction;
                        }

                        Console.ReadLine();

                        lock (server.SyncObj)
                            botToView.Log.StatusUpdated -= printAction;

                        break;
                }
            });

            cmdEngine.Run();

            server.Close();
        }

        private static string GetDisplayName(IAccount acc)
        {
            return string.Format("account {0} : {1}", acc.Address, acc.Username);
        }

        private static void InitLogger(ILoggerFactory factory)
        {
            CoreLoggerFactory.Init(cn => new LoggerAdapter(factory.CreateLogger(cn)));
        }

        private static PluginConfig SetupBot(AlgoPluginDescriptor descriptor)
        {
            var config = new BarBasedConfig();

            config.PriceType = BarPriceType.Bid;
            config.MainSymbol = CommandUi.InputString("symbol");

            foreach (var prop in descriptor.AllProperties)
                config.Properties.Add(InputBotParam(prop));

            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("\tMain Symbol - {0}", config.MainSymbol);

            foreach (var p in config.Properties)
                PrintProperty(p);

            return config;
        }

        private static Property InputBotParam(AlgoPropertyDescriptor descriptor)
        {
            if (descriptor is ParameterDescriptor)
            {
                var paramDescriptor = (ParameterDescriptor)descriptor;
                var id = descriptor.Id;

                if (paramDescriptor.IsEnum)
                {
                    var enumVal = CommandUi.ChooseNullable(descriptor.DisplayName, paramDescriptor.EnumValues.ToArray());
                    return new EnumParameter() { Id = id, Value = enumVal ?? (string)paramDescriptor.DefaultValue };
                }

                switch (paramDescriptor.DataType)
                {
                    case "System.Int32":
                        var valInt32 = CommandUi.InputNullabelInteger(paramDescriptor.DisplayName);
                        return new IntParameter() { Id = id, Value = valInt32 ?? (int)paramDescriptor.DefaultValue };
                    case "System.Double":
                        var valDouble = CommandUi.InputNullableDouble(paramDescriptor.DisplayName);
                        return new DoubleParameter() { Id = id, Value = valDouble ?? (double)paramDescriptor.DefaultValue };
                    case "System.String":
                        var strVal = CommandUi.InputString(paramDescriptor.DisplayName);
                        return new StringParameter() { Id = id, Value = CommandUi.InputString(paramDescriptor.DisplayName) };
                    case "TickTrader.Algo.Api.File":
                        return new FileParameter() { Id = id, FileName = CommandUi.InputString(paramDescriptor.DisplayName) };
                }
            }

            throw new ApplicationException($"Parameter '{descriptor.DisplayName}' is of unsupported type!");
        }

        private static void PrintProperty(Property p)
        {
            if (p is Parameter)
                Console.WriteLine("\t{0} - {1}", p.Id, ((Parameter)p).ValObj);
        }
    }
}