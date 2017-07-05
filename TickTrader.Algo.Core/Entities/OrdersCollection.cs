﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.Core
{
    public class OrdersCollection
    {
        private PluginBuilder builder;
        private OrdersFixture fixture = new OrdersFixture();

        internal OrderList OrderListImpl { get { return fixture; } }
        internal bool IsEnabled { get { return true; } }

        internal OrdersCollection(PluginBuilder builder)
        {
            this.builder = builder;
        }

        public OrderAccessor Add(OrderEntity entity)
        {
            var result = fixture.Add(entity);
            Added?.Invoke(result);
            return result;
        }

        public OrderAccessor Replace(OrderEntity entity)
        {
            var order = fixture.Replace(entity, IsEnabled);
            if (order != null)
                Replaced?.Invoke(order);
            return order;
        }

        public Order GetOrderOrNull(string id)
        public OrderEntity GetOrderOrNull(string id)
        public OrderAccessor GetOrderOrNull(string id)
        {
            return fixture.GetOrNull(id);
        }

        public OrderAccessor Remove(string orderId)
        {
            var order = fixture.Remove(orderId);
            if (order != null)
                Removed(order);
            return order;
        }

        public void FireOrderOpened(OrderOpenedEventArgs args)
        {
            builder.InvokePluginMethod(() => fixture.FireOrderOpened(args));
        }

        public void FireOrderModified(OrderModifiedEventArgs args)
        {
            builder.InvokePluginMethod(() => fixture.FireOrderModified(args));
        }

        public void FireOrderClosed(OrderClosedEventArgs args)
        {
            builder.InvokePluginMethod(() => fixture.FireOrderClosed(args));
        }

        public void FireOrderCanceled(OrderCanceledEventArgs args)
        {
            builder.InvokePluginMethod(() => fixture.FireOrderCanceled(args));
        }

        public void FireOrderExpired(OrderExpiredEventArgs args)
        {
            builder.InvokePluginMethod(() => fixture.FireOrderExpired(args));
        }

        public void FireOrderFilled(OrderFilledEventArgs args)
        {
            builder.InvokePluginMethod(() => fixture.FireOrderFilled(args));
        }

        public event Action<OrderAccessor> Added;
        public event Action<OrderAccessor> Removed;
        public event Action<OrderAccessor> Replaced;

        internal class OrdersFixture : OrderList, IEnumerable<OrderAccessor>
        {
            private ConcurrentDictionary<string, OrderAccessor> orders = new ConcurrentDictionary<string, OrderAccessor>();

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

            public OrderEntity GetOrNull(string id)
            {
                OrderEntity entity;
                orders.TryGetValue(id, out entity);
                return entity;
            }

            public OrderAccessor Add(OrderEntity entity)
            {
                var accessor = new OrderAccessor(entity);
                orders.TryAdd(entity.Id, accessor);
                return accessor;
            }

            public OrderAccessor Replace(OrderEntity entity, bool fireEvent)
            {
                OrderAccessor order;

                if (this.orders.TryGetValue(entity.Id, out order))
                {
                    if (order.Modified <= entity.Modified)
                        order.Update(entity);
                }

                return order;
            }

            public OrderAccessor Remove(string orderId)
            {
                OrderAccessor removed;
                orders.TryRemove(orderId, out removed);
                return removed;
            }

            public OrderAccessor GetOrNull(string orderId)
            {
                OrderAccessor entity;
                orders.TryGetValue(orderId, out entity);
                return entity;
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

            public event Action<OrderClosedEventArgs> Closed = delegate { };
            public event Action<OrderModifiedEventArgs> Modified = delegate { };
            public event Action<OrderOpenedEventArgs> Opened = delegate { };
            public event Action<OrderCanceledEventArgs> Canceled = delegate { };
            public event Action<OrderExpiredEventArgs> Expired = delegate { };
            public event Action<OrderFilledEventArgs> Filled = delegate { };

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
