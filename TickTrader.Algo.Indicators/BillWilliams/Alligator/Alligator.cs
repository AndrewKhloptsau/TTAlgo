﻿using TickTrader.Algo.Api;
using TickTrader.Algo.Indicators.Trend.MovingAverage;

namespace TickTrader.Algo.Indicators.BillWilliams.Alligator
{
    [Indicator(IsOverlay = true, Category = "Bill Williams", DisplayName = "Bill Williams/Alligator")]
    public class Alligator : Indicator
    {
        private MovingAverage _jaws, _teeth, _lips;

        [Parameter(DefaultValue = 13, DisplayName = "Jaws Period")]
        public int JawsPeriod { get; set; }

        [Parameter(DefaultValue = 8, DisplayName = "Jaws Shift")]
        public int JawsShift { get; set; }

        [Parameter(DefaultValue = 8, DisplayName = "Teeth Period")]
        public int TeethPeriod { get; set; }

        [Parameter(DefaultValue = 5, DisplayName = "Teeth Shift")]
        public int TeethShift { get; set; }

        [Parameter(DefaultValue = 5, DisplayName = "Lips Period")]
        public int LipsPeriod { get; set; }

        [Parameter(DefaultValue = 3, DisplayName = "Lips Shift")]
        public int LipsShift { get; set; }

        [Parameter(DefaultValue = Method.Smoothed, DisplayName = "Method")]
        public Method TargetMethod { get; set; }

        [Input]
        public DataSeries Price { get; set; }

        [Output(DefaultColor = Colors.Blue)]
        public DataSeries Jaws { get; set; }

        [Output(DefaultColor = Colors.Red)]
        public DataSeries Teeth { get; set; }

        [Output(DefaultColor = Colors.Green)]
        public DataSeries Lips { get; set; }

        public int LastPositionChanged { get { return _jaws.LastPositionChanged; } }

        public Alligator() { }

        public Alligator(DataSeries price, int jawsPeriod, int jawsShift, int teethPeriod, int teethShift,
            int lipsPeriod, int lipsShift, Method targetMethod = Method.Simple)
        {
            Price = price;
            JawsPeriod = jawsPeriod;
            JawsShift = jawsShift;
            TeethPeriod = teethPeriod;
            TeethShift = teethShift;
            LipsPeriod = lipsPeriod;
            LipsShift = lipsShift;
            TargetMethod = targetMethod;

            InitializeIndicator();
        }

        private void InitializeIndicator()
        {
            _jaws = new MovingAverage(Price, JawsPeriod, JawsShift, TargetMethod);
            _teeth = new MovingAverage(Price, TeethPeriod, TeethShift, TargetMethod);
            _lips = new MovingAverage(Price, LipsPeriod, LipsShift, TargetMethod);
        }

        protected override void Init()
        {
            InitializeIndicator();
        }

        protected override void Calculate()
        {
            var pos = LastPositionChanged;
            Jaws[pos] = _jaws.Average[pos];
            Teeth[pos] = _teeth.Average[pos];
            Lips[pos] = _lips.Average[pos];
        }
    }
}