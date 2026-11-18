namespace MafiaGame.Application.Interfaces;

using MafiaGame.Domain.Entities;

public record NightResolutionResult(
    List<Guid> KilledUserIds,
    bool SavedByDoctor,
    Guid? SavedUserId
);

public record DayVoteResolutionResult(
    Guid? ExecutedUserId,
    int TopVotes,
    bool IsTie,
    bool SkippedByMajority
);

public interface IGameStateManager
{
    void ResetRoom(string roomCode);
    void StartNewPhase(string roomCode, IEnumerable<RoomPlayer> aliveBots, bool isNight);

    void RecordDayVote(string roomCode, Guid voterId, Guid? targetId, IEnumerable<RoomPlayer> alivePlayers);
    void RecordNightAction(string roomCode, Guid actorId, Guid targetId);

    Dictionary<Guid, int> GetDayVoteCounts(string roomCode);
    int GetSkipVoteCount(string roomCode);
    bool HasVotedDay(string roomCode, Guid userId);
    bool HasActedNight(string roomCode, Guid userId);

    bool AreAllDayVotesIn(string roomCode, IEnumerable<RoomPlayer> alivePlayers);
    bool AreAllNightActionsIn(string roomCode, IEnumerable<RoomPlayer> alivePlayers);

    DayVoteResolutionResult ResolveDayVotes(string roomCode);
    NightResolutionResult ResolveNightActions(string roomCode, IEnumerable<RoomPlayer> players);

    // AI Bot Brain & Dialogue Support
    bool IsBotReadyToChat(string roomCode, Guid botId);
    bool IsBotReadyToAct(string roomCode, Guid botId);
    void MarkBotChatted(string roomCode, Guid botId);

    void RecordPoliceDiscovery(string roomCode, Guid policeId, Guid targetId, bool isMafia);
    void NotifyPlayerMentionedInChat(string roomCode, string message, IEnumerable<RoomPlayer> players, Guid senderId);

    Guid? ChooseBotDayVote(string roomCode, RoomPlayer bot, IEnumerable<RoomPlayer> alivePlayers);
    Guid? ChooseBotNightTarget(string roomCode, RoomPlayer bot, IEnumerable<RoomPlayer> alivePlayers);
    string GenerateBotChatMessage(string roomCode, RoomPlayer bot, IEnumerable<RoomPlayer> alivePlayers, string language, int currentDay);

    void SetLastNightOutcome(string roomCode, string? killedUsername, bool savedByDoctor);
}
