using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources.Tests
{
    internal class MongoDBDiagnosticObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly Action<KeyValuePair<string, object>> nextEventCallback;

        public MongoDBDiagnosticObserver(Action<KeyValuePair<string, object>> nextEventCallback)
        {
            this.nextEventCallback = nextEventCallback;
        }

        void IObserver<KeyValuePair<string, object>>.OnNext(KeyValuePair<string, object> pair)
        {
            this.nextEventCallback(pair);
        }

        void IObserver<KeyValuePair<string, object>>.OnError(Exception error)
        { }

        void IObserver<KeyValuePair<string, object>>.OnCompleted()
        { }

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener diagnosticListener)
        {
            if (diagnosticListener.Name == "MongoDB.Driver.Core.Extensions.DiagnosticSources")
            {
                var subscription = diagnosticListener.Subscribe(this);
                _subscriptions.Add(subscription);
            }
        }

        void IObserver<DiagnosticListener>.OnError(Exception error)
        { }

        void IObserver<DiagnosticListener>.OnCompleted()
        {
            _subscriptions.ForEach(x => x.Dispose());
            _subscriptions.Clear();
        }
    }
}