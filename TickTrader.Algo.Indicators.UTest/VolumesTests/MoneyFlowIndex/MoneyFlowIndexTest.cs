﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using TickTrader.Algo.Indicators.UTest.Utility;

namespace TickTrader.Algo.Indicators.UTest.VolumesTests.MoneyFlowIndex
{
    [TestClass]
    public class MoneyFlowIndexTest : TestBase
    {
        private void TestMeasures(string symbol, string timeframe, int period)
        {
            var dir = PathHelper.MeasuresDir("Volumes", "MoneyFlowIndex");
            var test = new MoneyFlowIndexTestCase(typeof (Indicators.Volumes.MoneyFlowIndex.MoneyFlowIndex), symbol,
                $"{dir}bids_{symbol}_{timeframe}_{period}.bin", $"{dir}MFI_{symbol}_{timeframe}_{period}", period);
            LaunchTestCase(test);
        }

        [TestMethod]
        public void TestMeasuresAUDJPY_M30_14()
        {
            TestMeasures("AUDJPY", "M30", 14);
        }

        [TestMethod]
        public void TestMeasuresAUDJPY_M30_20()
        {
            TestMeasures("AUDJPY", "M30", 20);
        }

        [TestMethod]
        public void TestMeasuresAUDNZD_M15_10()
        {
            TestMeasures("AUDNZD", "M15", 10);
        }

        [TestMethod]
        public void TestMeasuresAUDNZD_M15_25()
        {
            TestMeasures("AUDNZD", "M15", 25);
        }

        [TestMethod]
        public void TestMeasuresEURUSD_H1_17()
        {
            TestMeasures("EURUSD", "H1", 17);
        }

        [TestMethod]
        public void TestMeasuresEURUSD_H1_40()
        {
            TestMeasures("EURUSD", "H1", 40);
        }
    }
}
