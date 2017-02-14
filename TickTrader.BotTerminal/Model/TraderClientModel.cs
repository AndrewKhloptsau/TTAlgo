﻿using SoftFX.Extended;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TickTrader.BotTerminal.Lib;

namespace TickTrader.BotTerminal
{
    internal class TraderClientModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public TraderClientModel(ConnectionModel connection)
        {
            this.Connection = connection;

            connection.Initalizing += Connection_Initalizing;
            connection.State.StateChanged += State_StateChanged;
            connection.Deinitalizing += Connection_Deinitalizing;

            this.Symbols = new SymbolCollectionModel(connection);
            this.History = new FeedHistoryProviderModel(connection);
            this.TradeApi = new TradeExecutor(this);
            this.Account = new AccountModel(this);
            this.Currencies = new Dictionary<string, CurrencyInfo>();
        }

        private void State_StateChanged(ConnectionModel.States oldState, ConnectionModel.States newState)
        {
            if (newState == ConnectionModel.States.Connecting)
            {
                IsConnecting = true;
                IsConnectingChanged?.Invoke();
            }
            else
            {
                if (IsConnecting)
                {
                    IsConnecting = false;
                    IsConnectingChanged?.Invoke();
                }
            }
        }

        private void OnConnected()
        {
            try
            {
                IsConnected = true;
                Connected?.Invoke();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Connection_Connected() failed.");
            }
        }

        private void OnDisconnected()
        {
            try
            {
                IsConnected = false;
                Disconnected?.Invoke();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Connection_Disconnected() failed.");
            }
        }

        private async Task Connection_Initalizing(object sender, CancellationToken cancelToken)
        {
            try
            {
                var cache = Connection.FeedProxy.Cache;
                await History.Init();
                Currencies.Clear();
                foreach (var c in cache.Currencies)
                    Currencies.Add(c.Name, c);
                await Symbols.Initialize(cache.Symbols, Currencies);
                Account.Init(Currencies);
                if (Initializing != null)
                    await Initializing.InvokeAsync(this, cancelToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Connection_Initalizing() failed.");
            }

            OnConnected();
        }

        private async Task Connection_Deinitalizing(object sender, CancellationToken cancelToken)
        {
            OnDisconnected();

            try
            {
                await Symbols.Deinit();
                await Account.Deinit();
                await History.Deinit();
                if (Deinitializing != null)
                    await Deinitializing.InvokeAsync(this, cancelToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Connection_Deinitalizing() failed.");
            }
        }

        public bool IsConnecting { get; private set; }
        public bool IsConnected { get; private set; }

        public event AsyncEventHandler Initializing;
        public event Action IsConnectingChanged;
        public event Action Connected;
        public event AsyncEventHandler Deinitializing;
        public event Action Disconnected;

        public ConnectionModel Connection { get; private set; }
        public TradeExecutor TradeApi { get; private set; }
        public AccountModel Account { get; private set; }
        public SymbolCollectionModel Symbols { get; private set; }
        public QuoteDistributor Distributor { get { return Symbols.Distributor; } }
        public FeedHistoryProviderModel History { get; private set; }
        public Dictionary<string, CurrencyInfo> Currencies { get; private set; }
    }
}