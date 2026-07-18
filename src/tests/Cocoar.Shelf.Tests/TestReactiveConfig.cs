using Cocoar.Configuration.Reactive;

namespace Cocoar.Shelf.Tests;

internal sealed class TestReactiveConfig<T>(T value) : IReactiveConfig<T>
{
    public T CurrentValue => value;

    public IDisposable Subscribe(IObserver<T> observer)
    {
        observer.OnNext(value);
        return new NoopDisposable();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
