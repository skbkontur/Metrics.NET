﻿using System;
using System.Diagnostics;

using Metrics.ConcurrencyUtilities;

namespace Metrics.Utils
{
    /// <summary>
    ///     An exponentially-weighted moving average.
    ///     <a href="http://www.teamquest.com/pdfs/whitepaper/ldavg1.pdf">UNIX Load Average Part 1: How It Works</a>
    ///     <a href="http://www.teamquest.com/pdfs/whitepaper/ldavg2.pdf">UNIX Load Average Part 2: Not Your Average Average</a>
    ///     <a href="http://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average">EMA</a>
    /// </summary>
    public sealed class EWMA
    {
        public EWMA(double alpha, long interval, TimeUnit intervalUnit)
        {
            Debug.Assert(interval > 0);
            this.interval = intervalUnit.ToNanoseconds(interval);
            this.alpha = alpha;
        }

        private const int Interval = 5;
        private const double SecondsPerMinute = 60.0;
        private const int OneMinute = 1;
        private const int FiveMinutes = 5;
        private const int FifteenMinutes = 15;

        public static EWMA OneMinuteEWMA()
        {
            return new EWMA(M1Alpha, Interval, TimeUnit.Seconds);
        }

        public static EWMA FiveMinuteEWMA()
        {
            return new EWMA(M5Alpha, Interval, TimeUnit.Seconds);
        }

        public static EWMA FifteenMinuteEWMA()
        {
            return new EWMA(M15Alpha, Interval, TimeUnit.Seconds);
        }

        public void Update(long value)
        {
            uncounted.Add(value);
        }

        public void Tick(long externallyCounted = 0L)
        {
            var count = uncounted.GetAndReset() + externallyCounted;

            var instantRate = count / interval;
            if (initialized)
            {
                var doubleRate = rate.GetValue();
                rate.SetValue(doubleRate + alpha * (instantRate - doubleRate));
            }
            else
            {
                rate.SetValue(instantRate);
                initialized = true;
            }
        }

        public double GetRate(TimeUnit rateUnit)
        {
            return rate.GetValue() * rateUnit.ToNanoseconds(1L);
        }

        public void Reset()
        {
            uncounted.Reset();
            rate.SetValue(0.0);
        }

        private static readonly double M1Alpha = 1 - Math.Exp(-Interval / SecondsPerMinute / OneMinute);
        private static readonly double M5Alpha = 1 - Math.Exp(-Interval / SecondsPerMinute / FiveMinutes);
        private static readonly double M15Alpha = 1 - Math.Exp(-Interval / SecondsPerMinute / FifteenMinutes);

        private volatile bool initialized;
        private VolatileDouble rate = new VolatileDouble(0.0);

        private readonly StripedLongAdder uncounted = new StripedLongAdder();
        private readonly double alpha;
        private readonly double interval;
    }
}