using System;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using MongoDB.Driver.Core.Extensions.OpenTelemetry.Implementation;
using OpenTelemetry.Adapter;
using OpenTelemetry.Trace;

namespace MongoDB.Driver.Core.Extensions.OpenTelemetry
{
    public class MongoDBCommandAdapter : IDisposable
    {
        private readonly DiagnosticSourceSubscriber _diagnosticSourceSubscriber;

        public MongoDBCommandAdapter(Tracer tracer, MongoDBInstrumentationOptions options)
        {
            _diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new CommandListener(DiagnosticsActivityEventSubscriber.ActivityName, tracer, options), null);
            _diagnosticSourceSubscriber.Subscribe();
        }

        public void Dispose()
            => _diagnosticSourceSubscriber?.Dispose();
    }
}
