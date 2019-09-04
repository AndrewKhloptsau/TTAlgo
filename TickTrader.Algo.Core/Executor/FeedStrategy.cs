﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Api;
using TickTrader.Algo.Core.Infrastructure;
using TickTrader.Algo.Core.Lib;

namespace TickTrader.Algo.Core
{
    public abstract class FeedStrategy : CrossDomainObject, IFeedBuferStrategyContext, CustomFeedProvider
    {
        private MarketStateFixture _marketFixture;
        private IFeedSubscription _defaultSubscription;
        private readonly List<Action<FeedStrategy>> _setupActions = new List<Action<FeedStrategy>>();

        public FeedStrategy()
        {
        }

        internal IFixtureContext ExecContext { get; private set; }
        internal IFeedProvider Feed { get; private set; }
        internal IFeedHistoryProvider FeedHistory { get; private set; }
        internal SubscriptionFixtureManager RateDispenser => ExecContext.Dispenser;

        public abstract int BufferSize { get; }
        public abstract IFeedBuffer MainBuffer { get; }
        public abstract IEnumerable<string> BufferedSymbols { get; }

        internal abstract void OnInit();
        public FeedBufferStrategy BufferingStrategy { get; private set; }
        protected abstract BufferUpdateResult UpdateBuffers(RateUpdate update);
        protected abstract RateUpdate Aggregate(RateUpdate last, QuoteEntity quote);
        protected abstract BarSeries GetBarSeries(string symbol);
        protected abstract BarSeries GetBarSeries(string symbol, BarPriceType side);
        protected abstract FeedStrategy CreateClone();
        //protected abstract IEnumerable<Bar> QueryBars(string symbol, TimeFrames timeFrame, DateTime from, DateTime to);
        //protected abstract IEnumerable<Quote> QueryQuotes(string symbol, DateTime from, DateTime to, bool level2);

        internal void Init(IFixtureContext executor, FeedBufferStrategy bStrategy, MarketStateFixture marketFixture)
        {
            ExecContext = executor;
            _marketFixture = marketFixture;
            Feed = executor.FeedProvider;
            BufferingStrategy = bStrategy;
            RateDispenser.ClearUserSubscriptions();
            OnInit();
            BufferingStrategy.Init(this);
            _setupActions.ForEach(a => a(this));
        }

        internal virtual void Start()
        {
            InitDefaultSubscription();
            Feed.Sync.Invoke(StartStrategy);
            ExecContext.EnqueueCustomInvoke(b => LoadDataAndBuild());
            ExecContext.Builder.CustomFeedProvider = this;
        }

        internal virtual void Stop()
        {
            Feed.Sync.Invoke(StopStrategy);
            CancelDefaultSubscription();
        }

        internal FeedStrategy Clone()
        {
            var copy = CreateClone();
            copy._setupActions.AddRange(_setupActions);
            return copy;
        }

        internal void SetUserSubscription(string symbol, int depth)
        {
            RateDispenser.SetUserSubscription(symbol, depth);
        }

        protected void AddSetupAction(Action<FeedStrategy> setupAction)
        {
            _setupActions.Add(setupAction);
        }

        private void StartStrategy()
        {
            RateDispenser.Start();
            Feed.RateUpdated += Feed_RateUpdated;
            Feed.RatesUpdated += Feed_RatesUpdated;

            // apply snapshot
            //foreach (var quote in Feed.GetSnapshot())
            //    _marketFixture.UpdateRate(quote);
        }

        private void StopStrategy()
        {
            RateDispenser.Stop();
            Feed.RateUpdated -= Feed_RateUpdated;
            Feed.RatesUpdated -= Feed_RatesUpdated;
            //Feed.CancelAll();
        }

        private void LoadDataAndBuild()
        {
            BufferingStrategy.Start();

            var builder = ExecContext.Builder;
            var barCount = BufferSize;

            builder.StartBatch();

            for (int i = 0; i < barCount; i++)
            {
                builder.IncreaseVirtualPosition();
                builder.InvokeCalculate(false);
            }

            builder.StopBatch();
        }

        private void Feed_RatesUpdated(List<QuoteEntity> updates)
        {
            foreach (var update in updates)
                ExecContext.EnqueueQuote(update);
        }

