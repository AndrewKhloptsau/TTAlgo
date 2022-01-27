﻿using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TickTrader.Algo.Core;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.CoreV1;
using TickTrader.Algo.CoreV1.Metadata;
using TickTrader.Algo.Domain;

namespace TickTrader.Algo.Backtester
{
    public class Backtester : IDisposable, IPluginSetupTarget, IPluginMetadata, IBacktesterSettings, ITestExecController
    {
        private static int IdSeed;

        private readonly PluginKey _pluginKey;
        private readonly FeedEmulator _feed;
        private readonly PluginExecutorCore _executorCore;
        private readonly BacktesterMarshaller _marshaller;
        private readonly EmulationControlFixture _control;

        public Backtester(PluginKey pluginKey, ISyncContext syncObj, DateTime? from, DateTime? to)
        {
            _pluginKey = pluginKey ?? throw new ArgumentNullException(nameof(pluginKey));
            var metadata = PackageMetadataCache.GetPlugin(pluginKey) ?? throw new ArgumentException("metadata not found", nameof(pluginKey));
            PluginInfo = metadata.Descriptor;
            _executorCore = new PluginExecutorCore(pluginKey);
            _marshaller = new BacktesterMarshaller(_executorCore, syncObj);
            _executorCore.Metadata = this;

            CommonSettings.EmulationPeriodStart = from;
            CommonSettings.EmulationPeriodEnd = to;

            _control = InitEmulation();
            _feed = _control.Feed;
            _executorCore.Feed = _feed;
            _executorCore.FeedHistory = _feed;
            _executorCore.InitBarStrategy(Domain.Feed.Types.MarketSide.Bid);

            CommonSettings.Leverage = 100;
            CommonSettings.InitialBalance = 10000;
            CommonSettings.BalanceCurrency = "USD";
            CommonSettings.AccountType = AccountInfo.Types.Type.Gross;

            _control.StateUpdated += s =>
            {
                State = s;
                StateChanged?.Invoke(s);
            };
        }

        public CommonTestSettings CommonSettings { get; } = new CommonTestSettings();

        public BacktesterMarshaller Executor => _marshaller;
        public PluginDescriptor PluginInfo { get; }
        public int TradesCount => _control.TradeHistory.Count;
        public FeedEmulator Feed => _feed;
        //public TimeSpan ServerPing { get; set; }
        //public int WarmupSize { get; set; } = 10;
        //public WarmupUnitTypes WarmupUnits { get; set; } = WarmupUnitTypes.Bars;
        public DateTime? CurrentTimePoint => _control?.EmulationTimePoint;
        public JournalOptions JournalFlags { get; set; } = JournalOptions.Enabled | JournalOptions.WriteInfo | JournalOptions.WriteCustom | JournalOptions.WriteTrade | JournalOptions.WriteAlert;
        public string JournalPath { get; set; }
        public EmulatorStates State { get; private set; }
        public event Action<EmulatorStates> StateChanged;
        public event Action<Exception> ErrorOccurred { add => Executor.ErrorOccurred += value; remove => Executor.ErrorOccurred -= value; }

        public event Action<BarData, string, DataSeriesUpdate.Types.UpdateAction> OnChartUpdate
        {
            add { Executor.ChartBarUpdated += value; }
            remove { Executor.ChartBarUpdated -= value; }
        }

        public event Action<DataSeriesUpdate> OnOutputUpdate
        {
            add { Executor.OutputUpdate += value; }
            remove { Executor.OutputUpdate -= value; }
        }

        public Dictionary<string, TestDataSeriesFlags> SymbolDataConfig { get; } = new Dictionary<string, TestDataSeriesFlags>();
        public TestDataSeriesFlags MarginDataMode { get; set; } = TestDataSeriesFlags.Snapshot;
        public TestDataSeriesFlags EquityDataMode { get; set; } = TestDataSeriesFlags.Snapshot;
        public TestDataSeriesFlags OutputDataMode { get; set; } = TestDataSeriesFlags.Disabled;
        public bool StreamExecReports { get; set; }

