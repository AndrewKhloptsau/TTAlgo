﻿using System;
using System.Collections.Generic;
using System.Linq;
using TickTrader.Algo.Core.Calc.Conversion;
using TickTrader.Algo.Core.Infrastructure;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Domain;

namespace TickTrader.Algo.Core.Calc
{
    public abstract class MarketStateBase
    {
        private Dictionary<Tuple<string, string>, OrderCalculator> _orderCalculators = new Dictionary<Tuple<string, string>, OrderCalculator>();
        private Dictionary<string, ICurrencyInfo> _currenciesByName = new Dictionary<string, ICurrencyInfo>();

        public IFeedSubscription Subscriptions { get; set; }

        public MarketStateBase()
        {
            Conversion = new ConversionManager(this);
        }

        public IEnumerable<ISymbolInfo> Symbols { get; private set; }
        public IEnumerable<ICurrencyInfo> Currencies { get; private set; }

        public ConversionManager Conversion { get; }

        public void Init(IEnumerable<ISymbolInfo> symbolList, IEnumerable<ICurrencyInfo> currencyList)
        {
            Currencies = currencyList.ToList();
            _currenciesByName = currencyList.ToDictionary(c => c.Name);
            CurrenciesChanged?.Invoke();

            Symbols = symbolList.ToList();
            InitNodes();
            SymbolsChanged?.Invoke();

            Conversion.Init();
        }

        protected abstract void InitNodes();

        internal abstract SymbolMarketNode GetSymbolNodeInternal(string smb);

        public ICurrencyInfo GetCurrencyOrThrow(string name)
        {
            var result = _currenciesByName.GetOrDefault(name);
            if (result == null)
                throw new MarketConfigurationException("Currency Not Found: " + name);
            return result;
        }

        public event Action SymbolsChanged;
        public event Action CurrenciesChanged;

        internal OrderCalculator GetCalculator(string symbol, IMarginAccountInfo2 account)
        {
            var key = Tuple.Create(symbol, account.BalanceCurrency);

            OrderCalculator calculator;
            if (!_orderCalculators.TryGetValue(key, out calculator))
            {
                var tracker = GetSymbolNodeInternal(symbol);
                if (tracker != null)
                {
                    calculator = new OrderCalculator(tracker, Conversion, account);
                    _orderCalculators.Add(key, calculator);
                }
            }
            return calculator;
        }
    }

    public class MarketState : MarketStateBase
    {
        private readonly Dictionary<string, SymbolMarketNode> _smbMap = new Dictionary<string, SymbolMarketNode>();

        public void Update(IRateInfo rate)
        {
            var tracker = GetSymbolNodeOrNull(rate.Symbol);
            tracker.Update(rate);
        }

        public void Update(IEnumerable<IRateInfo> rates)
        {
            if (rates == null)
                return;

            foreach (IRateInfo rate in rates)
                Update(rate);
        }

        internal SymbolMarketNode GetSymbolNodeOrNull(string symbol)
        {
            return _smbMap.GetOrDefault(symbol);
        }

        protected override void InitNodes()
        {
            _smbMap.Clear();

            foreach (var smb in Symbols)
                _smbMap.Add(smb.Name, new SymbolMarketNode(smb));
        }

        internal override SymbolMarketNode GetSymbolNodeInternal(string smb)
        {
            return GetSymbolNodeOrNull(smb);
        }
    }

    public class AlgoMarketState : MarketStateBase
    {
        private readonly Dictionary<string, AlgoMarketNode> _smbMap = new Dictionary<string, AlgoMarketNode>();

        public AlgoMarketState()
        {
        }

        public IEnumerable<AlgoMarketNode> Nodes => _smbMap.Values;

        protected override void InitNodes()
        {
            _smbMap.Clear();

            foreach (var smb in Symbols)
                _smbMap.Add(smb.Name, new AlgoMarketNode(smb));
        }

        public AlgoMarketNode GetSymbolNodeOrNull(string symbol)
        {
            return _smbMap.GetOrDefault(symbol);
        }

        internal override SymbolMarketNode GetSymbolNodeInternal(string smb)
        {
            return GetSymbolNodeOrNull(smb);
        }

        public void UpdateRate(IRateInfo newRate, out AlgoMarketNode node)
        {
            node = GetSymbolNodeOrNull(newRate.Symbol);
            if (node != null)
            {
                node.SymbolInfo.UpdateRate(newRate.LastQuote);
                node.Update(newRate);
            }
        }
    }
}
