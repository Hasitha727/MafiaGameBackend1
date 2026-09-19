namespace MafiaGame.Infrastructure.Services;

using System.Collections.Concurrent;
using MafiaGame.Application.Interfaces;

public class GracePeriodManager : IGracePeriodManager
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();

    public void StartGracePeriod(Guid userId, TimeSpan duration, Func<Task> onExpired)
    {
        CancelGracePeriod(userId);

        var cts = new CancellationTokenSource();
        _timers[userId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(duration, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    await onExpired();
                }
            }
            catch (TaskCanceledException)
            {
                // Disconnection timer cancelled because player reconnected!
            }
            finally
            {
                _timers.TryRemove(userId, out _);
            }
        }, cts.Token);
    }

    public void CancelGracePeriod(Guid userId)
    {
        if (_timers.TryRemove(userId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
