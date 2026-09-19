namespace MafiaGame.Application.Interfaces;

public interface IGameHubClient
{
    Task ReceiveRoomState(object roomState);
    Task ReceiveVoteTrail(Guid voterId, string voterName, Guid targetId, string targetName, int voteCount);
    Task ReceiveGameAnnouncement(string title, string message, string phase);
    Task PlayerStatusChanged(Guid userId, string username, bool isConnected);
    Task PlayerAbandonedGame(string username, string reason);
}
