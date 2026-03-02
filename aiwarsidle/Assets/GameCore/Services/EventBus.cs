using System;
using System.Collections.Generic;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class EventBus : IEventBus
    {
        private sealed class Subscription : IDisposable
        {
            private readonly EventBus _bus;
            private readonly Type _type;
            private readonly Delegate _handler;
            private bool _disposed;

            public Subscription(EventBus bus, Type type, Delegate handler)
            {
                _bus = bus;
                _type = type;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _bus.Unsubscribe(_type, _handler);
            }
        }

        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }

            list.Add(handler);
            return new Subscription(this, type, handler);
        }

        public void Publish<T>(T evt)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list) || list.Count == 0) return;

            for (var i = 0; i < list.Count; i++)
            {
                ((Action<T>)list[i]).Invoke(evt);
            }
        }

        private void Unsubscribe(Type type, Delegate handler)
        {
            if (!_handlers.TryGetValue(type, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0)
            {
                _handlers.Remove(type);
            }
        }
    }
}

