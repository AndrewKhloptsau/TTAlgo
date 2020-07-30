﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TickTrader.Algo.Api;
using TickTrader.Algo.Core.Calc;
using TickTrader.Algo.Domain;

namespace TickTrader.Algo.Core
{
    public class AccountAccessor : AccountDataProvider, IMarginAccountInfo2, ICashAccountInfo2
    {
        private readonly PluginBuilder _builder;
        private Dictionary<string, OrderFilteredCollection> bySymbolFilterCache;
        private Dictionary<string, OrderFilteredCollection> byTagFilterCache;
        private Dictionary<Predicate<Order>, OrderFilteredCollection> customFilterCache;
        private OrdersCollection _orders;
        private PositionCollection _positions;
        private AssetsCollection _assets;
        private bool _blEventsEnabled;
        private decimal _balance;
        private string _balanceCurrency;
        private string _id;
        private AccountInfo.Types.Type _type;
        private AccountTypes _apiType;

        public AccountAccessor(PluginBuilder builder)
        {
            _builder = builder;
        }

        public OrdersCollection Orders
        {
            get
            {
                OnTradeInfoAccess();
                return _orders;
            }
        }

        public PositionCollection NetPositions
        {
            get
            {
                OnTradeInfoAccess();
                return _positions;
            }
        }

        public AssetsCollection Assets
        {
            get
            {
                OnCalcAccess();
                return _assets;
            }
        }

        public TradeHistory HistoryProvider { get; set; }

        public string Id
        {
            get
            {
                OnTradeInfoAccess();
                return _id;
            }
            set
            {
                _id = value;
            }
        }
        public decimal Balance
        {
            get
            {
                OnTradeInfoAccess();
                return _balance;
            }
            set
            {
                _balance = value;
            }
        }
        public string BalanceCurrency
        {
            get
            {
                OnTradeInfoAccess();
                return _balanceCurrency;
            }
            private set
            {
                _balanceCurrency = value;
            }
        }
        public Currency BalanceCurrencyInfo { get; private set; }
        public int Leverage { get; internal set; }
        public AccountInfo.Types.Type Type
        {
            get
            {
                OnTradeInfoAccess();
                return _type;
            }
            internal set
            {
                _type = value;
                _apiType = value.ToApiEnum();
            }
        }
        public bool Isolated { get; set; }
        public string InstanceId { get; internal set; }
        public NumberFormatInfo BalanceCurrencyFormat { get; private set; }

        public bool IsMarginType => Type == AccountInfo.Types.Type.Net || Type == AccountInfo.Types.Type.Gross;
        public bool IsCashType => Type == AccountInfo.Types.Type.Cash;

        double IMarginAccountInfo2.Balance => (double)Balance;

        AccountTypes AccountDataProvider.Type
        {
            get
            {
                OnTradeInfoAccess();
                return _apiType;
            }
        }

        public void Update(Domain.AccountInfo info, Dictionary<string, Currency> currencies)
        {
            Id = info.Id;
            Type = info.Type;
            Leverage = info.Leverage;
            Balance = (decimal)info.Balance;
            UpdateCurrency(currencies.GetOrStub(info.BalanceCurrency));
            Assets.Clear();
            foreach (var asset in info.Assets)
                _builder.Account.Assets.Update(asset, currencies);
        }

        internal void UpdateCurrency(Currency currency)
        {
            BalanceCurrency = currency.Name;
            BalanceCurrencyInfo = currency;
            BalanceCurrencyFormat = ((CurrencyEntity)currency).Format;
        }

        internal void ResetCurrency()
        {
            BalanceCurrency = "";
            BalanceCurrencyInfo = null;
            BalanceCurrencyFormat = null;
        }

        internal void FireBalanceUpdateEvent()
        {
            _builder.InvokePluginMethod((b, p) => p.BalanceUpdated(), this);
        }

        internal void FireBalanceDividendEvent(BalanceDividendEventArgsImpl args)
        {
            _builder.InvokePluginMethod((b, p) => p.BalanceDividend(args), this);
        }

        internal void FireResetEvent()
        {
            _builder.InvokePluginMethod((b, p) => p.Reset(), this);
        }

        public OrderList OrdersByTag(string orderTag)
        {
            if (string.IsNullOrEmpty(orderTag))
                throw new ArgumentException("Order tag cannot be null or empty string!");

            if (byTagFilterCache == null)
                byTagFilterCache = new Dictionary<string, OrderFilteredCollection>();

            OrderFilteredCollection collection;
            if (byTagFilterCache.TryGetValue(orderTag, out collection))
                return collection;

            collection = new OrderFilteredCollection(Orders, o => o.Comment == orderTag);
            byTagFilterCache.Add(orderTag, collection);
            return collection;
        }

