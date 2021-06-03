﻿using ActorSharp;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Domain;
using TickTrader.FeedStorage;

namespace TickTrader.BotTerminal
{
    internal class FeedExportViewModel : Screen, IWindowModel
    {
        //private FeedCacheKey _key;
        private SymbolStorageSeries _series;
        private List<FeedExporter> _exporters = new List<FeedExporter>();
        private FeedExporter _selectedExporter;
        //private FeedCache.Handler _storage;
        private bool _isRangeLoaded;
        private bool _showDownloadUi;
        private CancellationTokenSource _cancelExportSrc;
        private Task _exportTask;

        public FeedExportViewModel(SymbolStorageSeries series)
        {
            _series = series;
            var key = series.Key;
            //_storage = diskStorage;

            _exporters.Add(new CsvExporter());

            DisplayName = string.Format("Export Series: {0} {1} {2}", key.Symbol, key.Frame, key.MarketSide);
            
            DateRange = new DateRangeSelectionViewModel();
            ExportObserver = new ProgressViewModel();

            SelectedExporter = _exporters[0];
            UpdateAvailableRange();
        }

        public IEnumerable<FeedExporter> Exporters => _exporters;
        public DateRangeSelectionViewModel DateRange { get; }
        public ProgressViewModel ExportObserver { get; }
        public bool CanExport { get; private set; }
        public bool CanCancel { get; private set; }
        public bool IsExporting => _cancelExportSrc != null;
        public bool IsCancelling => _cancelExportSrc?.IsCancellationRequested ?? false;
        public bool IsInputEnabled => !IsExporting && IsRangeLoaded;

        #region Observable Properties

        public bool ShowDownloadUi
        {
            get => _showDownloadUi;
            set
            {
                if (_showDownloadUi != value)
                {
                    _showDownloadUi = value;
                    NotifyOfPropertyChange(nameof(ShowDownloadUi));
                }
            }
        }

        public bool IsRangeLoaded
        {
            get => _isRangeLoaded;
            set
            {
                if (_isRangeLoaded != value)
                {
                    _isRangeLoaded = value;
                    NotifyOfPropertyChange(nameof(IsRangeLoaded));
                    UpdateState();
                }
            }
        }

        public FeedExporter SelectedExporter
        {
            get => _selectedExporter;
            set
            {
                if (_selectedExporter != value)
                {
                    if (SelectedExporter != null)
                        _selectedExporter.PropertyChanged -= _selectedExporter_PropertyChanged;
                    _selectedExporter = value;
                    _selectedExporter.PropertyChanged += _selectedExporter_PropertyChanged;
                    NotifyOfPropertyChange(nameof(SelectedExporter));
                    UpdateState();
                }
            }
        }

        private void _selectedExporter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateState();
        }

        #endregion Observable Properties

        public void Export()
        {
            _exportTask = DoExport();
        }

        public async void Cancel()
        {
            if (IsExporting)
            {
                _cancelExportSrc.Cancel();
                UpdateState();
                await _exportTask;
            }
            else
                await TryCloseAsync();
        }

        public void Dispose()
        {
        }

        private void UpdateState()
        {
            CanExport = DateRange.From != null && DateRange.To != null && !IsExporting && SelectedExporter.CanExport;
            CanCancel = !IsCancelling;
            NotifyOfPropertyChange(nameof(CanExport));
            NotifyOfPropertyChange(nameof(CanCancel));
            NotifyOfPropertyChange(nameof(IsInputEnabled));
        }

