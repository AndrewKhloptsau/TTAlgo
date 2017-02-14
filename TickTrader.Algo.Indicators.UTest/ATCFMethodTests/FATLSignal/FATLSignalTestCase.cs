﻿using System;
using TickTrader.Algo.Indicators.UTest.TestCases;

namespace TickTrader.Algo.Indicators.UTest.ATCFMethodTests.FATLSignal
{
    public class FatlSignalTestCase : DigitalIndicatorTestCase
    {
        public double PointSize { get; protected set; }

        public FatlSignalTestCase(Type indicatorType, string symbol, string quotesPath, string answerPath,
            double pointSize) : base(indicatorType, symbol, quotesPath, answerPath, 16)
        {
            PointSize = pointSize;
        }

        protected override void SetupParameters()
        {
            base.SetupParameters();
            SetParameter("PointSize", PointSize);
        }

        protected override void GetOutput()
        {
            PutOutputToBuffer("Up", 0);
            PutOutputToBuffer("Down", 1);
        }
    }
}