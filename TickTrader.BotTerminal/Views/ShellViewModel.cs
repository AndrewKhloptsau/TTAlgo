﻿using Caliburn.Micro;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using System.Windows.Threading;
using TickTrader.Algo.Api;

namespace TickTrader.BotTerminal
{
    internal class ShellViewModel : Screen, IConnectionViewModel, iOrderUi, IShell, ToolWindowsManager
    {
        private ConnectionManager cManager;
        private TraderClientModel clientModel;
        private WindowManager wndManager;
        private PluginCatalog catalog = new PluginCatalog();
        private PersistModel storage;
        private EventJournal eventJournal;
        private BotJournal botJournal;
        private Logger logger;
        private bool isClosed;
        private INotificationCenter notificationCenter;

        public ShellViewModel()
        {
            DisplayName = EnvService.Instance.ApplicationName;

            logger = NLog.LogManager.GetCurrentClassLogger();
            notificationCenter = new NotificationCenter(new PopupNotification(), new SoundNotification());
            eventJournal = new EventJournal(1000);
            botJournal = new BotJournal(1000);
            storage = new PersistModel();

            wndManager = new MdiWindowManager(this);

            cManager = new ConnectionManager(storage, eventJournal);
            clientModel = new TraderClientModel(cManager.Connection);

            ConnectionLock = new UiLock();
            AlgoList = new AlgoListViewModel(catalog);
            SymbolList = new SymbolListViewModel(clientModel.Symbols, this);

            Trade = new TradeInfoViewModel(clientModel);

            TradeHistory = new TradeHistoryViewModel(clientModel.Account, cManager.Connection);

            Notifications = new NotificationsViewModel(notificationCenter, clientModel.Account, cManager.Connection);

            Charts = new ChartCollectionViewModel(clientModel, catalog, this, botJournal);
            AccountPane = new AccountPaneViewModel(cManager, this, this);
            Journal = new JournalViewModel(eventJournal);
            BotJournal = new BotJournalViewModel(botJournal);
            CanConnect = true;

            UpdateCommandStates();
            cManager.StateChanged += s => UpdateDisplayName();
            cManager.StateChanged += s => UpdateCommandStates();
            SymbolList.NewChartRequested += s => Charts.Open(s);
            ConnectionLock.PropertyChanged += (s, a) => UpdateCommandStates();

            catalog.AddFolder(EnvService.Instance.AlgoRepositoryFolder);
            catalog.AddAssembly(Assembly.Load("TickTrader.Algo.Indicators"));

            clientModel.Connected += OpenDefaultChart;

            LogStateLoop();
        }

        private void OpenDefaultChart()
        {
            if (clientModel.Symbols.Snapshot.Any())
            {
                var defaultSymbol = string.Empty;
                switch (clientModel.Account.Type)
                {
                    case SoftFX.Extended.AccountType.Gross:
                    case SoftFX.Extended.AccountType.Cash:
                        defaultSymbol = "EURUSD";
                        break;
                    case SoftFX.Extended.AccountType.Net:
                        defaultSymbol = "EUR/USD";
                        break;
                }

                Charts.Open(clientModel.Symbols.GetOrDefault(defaultSymbol)?.Name ?? clientModel.Symbols.Snapshot.First().Key);
                clientModel.Connected -= OpenDefaultChart;
            }
        }

        private void UpdateDisplayName()
        {
            if (cManager.State == ConnectionManager.States.Online)
                DisplayName = $"{cManager.Creds.Login} {cManager.Creds.Server.Address} - {EnvService.Instance.ApplicationName}";
        }

        private void UpdateCommandStates()
        {
            var state = cManager.State;
            CanConnect = !ConnectionLock.IsLocked;
            CanDisconnect = state == ConnectionManager.States.Online && !ConnectionLock.IsLocked;
            NotifyOfPropertyChange(nameof(CanConnect));
            NotifyOfPropertyChange(nameof(CanDisconnect));
        }

        public bool CanConnect { get; private set; }
        public bool CanDisconnect { get; private set; }

        public void Connect()
        {
            Connect(null);
        }

