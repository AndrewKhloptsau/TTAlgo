﻿using System.ComponentModel;
using System.Text;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.TestCollection.Bots
{
    public abstract class TradeBotCommon : TradeBot
    {
        public string ToObjectPropertiesString(string name, object obj)
        {
            var sb = new StringBuilder();
            sb.AppendLine($" ------------ {name} ------------");
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
            {
                var pNname = descriptor.Name;
                var pValue = descriptor.GetValue(obj);
                sb.AppendLine($"{pNname} = {pValue}");
            }
            sb.AppendLine();
            return sb.ToString();
        }
    }
}