# MongoDB.Driver.Core.Extensions.DiagnosticSources

![CI](https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources/workflows/CI/badge.svg)
[![NuGet](https://img.shields.io/nuget/dt/MongoDB.Driver.Core.Extensions.DiagnosticSources.svg)](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.DiagnosticSources) 
[![NuGet](https://img.shields.io/nuget/vpre/MongoDB.Driver.Core.Extensions.DiagnosticSources.svg)](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.DiagnosticSources)
[![MyGet (dev)](https://img.shields.io/myget/jbogard-ci/v/MongoDB.Driver.Core.Extensions.DiagnosticSources.svg)](https://myget.org/gallery/jbogard-ci)

This package supports MongoDB C# Driver versions 3.0.0 and above.

## Usage

This repo includes the package:

 - [MongoDB.Driver.Core.Extensions.DiagnosticSources](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.DiagnosticSources/)

The `MongoDB.Driver.Core.Extensions.DiagnosticSources` package extends the core MongoDB C# driver to expose telemetry information via `System.Diagnostics`.

To use `MongoDB.Driver.Core.Extensions.DiagnosticSources`, you need to configure your `MongoClientSettings` to add this MongoDB event subscriber:

```csharp
var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
clientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
var mongoClient = new MongoClient(clientSettings);
```

To capture the command text as part of the activity:

```csharp
var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
var options = new InstrumentationOptions { CaptureCommandText = true };
clientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber(options));
var mongoClient = new MongoClient(clientSettings);
```

To filter activities by collection name:

```csharp
var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
var options = new InstrumentationOptions { ShouldStartActivity = @event => !"collectionToIgnore".Equals(@event.GetCollectionName()) };
clientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber(options));
var mongoClient = new MongoClient(clientSettings);
```

When using `CaptureCommandText = true` with GridFS, consider customizing the `ShouldStartActivity` logic to prevent large binary file data from chunk collection operations being captured in activity traces.

This package exposes an [`ActivitySource`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activitysource?view=net-5.0) with a `Name` the same as the assembly, `MongoDB.Driver.Core.Extensions.DiagnosticSources`, or use the `DiagnosticsActivityEventSubscriber.ActivitySourceName`. Use this name in any [`ActivityListener`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activitylistener?view=net-5.0)-based listeners.

All the available [OpenTelemetry semantic tags](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md) are set.
 
### Usage with OpenTelemetry

To add the listener for the OpenTelemetry SDK, add your listener in the OpenTelemetry tracing configuration, usually configured with the [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting) package:

```csharp
builder.Services.AddOpenTelemetry()
    // Metrics, logging etc.
    .WithTracing(tracing =>
    {
        // Other tracing configuration

        tracing
            // Other sources (ASP.NET Core, HttpClient etc.)
            .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources");
    });
```

This can also be added with the OpenTelemetry SDK directly.