        public void Disconnect()
        {
            try
            {
                cManager.TriggerDisconnect();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        protected override void OnDeactivate(bool close)
        {
            if (close)
                Shutdown();
        }

        public override void CanClose(Action<bool> callback)
        {
            var exit = new ExitDialogViewModel();
            wndManager.ShowDialog(exit);
            callback(exit.IsConfirmed);
        }

        public void ManageAccounts()
        {
        }

        public void Connect(AccountAuthEntry creds = null)
        {
            try
            {
                if (creds == null || !creds.HasPassword)
                {
                    LoginDialogViewModel model = new LoginDialogViewModel(cManager, creds);
                    wndManager.ShowDialog(model);
                }
                else
                    cManager.TriggerConnect(creds);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public TradeInfoViewModel Trade { get; }
        public TradeHistoryViewModel TradeHistory { get; }
        public AlgoListViewModel AlgoList { get; set; }
        public SymbolListViewModel SymbolList { get; private set; }
        public ChartCollectionViewModel Charts { get; private set; }
        public AccountPaneViewModel AccountPane { get; private set; }
        public JournalViewModel Journal { get; set; }
        public BotJournalViewModel BotJournal { get; set; }
        public iOrderUi OrderCommands { get { return this; } }
        public UiLock ConnectionLock { get; private set; }
        public ToolWindowsManager ToolWndManager { get { return this; } }

        public NotificationsViewModel Notifications { get; private set; }

        private async void Shutdown()
        {
            isClosed = true;

            try
            {
                await cManager.Disconnect();
            }
            catch (Exception) { }

            App.Current.Shutdown();
        }

        protected override void OnViewLoaded(object view)
        {
            eventJournal.Info("BotTrader started");
            PrintSystemInfo();
            ConnectLast();
        }

        private async void PrintSystemInfo()
        {
            await Task.Run(() =>
            {
                var os = ComputerInfo.OperatingSystem;
                var cpu = ComputerInfo.Processor;
                var sign = TimeZoneInfo.Local.BaseUtcOffset < TimeSpan.Zero ? "-" : "+";
                eventJournal.Info("{0} ({1}), {2}, RAM: {3} / {4} Mb, UTC{5}{6:hh\\:mm}",
                    os.Name, os.Architecture,
                    cpu.Name,
                    os.FreePhysicalMemory / 1024,
                    os.TotalVisibleMemorySize / 1024,
                    sign,
                    TimeZoneInfo.Local.BaseUtcOffset);
            });
        }

        private void ConnectLast()
        {
            var last = cManager.GetLast();
            if (last != null)
                Connect(last);
        }

        private async void LogStateLoop()
        {
            StringBuilder builder = new StringBuilder();

            while (!isClosed)
            {
                builder.Clear();
                builder.Append("STATE SNAPSHOT ");

                LogState(builder, "ConnectionManager", cManager.State.ToString());
                LogState(builder, "Connection", cManager.Connection.State.Current.ToString());

                logger.Debug(builder.ToString());

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        private static void LogState(StringBuilder builder, string name, string state)
        {
            builder.Append(name).Append(':').Append(state).Append(" ");
        }

        #region OrderUi implementation

        public void OpenMarkerOrder(string symbol)
        {
            try
            {
                using (var openOrderModel = new OpenOrderDialogViewModel(clientModel, symbol))
                    wndManager.ShowWindow(openOrderModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public void OpenMarkerOrder(string symbol, decimal volume, OrderSide side)
        {

        }

        #endregion

        #region ToolWindowsManager implementation

        private Dictionary<object, IScreen> wndModels = new Dictionary<object, IScreen>();

        public IScreen GetWindow(object key)
        {
            return wndModels.GetValueOrDefault(key);
        }

        public void OpenWindow(object wndKey, IScreen wndModel, bool closeExisting = false)
        {
            IScreen existing = GetWindow(wndKey);
            if (existing != null)
            {
                if (closeExisting)
                    existing.TryClose();
                else
                    throw new Exception("Window already opened!");
            }
            wndModel.Deactivated += WndModel_Deactivated;
            wndModels[wndKey] = wndModel;
            wndManager.ShowWindow(wndModel);
        }

        private void WndModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            if (e.WasClosed)
            {
                var wndModel = sender as IScreen;
                wndModel.Deactivated -= WndModel_Deactivated;

                var keyValue = wndModels.FirstOrDefault(m => m.Value == wndModel);
                if (keyValue.Key != null)
                    wndModels.Remove(keyValue.Key);
            }
        }

        public void CloseWindow(object wndKey)
        {
            GetWindow(wndKey)?.TryClose();
        }

        #endregion
    }

    internal interface IConnectionViewModel
    {
        void Connect(AccountAuthEntry creds);
    }
}