        public async Task Run(CancellationToken cToken)
        {
            cToken.Register(() => _control.CancelEmulation());

            await Task.Factory.StartNew(SetupAndRun, TaskCreationOptions.LongRunning);
        }


        private EmulationControlFixture InitEmulation()
        {
            var calcFixture = _executorCore.GetCalcFixture();
            var fixture = new EmulationControlFixture(this, _executorCore, calcFixture, null);

            var iStrategy = fixture.InvokeEmulator;
            calcFixture.OnFatalError = e => fixture.InvokeEmulator.SetFatalError(e);
            _executorCore.InitEmulation(fixture.InvokeEmulator, null, null, null,
                c => new TradeEmulator(c, this, calcFixture, fixture.InvokeEmulator, fixture.Collector, fixture.TradeHistory, PluginInfo.Type),
                fixture.Collector, new TimerApiEmulator(_executorCore, fixture.InvokeEmulator), m => new SimplifiedBuilder(m));
            return fixture;
        }

        private void SetupAndRun()
        {
            _executorCore.InitSlidingBuffering(4000);

            _executorCore.MainSymbolCode = CommonSettings.MainSymbol;
            _executorCore.TimeFrame = CommonSettings.MainTimeframe;
            _executorCore.ModelTimeFrame = CommonSettings.ModelTimeframe;
            _executorCore.InstanceId = "Baсktesting-" + Interlocked.Increment(ref IdSeed).ToString();
            _executorCore.Permissions = new PluginPermissions() { TradeAllowed = true };

            bool isRealtime = MarginDataMode.HasFlag(TestDataSeriesFlags.Realtime)
                || EquityDataMode.HasFlag(TestDataSeriesFlags.Realtime)
                || OutputDataMode.HasFlag(TestDataSeriesFlags.Realtime)
                || SymbolDataConfig.Any(s => s.Value.HasFlag(TestDataSeriesFlags.Realtime));

            _executorCore.StartUpdateMarshalling();

            try
            {
                if (!_control.OnStart())
                {
                    _control.Collector.AddEvent(PluginLogRecord.Types.LogSeverity.Error, "No data for requested period!");
                    return;
                }

                //if (PluginInfo.Type == AlgoTypes.Robot) // no warm-up for indicators
                //{
                //    if (!_control.WarmUp(WarmupSize, WarmupUnits))
                //        return;
                //}

                //_executor.Core.Start();


                if (PluginInfo.IsTradeBot)
                    _control.EmulateExecution(CommonSettings.WarmupSize, CommonSettings.WarmupUnits);
                else // no warm-up for indicators
                    _control.EmulateExecution(0, WarmupUnitTypes.Bars);
            }
            finally
            {
                _control.OnStop();
                _executorCore.StopUpdateMarshalling();
            }
        }

        public void Pause()
        {
            _control.Pause();
        }

        public void Resume()
        {
            _control.Resume();
        }

        public void CancelTesting()
        {
            _control.CancelEmulation();
        }

        public void SetExecDelay(int delayMs)
        {
            _control.SetExecDelay(delayMs);
        }

        public int GetSymbolHistoryBarCount(string symbol)
        {
            return _control.Collector.GetSymbolHistoryBarCount(symbol);
        }

        public IPagedEnumerator<BarData> GetSymbolHistory(string symbol, Feed.Types.Timeframe timeframe)
        {
            return _control.Collector.GetSymbolHistory(symbol, timeframe);
        }

        public IPagedEnumerator<BarData> GetEquityHistory(Feed.Types.Timeframe timeframe)
        {
            return _control.Collector.GetEquityHistory(timeframe);
        }

        public IPagedEnumerator<BarData> GetMarginHistory(Feed.Types.Timeframe timeframe)
        {
            return _control.Collector.GetMarginHistory(timeframe);
        }

        public IPagedEnumerator<Domain.TradeReportInfo> GetTradeHistory()
        {
            return _control.TradeHistory.Marshal();
        }

        public IPagedEnumerator<OutputPoint> GetOutputData(string id)
        {
            return _control.Collector.GetOutputData(id);
        }

