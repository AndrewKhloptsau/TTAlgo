﻿using System;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.Core
{
    [Serializable]
    public class BarSequenceBuilder : ITimeSequenceRef
    {
        private readonly BarSampler _sampler;
        private BarEntity _currentBar;
        private TimeFrames _timeframe;

        private static int IdSeed;
        private int Id;

        private BarSequenceBuilder(TimeFrames timeframe)
        {
            Id = System.Threading.Interlocked.Increment(ref IdSeed);

            _timeframe = timeframe;
            _sampler = BarSampler.Get(timeframe);
        }

        public static BarSequenceBuilder Create(TimeFrames timeframe)
        {
            return new BarSequenceBuilder(timeframe);
        }

        public static BarSequenceBuilder Create(ITimeSequenceRef master)
        {
            return new Slave(master);
        }

        public event Action<BarEntity> BarOpened;
        public event Action<BarEntity> BarClosed;
        public event Action<BarEntity> BarUpdated;
        public BarSampler Sampler => _sampler;
        public TimeFrames TimeFrame => _timeframe;
        public DateTime? TimeEdge => _currentBar?.OpenTime;

        public void AppendBar(BarEntity bar)
        {
            var boundaries = _sampler.GetBar(bar.OpenTime);

            if (_currentBar != null && _currentBar.OpenTime >= boundaries.Open)
                throw new ArgumentException("Invalid time sequnce!");

            if (boundaries.Open == bar.OpenTime || boundaries.Close != bar.CloseTime)
                throw new ArgumentException("Bar has invalid time boundaries!");

            Append(bar);
        }

        public BarEntity AppendQuote(DateTime time, double price, double volume)
        {
            //if (_currentBar != null && _currentBar.OpenTime > time)
            //    throw new ArgumentException("Invalid time sequnce!");

            if (_currentBar != null)
            {
                if (time < _currentBar.OpenTime)
                    throw new ArgumentException("Invalid time sequnce!");

                if (time < _currentBar.CloseTime)
                {
                    // append last bar
                    _currentBar.Append(price, volume);
                    BarUpdated?.Invoke(_currentBar);
                    return null; ;
                }
            }

            // add new bar
            var boundaries = _sampler.GetBar(time);
            var newBar = new BarEntity(boundaries.Open, boundaries.Close, price, volume);
            return Append(newBar);
        }

        public BarEntity AppendBarPart(DateTime time, double open, double high, double low, double close, double volume)
        {
            var boundaries = _sampler.GetBar(time);

            if (_currentBar != null && _currentBar.OpenTime > boundaries.Open)
                throw new ArgumentException("Invalid time sequnce!");

            if (_currentBar != null && _currentBar.OpenTime == boundaries.Open)
            {
                // join
                _currentBar.AppendPart(open, high, low, close, volume);
                BarUpdated?.Invoke(_currentBar);
            }
            else
            {
                // append
                var entity = new BarEntity();
                entity.OpenTime = boundaries.Open;
                entity.CloseTime = boundaries.Close;
                entity.Open = open;
                entity.High = high;
                entity.Low = low;
                entity.Close = close;
                entity.Volume = volume;
                return Append(entity);
            }

            return null;
        }

        public BarEntity CloseSequence()
        {
            BarEntity closedBar = null;
            if (_currentBar != null)
            {
                BarClosed?.Invoke(_currentBar);
                closedBar = _currentBar;
                _currentBar = null;
                
            }
            return closedBar;
        }

        protected virtual BarEntity Append(BarEntity newBar)
        {
            BarEntity closedBar = null;
            if (_currentBar != null)
            {
                BarClosed?.Invoke(_currentBar);
                closedBar = _currentBar;
            }
            _currentBar = newBar;
            OnBarOpened(newBar);
            return closedBar;
        }

        protected void OnBarOpened(BarEntity newBar)
        {
            BarOpened?.Invoke(newBar);
        }

        [Serializable]
        private class Slave : BarSequenceBuilder
        {
            private ITimeSequenceRef _master;

            public Slave(ITimeSequenceRef masterSequence)
                : base(masterSequence.TimeFrame)
            {
                _master = masterSequence;
                //_master.BarOpened += MasterSequence_Appended;
                _master.BarOpened += _master_BarOpened;
                _master.BarClosed += _master_BarClosed;
            }

            private void _master_BarClosed(BarEntity closedBar)
            {
                var masterBarTime = closedBar.OpenTime;

                if (_currentBar == null || _currentBar.OpenTime > masterBarTime)
                {
                    // fill with Nan
                    var fillBoundaries = _sampler.GetBar(masterBarTime);
                    var fillBar = new BarEntity(fillBoundaries.Open, fillBoundaries.Close, double.NaN, 0);
                    base.OnBarOpened(fillBar);
                }
                else if (_currentBar.OpenTime < masterBarTime)
                {
                    // fill with previos
                    var fillBoundaries = _sampler.GetBar(masterBarTime);
                    var fillBar = new BarEntity(fillBoundaries.Open, fillBoundaries.Close, _currentBar.Close, 0);
                    base.OnBarOpened(fillBar);
                }
            }

            private void _master_BarOpened(BarEntity openedBar)
            {
                if (_currentBar != null && _currentBar.OpenTime == openedBar.OpenTime)
                    base.Append(_currentBar); // accept current bar
            }

            private bool IsSync(BarEntity someBar) => _master.TimeEdge == someBar.OpenTime;

            protected override BarEntity Append(BarEntity newBar)
            {
                var masterTime = _master.TimeEdge;

                if (IsSync(newBar))
                    return base.Append(newBar);
                else
                {
                    _currentBar = newBar;
                    return null;
                }
            }

            //private void MasterSequence_Appended()
            //{
            //    var masterTime = _master.TimeEdge.Value;

            //    if (_currentBar != null)
            //    {
            //        if (_currentBar.OpenTime > masterTime)
            //            throw new Exception("Synchronization with master sequence is broken!");
            //        else if (_currentBar.OpenTime == masterTime)
            //        {
            //            // accept current bar as synchronized
            //            base.Append(_currentBar);
            //            return;
            //        }
            //    }

            //    // close current bar & create empty
            //    var newBoundaries = _sampler.GetBar(masterTime);
            //    var newBar = new BarEntity(newBoundaries.Open, newBoundaries.Close, _currentBar?.Close ?? double.NaN, 0);
            //    base.Append(newBar);
            //}
        }

        public override string ToString()
        {
            return $"Builder {Id}";
        }
    }

    public interface ITimeSequenceRef
    {
        TimeFrames TimeFrame { get; }
        DateTime? TimeEdge { get; }
        event Action<BarEntity> BarOpened;
        event Action<BarEntity> BarClosed;
    }
}
