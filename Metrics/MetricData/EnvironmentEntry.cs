﻿namespace Metrics.MetricData
{
    public struct EnvironmentEntry
    {
        public EnvironmentEntry(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        public readonly string Name;
        public readonly string Value;
    }
}