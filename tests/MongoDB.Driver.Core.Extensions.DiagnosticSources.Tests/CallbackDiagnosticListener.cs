using System;
using System.Collections.Generic;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources.Tests
{
    public class CallbackDiagnosticListener : IObserver<KeyValuePair<string, object>>
    {
        private readonly Action<KeyValuePair<string, object>> _callback;

        public CallbackDiagnosticListener(Action<KeyValuePair<string, object>> callback)
            => _callback = callback;

        public void OnNext(KeyValuePair<string, object> value)
            => _callback(value);

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}