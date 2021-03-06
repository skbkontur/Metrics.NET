using System;
using System.Diagnostics;

using Metrics.Reporters;
using Metrics.Utils;

namespace Metrics
{
    public sealed class MetricsConfig : IDisposable, IHideObjectMembers
    {
        public MetricsConfig(MetricsContext context)
        {
            this.context = context;

            if (!GloballyDisabledMetrics)
            {
                healthStatus = HealthChecks.GetStatus;
                reports = new MetricsReports(this.context.DataProvider, healthStatus);

                this.context.Advanced.ContextDisabled += (s, e) =>
                    {
                        isDisabled = true;
                        DisableAllReports();
                    };
            }
        }

        /// <summary>
        ///     Gets the currently configured default sampling type to use for histogram sampling.
        /// </summary>
        public SamplingType DefaultSamplingType
        {
            get
            {
                Debug.Assert(defaultSamplingType != SamplingType.Default);
                return defaultSamplingType;
            }
        }

        /// <summary>
        ///     Configure Metrics library to use a custom health status reporter. By default HealthChecks.GetStatus() is used.
        /// </summary>
        /// <param name="healthStatus">Function that provides the current health status.</param>
        /// <returns>Chain-able configuration object.</returns>
        public MetricsConfig WithHealthStatus(Func<HealthStatus> healthStatus)
        {
            if (!isDisabled)
            {
                this.healthStatus = healthStatus;
            }
            return this;
        }

        /// <summary>
        ///     Error handler for the metrics library. If a handler is registered any error will be passed to the handler.
        ///     By default unhandled errors are logged with Trace.TracError.
        /// </summary>
        /// <param name="errorHandler">Action with will be executed with the exception and a specific message.</param>
        /// <param name="clearExistingHandlers">Is set to true, remove any existing handler.</param>
        /// <returns>Chain able configuration object.</returns>
        public MetricsConfig WithErrorHandler(Action<Exception, string, object[]> errorHandler, bool clearExistingHandlers = false)
        {
            if (clearExistingHandlers)
            {
                MetricsErrorHandler.ClearHandlers();
            }

            if (!isDisabled)
            {
                MetricsErrorHandler.AddHandler(errorHandler);
            }

            return this;
        }

        /// <summary>
        ///     Configure the way metrics are reported
        /// </summary>
        /// <param name="reportsConfig">Reports configuration action</param>
        /// <returns>Chain-able configuration object.</returns>
        public MetricsConfig WithReporting(Action<MetricsReports> reportsConfig)
        {
            if (!isDisabled)
            {
                reportsConfig(reports);
            }

            return this;
        }

        /// <summary>
        ///     This method is used for customizing the metrics configuration.
        ///     The <paramref name="extension" /> will be called with the current MetricsContext and HealthStatus provider.
        /// </summary>
        /// <remarks>
        ///     In general you don't need to call this method directly.
        /// </remarks>
        /// <param name="extension">Action to apply extra configuration.</param>
        /// <returns>Chain-able configuration object.</returns>
        public MetricsConfig WithConfigExtension(Action<MetricsContext, Func<HealthStatus>> extension)
        {
            if (isDisabled)
            {
                return this;
            }

            return WithConfigExtension((m, h) =>
                {
                    extension(m, h);
                    return this;
                }, () => this);
        }

        /// <summary>
        ///     This method is used for customizing the metrics configuration.
        ///     The <paramref name="extension" /> will be called with the current MetricsContext and HealthStatus provider.
        /// </summary>
        /// <remarks>
        ///     In general you don't need to call this method directly.
        /// </remarks>
        /// <param name="extension">Action to apply extra configuration.</param>
        /// <param name="defaultValueProvider">Default value provider for T, which will be used when metrics are disabled.</param>
        /// <returns>The result of calling the extension.</returns>
        public T WithConfigExtension<T>(Func<MetricsContext, Func<HealthStatus>, T> extension, Func<T> defaultValueProvider)
        {
            if (isDisabled)
            {
                return defaultValueProvider();
            }

            return extension(context, healthStatus);
        }

        /// <summary>
        ///     Configure the default sampling type to use for histograms.
        /// </summary>
        /// <param name="type">Type of sampling to use.</param>
        /// <returns>Chain-able configuration object.</returns>
        public MetricsConfig WithDefaultSamplingType(SamplingType type)
        {
            if (isDisabled)
            {
                return this;
            }

            if (type == SamplingType.Default)
            {
                throw new ArgumentException("Sampling type other than default must be specified", nameof(type));
            }
            defaultSamplingType = type;
            return this;
        }

        public MetricsConfig WithInternalMetrics()
        {
            if (isDisabled)
            {
                return this;
            }

            Metric.EnableInternalMetrics();
            return this;
        }

        public void Dispose()
        {
            reports.Dispose();
        }

        private void DisableAllReports()
        {
            reports.StopAndClearAllReports();
        }

        private static bool ReadGloballyDisableMetricsSetting()
        {
            try
            {
                var isDisabled = Environment.GetEnvironmentVariable("Metrics.CompletelyDisableMetrics");
                return !string.IsNullOrEmpty(isDisabled) && isDisabled.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception x)
            {
                MetricsErrorHandler.Handle(x, "Invalid Metrics Configuration: Metrics.CompletelyDisableMetrics must be set to TRUE or FALSE");
                return false;
            }
        }

        public static readonly bool GloballyDisabledMetrics = ReadGloballyDisableMetricsSetting();

        private readonly MetricsContext context;
        private readonly MetricsReports reports;

        private Func<HealthStatus> healthStatus;

        private SamplingType defaultSamplingType = SamplingType.ExponentiallyDecaying;

        private bool isDisabled = GloballyDisabledMetrics;
    }
}