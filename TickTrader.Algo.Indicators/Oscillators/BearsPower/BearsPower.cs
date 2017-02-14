﻿using TickTrader.Algo.Api;
using TickTrader.Algo.Indicators.Trend.MovingAverage;
using TickTrader.Algo.Indicators.Utility;

namespace TickTrader.Algo.Indicators.Oscillators.BearsPower
{
    [Indicator(Category = "Oscillators", DisplayName = "Oscillators/Bears Power")]
    public class BearsPower : Indicator
    {
        private MovingAverage _ema;

        [Parameter(DefaultValue = 13, DisplayName = "Period")]
        public int Period { get; set; }

        [Parameter(DefaultValue = AppliedPrice.Target.Close, DisplayName = "Apply To")]
        public AppliedPrice.Target TargetPrice { get; set; }

        [Input]
        public new BarSeries Bars { get; set; }

        [Output(DisplayName = "Bears", DefaultColor = Colors.Silver)]
        public DataSeries Bears { get; set; }

        public int LastPositionChanged { get { return _ema.LastPositionChanged; } }

        public BearsPower() { }

        public BearsPower(BarSeries bars, int period, AppliedPrice.Target targetPrice = AppliedPrice.Target.Close)
        {
            Bars = bars;
            Period = period;
            TargetPrice = targetPrice;

            InitializeIndicator();
        }

        protected void InitializeIndicator()
        {
            _ema = new MovingAverage(AppliedPrice.GetDataSeries(Bars, TargetPrice), Period, 0, Method.Exponential);
        }

        protected override void Init()
        {
            InitializeIndicator();
        }

        protected override void Calculate()
        {
            var pos = LastPositionChanged;
            Bears[pos] = Bars.Low[pos] - _ema.Average[pos];
        }
    }
}