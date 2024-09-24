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

            var databaseName = @event.DatabaseNamespace?.DatabaseName;
            var collectionName = @event.GetCollectionName();

            // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md
            activity.DisplayName = string.IsNullOrEmpty(collectionName) ? $"{@event.CommandName} {databaseName}" : $"{@event.CommandName} {collectionName}";

            activity.AddTag("db.system", "mongodb");
            activity.AddTag("db.connection_id", @event.ConnectionId?.ToString());
            activity.AddTag("db.namespace", databaseName);
            activity.AddTag("db.collection.name", collectionName);
            activity.AddTag("db.operation.name", @event.CommandName);
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

            if (activity.IsAllDataRequested && _options.CaptureCommandText)
            {
                activity.AddTag("db.query.text", @event.Command.ToString());
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
                    var tags = new ActivityTagsCollection
                    {
                        { "exception.type", @event.Failure.GetType().FullName },
                        { "exception.stacktrace", @event.Failure.ToString() },
                    };

                    if (!string.IsNullOrEmpty(@event.Failure.Message))
                    {
                        tags.Add("exception.message", @event.Failure.Message);
                    }

                    activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
                    activity.SetStatus(ActivityStatusCode.Error);
                    activity.Stop();
                });
            }
        }

        private static void WithReplacedActivityCurrent(Activity activity, Action action)
        {
            var current = Activity.Current;
            if (activity == current)
            {
                action();
                return;
            }

            try
            {
                Activity.Current = activity;
                action();
            }
            finally
            {
                if (current?.IsStopped == true) // it's forbidden to assign stopped activity to Activity.Current
                {
                    Activity.Current = null;
                }
                else
                {
                    Activity.Current = current;
                }
            }
        }
    }
}
