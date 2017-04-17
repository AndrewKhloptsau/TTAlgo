﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TickTrader.Algo.Api
{
    [Serializable]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class InputAttribute : Attribute
    {
        public string DisplayName { get; set; }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OutputAttribute : Attribute
    {
        public OutputAttribute()
        {
            DefaultColor = Colors.Auto;
        }

        public string DisplayName { get; set; }
        public Colors DefaultColor { get; set; }
        public LineStyles DefaultLineStyle { get; set; }
        public float DefaultThickness { get; set; }
        public PlotType PlotType { get; set; }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ParameterAttribute : Attribute
    {
        public object DefaultValue { get; set; }
        public string DisplayName { get; set; }
        public bool IsRequired { get; set; }
    }

    [Serializable]
    public abstract class AlgoPluginAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class IndicatorAttribute : AlgoPluginAttribute
    {
        public bool IsOverlay { get; set; }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TradeBotAttribute : AlgoPluginAttribute
    {
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CopyrightAttribute : Attribute
    {
        public CopyrightAttribute(string copyrightText)
        {
            Text = copyrightText;
        }

        public string Text { get; set; }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class FileFilterAttribute : Attribute
    {
        public FileFilterAttribute(string name, string mask)
        {
            Name = name;
            Mask = mask;
        }

        public string Name { get; set; }
        public string Mask { get; set; }
    }
}