        public OrderList OrdersBySymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException("Symbol cannot be null or empty string!");

            if (bySymbolFilterCache == null)
                bySymbolFilterCache = new Dictionary<string, OrderFilteredCollection>();

            OrderFilteredCollection collection;
            if (bySymbolFilterCache.TryGetValue(symbol, out collection))
                return collection;

            collection = new OrderFilteredCollection(Orders, o => o.Symbol == symbol);
            bySymbolFilterCache.Add(symbol, collection);
            return collection;
        }

        public OrderList OrdersBy(Predicate<Order> customCondition)
        {
            if (customCondition == null)
                throw new ArgumentNullException("customCondition");

            if (customFilterCache == null)
                customFilterCache = new Dictionary<Predicate<Order>, OrderFilteredCollection>();

            OrderFilteredCollection collection;
            if (customFilterCache.TryGetValue(customCondition, out collection))
                return collection;

            collection = new OrderFilteredCollection(Orders, customCondition);
            customFilterCache.Add(customCondition, collection);
            return collection;
        }

        OrderList AccountDataProvider.Orders
        {
            get
            {
                if (!Isolated)
                    return Orders;
                else
                    return OrdersBy(TagFilter);
            }
        }

        private bool TagFilter(Order order)
        {
            return InstanceId == order.InstanceId;
        }

        AssetList AccountDataProvider.Assets { get { return Assets.AssetListImpl; } }
        NetPositionList AccountDataProvider.NetPositions => NetPositions;
        TradeHistory AccountDataProvider.TradeHistory => HistoryProvider;

        internal MarginAccountCalculator MarginCalc { get; set; }
        public double Equity => GetCalc()?.Equity ?? double.NaN;
        public double Margin => GetCalc()?.Margin ?? double.NaN;
        public double MarginLevel => GetCalc()?.MarginLevel ?? double.NaN;
        public double Profit => GetCalc()?.Profit ?? double.NaN;
        public double Commision => (double?)GetCalc()?.Commission ?? double.NaN;

        double AccountDataProvider.Balance => (double)Balance;

        #region Lazy init

        internal event Action CalcRequested;
        internal event Action TradeInfoRequested;

        private MarginAccountCalculator GetCalc()
        {
            OnCalcAccess();
            return MarginCalc;
        }

        private void OnCalcAccess()
        {
            if (MarginCalc == null)
            {
                OnTradeInfoAccess();
                CalcRequested?.Invoke();
            }
        }

        internal void OnTradeInfoAccess()
        {
            if (_orders == null)
            {
                _orders = new OrdersCollection(_builder);
                _positions = new PositionCollection(_builder);
                _assets = new AssetsCollection(_builder);
                TradeInfoRequested?.Invoke();
            }
        }

        internal void Init(IAccountInfoProvider dataProvider, Dictionary<string, Currency> currencies)
        {
            var accInfo = dataProvider.GetAccountInfo();

            _orders.Clear();
            _positions.Clear();
            _assets.Clear();

            Update(accInfo, currencies);

            foreach (var order in dataProvider.GetOrders())
                _orders.Add(order);
            foreach (var position in dataProvider.GetPositions())
                _positions.UpdatePosition(position);
        }

        #endregion

        IEnumerable<IOrderInfo> IAccountInfo2.Orders => Orders.Values;
        IEnumerable<IPositionInfo> IMarginAccountInfo2.Positions => NetPositions.Values.Select(u => (IPositionInfo)u.Info);
        IEnumerable<IAssetInfo> ICashAccountInfo2.Assets => Assets;

        public event Action<IOrderInfo> OrderAdded = delegate { };
        public event Action<IEnumerable<IOrderInfo>> OrdersAdded { add { } remove { } }
        public event Action<IOrderInfo> OrderRemoved = delegate { };
        //public event Action<IOrderModel2> OrderReplaced = delegate { };
        public event Action BalanceUpdated = delegate { };
        public event Action<IBalanceDividendEventArgs> BalanceDividend = delegate { };
        public event Action Reset = delegate { };
        public event Action<IPositionInfo> PositionChanged;
        public event Action<IAssetInfo, AssetChangeType> AssetsChanged;
        public event Action<IPositionInfo> PositionRemoved;

