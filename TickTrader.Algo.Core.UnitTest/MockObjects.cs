﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Api;
using TickTrader.Algo.Core.Calc;

namespace TickTrader.Algo.Core.UnitTest
{
    public static class MockHelper
    {
        internal static List<BarEntity> Add(this List<BarEntity> list, DateTime openTime, double open,
            double? close = null, double? high = null, double? low = null)
        {
            var bar = new BarEntity()
            {
                OpenTime = openTime,
                Open = open,
                Close = close ?? open,
                High = high ?? open,
                Low = low ?? open
            };
            list.Add(bar);
            return list;
        }

        internal static List<BarEntity> Add(this List<BarEntity> list, string openTime, double open,
            double? close = null, double? high = null, double? low = null)
        {
            return Add(list, DateTime.Parse(openTime), open, close, high, low);
        }

        internal static Api.Quote CreateQuote(string timestamp, double bid, double? ask)
        {
            return CreateQuote(null, DateTime.Parse(timestamp), bid, ask);
        }

        internal static Api.Quote CreateQuote(string symbol, DateTime timestamp, double bid, double? ask = null)
        {
            return new QuoteEntity(symbol, timestamp, bid, ask ?? bid);
        }

        internal static void UpdateRate(this BarSeriesFixture fixture, string timestamp, double bid, double? ask = null)
        {
            fixture.Update(CreateQuote(timestamp, bid, ask));
        }
    }

    internal class MockBot : TradeBot
    {
    }

    internal class MockFixtureContext : IFixtureContext
    {
        private SubscriptionFixtureManager dispenser;
        private FeedBufferStrategy bStrategy;

        public MockFixtureContext()
        {
            dispenser = new SubscriptionFixtureManager(this);
            Builder = new PluginBuilder(new Metadata.PluginMetadata(typeof(MockBot)));
            Builder.Logger = new NullLogger();
            bStrategy = new TimeSpanStrategy(TimePeriodStart, TimePeriodEnd);
        }

        public PluginBuilder Builder { get; private set; }
        public PluginLoggerAdapter Logger => Builder.LogAdapter;
        public string MainSymbolCode { get; set; }
        public TimeFrames TimeFrame { get; set; }
        public DateTime TimePeriodEnd { get; set; }
        public DateTime TimePeriodStart { get; set; }
        public IFeedProvider FeedProvider => throw new NotImplementedException();
        public FeedBufferStrategy BufferingStrategy => bStrategy;
        public SubscriptionFixtureManager Dispenser => dispenser;

        public IAccountInfoProvider AccInfoProvider => throw new NotImplementedException();
        public ITradeExecutor TradeExecutor => throw new NotImplementedException();

        public bool IsGlobalUpdateMarshalingEnabled => false;

        public AlgoMarketState MarketData { get; } = new AlgoMarketState();

        public void EnqueueTradeUpdate(Action<PluginBuilder> action)
        {
        }

        public void EnqueueQuote(QuoteEntity update)
        {
        }

        public void EnqueueEvent(Action<PluginBuilder> action)
        {
        }

        public void EnqueueCustomInvoke(Action<PluginBuilder> action)
        {
        }

        public void ProcessNextOrderUpdate()
        {
        }

        public void OnInternalException(Exception ex)
        {
        }

        public void EnqueueUserCallback(Action<PluginBuilder> action)
        {
            throw new NotImplementedException();
        }

        public void SendExtUpdate(object update)
        {
        }
    }
}
