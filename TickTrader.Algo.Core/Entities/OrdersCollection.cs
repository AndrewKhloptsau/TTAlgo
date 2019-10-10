﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.Core
{
    public class OrdersCollection : IEnumerable<OrderAccessor>
    {
        private PluginBuilder builder;
        private OrdersAdapter fixture;

        internal OrdersAdapter OrderListImpl => fixture;
        internal bool IsEnabled { get { return true; } }

        internal OrdersCollection(PluginBuilder builder)
        {
            this.builder = builder;
            fixture = new OrdersAdapter(builder.Symbols);
        }

        internal void Add(OrderAccessor order)
        {
            fixture.Add(order);
            Added?.Invoke(order);
        }

        public OrderAccessor Add(OrderEntity entity, AccountAccessor acc)
        {
            var result = fixture.Add(entity, acc);
            Added?.Invoke(result);
            return result;
        }

        public OrderAccessor Replace(OrderEntity entity)
        {
            return fixture.Update(entity, IsEnabled);
        }

        public OrderAccessor GetOrderOrThrow(string id)
        {
            var order = fixture.GetOrNull(id);
            if (order == null)
                throw new OrderValidationError("Order Not Found " + id, OrderCmdResultCodes.OrderNotFound);
            return order;
        }

        public OrderAccessor GetOrderOrNull(string id)
        {
            return fixture.GetOrNull(id);
        }

        public OrderAccessor Remove(string orderId)
        {
            var order = fixture.Remove(orderId);
            if (order != null)
                Removed?.Invoke(order);
            return order;
        }

        public OrderAccessor UpdateAndRemove(OrderEntity entity)
        {
            var order = fixture.Remove(entity.Id);
            order?.Update(entity);
            if (order != null)
                Removed?.Invoke(order);
            return order;
        }

        public void Clear()
        {
            fixture.Clear();
        }

        public void FireOrderOpened(OrderOpenedEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderOpened(p), args);
        }

        public void FireOrderModified(OrderModifiedEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderModified(p), args);
        }

        public void FireOrderSplitted(OrderSplittedEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderSplitted(p), args);
        }

        public void FireOrderClosed(OrderClosedEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderClosed(p), args);
        }

        public void FireOrderCanceled(OrderCanceledEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderCanceled(p), args);
        }

        public void FireOrderExpired(OrderExpiredEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderExpired(p), args);
        }

        public void FireOrderFilled(OrderFilledEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderFilled(p), args);
        }

        public void FireOrderActivated(OrderActivatedEventArgs args)
        {
            builder.InvokePluginMethod((b, p) => b.Account.Orders.OrderListImpl.FireOrderActivated(args));
        }

        public IEnumerator<OrderAccessor> GetEnumerator()
        {
            return ((IEnumerable<OrderAccessor>)OrderListImpl).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public event Action<OrderAccessor> Added;
        public event Action<OrderAccessor> Removed;
        //public event Action<OrderAccessor> Replaced;

        internal class OrdersAdapter : OrderList, IEnumerable<OrderAccessor>
        {
            private ConcurrentDictionary<string, OrderAccessor> orders = new ConcurrentDictionary<string, OrderAccessor>();
            private SymbolsCollection _symbols;

            internal OrdersAdapter(SymbolsCollection symbols)
            {
                _symbols = symbols;
            }

            public int Count { get { return orders.Count; } }

            public Order this[string id]
            {
                get
                {
                    OrderAccessor entity;
                    if (!orders.TryGetValue(id, out entity))
                        return Null.Order;
                    return entity;
                }
            }

            public void Add(OrderAccessor order)
            {
                if (!orders.TryAdd(order.Id, order))
                    throw new ArgumentException("Order #" + order.Id + " already exist!");

                Added?.Invoke(order);
            }

            public OrderAccessor Add(OrderEntity entity, AccountAccessor acc)
            {
                var accessor = new OrderAccessor(entity, _symbols.GetOrDefault, acc.Leverage);
                if (!orders.TryAdd(entity.Id, accessor))
                    throw new ArgumentException("Order #" + entity.Id + " already exist!");

                Added?.Invoke(accessor);

                return accessor;
            }

            public OrderAccessor Update(OrderEntity entity, bool fireEvent)
            {
                OrderAccessor order;

                if (this.orders.TryGetValue(entity.Id, out order))
                {
                    if (order.Modified <= entity.Modified)
                    {
                        order.Update(entity);
                        Replaced?.Invoke(order);
                    }
                }

                return order;
            }

            public OrderAccessor Remove(string orderId)
            {
                OrderAccessor removed;

                if (orders.TryRemove(orderId, out removed))
                    Removed?.Invoke(removed);

                return removed;
            }

            public OrderAccessor GetOrNull(string orderId)
            {
                OrderAccessor entity;
                orders.TryGetValue(orderId, out entity);
                return entity;
            }

            public void Clear()
            {
                orders.Clear();
                Cleared?.Invoke();
            }

            public void FireOrderOpened(OrderOpenedEventArgs args)
            {
                Opened(args);
            }

            public void FireOrderModified(OrderModifiedEventArgs args)
            {
                Modified(args);
            }

            public void FireOrderClosed(OrderClosedEventArgs args)
            {
                Closed(args);
            }

            public void FireOrderCanceled(OrderCanceledEventArgs args)
            {
                Canceled(args);
            }

            public void FireOrderExpired(OrderExpiredEventArgs args)
            {
                Expired(args);
            }

            public void FireOrderFilled(OrderFilledEventArgs args)
            {
                Filled(args);
            }

            public void FireOrderActivated(OrderActivatedEventArgs args)
            {
                Activated(args);
            }

            public void FireOrderSplitted(OrderSplittedEventArgs args)
            {
                Splitted(args);
            }

            public event Action<OrderClosedEventArgs> Closed = delegate { };
            public event Action<OrderModifiedEventArgs> Modified = delegate { };
            public event Action<OrderOpenedEventArgs> Opened = delegate { };
            public event Action<OrderCanceledEventArgs> Canceled = delegate { };
            public event Action<OrderExpiredEventArgs> Expired = delegate { };
            public event Action<OrderFilledEventArgs> Filled = delegate { };
            public event Action<OrderActivatedEventArgs> Activated = delegate { };
            public event Action<OrderSplittedEventArgs> Splitted = delegate { };
            public event Action<Order> Added;
            public event Action<Order> Removed;
            public event Action<Order> Replaced;
            public event Action Cleared;

            public IEnumerator<OrderAccessor> GetTypedEnumerator()
            {
                return orders.Values.GetEnumerator();
            }

            public IEnumerator<Order> GetEnumerator()
            {
                return orders.Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return orders.Values.GetEnumerator();
            }

            IEnumerator<OrderAccessor> IEnumerable<OrderAccessor>.GetEnumerator()
            {
                return orders.Values.GetEnumerator();
            }
        }      
    }
}