        internal void EnableBlEvents()
        {
            Orders.AddedInfo += OnOrderAdded; //restore events
            ////Orders.Replaced += OnOrderReplaced;
            Orders.RemovedInfo += OnOrderRemoved;

            NetPositions.PositionUpdated += OnPositionUpdated;
            //NetPositions.PositionRemoved += OnPositionRemoved;

            Assets.AssetChanged += OnAssetsChanged;

            _blEventsEnabled = true;
        }

        internal void DisableBlEvents()
        {
            if (_blEventsEnabled)
            {
                Orders.AddedInfo -= OnOrderAdded; //restore events
                ////Orders.Replaced -= OnOrderReplaced;
                Orders.RemovedInfo -= OnOrderRemoved;

                NetPositions.PositionUpdated -= OnPositionUpdated;
                //NetPositions.PositionRemoved -= OnPositionRemoved;

                Assets.AssetChanged -= OnAssetsChanged;

                _blEventsEnabled = false;
            }
        }

        private void OnOrderAdded(IOrderInfo order)
        {
            UpdateAccountInfo("Add order", () => OrderAdded?.Invoke(order));
        }

        //private void OnOrderReplaced(IOrderModel2 order)
        //{
        //    UpdateAccountInfo("Replace order", () => OrderReplaced?.Invoke(order));
        //}

        private void OnOrderRemoved(IOrderInfo order)
        {
            UpdateAccountInfo("Remove order", () => OrderRemoved?.Invoke(order));
        }

        private void OnPositionUpdated(IPositionInfo position)
        {
            UpdateAccountInfo("Update position", () => PositionChanged?.Invoke(position));
        }

        //private void OnPositionRemoved(IPositionModel2 position)
        //{
        //    UpdateAccountInfo("Remove position", () => PositionChanged?.Invoke(position, PositionChageTypes.Removed));
        //}

        private void OnAssetsChanged(IAssetInfo asset, AssetChangeType type)
        {
            UpdateAccountInfo($"Change asset({type})", () => AssetsChanged?.Invoke(asset, type));
        }

        private void UpdateAccountInfo(string actionName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _builder.Logger.OnError($"Account calculator: {actionName} failed", ex);
            }
        }

        #region Emulation

        internal OrderAccessor GetOrderOrThrow(string orderId)
        {
            return Orders.GetOrderOrNull(orderId)
                ?? throw new OrderValidationError("Order Not Found " + orderId, OrderCmdResultCodes.OrderNotFound);
        }

        //internal void IncreasePosition(string symbol, decimal amount, decimal price, Domain.OrderInfo.Types.Side side, Func<string> idGenerator)
        //{
        //    var pos = NetPositions.GetOrCreatePosition(symbol, idGenerator);
        //    pos.Increase(amount, price, side);
        //    OnPositionUpdated(pos.Info);
        //}

        internal void IncreaseAsset(string currency, decimal byAmount)
        {
            var asset = Assets.GetOrCreateAsset(currency, out var chType);
            asset.IncreaseBy(byAmount);
            OnAssetsChanged(asset, chType);
        }

        #endregion

        public double? GetSymbolMargin(string symbol, OrderSide side)
        {
            return _builder.Calculator?.GetSymbolMargin(symbol, side.ToCoreEnum());
        }

        public double? CalculateOrderMargin(string symbol, OrderType type, OrderSide side, double volume, double? maxVisibleVolume, double? price, double? stopPrice, double? sl = null, double? tp = null, Api.OrderExecOptions options = Api.OrderExecOptions.None)
        {
            var symbolAccessor = _builder?.Symbols?.GetOrDefault(symbol);
            if (symbolAccessor != null && _builder.Calculator != null)
            {
                var amount = volume * symbolAccessor.ContractSize;

                return _builder.Calculator.CalculateOrderMargin(symbolAccessor, amount, price, stopPrice, type.ToCoreEnum(), side.ToCoreEnum(), OrderEntity.IsHiddenOrder(maxVisibleVolume));
            }
            return null;
        }

        public bool HasEnoughMarginToOpenOrder(string symbol, OrderType type, OrderSide side, double volume, double? maxVisibleVolume, double? price, double? stopPrice, double? sl = null, double? tp = null, Api.OrderExecOptions options = Api.OrderExecOptions.None)
        {
            var symbolAccessor = _builder?.Symbols?.GetOrDefault(symbol);
            if (symbolAccessor != null && _builder.Calculator != null)
            {
                var amount = volume * symbolAccessor.ContractSize;

                return _builder.Calculator.HasEnoughMarginToOpenOrder(symbolAccessor, amount, type.ToCoreEnum(), side.ToCoreEnum(), price, stopPrice, OrderEntity.IsHiddenOrder(maxVisibleVolume), out _);
            }
            return false;
        }
    }
}