        public void Dispose()
        {
            _control.Dispose();
        }

        public TestingStatistics GetStats()
        {
            return _control.Collector.Stats;
        }

        public void SaveResults(string resultsPath)
        {
            var descriptor = PluginInfo;
            var mainTimeframe = CommonSettings.MainTimeframe;

            using (var file = File.Open(resultsPath, FileMode.CreateNew))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
            {
                SaveJson(zip, "stats.json", _control.Collector.Stats);

                zip.CreateEntryFromFile(JournalPath, "journal.csv");
                File.Delete(JournalPath);

                foreach (var symbol in SymbolDataConfig.Keys)
                {
                    SaveCsv(zip, $"feed.{symbol}.csv", _control.Collector.LocalGetSymbolHistory(symbol, mainTimeframe));
                }

                foreach(var output in descriptor.Outputs)
                {
                    var id = output.Id;
                    SaveCsv(zip, $"output.{id}.csv", _control.Collector.LocalGetOutputData(id));
                }

                if (descriptor.IsTradeBot)
                {
                    SaveCsv(zip, "equity.csv", _control.Collector.LocalGetEquityHistory(mainTimeframe));
                    SaveCsv(zip, "margin.csv", _control.Collector.LocalGetMarginHistory(mainTimeframe));

                    SaveBase64(zip, "trade-history.bs64", _control.TradeHistory.LocalGetReports());
                }
            }
        }


        private static void SaveJson<T>(ZipArchive zip, string entryName, T data)
        {
            var entry = zip.CreateEntry(entryName);
            using (var stream = entry.Open())
            using (var writer = new Utf8JsonWriter(stream))
            {
                JsonSerializer.Serialize(writer, data);
            }
        }

        private static void SaveCsv(ZipArchive zip, string entryName, IEnumerable<BarData> data)
        {
            var entry = zip.CreateEntry(entryName);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("DateTime,Open,High,Low,Close");

                foreach (var bar in data)
                {
                    writer.WriteLine();
                    writer.Write("{0},{1},{2},{3},{4}", InvariantFormat.CsvFormat(bar.OpenTime.ToDateTime()), bar.Open, bar.High, bar.Low, bar.Close);
                }
            }
        }

        private static void SaveCsv(ZipArchive zip, string entryName, IEnumerable<OutputPoint> data)
        {
            var entry = zip.CreateEntry(entryName);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("DateTime,Index,Value");

                foreach (var point in data)
                {
                    writer.WriteLine();
                    writer.Write("{0},{1},{2},{3},{4}", InvariantFormat.CsvFormat(point.Time.ToDateTime()), point.Index, point.Value);
                }
            }
        }

        private static void SaveBase64(ZipArchive zip, string entryName, IEnumerable<TradeReportInfo> data)
        {
            var entry = zip.CreateEntry(entryName);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream))
            {
                foreach (var report in data)
                {
                    writer.Write(report.ToByteString().ToBase64());
                }
            }
        }

        #region IPluginSetupTarget

        void IPluginSetupTarget.SetParameter(string id, object value)
        {
            _executorCore.SetParameter(id, value);
        }

        T IPluginSetupTarget.GetFeedStrategy<T>()
        {
            return _executorCore.GetFeedStrategy<T>();
        }

        void IPluginSetupTarget.SetupOutput<T>(string id, bool enabled)
        {
            _control.Collector.SetupOutput<T>(id, OutputDataMode);
        }

        #endregion

        #region IPluginMetadata

        IEnumerable<Domain.SymbolInfo> IPluginMetadata.GetSymbolMetadata() => CommonSettings.Symbols.Values;
        IEnumerable<Domain.CurrencyInfo> IPluginMetadata.GetCurrencyMetadata() => CommonSettings.Currencies.Values;
        public IEnumerable<Domain.FullQuoteInfo> GetLastQuoteMetadata() => CommonSettings.Symbols.Values.Select(u => (u.LastQuote as QuoteInfo)?.GetFullQuote());

        #endregion
    }
}