        private void Feed_RateUpdated(QuoteEntity upd)
        {
            ExecContext.EnqueueQuote(upd);
        }

        private void InitDefaultSubscription()
        {
            _defaultSubscription = RateDispenser.AddSubscription(BufferedSymbols, 1);
        }

        private void CancelDefaultSubscription()
        {
            _defaultSubscription.CancelAll();
            _defaultSubscription = null;
        }

        internal BufferUpdateResult ApplyUpdate(RateUpdate update, out AlgoMarketNode node)
        {
            node = _marketFixture.UpdateRate(update);

            var result = UpdateBuffers(update);

            if (result.IsLastUpdated)
                ExecContext.Builder.InvokeCalculate(true);

            for (int i = 0; i < result.ExtendedBy; i++)
            {
                ExecContext.Builder.IncreaseVirtualPosition();
                ExecContext.Builder.InvokeCalculate(false);
            }

            RateDispenser.OnUpdateEvent(node);

            return result;
        }

        internal RateUpdate InvokeAggregate(RateUpdate last, QuoteEntity quote)
        {
            return Aggregate(last, quote);
        }

        #region IFeedStrategyContext

        //IPluginFeedProvider IFeedFixtureContext.Feed { get { return Feed; } }

        //void IFeedFixtureContext.Add(IRateSubscription subscriber)
        //{
        //    dispenser.Add(subscriber);
        //}

        //void IFeedFixtureContext.Remove(IRateSubscription subscriber)
        //{
        //    dispenser.Remove(subscriber);
        //}

        #endregion IFeedStrategyContext

        #region IFeedBufferController

        IFeedBuffer IFeedBuferStrategyContext.MainBuffer => MainBuffer;

        void IFeedBuferStrategyContext.TruncateBuffers(int bySize)
        {
            ExecContext.Builder.TruncateBuffers(bySize);
        }

        #endregion

        #region CustomFeedProvider

        BarSeries CustomFeedProvider.GetBarSeries(string symbol)
        {
            return GetBarSeries(symbol);
        }

        BarSeries CustomFeedProvider.GetBarSeries(string symbol, BarPriceType side)
        {
            return GetBarSeries(symbol, side);
        }

        IEnumerable<Bar> CustomFeedProvider.GetBars(string symbol, TimeFrames timeFrame, DateTime from, DateTime to, BarPriceType side, bool backwardOrder)
        {
            const int pageSize = 500;
            List<BarEntity> page;
            int pageIndex;

            from = from.ToUniversalTime();
            to = to.ToUniversalTime().AddMilliseconds(-1);

            if (backwardOrder)
            {
                page = FeedHistory.QueryBars(symbol, side, to, -pageSize, timeFrame);
                pageIndex = page.Count - 1;

                while (true)
                {
                    if (pageIndex < 0)
                    {
                        if (page.Count < pageSize)
                            break; //last page
                        var timeRef = page.First().OpenTime.AddMilliseconds(-1);
                        page = FeedHistory.QueryBars(symbol, side, timeRef, -pageSize, timeFrame);
                        if (page.Count == 0)
                            break;
                        pageIndex = page.Count - 1;
                    } 

                    var item = page[pageIndex];
                    if (item.OpenTime < from)
                        break;
                    pageIndex--;
                    yield return item;
                }
            }
            else
            {
                page = FeedHistory.QueryBars(symbol, side, from, pageSize, timeFrame);
                pageIndex = 0;

                while (true)
                {
                    if (pageIndex >= page.Count)
                    {
                        if (page.Count < pageSize)
                            break; //last page
                        var timeRef = page.Last().CloseTime.AddMilliseconds(1);
                        page = FeedHistory.QueryBars(symbol, side, timeRef, pageSize, timeFrame);
                        if (page.Count == 0)
                            break;
                        pageIndex = 0;
                    }

                    var item = page[pageIndex];
                    if (item.OpenTime > to)
                        break;
                    pageIndex++;
                    yield return item;
                }
            }
        }

