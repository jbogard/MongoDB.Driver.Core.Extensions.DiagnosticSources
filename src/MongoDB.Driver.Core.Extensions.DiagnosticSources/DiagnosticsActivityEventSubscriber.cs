using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using MongoDB.Driver.Core.Events;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources
{
    public class DiagnosticsActivityEventSubscriber : IEventSubscriber
    {
        public const string ActivityName = "MongoDB.Driver.Core.Events.Command";
        private const string ActivityStartEventName = ActivityName + ".Start";
        private const string ActivityStopEventName = ActivityName + ".Stop";
        private const string ActivityExceptionEventName = ActivityName + ".Exception";

        private readonly DiagnosticListener _diagnosticListener;
        private readonly ReflectionEventSubscriber _subscriber;

        public DiagnosticsActivityEventSubscriber(DiagnosticListener diagnosticListener)
        {
            _diagnosticListener = diagnosticListener;
            _subscriber = new ReflectionEventSubscriber(this,
                bindingFlags: BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public DiagnosticsActivityEventSubscriber()
            : this(new DiagnosticListener(ActivityName))
        {
        }

        public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler)
            => _subscriber.TryGetEventHandler(out handler);

        private readonly ConcurrentDictionary<int, Activity> _activityMap
            = new ConcurrentDictionary<int, Activity>();

        private void Handle(CommandStartedEvent @event)
        {
            var activity = new Activity(ActivityName);

            if (_diagnosticListener.IsEnabled(ActivityStartEventName, @event))
            {
                _diagnosticListener.StartActivity(activity, @event);
            }
            else
            {
                activity.Start();
            }

            _activityMap.TryAdd(@event.RequestId, activity);
        }

        private void Handle(CommandSucceededEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                if (_diagnosticListener.IsEnabled(ActivityStopEventName, @event))
                {
                    _diagnosticListener.StopActivity(activity, @event);
                }
                else
                {
                    activity.Stop();
                }
            }
        }

        private void Handle(CommandFailedEvent @event)
        {
            if (_activityMap.TryRemove(@event.RequestId, out var activity))
            {
                if (_diagnosticListener.IsEnabled(ActivityExceptionEventName, @event))
                {
                    _diagnosticListener.Write(ActivityExceptionEventName, @event);
                }
                activity.Stop();
            }
        }
    }

}
