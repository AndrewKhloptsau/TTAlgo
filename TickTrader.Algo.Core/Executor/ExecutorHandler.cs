﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TickTrader.Algo.Api;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Core.Repository;

namespace TickTrader.Algo.Core
{
    public class PluginExecutor : CrossDomainObject, IDisposable
    {
        private ISynchronizationContext _syncContext;
        
        private AlgoPluginRef _ref;
        private CommonCdProxy _cProxy;
        private FeedCdProxy _fProxy;

        public PluginExecutor(AlgoPluginRef pluginRef, ISynchronizationContext updatesSync)
        {
            _ref = pluginRef;
            _syncContext = updatesSync;
            Core = pluginRef.CreateExecutor();
            IsIsolated = pluginRef.IsIsolated;

            Core.IsGlobalMarshalingEnabled = true;
            Core.IsBunchingRequired = IsIsolated || _syncContext != null;

            Core.MarshalUpdate = MarshalUpdate;
            Core.MarshalUpdates = MarshalUpdates;
            Core.Stopped += () =>
            {
                if (_syncContext != null)
                    _syncContext.Invoke(() => Stopped?.Invoke(this));
                else
                    Stopped?.Invoke(this);
            };
        }

        internal PluginExecutorCore Core { get; }
        public bool IsIsolated { get; }
        //public bool IsRunning { get; private set; }

        public IAccountInfoProvider AccInfoProvider { get; set; }
        public ITradeHistoryProvider TradeHistoryProvider { get; set; }
        public IPluginMetadata Metadata { get; set; }
        public IFeedProvider Feed { get; set; }
        public IFeedHistoryProvider FeedHistory { get; set; }

        public event Action<BotLogRecord> LogUpdated;
        public event Action<TesterTradeTransaction> TradesUpdated;
        public event Action<TradeReportEntity> TradeHistoryUpdated;
        public event Action<RateUpdate> SymbolRateUpdated;
        public event Action<BarEntity, string, SeriesUpdateActions> ChartBarUpdated;
        public event Action<BarEntity, SeriesUpdateActions> EquityUpdated;
        public event Action<BarEntity, SeriesUpdateActions> MarginUpdated;
        public event Action<IDataSeriesUpdate> OutputUpdate;
        public event Action<Exception> ErrorOccurred;

        public event Action<PluginExecutor> Stopped;

        #region Excec control

        public void Start()
        {
            ConfigurateCore();
            Core.Start();
        }

        public void Stop()
        {
            Core.Stop();
        }

        public void Abort()
        {
            Core.Abort();
        }

        public void HandleDisconnect()
        {
            Core.HandleDisconnect();
        }

        public void HandleReconnect()
        {
            Core.HandleReconnect();
        }

        #endregion

        #region Setup

        public PluginExecutorConfig Config { get; } = new PluginExecutorConfig();

        #endregion

        //public override void Dispose()
        //{
        //}

        private void ConfigurateCore()
        {
            if (IsIsolated)
            {
                _cProxy = new CommonCdProxy(AccInfoProvider, Metadata, TradeHistoryProvider);
                _fProxy = new FeedCdProxy(Feed, FeedHistory);

                Core.ApplyConfig(Config, _cProxy, _cProxy, _cProxy, _fProxy, _fProxy);
            }
            else
                Core.ApplyConfig(Config, AccInfoProvider, Metadata, TradeHistoryProvider, Feed, FeedHistory);
        }

        #region Update Marshalling 

        private void MarshalUpdatesToContext(IReadOnlyList<object> updates)
        {
            if (_syncContext != null)
                _syncContext.Invoke(MarshalUpdatesToContext, updates);
            else
                MarshalUpdatesToContext(updates);
        }

        private void MarshalUpdates(IReadOnlyList<object> updates)
        {
            for (int i = 0; i < updates.Count; i++)
            {
                try
                {
                    MarshalUpdate(updates[i]);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                }
            }
        }

        private void MarshalUpdate(object update)
        {
            if (update is BotLogRecord)
                LogUpdated?.Invoke((BotLogRecord)update);
            else if (update is TradeReportEntity)
                TradeHistoryUpdated?.Invoke((TradeReportEntity)update);
            else if (update is TesterTradeTransaction)
                TradesUpdated?.Invoke((TesterTradeTransaction)update);
            else if (update is RateUpdate)
                SymbolRateUpdated?.Invoke((RateUpdate)update);
            else if (update is IDataSeriesUpdate)
            {
                var seriesUpdate = (IDataSeriesUpdate)update;
                if (seriesUpdate.SeriesType == DataSeriesTypes.SymbolRate)
                {
                    var barUpdate = (DataSeriesUpdate<BarEntity>)update;
                    ChartBarUpdated?.Invoke(barUpdate.Value, barUpdate.SeriesId, barUpdate.Action);
                }
                else if (seriesUpdate.SeriesType == DataSeriesTypes.NamedStream)
                {
                    var barUpdate = (DataSeriesUpdate<BarEntity>)update;
                    if (barUpdate.SeriesId == BacktesterCollector.EquityStreamName)
                        EquityUpdated?.Invoke(barUpdate.Value, barUpdate.Action);
                    else if (barUpdate.SeriesId == BacktesterCollector.MarginStreamName)
                        MarginUpdated?.Invoke(barUpdate.Value, barUpdate.Action);
                }
                else if (seriesUpdate.SeriesType == DataSeriesTypes.Output)
                    OutputUpdate?.Invoke(seriesUpdate);
            }
            else if (update is Exception)
                ErrorOccurred?.Invoke((Exception)update);

            #endregion
        }
    }
}