        IEnumerable<Bar> CustomFeedProvider.GetBars(string symbol, TimeFrames timeFrame, DateTime from, int count, BarPriceType side)
        {
            const int pageSize = 500;
            List<BarEntity> page;
            int pageIndex;

            from = from.ToUniversalTime();
            var backwardOrder = count < 0;
            count = System.Math.Abs(count);

            while (count > 0)
            {
                if (backwardOrder)
                {
                    page = FeedHistory.QueryBars(symbol, side, from, -pageSize, timeFrame);
                    pageIndex = page.Count - 1;

                    while (pageIndex > 0)
                    {
                        var item = page[pageIndex];
                        pageIndex--;
                        yield return item;
                        count--;
                        if (count <= 0)
                            break;
                    }

                    if (page.Count < pageSize)
                        break; //last page
                    from = page.First().OpenTime.AddMilliseconds(-1);
                }
                else
                {
                    page = FeedHistory.QueryBars(symbol, side, from, pageSize, timeFrame);
                    pageIndex = 0;

                    while (pageIndex < page.Count)
                    {
                        var item = page[pageIndex];
                        pageIndex++;
                        yield return item;
                        count--;
                        if (count <= 0)
                            break;
                    }

                    if (page.Count < pageSize)
                        break; //last page
                    from = page.Last().CloseTime.AddMilliseconds(1);
                }
            }
        }

        IEnumerable<Quote> CustomFeedProvider.GetQuotes(string symbol, DateTime from, DateTime to, bool level2, bool backwardOrder)
        {
            const int pageSize = 500;
            List<QuoteEntity> page;
            int pageIndex;

            from = from.ToUniversalTime();
            to = to.ToUniversalTime();

            if (backwardOrder)
            {
                page = FeedHistory.QueryTicks(symbol, to, -pageSize, level2);
                pageIndex = page.Count - 1;

                while (true)
                {
                    if (pageIndex < 0)
                    {
                        if (page.Count < pageSize)
                            break; //last page
                        var timeRef = page.First().Time.AddMilliseconds(-1);
                        page = FeedHistory.QueryTicks(symbol, timeRef, -pageSize, level2);
                        if (page.Count == 0)
                            break;
                        pageIndex = page.Count - 1;
                    }

                    var item = page[pageIndex];
                    if (item.Time < from)
                        break;
                    pageIndex--;
                    yield return item;
                }
            }
            else
            {
                page = FeedHistory.QueryTicks(symbol, from, pageSize, level2);
                pageIndex = 0;

                while (true)
                {
                    if (pageIndex >= page.Count)
                    {
                        if (page.Count < pageSize)
                            break; //last page
                        var timeRef = page.Last().Time.AddMilliseconds(1);
                        page = FeedHistory.QueryTicks(symbol, timeRef, pageSize, level2);
                        if (page.Count == 0)
                            break;
                        pageIndex = 0;
                    }

                    var item = page[pageIndex];
                    if (item.Time > to)
                        break;
                    pageIndex++;
                    yield return item;
                }
            }
        }

        IEnumerable<Quote> CustomFeedProvider.GetQuotes(string symbol, DateTime from, int count, bool level2)
        {
            const int pageSize = 500;
            List<QuoteEntity> page;
            int pageIndex;

            from = from.ToUniversalTime();
            var backwardOrder = count < 0;
            count = System.Math.Abs(count);

            while (count > 0)
            {
                if (backwardOrder)
                {
                    page = FeedHistory.QueryTicks(symbol, from, -pageSize, level2);
                    pageIndex = page.Count - 1;

                    while (pageIndex > 0)
                    {
                        var item = page[pageIndex];
                        pageIndex--;
                        yield return item;
                        count--;
                        if (count <= 0)
                            break;
                    }

                    if (page.Count < pageSize)
                        break; //last page
                    from = page.First().Time.AddMilliseconds(-1);
                }
                else
                {
                    page = FeedHistory.QueryTicks(symbol, from, pageSize, level2);
                    pageIndex = 0;

                    while (pageIndex < page.Count)
                    {
                        var item = page[pageIndex];
                        pageIndex++;
                        yield return item;
                        count--;
                        if (count <= 0)
                            break;
                    }

                    if (page.Count < pageSize)
                        break; //last page
                    from = page.Last().Time.AddMilliseconds(1);
                }
            }
        }

        void CustomFeedProvider.Subscribe(string symbol, int depth)
        {
            RateDispenser.SetUserSubscription(symbol, depth);
        }

        void CustomFeedProvider.Unsubscribe(string symbol)
        {
            RateDispenser.RemoveUserSubscription(symbol);
        }

        #endregion
    }
}
