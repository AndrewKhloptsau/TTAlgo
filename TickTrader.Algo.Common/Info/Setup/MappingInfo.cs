﻿using TickTrader.Algo.Core.Repository;

namespace TickTrader.Algo.Common.Info
{
    public class MappingInfo
    {
        public MappingKey Key { get; set; }

        public string DisplayName { get; set; }


        public MappingInfo() { }

        public MappingInfo(MappingKey key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }
    }
}