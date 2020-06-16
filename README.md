# MongoDB.Driver.Core.Extensions.DiagnosticSources

![CI](https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources/workflows/CI/badge.svg)

## Usage

This repo includes two packages:

 - [MongoDB.Driver.Core.Extensions.DiagnosticSources](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.DiagnosticSources/)
 - [MongoDB.Driver.Core.Extensions.OpenTelemetry](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.OpenTelemetry/)
 
The `MongoDB.Driver.Core.Extensions.DiagnosticSources` package extends the core MongoDB C# driver to expose telemetry information via `System.Diagnostics`.

The `MongoDB.Driver.Core.Extensions.OpenTelemetry` package provides adapters to [OpenTelemetry](https://opentelemetry.io/).

To use `MongoDB.Driver.Core.Extensions.DiagnosticSources`, you need to configure your `MongoClientSettings` to add this MongoDB event subscriber:

```csharp
var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
clientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
var mongoClient = new MongoClient(clientSettings);
```

That event subscriber exposes Activity events via a DiagnosticListener under the root activity name, `MongoDB.Driver.Core.Events.Command`. To subscribe, you may use the `DiagnosticListener.AllListeners` observable.

The following MongoDB events are exposed via `DiagnosticListener` events, with the corresponding MongoDB event object as the diagnostics event argument.

 - [CommandStartedEvent](http://mongodb.github.io/mongo-csharp-driver/2.8/apidocs/html/T_MongoDB_Driver_Core_Events_CommandStartedEvent.htm) - `MongoDB.Driver.Core.Events.Command.Start`
 - [CommandSucceededEvent](http://mongodb.github.io/mongo-csharp-driver/2.8/apidocs/html/T_MongoDB_Driver_Core_Events_CommandSucceededEvent.htm) - `MongoDB.Driver.Core.Events.Command.Stop`
 - [CommandFailedEvent](http://mongodb.github.io/mongo-csharp-driver/2.8/apidocs/html/T_MongoDB_Driver_Core_Events_CommandFailedEvent.htm) - `MongoDB.Driver.Core.Events.Command.Exception`
 
 This package supports MongoDB C# Driver versions 2.3 to 3.0.

## OpenTelemetry usage

Once you've configured your MongoDB client to expose diagnostics events as above, you can configure OpenTelemetry (typically through the [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/0.2.0-alpha.275) package).

```csharp
services.AddOpenTelemetry(builder => {
    builder
        // Configure exporters
        .UseZipkin()
        // Configure adapters
        .UseRequestAdapter()
        .UseDependencyAdapter()
        .AddMongoDBAdapter(); // Adds MongoDB OTel support
});
```

This package supports the latest released alpha package on NuGet.
