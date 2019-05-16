﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TickTrader.BotTerminal
{
    public class PascalCaseSplitConverter : IValueConverter
    {
        private static Dictionary<string, string> _correctValues = new Dictionary<string, string>()
        {
            {"BuyStopLimit", "Buy StopLimit" },
            {"SellStopLimit", "Sell StopLimit" },
            {"SellStopLimitCanceled", "Sell StopLimit Canceled" },
            {"BuyStopLimitCanceled", "Buy StopLimit Canceled" },
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            var key = value.ToString();

            if (string.IsNullOrEmpty(key))
                return null;

            return _correctValues.ContainsKey(key) ? _correctValues[key] : SetCorrectValue(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string SetCorrectValue(string key)
        {
            var ans = new StringBuilder();

            foreach (var c in key)
            {
                if (char.IsUpper(c))
                    ans.Append(' ');
                ans.Append(c);
            }

            _correctValues.Add(key, ans.ToString().TrimStart());

            return _correctValues[key];
        }
    }
}
