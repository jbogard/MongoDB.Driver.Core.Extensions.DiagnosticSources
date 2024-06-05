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
        internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, SignalVersionHelper.GetVersion<DiagnosticsActivityEventSubscriber>());

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

            if (activity.IsAllDataRequested)
            {                
                activity.AddTag("db.system", "mongodb");
                activity.AddTag("db.connection_id", @event.ConnectionId?.ToString());
                activity.AddTag("db.name", @event.DatabaseNamespace?.DatabaseName);
                activity.AddTag("db.mongodb.collection", collectionName);
                activity.AddTag("db.operation", @event.CommandName);
                activity.AddTag("network.transport", "tcp");

                var endPoint = @event.ConnectionId?.ServerId?.EndPoint;
                switch (endPoint)
                {
                    case IPEndPoint ipEndPoint:
                        activity.AddTag("network.peer.address", ipEndPoint.Address.ToString());
                        activity.AddTag("network.peer.port", ipEndPoint.Port.ToString());
                        break;
                    case DnsEndPoint dnsEndPoint:
                        activity.AddTag("server.address", dnsEndPoint.Host);
                        activity.AddTag("server.port", dnsEndPoint.Port.ToString());
                        break;
                }

                if (_options.CaptureCommandText)
                {
                    activity.AddTag("db.statement", @event.Command.ToString());
                }
            }

            _activityMap.TryAdd(@event.RequestId, activity);
        }

        private void Handle(CommandSucceededEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                WithReplacedActivityCurrent(activity, () =>
                {
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
                        activity.SetStatus(ActivityStatusCode.Error, @event.Failure.Message);
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
