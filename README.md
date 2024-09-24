# MongoDB.Driver.Core.Extensions.DiagnosticSources

![CI](https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources/workflows/CI/badge.svg)
[![NuGet](https://img.shields.io/nuget/dt/MongoDB.Driver.Core.Extensions.DiagnosticSources.svg)](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.DiagnosticSources) 
[![NuGet](https://img.shields.io/nuget/vpre/MongoDB.Driver.Core.Extensions.DiagnosticSources.svg)](https://www.nuget.org/packages/MongoDB.Driver.Core.Extensions.DiagnosticSources)
[![MyGet (dev)](https://img.shields.io/myget/jbogard-ci/v/MongoDB.Driver.Core.Extensions.DiagnosticSources.svg)](https://myget.org/gallery/jbogard-ci)

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

This package exposes an [`ActivitySource`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activitysource?view=net-5.0) with a `Name` the same as the assembly, `MongoDB.Driver.Core.Extensions.DiagnosticSources`. Use this name in any [`ActivityListener`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activitylistener?view=net-5.0)-based listeners.

All the available [OpenTelemetry semantic tags](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md) are set.
 
This package supports MongoDB C# Driver versions 2.28.0 to 3.0.

