﻿﻿// Copyright (c) Attack Pattern LLC.  All rights reserved.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Diagnostics;
using Windows.Storage;

namespace CSharpAnalytics.Sessions
{
    public enum SessionStatus { Starting, Active, Ending };
    public enum VisitorStatus { Active, OptedOut, SampledOut };

    /// <summary>
    /// Manages visitors and sessions to ensure they are correctly saved, restored and time-out as appropriate.
    /// </summary>
    public class SessionManager
    {
        private const String guidKey = "GuidKey";

        private readonly Random random = new Random();
        private readonly Visitor visitor;

        protected DateTimeOffset lastActivityAt = DateTimeOffset.Now;
        protected readonly object newSessionLock = new object();

        private Func<double> sampleSelector;

        /// <summary>
        /// Recreate a SessionManager from state.
        /// </summary>
        /// <param name="sessionState">SessionState containing details captured from a previous SessionManager or null if no previous SessionManager.</param>
        /// <param name="sampleRate">Sample rate to determine likelyhood of a new installation being tracked or not.</param>
        /// <returns>Recreated SessionManager.</returns>
        public SessionManager(SessionState sessionState, double sampleRate = 100.0)
        {
            if (sessionState != null)
            {
                visitor = new Visitor(sessionState.VisitorId, sessionState.FirstVisitAt);
                Session = new Session(sessionState.SessionStartedAt, sessionState.SessionNumber, sessionState.SessionHitCount, sessionState.HitId);
                Referrer = sessionState.Referrer;
                PreviousSessionStartedAt = sessionState.PreviousSessionStartedAt;
                lastActivityAt = sessionState.LastActivityAt;
                SessionStatus = sessionState.SessionStatus;
                VisitorStatus = sessionState.VisitorStatus;
            }
            else
            {
                var localStorage = ApplicationData.Current.LocalSettings;
                Guid guid = Guid.Empty;
                if (localStorage.Values.ContainsKey(guidKey) ) {
                    guid =  (Guid)localStorage.Values[guidKey];
                } else {
                    // Need a better way to get a unique guid
                    guid = Guid.NewGuid();
                    localStorage.Values[guidKey] = guid;
                }

                visitor = new Visitor(guid);
                Session = new Session();
                PreviousSessionStartedAt = Session.StartedAt;
                SessionStatus = SessionStatus.Starting;
                var willTrackThisVisitor = ShouldTrackThisNewVisitor(sampleRate);
                VisitorStatus = willTrackThisVisitor ? VisitorStatus.Active : VisitorStatus.SampledOut;
                if (!willTrackThisVisitor)
                    Debug.WriteLine("Will not track this visitor. Sampling rate of {0} excluded them", sampleRate);
            }
        }

        /// <summary>
        /// Whether a new visitor should be tracked or not.
        /// </summary>
        /// <param name="sampleRate">Current sample rate to determine probability of selection.</param>
        /// <returns>True if this visitor should be tracked, false if they should be ignored.</returns>
        internal bool ShouldTrackThisNewVisitor(double sampleRate)
        {
            if (sampleRate == 100) return true;
            if (sampleRate == 0) return false;
            
            return SampleSelector() <= sampleRate;
        }

        /// <summary>
        /// Provides sample selection to determine if this installation will be included in
        /// analytics tracking.
        /// </summary>
        /// <remarks>
        /// This function is extracted into a property to facilitate unit testing.
        /// </remarks>
        internal Func<double> SampleSelector
        {
            private get { return sampleSelector ?? (() => random.NextDouble() * 100); }
            set { sampleSelector = value; }
        }

        /// <summary>
        /// Manually start a new session. Useful for scoping out session custom variables,
        /// e.g. if an anonymous user becomes known via sign-in.
        /// </summary>
        public void StartNewSession()
        {
            StartNewSession(DateTimeOffset.Now);
        }

        /// <summary>
        /// Current status of this session.
        /// </summary>
        public SessionStatus SessionStatus { get; private set; }

        /// <summary>
        /// Current status of this visitor.
        /// </summary>
        public VisitorStatus VisitorStatus { get; internal set; }

        /// <summary>
        /// Current session.
        /// </summary>
        public Session Session { get; private set; }

        /// <summary>
        /// Visitor.
        /// </summary>
        public Visitor Visitor { get { return visitor; } }

        /// <summary>
        /// When the previous session started at.
        /// </summary>
        public DateTimeOffset PreviousSessionStartedAt { get; private set; }

        /// <summary>
        /// Last page or URI visited to act as referrer for subsequent ones.
        /// </summary>
        public Uri Referrer { get; internal set; }

        /// <summary>
        /// Capture details of the SessionManager into a SessionState that can be safely stored and restored.
        /// </summary>
        /// <returns>SessionState representing the current state of the SessionManager.</returns>
        public SessionState GetState()
        {
            return new SessionState
            {
                FirstVisitAt = Visitor.FirstVisitAt,
                VisitorId = Visitor.ClientId,
                SessionStartedAt = Session.StartedAt,
                SessionHitCount = Session.HitCount,
                HitId = Session.HitId,
                SessionNumber = Session.Number,
                PreviousSessionStartedAt = PreviousSessionStartedAt,
                LastActivityAt = lastActivityAt,
                Referrer = Referrer,
                SessionStatus = SessionStatus,
                VisitorStatus = VisitorStatus
            };
        }

        /// <summary>
        /// Record a hit to this session to ensure counts and timeouts are honoured.
        /// </summary>
        internal virtual void Hit()
        {
            lastActivityAt = DateTimeOffset.Now;
            MoveToNextSessionStatus();
            Session.IncreaseHitCount();
        }

        /// <summary>
        /// Move to the next session status, e.g. Ending to Starting, Starting to Active.
        /// </summary>
        private void MoveToNextSessionStatus()
        {
            switch (SessionStatus)
            {
                case SessionStatus.Ending:
                    SessionStatus = SessionStatus.Starting;
                    break;
                case SessionStatus.Starting:
                    SessionStatus = SessionStatus.Active;
                    break;
            }
        }

        /// <summary>
        /// End the current session.
        /// </summary>
        internal void End()
        {
            SessionStatus = SessionStatus.Ending;
        }

        /// <summary>
        /// Start a new session.
        /// </summary>
        /// <param name="startedAt">When this session started.</param>
        protected void StartNewSession(DateTimeOffset startedAt)
        {
            lock (newSessionLock)
            {
                PreviousSessionStartedAt = Session.StartedAt;
                Session = new Session(startedAt, Session.Number + 1);
                SessionStatus = SessionStatus.Starting;
            }
        }
    }
}