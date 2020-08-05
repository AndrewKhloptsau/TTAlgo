﻿using System;
using System.Collections.Generic;
using TickTrader.Algo.Domain;

namespace TickTrader.Algo.Core
{
    internal class FeedQueue
    {
        private Queue<IRateInfo> _queue = new Queue<IRateInfo>();
        private Dictionary<string, IRateInfo> _lasts = new Dictionary<string, IRateInfo>();
        private FeedStrategy _fStrategy;

        public FeedQueue(FeedStrategy fStrategy)
        {
            _fStrategy = fStrategy;
        }

        public int Count { get { return _queue.Count; } }

        public void Enqueue(IRateInfo rate)
        {
            if (rate is QuoteInfo)
                Enqueue((QuoteInfo)rate);
            else if (rate is BarRateUpdate)
                Enqueue((BarRateUpdate)rate);
            else
                throw new Exception("Unsupported implementation of RateUpdate!");
        }

        public void Enqueue(BarRateUpdate bars)
        {
            _queue.Enqueue(bars);
            _lasts[bars.Symbol] = bars;
        }

        public void Enqueue(QuoteInfo quote)
        {
            _lasts.TryGetValue(quote.Symbol, out var last);
            var newUpdate = _fStrategy.InvokeAggregate(last, quote);
            if (newUpdate != null)
            {
                _lasts[quote.Symbol] = newUpdate;
                _queue.Enqueue(newUpdate);
            }
        }

        public IRateInfo Dequeue()
        {
            var update = _queue.Dequeue();
            var last = _lasts[update.Symbol];
            if (update == last)
                _lasts.Remove(update.Symbol);
            return update;
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}
