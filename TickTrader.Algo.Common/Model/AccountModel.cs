using System;
using System.Collections.Generic;
using System.Linq;
using TickTrader.Algo.Core;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Api;
using Machinarium.Qnil;
using TickTrader.Algo.Common.Lib;

namespace TickTrader.Algo.Common.Model
{
    public class AccountModel : CrossDomainObject, IOrderDependenciesResolver
    {
        private readonly VarDictionary<string, PositionModel> positions = new VarDictionary<string, PositionModel>();
        private readonly VarDictionary<string, AssetModel> assets = new VarDictionary<string, AssetModel>();
        private readonly VarDictionary<string, OrderModel> orders = new VarDictionary<string, OrderModel>();
        private AccountTypes? accType;
        private readonly IReadOnlyDictionary<string, CurrencyEntity> _currencies;
        private readonly IReadOnlyDictionary<string, SymbolModel> _symbols;
        private bool _isCalcStarted;

        public AccountModel(IVarSet<string, CurrencyEntity> currecnies, IVarSet<string, SymbolModel> symbols)
        {
            _currencies = currecnies.Snapshot;
            _symbols = symbols.Snapshot;
        }

        public event System.Action AccountTypeChanged = delegate { };
        public IVarSet<string, PositionModel> Positions { get { return positions; } }
        public IVarSet<string, OrderModel> Orders { get { return orders; } }
        public IVarSet<string, AssetModel> Assets { get { return assets; } }

        public AccountTypes? Type
        {
            get { return accType; }
            private set
            {
                if (accType != value)
                {
                    accType = value;
                    AccountTypeChanged();
                }
            }
        }

        public string Id { get; private set; }
        public double Balance { get; private set; }
        public string BalanceCurrency { get; private set; }
        public int BalanceDigits { get; private set; }
        public string Account { get; private set; }
        public int Leverage { get; private set; }
        public AccountCalculatorModel Calc { get; private set; }

        public event Action<OrderUpdateInfo> OrderUpdate;
        public event Action<PositionModel, OrderExecAction> PositionUpdate;
        public event Action<BalanceOperationReport> BalanceUpdate;

