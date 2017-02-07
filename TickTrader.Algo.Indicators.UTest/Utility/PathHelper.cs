﻿using System.IO;

namespace TickTrader.Algo.Indicators.UTest.Utility
{
    public static class PathHelper
    {
        //private static string testDataFolder;

        //static PathHelper()
        //{
        //    var assemblyFolder = Path.GetDirectoryName(typeof(PathHelper).Assembly.Location);
        //    var projectFolder = Path.GetDirectoryName(Path.GetDirectoryName(assemblyFolder));
        //    testDataFolder = Path.Combine(projectFolder, "TestsData");
        //}

        public static string MeasuresDir(string category, string indicatorName)
        {
            return $@"TickTrader.Algo.Indicators.UTest.TestsData.{category}Tests.{indicatorName}.";
        }
    }
}
