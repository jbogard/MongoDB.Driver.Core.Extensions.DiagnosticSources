using System;
using OpenTelemetry.Trace.Configuration;

namespace MongoDB.Driver.Core.Extensions.OpenTelemetry
{
    public static class TraceBuilderExtensions
    {
        public static TracerBuilder AddMongoDBAdapter(this TracerBuilder builder)
            => builder.AddMongoDBAdapter(null);

        public static TracerBuilder AddMongoDBAdapter(this TracerBuilder builder, Action<MongoDBInstrumentationOptions> configureInstrumentationOptions)
        {
            configureInstrumentationOptions ??= opt => { };

            var options = new MongoDBInstrumentationOptions();

            configureInstrumentationOptions(options);

            return builder.AddAdapter(t => new MongoDBCommandAdapter(t, options));
        }
    }
}