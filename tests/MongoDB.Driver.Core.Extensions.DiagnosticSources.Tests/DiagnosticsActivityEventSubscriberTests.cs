using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Linq;
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
            var stopFired = false;
            var startFired = false;

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Nonsense",
                ActivityStarted = _ => startFired = true,
                ActivityStopped = _ => stopFired = true
            };

            ActivitySource.AddActivityListener(listener);

            var behavior = new DiagnosticsActivityEventSubscriber();

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            startEvent(new CommandStartedEvent());
            stopEvent(new CommandSucceededEvent());

            startFired.ShouldBeFalse();
            stopFired.ShouldBeFalse();
        }

        [Fact]
        public void Should_fire_activity_start_stop_when_sampling()
        {
            var stopFired = false;
            var startFired = false;

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
                ActivityStarted = _ => startFired = true,
                ActivityStopped = _ => stopFired = true
            };
            ActivitySource.AddActivityListener(listener);

            var behavior = new DiagnosticsActivityEventSubscriber();

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
            var stopFired = false;
            var startFired = false;

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
                ActivityStarted = activity =>
                {
                    startFired = true;
                    activity.ShouldNotBeNull();
                    activity.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                },
                ActivityStopped = activity =>
                {
                    activity.ShouldNotBeNull();
                    activity.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                    stopFired = true;
                }
            };
            ActivitySource.AddActivityListener(listener);

            var behavior = new DiagnosticsActivityEventSubscriber();

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
            var exceptionFired = false;
            var startFired = false;

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {   
                    startFired = true;
                    activity.ShouldNotBeNull();
                    activity.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                },
                ActivityStopped = activity =>
                {
                    activity.ShouldNotBeNull();
                    activity.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                    var statusTag = activity.Tags.SingleOrDefault(t => t.Key == "otel.status_code");
                    statusTag.ShouldNotBe(default);
                    statusTag.Value.ShouldBe("Error");
                    exceptionFired = true;
                }
            };
            ActivitySource.AddActivityListener(listener);

            var behavior = new DiagnosticsActivityEventSubscriber();

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandFailedEvent>(out var stopEvent).ShouldBeTrue();

            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 8000)));
            var databaseNamespace = new DatabaseNamespace("test");
            var command = new BsonDocument(new Dictionary<string, object>
            {
                {"update", "my_collection"}
            });
            startEvent(new CommandStartedEvent("update", command, databaseNamespace, null, 1, connectionId));
            stopEvent(new CommandFailedEvent("update", new Exception("Failed"), null, 1, connectionId, TimeSpan.Zero));

            startFired.ShouldBeTrue();
            exceptionFired.ShouldBeTrue();
            Activity.Current.ShouldBeNull();
        }

        [Fact]
        public void Should_record_all_data()
        {
            var stopFired = false;
            var startFired = false;

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    startFired = true;
                    activity.ShouldNotBeNull();
                },
                ActivityStopped = activity =>
                {
                    activity.ShouldNotBeNull();
                    activity.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                    var instanceTag = activity.Tags.SingleOrDefault(t => t.Key == "db.instance");
                    instanceTag.ShouldNotBe(default);
                    instanceTag.Value.ShouldBe("test");

                    activity.Tags.SingleOrDefault(t => t.Key == "db.statement").ShouldBe(default);

                    stopFired = true;
                }
            };
            ActivitySource.AddActivityListener(listener);

            var options = new InstrumentationOptions {CaptureCommandText = false};
            var behavior = new DiagnosticsActivityEventSubscriber(options);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 8000)));
            var databaseNamespace = new DatabaseNamespace("test");
            var command = new BsonDocument(new Dictionary<string, object>
            {
                {"update", "my_collection"}
            });
            startEvent(new CommandStartedEvent("update", command, databaseNamespace, null, 1, connectionId));
            stopEvent(new CommandSucceededEvent("update", command, null, 1, connectionId, TimeSpan.Zero));

            startFired.ShouldBeTrue();
            stopFired.ShouldBeTrue();
        }
     
        [Fact]
        public void Should_record_command_text_when_option_set()
        {
            var stopFired = false;
            var startFired = false;

            var command = new BsonDocument(new Dictionary<string, object>
            {
                {"update", "my_collection"}
            });

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    startFired = true;
                    activity.ShouldNotBeNull();
                },
                ActivityStopped = activity =>
                {
                    activity.ShouldNotBeNull();
                    activity.OperationName.ShouldBe(DiagnosticsActivityEventSubscriber.ActivityName);
                    var statementTag = activity.Tags.SingleOrDefault(t => t.Key == "db.statement");
                    statementTag.ShouldNotBe(default);
                    statementTag.Value.ShouldBe(command.ToString());

                    stopFired = true;
                }
            };
            ActivitySource.AddActivityListener(listener);

            var options = new InstrumentationOptions {CaptureCommandText = true};
            var behavior = new DiagnosticsActivityEventSubscriber(options);

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 8000)));
            var databaseNamespace = new DatabaseNamespace("test");
            startEvent(new CommandStartedEvent("update", command, databaseNamespace, null, 1, connectionId));
            stopEvent(new CommandSucceededEvent("update", command, null, 1, connectionId, TimeSpan.Zero));

            startFired.ShouldBeTrue();
            stopFired.ShouldBeTrue();
        }

        [Fact]
        public void Should_handle_parallel_activities()
        {
            var activities = new List<Activity>();

            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = _ => activities.Add(Activity.Current),
                ActivityStopped = _ => activities.Add(Activity.Current)
            };
            ActivitySource.AddActivityListener(listener);

            var behavior = new DiagnosticsActivityEventSubscriber();

            behavior.TryGetEventHandler<CommandStartedEvent>(out var startEvent).ShouldBeTrue();
            behavior.TryGetEventHandler<CommandSucceededEvent>(out var stopEvent).ShouldBeTrue();

            var outerActivity = new Activity("Outer");
            outerActivity.Start();

            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 8000)));
            var databaseNamespace = new DatabaseNamespace("test");
            var updateCommand = new BsonDocument(new Dictionary<string, object>
            {
                {"update", "my_collection"}
            });
            var insertCommand = new BsonDocument(new Dictionary<string, object>
            {
                {"insert", "my_collection"}
            });
            startEvent(new CommandStartedEvent("update", updateCommand, databaseNamespace, null, 1, connectionId));
            startEvent(new CommandStartedEvent("insert", insertCommand, databaseNamespace, null, 2, connectionId));
            stopEvent(new CommandSucceededEvent("update", updateCommand, null, 1, connectionId, TimeSpan.Zero));
            stopEvent(new CommandSucceededEvent("insert", insertCommand, null, 2, connectionId, TimeSpan.Zero));

            outerActivity.Stop();

            activities.Count(activity => activity != null && activity.OperationName == DiagnosticsActivityEventSubscriber.ActivityName).ShouldBe(4);

            Activity.Current.ShouldBeNull();
        }
    }
}
