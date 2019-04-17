﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TickTrader.Algo.Core
{
    internal struct TimeEvent : IComparable<TimeEvent>
    {
        public TimeEvent(DateTime time, bool isTrade, object content)
        {
            Time = time;
            IsTrade = isTrade;
            Content = content;
        }

        public DateTime Time { get; }
        public bool IsTrade { get; }
        public object Content { get; }

        public int CompareTo(TimeEvent other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    internal interface ITimeEventSeries
    {
        DateTime NextOccurrance { get; }
        TimeEvent Take();
    }

    internal class TimeSeriesAggregator
    {
        private List<ITimeEventSeries> _seriesList = new List<ITimeEventSeries>();

        public void Add(ITimeEventSeries series)
        {
            _seriesList.Add(series);
        }

        public TimeEvent Dequeue()
        {
            var nextSeries = _seriesList.MinBy(s => s.NextOccurrance);
            return nextSeries.Take();
        }

        //public bool TryDequeue(out TimeEvent nextEvent)
        //{
        //    if (_seriesList.Count == 0)
        //    {
        //        nextEvent = default(TimeEvent);
        //        return false;
        //    }

        //    var nextSeries = _seriesList.MinBy(s => s.NextOccurrance);
        //    nextEvent = nextSeries.Take();
        //    CheckSeriesState(nextSeries);
        //    return true;
        //}

        //private void CheckSeriesState(ITimeEventSeries series)
        //{
        //    if (series.IsCompleted)
        //        _seriesList.Remove(series);

        //    if (_seriesList.All(s => !s.IsMandatory))
        //        _seriesList.Clear(); // the end
        //}
    }
}