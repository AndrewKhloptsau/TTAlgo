﻿using TickTrader.Algo.Api;
using TickTrader.Algo.Indicators.Utility;

namespace TickTrader.Algo.Indicators.Trend.IchimokuKinkoHyo
{
    [Indicator(IsOverlay = true, Category = "Trend", DisplayName = "Trend/Ichimoku Kinko Hyo")]
    public class IchimokuKinkoHyo : Indicator
    {
        private IShift _chikouShifter, _senkouAShifter, _senkouBShifter;

        [Parameter(DefaultValue = 9, DisplayName = "Tenkan-sen")]
        public int TenkanSen { get; set; }

        [Parameter(DefaultValue = 26, DisplayName = "Kijun-sen")]
        public int KijunSen { get; set; }

        [Parameter(DefaultValue = 52, DisplayName = "Senkou Span B")]
        public int SenkouSpanB { get; set; }

        [Input]
        public BarSeries Bars { get; set; }

        [Output(DisplayName = "Tenkan-sen", DefaultColor = Colors.Red)]
        public DataSeries Tenkan { get; set; }

        [Output(DisplayName = "Kijun-sen", DefaultColor = Colors.Blue)]
        public DataSeries Kijun { get; set; }

        [Output(DisplayName = "Senkou Span A", DefaultColor = Colors.SandyBrown, DefaultLineStyle = LineStyles.DotsRare)]
        public DataSeries SenkouA { get; set; }

        [Output(DisplayName = "Senkou Span B", DefaultColor = Colors.Thistle, DefaultLineStyle = LineStyles.DotsRare)]
        public DataSeries SenkouB { get; set; }

        [Output(DisplayName = "Chikou Span", DefaultColor = Colors.Lime)]
        public DataSeries Chikou { get; set; }

        public int LastPositionChanged { get { return 0; } }

        public IchimokuKinkoHyo() { }

        public IchimokuKinkoHyo(BarSeries bars, int tenkanSen, int kijunSen, int senkouSpanB)
        {
            Bars = bars;
            TenkanSen = tenkanSen;
            KijunSen = kijunSen;
            SenkouSpanB = senkouSpanB;
        }

        protected void InitializeIndicator()
        {
            _chikouShifter = new SimpleShifter(-KijunSen);
            _chikouShifter.Init();
            _senkouAShifter = new SimpleShifter(KijunSen);
            _senkouAShifter.Init();
            _senkouBShifter = new SimpleShifter(KijunSen);
            _senkouBShifter.Init();
        }

        protected override void Init()
        {
            InitializeIndicator();
        }

        protected override void Calculate()
        {
            var pos = LastPositionChanged;
            Tenkan[pos] = (PeriodHelper.FindMax(Bars.High, TenkanSen) + PeriodHelper.FindMin(Bars.Low, TenkanSen))/2;
            Kijun[pos] = (PeriodHelper.FindMax(Bars.High, KijunSen) + PeriodHelper.FindMin(Bars.Low, KijunSen))/2;
            Chikou[pos] = double.NaN;
            if (IsUpdate)
            {
                _chikouShifter.UpdateLast(Bars[pos].Close);
                _senkouAShifter.UpdateLast((Tenkan[pos] + Kijun[pos])/2);
                _senkouBShifter.UpdateLast((PeriodHelper.FindMax(Bars.High, SenkouSpanB) +
                                            PeriodHelper.FindMin(Bars.Low, SenkouSpanB))/2);
            }
            else
            {
                _chikouShifter.Add(Bars[pos].Close);
                _senkouAShifter.Add((Tenkan[pos] + Kijun[pos])/2);
                _senkouBShifter.Add((PeriodHelper.FindMax(Bars.High, SenkouSpanB) +
                                     PeriodHelper.FindMin(Bars.Low, SenkouSpanB))/2);
            }
            Chikou[_chikouShifter.Position] = _chikouShifter.Result;
            SenkouA[_senkouAShifter.Position] = _senkouAShifter.Result;
            SenkouB[_senkouBShifter.Position] = _senkouBShifter.Result;
        }
    }
}