        private async Task DoExport()
        {
            _cancelExportSrc = new CancellationTokenSource();
            ShowDownloadUi = true;
            UpdateState();

            try
            {
                var from = DateRange.From;
                var to = DateRange.To + TimeSpan.FromDays(1);

                await Actor.Spawn(async ()=> 
                {
                    ExportObserver.StartProgress(from.GetAbsoluteDay(), to.GetAbsoluteDay());

                    var exporter = SelectedExporter;

                    exporter.StartExport();

                    try
                    {
                        if (!_series.Key.Frame.IsTicks())
                        {
                            var i = _series.IterateBarCache(from, to);

                            while (await i.ReadNext())
                            {
                                var slice = i.Current;
                                exporter.ExportSlice(slice.From, slice.To, slice.Content);
                                ExportObserver.SetProgress(slice.To.GetAbsoluteDay());
                            }
                        }
                        else
                        {
                            var i = _series.IterateTickCache(from, to);

                            while (await i.ReadNext())
                            {
                                var slice = i.Current;
                                exporter.ExportSlice(slice.From, slice.To, slice.Content);
                                ExportObserver.SetProgress(slice.To.GetAbsoluteDay());
                            }
                        }
                    }
                    finally
                    {
                        exporter.EndExport();
                    }
                });
            }
            catch (Exception ex)
            {
                ExportObserver.SetMessage("Error:" + ex.Message);
            }
            finally
            {
                _cancelExportSrc = null;
                UpdateState();
            }

            await TryCloseAsync();
        }

        private async void UpdateAvailableRange()
        {
            IsRangeLoaded = false;
            DateRange.Reset();

            var key = _series.Key;
            var range = await _series.Symbol.GetAvailableRange(key.Frame, key.MarketSide);

            DateTime from = DateTime.UtcNow.Date;
            DateTime to = from;

            if (range.Item1 != null && range.Item2 != null)
            {
                from = range.Item1.Value;
                to = range.Item2.Value;
            }

            DateRange.UpdateBoundaries(from, to);
            IsRangeLoaded = true;
        }
    }

    internal abstract class FeedExporter : ObservableObject
    {
        private bool _valid;

        public FeedExporter(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public FeedCacheKey CollectionKey { get; private set; }

        public bool CanExport
        {
            get => _valid;
            set
            {
                if (_valid != value)
                {
                    _valid = value;
                    NotifyOfPropertyChange(nameof(CanExport));
                }
            }
        }

        public virtual void StartExport() { }
        public abstract void ExportSlice(DateTime from, DateTime to, ArraySegment<BarData> values);
        public abstract void ExportSlice(DateTime from, DateTime to, ArraySegment<QuoteInfo> values);
        public virtual void EndExport() { }

        public virtual void Init(FeedCacheKey key)
        {
            CollectionKey = key;
        }
    }

    internal class CsvExporter : FeedExporter
    {
        private StreamWriter _writer;
        private string _path;

        public CsvExporter() : base("CSV")
        {
        }

        public string FilePath
        {
            get => _path;
            set
            {
                _path = value;
                CanExport = !string.IsNullOrWhiteSpace(_path);
            }
        }

        private string GetCorrectPath()
        {
            if (!Path.HasExtension(FilePath))
                return FilePath + ".csv";
            return FilePath;
        }

        public override void StartExport()
        {
            _writer = new StreamWriter(File.Open(GetCorrectPath(), FileMode.Create));
        }

        public override void ExportSlice(DateTime from, DateTime to, ArraySegment<BarData> values)
        {
            foreach (var val in values)
            {
                _writer.Write(val.OpenTime.ToDateTime());
                _writer.Write(",");
                _writer.Write(val.Open);
                _writer.Write(",");
                _writer.Write(val.High);
                _writer.Write(",");
                _writer.Write(val.Low);
                _writer.Write(",");
                _writer.Write(val.Close);
                _writer.Write(",");
                _writer.WriteLine(val.RealVolume);
            }
        }

        public override void ExportSlice(DateTime from, DateTime to, ArraySegment<QuoteInfo> values)
        {
            foreach (var val in values)
            {
                _writer.Write(val.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                var bids = val.Bids;
                var asks = val.Asks;

                for (int i = 0; i < Math.Max(bids.Length, asks.Length); i++)
                {
                    if (i < bids.Length)
                    {
                        _writer.Write(",");
                        _writer.Write(bids[i].Price);
                        _writer.Write(",");
                        _writer.Write(bids[i].Amount);
                    }
                    else
                        _writer.Write(",,");

                    if (i < asks.Length)
                    {
                        _writer.Write(",");
                        _writer.Write(asks[i].Price);
                        _writer.Write(",");
                        _writer.Write(asks[i].Amount);
                    }
                    else
                        _writer.Write(",,");
                }

                _writer.WriteLine();
            }
        }

        public override void EndExport()
        {
            try
            {
                _writer.Close();
            }
            catch (Exception) { }
        }
    }
}
