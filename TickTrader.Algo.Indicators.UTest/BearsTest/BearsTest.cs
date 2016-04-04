﻿using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TickTrader.Algo.Api;
using TickTrader.Algo.Core;

namespace TickTrader.Algo.Indicators.UTest.BearsTest
{
    [TestClass]
    public class BearsTest
    {
        private StreamReader file;
        private List<double> metaResBB;
        private List<double> testResBB;

        private DirectReader<Api.Bar> reader;
        private DirectWriter<Api.Bar> writer;
        private IndicatorBuilder<Api.Bar> builder;

        [TestMethod]
        public void TestMeasuresEURUSD_OneDay()
        {
            metaResBB = new List<double>();
            testResBB = new List<double>();


            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02_indicators-EURUSD\EURUSD-M1-bids.txt");

            reader = new DirectReader<Api.Bar>(new TTQuoteFileReader(file));
            reader.AddMapping("Bars", b => b);

            writer = new DirectWriter<Api.Bar>();
            writer.AddMapping("ExtBearsBuffer", testResBB);

            builder = new IndicatorBuilder<Api.Bar>(typeof(Bears.Bears), reader, writer);
            builder.SetParameter("BearsPeriod", 13);


            builder.Build();

            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02_indicators-EURUSD\EURUSD-Bears.txt");
            string resStr;
            while ((resStr = file.ReadLine()) != null)
            {
                string[] splitResStr = resStr.Split('\t');
                metaResBB.Add(Convert.ToDouble(splitResStr[1]));
            }

            int bidsLen = testResBB.Count;
            for (int testInd = 150; testInd < bidsLen; testInd++)
            {
                AssertX.Greater(1e-8, Math.Abs(testResBB[testInd] - metaResBB[testInd]));

            }
        }

        [TestMethod]
        public void TestMeasuresEURUSD_TwoDay()
        {
            metaResBB = new List<double>();
            testResBB = new List<double>();


            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02-2015.11.03_indicators-EURUSD\EURUSD-M1-bids.txt");

            reader = new DirectReader<Api.Bar>(new TTQuoteFileReader(file));
            reader.AddMapping("Bars", b => b);

            writer = new DirectWriter<Api.Bar>();
            writer.AddMapping("ExtBearsBuffer", testResBB);

            builder = new IndicatorBuilder<Api.Bar>(typeof(Bears.Bears), reader, writer);
            builder.SetParameter("BearsPeriod", 13);


            builder.Build();

            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02-2015.11.03_indicators-EURUSD\EURUSD-Bears.txt");
            string resStr;
            while ((resStr = file.ReadLine()) != null)
            {
                string[] splitResStr = resStr.Split('\t');
                metaResBB.Add(Convert.ToDouble(splitResStr[1]));
            }

            int bidsLen = testResBB.Count;
            for (int testInd = 150; testInd < bidsLen; testInd++)
            {
                AssertX.Greater(1e-8, Math.Abs(testResBB[testInd] - metaResBB[testInd]));

            }
        }

        [TestMethod]
        public void TestMeasuresXAUUSD_OneDay()
        {
            metaResBB = new List<double>();
            testResBB = new List<double>();


            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02_indicators-XAUUSD\XAUUSD-M1-bids.txt");

            reader = new DirectReader<Api.Bar>(new TTQuoteFileReader(file));
            reader.AddMapping("Bars", b => b);

            writer = new DirectWriter<Api.Bar>();
            writer.AddMapping("ExtBearsBuffer", testResBB);

            builder = new IndicatorBuilder<Api.Bar>(typeof(Bears.Bears), reader, writer);
            builder.SetParameter("BearsPeriod", 13);


            builder.Build();

            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02_indicators-XAUUSD\XAUUSD-Bears.txt");
            string resStr;
            while ((resStr = file.ReadLine()) != null)
            {
                string[] splitResStr = resStr.Split('\t');
                metaResBB.Add(Convert.ToDouble(splitResStr[1]));
            }

            int bidsLen = testResBB.Count;
            for (int testInd = 200; testInd < bidsLen; testInd++)
            {
                AssertX.Greater(1e-5, Math.Abs(testResBB[testInd] - metaResBB[testInd]));

            }
        }

        [TestMethod]
        public void TestMeasuresXAUUSD_TwoDay()
        {
            metaResBB = new List<double>();
            testResBB = new List<double>();

            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02_indicators-XAUUSD\XAUUSD-M1-bids.txt");

            reader = new DirectReader<Api.Bar>(new TTQuoteFileReader(file));
            reader.AddMapping("Bars", b => b);

            writer = new DirectWriter<Api.Bar>();
            writer.AddMapping("ExtBearsBuffer", testResBB);

            builder = new IndicatorBuilder<Api.Bar>(typeof(Bears.Bears), reader, writer);
            builder.SetParameter("BearsPeriod", 13);


            builder.Build();

            file = File.OpenText(@"..\..\..\IndicatorFiles\2015.11.02-2015.11.03_indicators-XAUUSD\XAUUSD-Bears.txt");
            string resStr;
            while ((resStr = file.ReadLine()) != null)
            {
                string[] splitResStr = resStr.Split('\t');
                metaResBB.Add(Convert.ToDouble(splitResStr[1]));
            }

            int bidsLen = testResBB.Count;
            for (int testInd = 200; testInd < bidsLen; testInd++)
            {
                AssertX.Greater(1e-5, Math.Abs(testResBB[testInd] - metaResBB[testInd]));

            }
        }
    }
}
