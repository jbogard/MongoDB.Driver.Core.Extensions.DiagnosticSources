using System;
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
        internal static readonly DiagnosticSource DiagnosticSource = new DiagnosticListener(ActivitySourceName);

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

            if (DiagnosticSource.IsEnabled(activity.OperationName))
            {
                DiagnosticSource.StartActivity(activity, @event);
            }

            var collectionName = @event.GetCollectionName();

            // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/database.md
            activity.DisplayName = collectionName == null ? $"mongodb.{@event.CommandName}" : $"{collectionName}.{@event.CommandName}";

            activity.AddTag("db.system", "mongodb");
            activity.AddTag("db.name", @event.DatabaseNamespace?.DatabaseName);
            activity.AddTag("db.mongodb.collection", collectionName);
            activity.AddTag("db.operation", @event.CommandName);
            var endPoint = @event.ConnectionId?.ServerId?.EndPoint;
            switch (endPoint)
            {
                case IPEndPoint ipEndPoint:
                    activity.AddTag("db.user", $"mongodb://{ipEndPoint.Address}:{ipEndPoint.Port}");
                    activity.AddTag("net.peer.ip", ipEndPoint.Address.ToString());
                    activity.AddTag("net.peer.port", ipEndPoint.Port.ToString());
                    break;
                case DnsEndPoint dnsEndPoint:
                    activity.AddTag("db.user", $"mongodb://{dnsEndPoint.Host}:{dnsEndPoint.Port}");
                    activity.AddTag("net.peer.name", dnsEndPoint.Host);
                    activity.AddTag("net.peer.port", dnsEndPoint.Port.ToString());
                    break;
            }

            if (activity.IsAllDataRequested && _options.CaptureCommandText)
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
                    activity.AddTag("otel.status_code", "Ok");
                    
                    if (DiagnosticSource.IsEnabled(activity.OperationName))
                    {
                        DiagnosticSource.StopActivity(activity, null);
                    }
                    else
                    {
                        activity.Stop();
                    }
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
                        activity.AddTag("otel.status_code", "Error");
                        activity.AddTag("otel.status_description", @event.Failure.Message);
                        activity.AddTag("error.type", @event.Failure.GetType().FullName);
                        activity.AddTag("error.msg", @event.Failure.Message);
                        activity.AddTag("error.stack", @event.Failure.StackTrace);
                    }

                    if (DiagnosticSource.IsEnabled(activity.OperationName))
                    {
                        DiagnosticSource.StopActivity(activity, null);
                    }
                    else
                    {
                        activity.Stop();
                    }
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