        public EntityCacheUpdate CreateSnaphotUpdate(AccountEntity accInfo, List<OrderEntity> tradeRecords, List<PositionEntity> positions, List<AssetEntity> assets)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Init() symbols:{0} orders:{1} positions:{2}",
                _symbols.Count, tradeRecords.Count, positions.Count));

            return new Snapshot(accInfo, tradeRecords, positions, assets);

            //_client.BalanceReceived += OnBalanceOperation;
            //_client.ExecutionReportReceived += OnReport;
            //_client.PositionReportReceived += OnReport;
        }

        internal void StartCalculator(ClientModel.Data marketData)
        {
            if (!_isCalcStarted)
            {
                _isCalcStarted = true;

                Calc = AccountCalculatorModel.Create(this, marketData);
                Calc.Recalculate();
            }
        }

        public void Deinit()
        {
            //_client.BalanceReceived -= OnBalanceOperation;
            //_client.ExecutionReportReceived -= OnReport;
            //_client.PositionReportReceived -= OnReport;

            if (_isCalcStarted && Calc != null)
            {
                Calc.Dispose();
                Calc = null;
                _isCalcStarted = false;
            }
        }

        public AccountEntity GetAccountInfo()
        {
            return new AccountEntity
            {
                Id = Id,
                Balance = Balance,
                BalanceCurrency = BalanceCurrency,
                Leverage = Leverage,
                Type = Type ?? AccountTypes.Gross,
                Assets = Assets.Snapshot.Values.Select(a => a.GetEntity()).ToArray()
            };
        }

        internal void Clear()
        {
            positions.Clear();
            orders.Clear();
            assets.Clear();

            Account = "";
            Type = null;
            Balance = 0;
            BalanceCurrency = null;
            Leverage = 0;
            BalanceDigits = 2;
        }

        #region Balance and assets management

        private void OnBalanceChanged()
        {
            if (_isCalcStarted)
                Calc.Recalculate();
        }

        internal EntityCacheUpdate GetBalanceUpdate(BalanceOperationReport report)
        {
            return new BalanceUpdateAction(report);
        }

        private void UpdateBalance(ExecutionReport report)
        {
            if (Type == AccountTypes.Net && report.ExecutionType == ExecutionType.Trade)
            {
                switch (report.OrderStatus)
                {
                    case OrderStatus.Calculated:
                    case OrderStatus.Filled:
                        if (!double.IsNaN(report.Balance))
                        {
                            Balance = report.Balance;
                            OnBalanceChanged();
                        }
                        break;
                }
            }

            if (Type == AccountTypes.Cash)
            {
                foreach (var asset in report.Assets)
                    UpdateAsset(asset);
            }
        }

        private void UpdateAsset(AssetEntity assetInfo)
        {
            if (assetInfo.IsEmpty)
                assets.Remove(assetInfo.Currency);
            else
                assets[assetInfo.Currency] = new AssetModel(assetInfo, _currencies);
        }

        #endregion

        #region Postion management

        internal EntityCacheUpdate GetPositionUpdate(PositionEntity report)
        {
            if (report.IsEmpty)
                return new PositionUpdateAction(report, OrderEntityAction.Removed);
            else if (!positions.ContainsKey(report.Symbol))
                return new PositionUpdateAction(report, OrderEntityAction.Added);
            else
                return new PositionUpdateAction(report, OrderEntityAction.Updated);
        }

        private void OnPositionUpdated(PositionEntity position)
        {
            var model = UpsertPosition(position);
            PositionUpdate?.Invoke(model, OrderExecAction.Modified);
        }

        private void OnPositionAdded(PositionEntity position)
        {
            var model = UpsertPosition(position);
            PositionUpdate?.Invoke(model, OrderExecAction.Opened);
        }

        private void OnPositionRemoved(PositionEntity position)
        {
            PositionModel model;

            if (!positions.TryGetValue(position.Symbol, out model))
                return;

            positions.Remove(model.Symbol);
            PositionUpdate?.Invoke(model, OrderExecAction.Closed);
        }

        private PositionModel UpsertPosition(PositionEntity position)
        {
            var positionModel = new PositionModel(position, this);
            positions[position.Symbol] = positionModel;

            return positionModel;
        }

        #endregion

        #region Order management

        internal EntityCacheUpdate GetOrderUpdate(ExecutionReport report)
        {
            System.Diagnostics.Debug.WriteLine("ER  #" + report.OrderId + " " + report.OrderType + " " + report.ExecutionType + " opId=" + report.TradeRequestId);

            switch (report.ExecutionType)
            {
                case ExecutionType.Calculated:
                    if (orders.ContainsKey(report.OrderId))
                        return OnOrderUpdated(report, OrderExecAction.Opened);
                    else
                        return OnOrderAdded(report, OrderExecAction.Opened);

                case ExecutionType.Replace:
                    return OnOrderUpdated(report, OrderExecAction.Modified);

                case ExecutionType.Expired:
                    return OnOrderRemoved(report, OrderExecAction.Expired);

                case ExecutionType.Canceled:
                    return OnOrderRemoved(report, OrderExecAction.Canceled);

                case ExecutionType.Rejected:
                    return OnOrderRejected(report, OrderExecAction.Rejected);

                case ExecutionType.None:
                    if (report.OrderStatus == OrderStatus.Rejected)
                        return OnOrderRejected(report, OrderExecAction.Rejected);
                    break;

                case ExecutionType.Trade:
                    if (report.OrderType == OrderType.StopLimit)
                    {
                        return OnOrderRemoved(report, OrderExecAction.Activated);
                    }
                    else if (report.OrderType == OrderType.Limit || report.OrderType == OrderType.Stop)
                    {
                        if (report.ImmediateOrCancel)
                            return OnIocFilled(report);

                        if (report.LeavesVolume != 0)
                            return OnOrderUpdated(report, OrderExecAction.Filled);

                        if (Type != AccountTypes.Gross)
                            return OnOrderRemoved(report, OrderExecAction.Filled);
                    }
                    else if (report.OrderType == OrderType.Position)
                    {
                        if (report.OrderStatus == OrderStatus.PartiallyFilled)
                            return OnOrderUpdated(report, OrderExecAction.Closed);

                        if (report.OrderStatus == OrderStatus.Filled)
                            return OnOrderRemoved(report, OrderExecAction.Closed);
                    }
                    else if (report.OrderType == OrderType.Market)
                    {
                        if (Type == AccountTypes.Gross)
                            return MockMarkedFilled(report);

                        if (Type == AccountTypes.Net || Type == AccountTypes.Cash)
                            return OnMarketFilled(report, OrderExecAction.Filled);
                    }
                    break;
            }

            return null;
        }

        private OrderUpdateAction OnOrderAdded(ExecutionReport report, OrderExecAction algoAction)
        {
            if (report.ImmediateOrCancel)
                return null;

            return new OrderUpdateAction(report, algoAction, OrderEntityAction.Added);
        }

        private OrderUpdateAction MockMarkedFilled(ExecutionReport report)
        {
            report.OrderType = OrderType.Position;
            return new OrderUpdateAction(report, OrderExecAction.Opened, OrderEntityAction.Added);
        }

        private OrderUpdateAction OnIocFilled(ExecutionReport report)
        {
            if (accType == AccountTypes.Cash)
            {
                report.OrderType = OrderType.Market;
                return new OrderUpdateAction(report, OrderExecAction.Opened, OrderEntityAction.None);
            }
            else
                return OnOrderUpdated(report, OrderExecAction.Filled);
        }

        private OrderUpdateAction OnMarketFilled(ExecutionReport report, OrderExecAction algoAction)
        {
            return new OrderUpdateAction(report, algoAction, OrderEntityAction.None);
        }

        private OrderUpdateAction OnOrderRemoved(ExecutionReport report, OrderExecAction algoAction)
        {
            return new OrderUpdateAction(report, algoAction, OrderEntityAction.Removed);
        }

        private OrderUpdateAction OnOrderUpdated(ExecutionReport report, OrderExecAction algoAction)
        {
            return new OrderUpdateAction(report, algoAction, OrderEntityAction.Updated);
        }

        private OrderUpdateAction OnOrderRejected(ExecutionReport report, OrderExecAction algoAction)
        {
            return new OrderUpdateAction(report, algoAction, OrderEntityAction.None);
            //ExecReportToAlgo(algoAction, OrderEntityAction.None, report);
            //OrderUpdate?.Invoke(report, null, algoAction);
        }

        #endregion

        SymbolModel IOrderDependenciesResolver.GetSymbolOrNull(string name)
        {
            return _symbols.GetOrDefault(name);
        }

        public EntityCacheUpdate GetSnapshotUpdate()
        {
            var info = GetAccountInfo();
            var orders = Orders.Snapshot.Values.Select(o => o.GetEntity()).ToList();
            var positions = Positions.Snapshot.Values.Select(p => p.GetEntity()).ToList();
            var assets = Assets.Snapshot.Values.Select(a => a.GetEntity()).ToList();

            return new Snapshot(info, orders, positions, assets);
        }

        [Serializable]
        public class Snapshot : EntityCacheUpdate
        {
            private AccountEntity _accInfo;
            private IEnumerable<OrderEntity> _orders;
            private IEnumerable<PositionEntity> _positions;
            private IEnumerable<AssetEntity> _assets;

            public Snapshot(AccountEntity accInfo, IEnumerable<OrderEntity> orders,
                IEnumerable<PositionEntity> positions, IEnumerable<AssetEntity> assets)
            {
                _accInfo = accInfo;
                _orders = orders ?? Enumerable.Empty<OrderEntity>();
                _positions = positions ?? Enumerable.Empty<PositionEntity>();
                _assets = assets ?? Enumerable.Empty<AssetEntity>();
            }

            public void Apply(EntityCache cache)
            {
                var acc = cache.Account;

                acc.positions.Clear();
                acc.orders.Clear();
                acc.assets.Clear();

                var balanceCurrencyInfo = acc._currencies.Read(_accInfo.BalanceCurrency);

                acc.Account = _accInfo.Id;
                acc.Type = _accInfo.Type;
                acc.Balance = _accInfo.Balance;
                acc.BalanceCurrency = _accInfo.BalanceCurrency;
                acc.Leverage = _accInfo.Leverage;
                acc.BalanceDigits = balanceCurrencyInfo?.Digits ?? 2;

                foreach (var fdkPosition in _positions)
                    acc.positions.Add(fdkPosition.Symbol, new PositionModel(fdkPosition, acc));

                foreach (var fdkOrder in _orders)
                    acc.orders.Add(fdkOrder.OrderId, new OrderModel(fdkOrder, acc));

                foreach (var fdkAsset in _assets)
                    acc.assets.Add(fdkAsset.Currency, new AssetModel(fdkAsset, acc._currencies));
            }
        }

        [Serializable]
        public class LoadOrderUpdate : EntityCacheUpdate
        {
            public LoadOrderUpdate(OrderEntity order)
            {
                Order = order ?? throw new ArgumentNullException("symbol");
            }

            private OrderEntity Order { get; }

            public void Apply(EntityCache cache)
            {
                var acc = cache.Account;
                acc.orders.Add(Order.OrderId, new OrderModel(Order, acc));
            }
        }

        [Serializable]
        private class OrderUpdateAction: EntityCacheUpdate
        {
            private ExecutionReport _report;
            private OrderExecAction _execAction;
            private OrderEntityAction _entityAction;

            public OrderUpdateAction(ExecutionReport report, OrderExecAction execAction, OrderEntityAction entityAction)
            {
                _report = report;
                _execAction = execAction;
                _entityAction = entityAction;
            }

            public void Apply(EntityCache cache)
            {
                OrderModel order = null;

                if (_entityAction == OrderEntityAction.Added)
                {
                    order = new OrderModel(_report, cache.Account);
                    cache.Account.orders[order.Id] = order;
                }
                else if (_entityAction == OrderEntityAction.Removed)
                {
                    order = cache.Account.Orders.GetOrDefault(_report.OrderId);
                    cache.Account.orders.Remove(_report.OrderId);
                }
                else if (_entityAction == OrderEntityAction.Updated)
                {
                    order = cache.Account.Orders.GetOrDefault(_report.OrderId);
                    order.Update(_report);
                }
                else
                    order = new OrderModel(_report, cache.Account);

                cache.Account.OrderUpdate?.Invoke(new OrderUpdateInfo(_report, _execAction, _entityAction, order));
                cache.Account.UpdateBalance(_report);
            }
        }

        [Serializable]
        private class BalanceUpdateAction : EntityCacheUpdate
        {
            private BalanceOperationReport _report;

            public BalanceUpdateAction(BalanceOperationReport report)
            {
                _report = report;
            }

            public void Apply(EntityCache cache)
            {
                var acc = cache.Account;

                if (acc.Type == AccountTypes.Gross || acc.Type == AccountTypes.Net)
                {
                    acc.Balance = _report.Balance;
                    acc.OnBalanceChanged();
                }
                else if (acc.Type == AccountTypes.Cash)
                {
                    if (_report.Balance > 0)
                        acc.assets[_report.CurrencyCode] = new AssetModel(_report.Balance, _report.CurrencyCode, acc._currencies);
                    else
                    {
                        if (acc.assets.ContainsKey(_report.CurrencyCode))
                            acc.assets.Remove(_report.CurrencyCode);
                    }
                }

                acc.BalanceUpdate?.Invoke(_report);
            }
        }

        [Serializable]
        private class PositionUpdateAction : EntityCacheUpdate
        {
            private PositionEntity _report;
            private OrderEntityAction _entityAction;

            public PositionUpdateAction(PositionEntity report, OrderEntityAction action)
            {
                _report = report;
                _entityAction = action;
            }

            public void Apply(EntityCache cache)
            {
                if (_entityAction == OrderEntityAction.Added)
                    cache.Account.OnPositionAdded(_report);
                else if (_entityAction == OrderEntityAction.Updated)
                    cache.Account.OnPositionUpdated(_report);
                else if (_entityAction == OrderEntityAction.Removed)
                    cache.Account.OnPositionRemoved(_report);
            }
        }
    }

    public class OrderUpdateInfo
    {
        public OrderUpdateInfo(ExecutionReport report, OrderExecAction execAction, OrderEntityAction entityAction, OrderModel order)
        {
            Report = report;
            ExecAction = execAction;
            EntityAction = entityAction;
            Order = order;
        }

        public ExecutionReport Report { get; }
        public OrderExecAction ExecAction { get; }
        public OrderEntityAction EntityAction { get; }
        public OrderModel Order { get; }
    }
}
