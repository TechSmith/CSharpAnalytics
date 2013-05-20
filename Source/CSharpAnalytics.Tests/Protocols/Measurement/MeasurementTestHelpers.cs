﻿using System;
using CSharpAnalytics.Protocols.Measurement;
using CSharpAnalytics.Sessions;

namespace CSharpAnalytics.Test.Protocols.Measurement
{
    internal static class MeasurementTestHelpers
    {
        public static readonly MeasurementConfiguration MeasurementConfiguration = new MeasurementConfiguration("UA-319000-10", "AppName", "1.2.3.4");

        public static SessionManager CreateSessionManager()
        {
            return new SessionManager(TimeSpan.FromSeconds(20), new SessionState());
        }

        public static Environment CreateEnvironment()
        {
            return new Environment("en-us");
        }

        public static void ConfigureForTest(MeasurementAnalyticsClient client, Action<Uri> sender)
        {
            client.Configure(MeasurementConfiguration, CreateSessionManager(), CreateEnvironment(), sender);
        }
    }
}