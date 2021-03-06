using System;
using System.Collections.Generic;

using Metrics.MetricData;
using Metrics.Utils;

namespace Metrics.Reporters
{
    public sealed class MetricsReports : IHideObjectMembers, IDisposable
    {
        public MetricsReports(MetricsDataProvider metricsDataProvider, Func<HealthStatus> healthStatus)
        {
            this.metricsDataProvider = metricsDataProvider;
            this.healthStatus = healthStatus;
        }

        /// <summary>
        ///     Schedule a generic reporter to be executed at a fixed <paramref name="interval" />
        /// </summary>
        /// <param name="report">Function that returns an instance of a reporter</param>
        /// <param name="interval">Interval at which to run the report.</param>
        /// <param name="filter">Only report metrics that match the filter.</param>
        public MetricsReports WithReport(MetricsReport report, TimeSpan interval, MetricsFilter filter = null)
        {
            var toleratedConsecutiveFailures = ReadToleratedFailuresConfig();
            var newReport = new ScheduledReporter(report, metricsDataProvider.WithFilter(filter), healthStatus, interval, new ActionScheduler(toleratedConsecutiveFailures));
            reports.Add(newReport);
            return this;
        }

        /// <summary>
        ///     Schedule a Console Report to be executed and displayed on the console at a fixed <paramref name="interval" />.
        /// </summary>
        /// <param name="interval">Interval at which to display the report on the Console.</param>
        /// <param name="filter">Only report metrics that match the filter.</param>
        public MetricsReports WithConsoleReport(TimeSpan interval, MetricsFilter filter = null)
        {
            return WithReport(new ConsoleReport(), interval, filter);
        }

        /// <summary>
        ///     Configure Metrics to append a line for each metric to a CSV file in the <paramref name="directory" />.
        /// </summary>
        /// <param name="directory">Directory where to store the CSV files.</param>
        /// <param name="interval">Interval at which to append a line to the files.</param>
        /// <param name="delimiter">CSV delimiter to use</param>
        /// <param name="filter">Only report metrics that match the filter.</param>
        public MetricsReports WithCSVReports(string directory, TimeSpan interval, MetricsFilter filter = null, string delimiter = CSVAppender.CommaDelimiter)
        {
            return WithReport(new CSVReport(new CSVFileAppender(directory, delimiter)), interval, filter);
        }

        /// <summary>
        ///     Schedule a Human Readable report to be executed and appended to a text file.
        /// </summary>
        /// <param name="filePath">File where to append the report.</param>
        /// <param name="interval">Interval at which to run the report.</param>
        /// <param name="filter">Only report metrics that match the filter.</param>
        public MetricsReports WithTextFileReport(string filePath, TimeSpan interval, MetricsFilter filter = null)
        {
            return WithReport(new TextFileReport(filePath), interval, filter);
        }

        /// <summary>
        ///     Stop all registered reports and clear the registrations.
        /// </summary>
        public void StopAndClearAllReports()
        {
            reports.ForEach(r => r.Dispose());
            reports.Clear();
        }

        public void Dispose()
        {
            StopAndClearAllReports();
        }

        private static int ReadToleratedFailuresConfig()
        {
            const string configKey = "Metrics.Reports.ToleratedConsecutiveFailures";
            var configValue = Environment.GetEnvironmentVariable(configKey);

            if (configValue == null)
                return 0;

            if (!int.TryParse(configValue, out var toleratedConsecutiveFailures) || toleratedConsecutiveFailures < -1)
                throw new InvalidOperationException($"Invalid Metrics Configuration for {configKey}: \"{configValue}\". Value must be an integer >= -1.");

            return toleratedConsecutiveFailures;
        }

        private readonly MetricsDataProvider metricsDataProvider;
        private readonly Func<HealthStatus> healthStatus;

        private readonly List<ScheduledReporter> reports = new List<ScheduledReporter>();
    }
}