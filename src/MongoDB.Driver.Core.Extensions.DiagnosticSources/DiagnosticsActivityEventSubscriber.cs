﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using MongoDB.Driver.Core.Events;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources
{
    public class DiagnosticsActivityEventSubscriber : IEventSubscriber
    {
        private readonly InstrumentationOptions _options;
        internal static readonly AssemblyName AssemblyName = typeof(DiagnosticsActivityEventSubscriber).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

        public const string ActivityName = "MongoDB.Driver.Core.Events.Command";

        private readonly ReflectionEventSubscriber _subscriber;
        private readonly ConcurrentDictionary<int, Activity> _activityMap = new();

        public DiagnosticsActivityEventSubscriber() : this(new InstrumentationOptions { CaptureCommandText = false })
        {
        }

        public DiagnosticsActivityEventSubscriber(InstrumentationOptions options)
        {
            _options = options;
            _subscriber = new ReflectionEventSubscriber(this, bindingFlags: BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler)
            => _subscriber.TryGetEventHandler(out handler);

        private void Handle(CommandStartedEvent @event)
        {
            if (_options.ShouldStartActivity != null && !_options.ShouldStartActivity(@event))
            {
                return;
            }

            var activity = ActivitySource.StartActivity(ActivityName, ActivityKind.Client);

            if (activity == null)
            {
                return;
            }

            var collectionName = @event.GetCollectionName();

            // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/database.md
            activity.DisplayName = collectionName == null ? $"mongodb.{@event.CommandName}" : $"{collectionName}.{@event.CommandName}";

            activity.AddTag("db.system", "mongodb");
            activity.AddTag("db.connection_id", @event.ConnectionId?.ToString());
            activity.AddTag("db.name", @event.DatabaseNamespace?.DatabaseName);
            activity.AddTag("db.mongodb.collection", collectionName);
            activity.AddTag("db.operation", @event.CommandName);
            activity.AddTag("net.transport", "ip_tcp");

            var endPoint = @event.ConnectionId?.ServerId?.EndPoint;
            switch (endPoint)
            {
                case IPEndPoint ipEndPoint:
                    activity.AddTag("net.peer.port", ipEndPoint.Port.ToString());
                    activity.AddTag("net.sock.peer.addr", ipEndPoint.Address.ToString());
                    break;
                case DnsEndPoint dnsEndPoint:
                    activity.AddTag("net.peer.name", dnsEndPoint.Host);
                    activity.AddTag("net.peer.port", dnsEndPoint.Port.ToString());
                    break;
            }

            if (activity.IsAllDataRequested && (_options.CaptureCommandText || _options.ShouldCaptureCommandText?.Invoke(@event) == true))
            {
                activity.AddTag("db.statement", @event.Command.ToString());
            }

            _activityMap.TryAdd(@event.RequestId, activity);
        }

        private void Handle(CommandSucceededEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                WithReplacedActivityCurrent(activity, () =>
                {
                    activity.AddTag("otel.status_code", "OK");
                    activity.SetStatus(ActivityStatusCode.Ok);
                    activity.Stop();
                });
            }
        }

        private void Handle(CommandFailedEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                WithReplacedActivityCurrent(activity, () =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.AddTag("otel.status_code", "ERROR");
                        activity.AddTag("otel.status_description", @event.Failure.Message);
                        activity.AddTag("exception.type", @event.Failure.GetType().FullName);
                        activity.AddTag("exception.message", @event.Failure.Message);
                        activity.AddTag("exception.stacktrace", @event.Failure.StackTrace);
                    }
   
                    activity.SetStatus(ActivityStatusCode.Error);
                    activity.Stop();
                });
            }
        }

        private static void WithReplacedActivityCurrent(Activity activity, Action action)
        {
            var current = Activity.Current;
            try
            {
                Activity.Current = activity;
                action();
            }
            finally
            {
                Activity.Current = current;
            }
        }
    }
}
