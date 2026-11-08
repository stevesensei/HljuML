namespace HljuML.Core.GamePlay;

public sealed class OperationCoordinatorService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Lease(_gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}
