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

That event subscriber exposes Activity events via a DiagnosticListener under the root activity name, `MongoDB.Driver.Core.Events.Command`. To subscribe, you may use the `DiagnosticListener.AllListeners` observable.

The following MongoDB events are exposed via `DiagnosticListener` events, with the corresponding MongoDB event object as the diagnostics event argument.

 - [CommandStartedEvent](http://mongodb.github.io/mongo-csharp-driver/2.8/apidocs/html/T_MongoDB_Driver_Core_Events_CommandStartedEvent.htm) - `MongoDB.Driver.Core.Events.Command.Start`
 - [CommandSucceededEvent](http://mongodb.github.io/mongo-csharp-driver/2.8/apidocs/html/T_MongoDB_Driver_Core_Events_CommandSucceededEvent.htm) - `MongoDB.Driver.Core.Events.Command.Stop`
 - [CommandFailedEvent](http://mongodb.github.io/mongo-csharp-driver/2.8/apidocs/html/T_MongoDB_Driver_Core_Events_CommandFailedEvent.htm) - `MongoDB.Driver.Core.Events.Command.Exception`
 
 This package supports MongoDB C# Driver versions 2.3 to 3.0.

