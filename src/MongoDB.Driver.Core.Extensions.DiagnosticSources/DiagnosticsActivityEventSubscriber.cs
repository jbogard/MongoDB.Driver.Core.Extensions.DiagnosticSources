using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private static readonly HashSet<string> CommandsWithCollectionNameAsValue =
            new HashSet<string>
            {
                "aggregate",
                "count",
                "distinct",
                "mapReduce",
                "geoSearch",
                "delete",
                "find",
                "killCursors",
                "findAndModify",
                "insert",
                "update",
                "create",
                "drop",
                "createIndexes",
                "listIndexes"
            };

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
            var activity = ActivitySource.StartActivity(ActivityName, ActivityKind.Client);

            if (activity == null)
            {
                return;
            }

            activity.DisplayName = $"mongodb.{@event.CommandName}";

            if (activity.IsAllDataRequested)
            {
                activity.AddTag("db.system", "mongo");
                activity.AddTag("db.instance", @event.DatabaseNamespace.DatabaseName);
;               activity.AddTag("db.mongodb.collection", GetCollectionName(@event));
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
                    activity.AddTag("otel.status_code", "Ok");
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
                        activity.AddTag("otel.status_code", "Error");
                        activity.AddTag("otel.status_description", @event.Failure.Message);
                        activity.AddTag("error.type", @event.Failure.GetType().FullName);
                        activity.AddTag("error.msg", @event.Failure.Message);
                        activity.AddTag("error.stack", @event.Failure.StackTrace);
                    }

                    activity.Stop();
                });
            }
        }

        private static string GetCollectionName(CommandStartedEvent @event)
        {
            if (@event.CommandName == "getMore")
            {
                if (@event.Command.Contains("collection"))
                {
                    var collectionValue = @event.Command.GetValue("collection");
                    if (collectionValue.IsString)
                    {
                        return collectionValue.AsString;
                    }
                }
            }
            else if (CommandsWithCollectionNameAsValue.Contains(@event.CommandName))
            {
                var commandValue = @event.Command.GetValue(@event.CommandName);
                if (commandValue != null && commandValue.IsString)
                {
                    return commandValue.AsString;
                }
            }

            return null;
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
