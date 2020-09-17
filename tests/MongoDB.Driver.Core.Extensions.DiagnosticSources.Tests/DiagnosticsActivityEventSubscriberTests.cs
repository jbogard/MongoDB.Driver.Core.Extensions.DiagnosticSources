using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;
using Shouldly;
using Xunit;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources.Tests
{
    public class DiagnosticsActivityEventSubscriberTests 
    {
        static DiagnosticsActivityEventSubscriberTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        [Fact]
        public void Should_not_fire_activity_start_stop_when_no_listener_attached()
        {
            var diagnosticListener = new DiagnosticListener("DummySource");
            var stopFired = false;
            var startFired = false;

            diagnosticListener.Subscribe(new CallbackDiagnosticListener(pair =>
                {
                    // This should not fire
                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Start")
                    {
                        startFired = true;
                    }

                    // This should not fire
                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Stop")
                    {
                        stopFired = true;
                    }
                }),
                (s, o, arg3) => false);

            var behavior = new DiagnosticsActivityEventSubscriber(diagnosticListener);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            startEvent(new CommandStartedEvent());
            stopEvent(new CommandSucceededEvent());

            startFired.ShouldBeFalse();
            stopFired.ShouldBeFalse();
        }

        [Fact]
        public void Should_fire_activity_start_stop_when_listener_attached()
        {
            var diagnosticListener = new DiagnosticListener("DummySource");
            var stopFired = false;
            var startFired = false;

            diagnosticListener.Subscribe(new CallbackDiagnosticListener(pair =>
                {
                    // This should not fire
                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Start")
                    {
                        startFired = true;
                    }

                    // This should not fire
                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Stop")
                    {
                        stopFired = true;
                    }
                }));

            var behavior = new DiagnosticsActivityEventSubscriber(diagnosticListener);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            startEvent(new CommandStartedEvent());
            stopEvent(new CommandSucceededEvent());

            startFired.ShouldBeTrue();
            stopFired.ShouldBeTrue();
            Activity.Current.ShouldBeNull();
        }

        [Fact]
        public void Should_start_and_log_successful_activity()
        {
            var diagnosticListener = new DiagnosticListener("DummySource");
            var stopFired = false;
            var startFired = false;

            diagnosticListener.Subscribe(new CallbackDiagnosticListener(pair =>
                {
                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Start")
                    {
                        startFired = true;
                        pair.Value.ShouldNotBeNull();
                        Activity.Current.ShouldNotBeNull();
                        Activity.Current.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                        pair.Value.ShouldBeAssignableTo<CommandStartedEvent>();
                    }

                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Stop")
                    {
                        stopFired = true;
                        pair.Value.ShouldNotBeNull();
                        Activity.Current.ShouldNotBeNull();
                        Activity.Current.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                        pair.Value.ShouldBeAssignableTo<CommandSucceededEvent>();
                    }
                }));

            var behavior = new DiagnosticsActivityEventSubscriber(diagnosticListener);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            startEvent(new CommandStartedEvent());
            stopEvent(new CommandSucceededEvent());

            startFired.ShouldBeTrue();
            stopFired.ShouldBeTrue();
            Activity.Current.ShouldBeNull();
        }

        [Fact]
        public void Should_start_and_log_failed_activity()
        {
            var diagnosticListener = new DiagnosticListener("DummySource");
            var exceptionFired = false;
            var startFired = false;

            diagnosticListener.Subscribe(new CallbackDiagnosticListener(pair =>
                {
                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Start")
                    {
                        startFired = true;
                        pair.Value.ShouldNotBeNull();
                        Activity.Current.ShouldNotBeNull();
                        Activity.Current.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                        pair.Value.ShouldBeAssignableTo<CommandStartedEvent>();
                    }

                    if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Exception")
                    {
                        exceptionFired = true;
                        pair.Value.ShouldNotBeNull();
                        Activity.Current.ShouldNotBeNull();
                        Activity.Current.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                        pair.Value.ShouldBeAssignableTo<CommandFailedEvent>();
                    }
                }));

            var behavior = new DiagnosticsActivityEventSubscriber(diagnosticListener);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandFailedEvent>(out var stopEvent).ShouldBeTrue();

            startEvent(new CommandStartedEvent());
            stopEvent(new CommandFailedEvent());

            startFired.ShouldBeTrue();
            exceptionFired.ShouldBeTrue();
            Activity.Current.ShouldBeNull();
        }

        [Fact]
        public void Should_handle_parallel_activities()
        {
            var diagnosticListener = new DiagnosticListener("DummySource");

            var activities = new List<Activity>();

            diagnosticListener.Subscribe(new CallbackDiagnosticListener(pair =>
            {
                if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Start")
                {
                    pair.Value.ShouldNotBeNull();
                    pair.Value.ShouldBeAssignableTo<CommandStartedEvent>();
                    activities.Add(Activity.Current);
                }

                if (pair.Key == $"{DiagnosticsActivityEventSubscriber.ActivityName}.Stop")
                {
                    pair.Value.ShouldNotBeNull();
                    pair.Value.ShouldBeAssignableTo<CommandSucceededEvent>();
                    activities.Add(Activity.Current);
                }
            }));

            var behavior = new DiagnosticsActivityEventSubscriber(diagnosticListener);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            var outerActivity = new Activity("Outer");
            outerActivity.Start();

            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 8000)));
            var databaseNamespace = new DatabaseNamespace("test");
            startEvent(new CommandStartedEvent("update", new BsonDocument(), databaseNamespace, null, 1, connectionId));
            startEvent(new CommandStartedEvent("insert", new BsonDocument(), databaseNamespace, null, 2, connectionId));
            stopEvent(new CommandSucceededEvent("update", new BsonDocument(), null, 1, connectionId, TimeSpan.Zero));
            stopEvent(new CommandSucceededEvent("insert", new BsonDocument(), null, 2, connectionId, TimeSpan.Zero));

            outerActivity.Stop();

            activities.Count(activity => activity != null && activity.OperationName == DiagnosticsActivityEventSubscriber.ActivityName).ShouldBe(4);

            Activity.Current.ShouldBeNull();
        }
    }
}
