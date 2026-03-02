using System;

namespace AIWarsIdle.GameCore.Services
{
    public interface IEventBus
    {
        IDisposable Subscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
    }
}

