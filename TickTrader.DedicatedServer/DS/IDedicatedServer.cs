﻿
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using TickTrader.Algo.Common.Model;
using TickTrader.Algo.Common.Model.Config;
using TickTrader.Algo.Common.Model.Setup;
using TickTrader.Algo.Core;
using TickTrader.Algo.Core.Metadata;
using TickTrader.DedicatedServer.DS.Models;

namespace TickTrader.DedicatedServer.DS
{
    public interface IDedicatedServer
    {
        IPackage AddPackage(byte[] fileContent, string fileName);
        IPackage[] GetPackages();
        void RemovePackage(string package);
        PluginInfo[] GetAllPlugins();
        PluginInfo[] GetPluginsByType(AlgoTypes type);

        IEnumerable<IAccount> Accounts { get; }
        IEnumerable<ITradeBot> TradeBots { get; }
        event Action<IAccount, ChangeAction> AccountChanged;
        event Action<ITradeBot, ChangeAction> BotChanged;
        event Action<ITradeBot> BotStateChanged;
        event Action<IPackage, ChangeAction> PackageChanged;

        string AutogenerateBotId(string botDisplayName);

        void AddAccount(AccountKey key, string password);
        void RemoveAccount(AccountKey key);
        void ChangeAccountPassword(AccountKey key, string password);
        ConnectionErrorCodes TestAccount(AccountKey accountId);
        ConnectionErrorCodes TestCreds(string login, string password, string server);

        ITradeBot AddBot(string botId, AccountKey accountId, PluginKey pluginId, PluginConfig botConfig);
        void RemoveBot(string botId);
    }

    public interface IAccount
    {
        string Address { get; }
        string Username { get; }
        ConnectionStates ConnectionState { get; }
        IEnumerable<ITradeBot> TradeBots { get; }

        Task<ConnectionErrorCodes> TestConnection();
        void ChangePassword(string password);

        ITradeBot AddBot(string botId, PluginKey pluginId, PluginConfig botConfig);
        void RemoveBot(string botId);
    }

    public enum ConnectionStates { Offline, Connecting, Online, Disconnecting }
    public enum BotStates { Offline, Started, Initializing, Faulted, Online, Stopping }

    public interface ITradeBot
    {
        string Id { get; }
        bool IsRunning { get; }
        IBotLog Log { get; }
        IAccount Account { get; }
        BotStates State { get; }
        void Start();
        Task StopAsync();
    }

    public interface IPackage
    {
        string Name { get; }
        DateTime Created { get; }
        bool IsValid { get; }

        IEnumerable<PluginInfo> GetPluginsByType(AlgoTypes type);
    }

    public interface IBotLog
    {
        string Status { get; }
        event Action<string> StatusUpdated;
    }

    public class PluginInfo
    {
        public PluginInfo(PluginKey key, AlgoPluginDescriptor descriptor)
        {
            Id = key;
            Descriptor = descriptor;
        }

        public PluginKey Id { get; }
        public AlgoPluginDescriptor Descriptor { get; }
    }

    public class PluginKey
    {
        public PluginKey(string package, string id)
        {
            PackageName = package;
            DescriptorId = id;
        }

        public string PackageName { get; private set; }
        public string DescriptorId { get; private set; }
    }

    public class AccountKey
    {
        public AccountKey(string login, string server)
        {
            Login = login;
            Server = server;
        }

        public string Login { get; private set; }
        public string Server { get; private set; }
    }
}
