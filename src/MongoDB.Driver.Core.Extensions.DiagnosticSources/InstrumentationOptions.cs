using System;
using MongoDB.Driver.Core.Events;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources
{
    public class InstrumentationOptions
    {
        public bool CaptureCommandText { get; set; }
        public Func<CommandStartedEvent, bool> ShouldStartActivity { get; set; }
    }
}