﻿using System;
using System.Collections.Generic;
using System.Threading;
using Metrics.Utils;

namespace Metrics.Core
{
    public sealed class ExponentiallyDecayingReservoir : Reservoir, IDisposable
    {
        private const int DefaultSize = 1028;
        private const double DefaultAlpha = 0.015;
        private static readonly TimeSpan RescaleInterval = TimeSpan.FromHours(1);

        private class ReverseOrderDoubleComparer : IComparer<double>
        {
            public static readonly IComparer<double> Instance = new ReverseOrderDoubleComparer();

            public int Compare(double x, double y)
            {
                return y.CompareTo(x);
            }
        }

        private readonly SortedList<double, WeightedSample> values;

        private SpinLock @lock = new SpinLock();

        private readonly double alpha;
        private readonly int size;
        private AtomicLong count = new AtomicLong();
        private AtomicLong startTime;

        private readonly Clock clock;

        private readonly Scheduler rescaleScheduler;

        public ExponentiallyDecayingReservoir()
            : this(DefaultSize, DefaultAlpha)
        { }

        public ExponentiallyDecayingReservoir(int size, double alpha)
            : this(size, alpha, Clock.Default, new ActionScheduler())
        { }

        public ExponentiallyDecayingReservoir(Clock clock, Scheduler scheduler)
            : this(DefaultSize, DefaultAlpha, clock, scheduler)
        { }

        public ExponentiallyDecayingReservoir(int size, double alpha, Clock clock, Scheduler scheduler)
        {
            this.size = size;
            this.alpha = alpha;
            this.clock = clock;

            this.values = new SortedList<double, WeightedSample>(size, ReverseOrderDoubleComparer.Instance);

            this.rescaleScheduler = scheduler;
            this.rescaleScheduler.Start(RescaleInterval, () => Rescale());

            this.startTime = new AtomicLong(clock.Seconds);
        }

        public int Size { get { return Math.Min(this.size, (int)this.count.Value); } }

        public Snapshot Snapshot
        {
            get
            {
                bool lockTaken = false;
                try
                {
                    this.@lock.Enter(ref lockTaken);
                    return new WeightedSnapshot(this.values.Values);
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.@lock.Exit();
                    }
                }
            }
        }

        public void Update(long value)
        {
            this.Update(value, this.clock.Seconds);
        }

        public void Reset()
        {
            bool lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);
                this.values.Clear();
                this.count.SetValue(0L);
                this.startTime = new AtomicLong(this.clock.Seconds);
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }

        private void Update(long value, long timestamp)
        {
            bool lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);
                double itemWeight = Math.Exp(alpha * (timestamp - startTime.Value));
                var sample = new WeightedSample(value, itemWeight);
                double priority = itemWeight / ThreadLocalRandom.NextDouble();

                long newCount = count.Increment();
                if (newCount <= size)
                {
                    this.values[priority] = sample;
                }
                else
                {
                    var first = this.values.Keys[this.values.Count - 1];
                    if (first < priority)
                    {
                        this.values.Remove(first);
                        this.values[priority] = sample;
                    }
                }
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }

        public void Dispose()
        {
            using (this.rescaleScheduler) { }
        }

        ///* "A common feature of the above techniques—indeed, the key technique that
        // * allows us to track the decayed weights efficiently—is that they maintain
        // * counts and other quantities based on g(ti − L), and only scale by g(t − L)
        // * at query time. But while g(ti −L)/g(t−L) is guaranteed to lie between zero
        // * and one, the intermediate values of g(ti − L) could become very large. For
        // * polynomial functions, these values should not grow too large, and should be
        // * effectively represented in practice by floating point values without loss of
        // * precision. For exponential functions, these values could grow quite large as
        // * new values of (ti − L) become large, and potentially exceed the capacity of
        // * common floating point types. However, since the values stored by the
        // * algorithms are linear combinations of g values (scaled sums), they can be
        // * rescaled relative to a new landmark. That is, by the analysis of exponential
        // * decay in Section III-A, the choice of L does not affect the final result. We
        // * can therefore multiply each value based on L by a factor of exp(−α(L′ − L)),
        // * and obtain the correct value as if we had instead computed relative to a new
        // * landmark L′ (and then use this new L′ at query time). This can be done with
        // * a linear pass over whatever data structure is being used."
        // */
        private void Rescale()
        {
            bool lockTaken = false;
            try
            {
                this.@lock.Enter(ref lockTaken);
                long oldStartTime = startTime.Value;
                this.startTime.SetValue(this.clock.Seconds);

                double scalingFactor = Math.Exp(-alpha * (startTime.Value - oldStartTime));

                var keys = new List<double>(this.values.Keys);
                foreach (var key in keys)
                {
                    WeightedSample sample = this.values[key];
                    this.values.Remove(key);
                    double newKey = key * Math.Exp(-alpha * (startTime.Value - oldStartTime));
                    var newSample = new WeightedSample(sample.Value, sample.Weight * scalingFactor);
                    this.values[newKey] = newSample;
                }
                // make sure the counter is in sync with the number of stored samples.
                this.count.SetValue(values.Count);
            }
            finally
            {
                if (lockTaken)
                {
                    this.@lock.Exit();
                }
            }
        }
    }
}
