using System.Diagnostics;
using HljuML.Core.Infrastructure.Settings;

namespace HljuML.Core.Infrastructure.Downloads;

/// <summary>所有下载共用并发配额和带宽，限速以十进制 MB/s 计算，不占用网络超时时间。</summary>
public sealed class DownloadTrafficService : IDisposable
{
    private readonly LauncherSettingsService _settings;
    private readonly object _sync = new();
    private TaskCompletionSource _changed = NewSignal();
    private readonly SemaphoreSlim _bandwidth = new(1, 1);
    private int _active;
    private double _nextTransfer;

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public int Concurrency => _settings.Value.DownloadThreads;

    public DownloadTrafficService(LauncherSettingsService settings)
    {
        _settings = settings;
        settings.Changed += OnChanged;
    }

    public async ValueTask<IDisposable> AcquireAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Task wait;
            lock (_sync)
            {
                if (_active < Concurrency)
                {
                    _active++;
                    return new Lease(this);
                }

                wait = _changed.Task;
            }

            await wait.WaitAsync(token).ConfigureAwait(false);
        }
    }

    public async ValueTask ThrottleAsync(int bytes, CancellationToken token)
    {
        if (_settings.Value.DownloadLimitMb == 0)
        {
            return;
        }

        await _bandwidth.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var remaining = (double)bytes;
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested();
                var rate = (double)_settings.Value.DownloadLimitMb * 1_000_000;
                if (rate == 0)
                {
                    _nextTransfer = 0;
                    return;
                }

                var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                if (_nextTransfer > now)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(_nextTransfer - now, 0.1)), token)
                        .ConfigureAwait(false);
                }
                else
                {
                    // 小时间片使更改限速、取消下载均能及时生效，最多允许一个时间片的突发流量。
                    var chunk = Math.Min(remaining, rate * 0.1);
                    _nextTransfer = now + chunk / rate;
                    remaining -= chunk;
                }
            }
        }
        finally
        {
            _bandwidth.Release();
        }
    }

    private void OnChanged()
    {
        lock (_sync)
        {
            Pulse();
        }
    }

    private void Release()
    {
        lock (_sync)
        {
            _active--;
            Pulse();
        }
    }

    private void Pulse()
    {
        var previous = _changed;
        _changed = NewSignal();
        previous.TrySetResult();
    }

    public void Dispose()
    {
        _settings.Changed -= OnChanged;
    }

    private sealed class Lease(DownloadTrafficService owner) : IDisposable
    {
        private DownloadTrafficService? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}