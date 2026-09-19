namespace MafiaGame.Application.Interfaces;

public interface IGracePeriodManager
{
    void StartGracePeriod(Guid userId, TimeSpan duration, Func<Task> onExpired);
    void CancelGracePeriod(Guid userId);
}
