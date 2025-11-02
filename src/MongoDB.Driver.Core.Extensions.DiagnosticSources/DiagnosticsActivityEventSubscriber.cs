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
        public static readonly string ActivitySourceName = AssemblyName.Name;
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

            if (activity is null)
            {
                return;
            }

            var databaseName = @event.DatabaseNamespace?.DatabaseName;
            var collectionName = @event.GetCollectionName();
            // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md
            activity.DisplayName = string.IsNullOrEmpty(collectionName) ? $"{@event.CommandName} {databaseName}" : $"{@event.CommandName} {collectionName}";

            if (activity is { IsAllDataRequested: true })
            {
                activity.AddTag("db.system.name", "mongodb");
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

                if (_options.CaptureCommandText)
                {
                    activity.AddTag("db.query.text", @event.Command.ToString());
                }   
            }

            _activityMap.TryAdd(@event.RequestId, activity);
        }

        private void Handle(CommandSucceededEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                WithReplacedActivityCurrent(activity, @event, static (a, _) =>
                {
                    a.Stop();
                });
            }
        }

        private void Handle(CommandFailedEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                WithReplacedActivityCurrent(activity, @event, static (a, e) =>
                {
                    if (a is { IsAllDataRequested: true })
                    {
                        var tags = new ActivityTagsCollection
                        {
                            { "exception.type", e.Failure.GetType().FullName },
                            { "exception.stacktrace", e.Failure.ToString() },
                        };

                        if (!string.IsNullOrEmpty(e.Failure.Message))
                        {
                            tags.Add("exception.message", e.Failure.Message);
                        }

                        a.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
                    }
                    
                    a.SetStatus(ActivityStatusCode.Error);
                    a.Stop();
                });
            }
        }

        private static void WithReplacedActivityCurrent<TEvent>(Activity activity, TEvent @event, Action<Activity, TEvent> action)
        {
            var current = Activity.Current;
            if (activity == current)
            {
                action(activity, @event);
                return;
            }

            try
            {
                Activity.Current = activity;
                action(activity, @event);
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
