﻿using System;

namespace Metrics.Core
{
    public class HealthCheck
    {
        protected HealthCheck(string name)
            : this(name, () => { })
        {
        }

        public HealthCheck(string name, Action check)
            : this(name, () =>
                {
                    check();
                    return string.Empty;
                })
        {
        }

        public HealthCheck(string name, Func<string> check)
            : this(name, () => HealthCheckResult.Healthy(check()))
        {
        }

        public HealthCheck(string name, Func<HealthCheckResult> check)
        {
            this.Name = name;
            this.check = check;
        }

        public struct Result
        {
            public Result(string name, HealthCheckResult check)
            {
                this.Name = name;
                this.Check = check;
            }

            public readonly string Name;
            public readonly HealthCheckResult Check;
        }

        public string Name { get; }

        protected virtual HealthCheckResult Check()
        {
            return this.check();
        }

        public Result Execute()
        {
            try
            {
                return new Result(this.Name, this.Check());
            }
            catch (Exception x)
            {
                return new Result(this.Name, HealthCheckResult.Unhealthy(x));
            }
        }

        private readonly Func<HealthCheckResult> check;
    }
}