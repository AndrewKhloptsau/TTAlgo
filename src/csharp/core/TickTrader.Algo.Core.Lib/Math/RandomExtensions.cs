﻿using System;

namespace TickTrader.Algo.Core.Lib.Math
{
    public static class RandomExtensions
    {
        public static double NextDoubleInRange(this Random random, double min, double max)
        {
            return random.NextDouble() * (max - min) + min;
        }
    }
}
