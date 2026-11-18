namespace MafiaGame.Infrastructure.Services;

using System.Collections.Concurrent;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Domain.Enums;

public class GameStateManager : IGameStateManager
{
    private readonly ConcurrentDictionary<string, RoomMatchState> _rooms = new(StringComparer.OrdinalIgnoreCase);

    private RoomMatchState GetOrCreate(string roomCode) =>
        _rooms.GetOrAdd(roomCode.ToUpper(), _ => new RoomMatchState());

    public void ResetRoom(string roomCode)
    {
        _rooms[roomCode.ToUpper()] = new RoomMatchState();
    }

    public void StartNewPhase(string roomCode, IEnumerable<RoomPlayer> aliveBots, bool isNight)
    {
        var state = GetOrCreate(roomCode);
        var now = DateTime.UtcNow;

        lock (state.SyncRoot)
        {
            state.DayVotes.Clear();
            state.NightActions.Clear();
            state.BotChattedThisPhase.Clear();
            state.BotChatUnlockTimes.Clear();
            state.BotActionUnlockTimes.Clear();

            var bots = aliveBots.Where(b => b.IsBot && b.IsAlive).ToList();
            for (int i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (isNight)
                {
                    // Stagger night actions over 3s to 12s
                    var delaySec = 3 + Random.Shared.Next(0, 8) + i;
                    state.BotActionUnlockTimes[bot.UserId] = now.AddSeconds(delaySec);
                }
                else
                {
                    // Stagger Day chat (3s to 18s) and Day vote (5s to 10s after chat)
                    var chatDelaySec = 3 + (i * 3) + Random.Shared.Next(0, 5);
                    var voteDelaySec = chatDelaySec + 5 + Random.Shared.Next(0, 6);
                    state.BotChatUnlockTimes[bot.UserId] = now.AddSeconds(chatDelaySec);
                    state.BotActionUnlockTimes[bot.UserId] = now.AddSeconds(voteDelaySec);
                }
            }
        }
    }

    public void RecordDayVote(string roomCode, Guid voterId, Guid? targetId, IEnumerable<RoomPlayer> alivePlayers)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            state.DayVotes[voterId] = targetId;

            if (targetId.HasValue)
            {
                // Majority consensus awareness: alive bots notice who is being voted for
                foreach (var bot in alivePlayers.Where(p => p.IsBot && p.IsAlive && p.UserId != voterId))
                {
                    var scores = GetSuspicionMap(state, bot.UserId);
                    if (targetId.Value == bot.UserId)
                    {
                        // Someone voted for this bot -> bot gets suspicious of the accuser!
                        scores[voterId] = scores.GetValueOrDefault(voterId, 0) + 25;
                    }
                    else if (bot.Role != RoleType.Mafia)
                    {
                        // Town bots pay attention to growing consensus (bandwagon effect)
                        scores[targetId.Value] = scores.GetValueOrDefault(targetId.Value, 0) + 18;
                    }
                }
            }
        }
    }

    public void RecordNightAction(string roomCode, Guid actorId, Guid targetId)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            state.NightActions[actorId] = targetId;
        }
    }

    public Dictionary<Guid, int> GetDayVoteCounts(string roomCode)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            var counts = new Dictionary<Guid, int>();
            foreach (var target in state.DayVotes.Values)
            {
                if (target.HasValue)
                {
                    counts[target.Value] = counts.GetValueOrDefault(target.Value, 0) + 1;
                }
            }
            return counts;
        }
    }

    public int GetSkipVoteCount(string roomCode)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            return state.DayVotes.Values.Count(v => !v.HasValue);
        }
    }

    public bool HasVotedDay(string roomCode, Guid userId)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            return state.DayVotes.ContainsKey(userId);
        }
    }

    public bool HasActedNight(string roomCode, Guid userId)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            return state.NightActions.ContainsKey(userId);
        }
    }

    public bool AreAllDayVotesIn(string roomCode, IEnumerable<RoomPlayer> alivePlayers)
    {
        var state = GetOrCreate(roomCode);
        var aliveIds = alivePlayers.Where(p => p.IsAlive).Select(p => p.UserId).ToList();
        if (aliveIds.Count == 0) return false;

        lock (state.SyncRoot)
        {
            return aliveIds.All(id => state.DayVotes.ContainsKey(id));
        }
    }

    public bool AreAllNightActionsIn(string roomCode, IEnumerable<RoomPlayer> alivePlayers)
    {
        var state = GetOrCreate(roomCode);
        var nightActors = alivePlayers
            .Where(p => p.IsAlive && (p.Role == RoleType.Mafia || p.Role == RoleType.Doctor || p.Role == RoleType.Police || p.Role == RoleType.SerialKiller))
            .Select(p => p.UserId)
            .ToList();

        if (nightActors.Count == 0) return true;

        lock (state.SyncRoot)
        {
            return nightActors.All(id => state.NightActions.ContainsKey(id));
        }
    }

    public DayVoteResolutionResult ResolveDayVotes(string roomCode)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            int skipVotes = state.DayVotes.Values.Count(v => !v.HasValue);
            var counts = new Dictionary<Guid, int>();

            foreach (var v in state.DayVotes.Values)
            {
                if (v.HasValue)
                    counts[v.Value] = counts.GetValueOrDefault(v.Value, 0) + 1;
            }

            if (counts.Count == 0)
                return new DayVoteResolutionResult(null, 0, false, true);

            var sorted = counts.OrderByDescending(kv => kv.Value).ToList();
            var top = sorted[0];

            // Skip beats or ties the top candidate -> no execution
            if (skipVotes >= top.Value)
                return new DayVoteResolutionResult(null, top.Value, false, true);

            // Tie between top two players -> no execution
            if (sorted.Count > 1 && sorted[1].Value == top.Value)
                return new DayVoteResolutionResult(null, top.Value, true, false);

            return new DayVoteResolutionResult(top.Key, top.Value, false, false);
        }
    }

    public NightResolutionResult ResolveNightActions(string roomCode, IEnumerable<RoomPlayer> players)
    {
        var state = GetOrCreate(roomCode);
        var alivePlayers = players.Where(p => p.IsAlive).ToList();

        lock (state.SyncRoot)
        {
            // Doctor save target
            Guid? doctorSaveTarget = null;
            var doctor = alivePlayers.FirstOrDefault(p => p.Role == RoleType.Doctor);
            if (doctor != null && state.NightActions.TryGetValue(doctor.UserId, out var docTarget))
            {
                doctorSaveTarget = docTarget;
            }

            // Mafia consensus target
            var mafiaTargets = alivePlayers
                .Where(p => p.Role == RoleType.Mafia && state.NightActions.ContainsKey(p.UserId))
                .Select(p => state.NightActions[p.UserId])
                .ToList();

            Guid? mafiaTarget = null;
            if (mafiaTargets.Count > 0)
            {
                mafiaTarget = mafiaTargets
                    .GroupBy(id => id)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
            }
            else
            {
                // Fallback if mafia didn't pick in time: no kill
                mafiaTarget = null;
            }

            // Serial killer target (if present)
            Guid? skTarget = null;
            var sk = alivePlayers.FirstOrDefault(p => p.Role == RoleType.SerialKiller);
            if (sk != null && state.NightActions.TryGetValue(sk.UserId, out var skPicked))
            {
                skTarget = skPicked;
            }

            var killed = new List<Guid>();
            bool savedByDoctor = false;

            if (mafiaTarget.HasValue)
            {
                if (doctorSaveTarget.HasValue && doctorSaveTarget.Value == mafiaTarget.Value)
                {
                    savedByDoctor = true;
                }
                else
                {
                    killed.Add(mafiaTarget.Value);
                }
            }

            if (skTarget.HasValue && !killed.Contains(skTarget.Value))
            {
                if (doctorSaveTarget.HasValue && doctorSaveTarget.Value == skTarget.Value)
                {
                    savedByDoctor = true;
                }
                else
                {
                    killed.Add(skTarget.Value);
                }
            }

            return new NightResolutionResult(killed, savedByDoctor, savedByDoctor ? doctorSaveTarget : null);
        }
    }

    public bool IsBotReadyToChat(string roomCode, Guid botId)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            if (state.BotChattedThisPhase.Contains(botId)) return false;
            return state.BotChatUnlockTimes.TryGetValue(botId, out var unlockAt) && DateTime.UtcNow >= unlockAt;
        }
    }

    public bool IsBotReadyToAct(string roomCode, Guid botId)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            return state.BotActionUnlockTimes.TryGetValue(botId, out var unlockAt) && DateTime.UtcNow >= unlockAt;
        }
    }

    public void MarkBotChatted(string roomCode, Guid botId)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            state.BotChattedThisPhase.Add(botId);
        }
    }

    public void RecordPoliceDiscovery(string roomCode, Guid policeId, Guid targetId, bool isMafia)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            if (isMafia)
            {
                if (!state.PoliceKnownMafia.TryGetValue(policeId, out var set))
                    state.PoliceKnownMafia[policeId] = set = new HashSet<Guid>();
                set.Add(targetId);

                var scores = GetSuspicionMap(state, policeId);
                scores[targetId] = 100;
            }
            else
            {
                if (!state.PoliceKnownInnocent.TryGetValue(policeId, out var set))
                    state.PoliceKnownInnocent[policeId] = set = new HashSet<Guid>();
                set.Add(targetId);

                var scores = GetSuspicionMap(state, policeId);
                scores[targetId] = -50;
            }
        }
    }

    public void NotifyPlayerMentionedInChat(string roomCode, string message, IEnumerable<RoomPlayer> players, Guid senderId)
    {
        var state = GetOrCreate(roomCode);
        var lower = message.ToLowerInvariant();
        var alivePlayers = players.Where(p => p.IsAlive).ToList();

        // Check if message contains accusatory keywords or a player's name
        bool soundsAccusatory = lower.Contains("mafia") || lower.Contains("saka") || lower.Contains("vote") ||
                                lower.Contains("sus") || lower.Contains("kill") || lower.Contains("maruw") ||
                                lower.Contains("maru") || lower.Contains("eyaa") || lower.Contains("thama");

        lock (state.SyncRoot)
        {
            foreach (var mentioned in alivePlayers)
            {
                if (mentioned.UserId == senderId) continue;
                var nameLower = mentioned.User.Username.ToLowerInvariant();
                var shortName = nameLower.Replace("_bot", "");

                if (lower.Contains(nameLower) || (shortName.Length >= 3 && lower.Contains(shortName)))
                {
                    foreach (var bot in alivePlayers.Where(p => p.IsBot && p.UserId != senderId))
                    {
                        var scores = GetSuspicionMap(state, bot.UserId);
                        if (bot.UserId == mentioned.UserId)
                        {
                            // Mentioned bot feels targeted by sender
                            scores[senderId] = scores.GetValueOrDefault(senderId, 0) + 15;
                        }
                        else if (bot.Role != RoleType.Mafia)
                        {
                            // Other Town bots take note of the accusation!
                            scores[mentioned.UserId] = scores.GetValueOrDefault(mentioned.UserId, 0) + (soundsAccusatory ? 14 : 6);
                        }
                    }
                }
            }
        }
    }

    public Guid? ChooseBotDayVote(string roomCode, RoomPlayer bot, IEnumerable<RoomPlayer> alivePlayers)
    {
        var state = GetOrCreate(roomCode);
        var candidates = alivePlayers.Where(p => p.IsAlive && p.UserId != bot.UserId).ToList();
        if (candidates.Count == 0) return null;

        lock (state.SyncRoot)
        {
            // Mafia bots never vote out their Mafia teammate!
            if (bot.Role == RoleType.Mafia)
            {
                var nonMafia = candidates.Where(c => c.Role != RoleType.Mafia).ToList();
                if (nonMafia.Count > 0) candidates = nonMafia;
            }

            // Police bot immediately votes for confirmed Mafia if alive!
            if (bot.Role == RoleType.Police && state.PoliceKnownMafia.TryGetValue(bot.UserId, out var foundMafia))
            {
                var confirmedAlive = candidates.FirstOrDefault(c => foundMafia.Contains(c.UserId));
                if (confirmedAlive != null) return confirmedAlive.UserId;
            }

            var scores = GetSuspicionMap(state, bot.UserId);

            // Check current live vote leader (majority consensus pull)
            var currentCounts = new Dictionary<Guid, int>();
            foreach (var v in state.DayVotes.Values)
            {
                if (v.HasValue) currentCounts[v.Value] = currentCounts.GetValueOrDefault(v.Value, 0) + 1;
            }

            // Score each candidate combining suspicion + consensus pull + slight personality randomness
            var ranked = candidates.Select(c =>
            {
                int score = scores.GetValueOrDefault(c.UserId, 0);
                int liveVotes = currentCounts.GetValueOrDefault(c.UserId, 0);

                // Majority bandwagon bonus (so bots reach real consensus!)
                score += liveVotes * (bot.BotPersonality == "Quiet" ? 12 : 20);

                // Avoid voting confirmed innocent if Police
                if (bot.Role == RoleType.Police &&
                    state.PoliceKnownInnocent.TryGetValue(bot.UserId, out var innocents) &&
                    innocents.Contains(c.UserId))
                {
                    score -= 80;
                }

                // Small jitter so bots don't always clone identically
                score += Random.Shared.Next(0, 14);
                return (Candidate: c, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ToList();

            var top = ranked[0];

            // Quiet bots may skip on Day 1 if nobody looks suspicious yet
            if (bot.BotPersonality == "Quiet" && top.Score < 12 && Random.Shared.Next(0, 100) < 25)
            {
                return null; // Skip vote
            }

            return top.Candidate.UserId;
        }
    }

    public Guid? ChooseBotNightTarget(string roomCode, RoomPlayer bot, IEnumerable<RoomPlayer> alivePlayers)
    {
        var state = GetOrCreate(roomCode);
        var alive = alivePlayers.Where(p => p.IsAlive).ToList();

        lock (state.SyncRoot)
        {
            if (bot.Role == RoleType.Mafia)
            {
                // Target an alive non-Mafia player
                var victims = alive.Where(p => p.Role != RoleType.Mafia).ToList();
                if (victims.Count == 0) return null;

                // Coordinate with any Mafia teammate who already picked!
                var teammatePick = alive
                    .Where(p => p.Role == RoleType.Mafia && p.UserId != bot.UserId && state.NightActions.ContainsKey(p.UserId))
                    .Select(p => state.NightActions[p.UserId])
                    .FirstOrDefault();

                if (teammatePick != Guid.Empty && victims.Any(v => v.UserId == teammatePick))
                    return teammatePick;

                return victims[Random.Shared.Next(victims.Count)].UserId;
            }

            if (bot.Role == RoleType.Doctor)
            {
                // Protect either self (30% chance) or a random alive player
                if (Random.Shared.Next(0, 100) < 30) return bot.UserId;
                return alive[Random.Shared.Next(alive.Count)].UserId;
            }

            if (bot.Role == RoleType.Police)
            {
                var knownMafia = state.PoliceKnownMafia.GetValueOrDefault(bot.UserId) ?? new HashSet<Guid>();
                var knownInnocent = state.PoliceKnownInnocent.GetValueOrDefault(bot.UserId) ?? new HashSet<Guid>();
                var unknowns = alive
                    .Where(p => p.UserId != bot.UserId && !knownMafia.Contains(p.UserId) && !knownInnocent.Contains(p.UserId))
                    .ToList();

                if (unknowns.Count == 0)
                    unknowns = alive.Where(p => p.UserId != bot.UserId).ToList();

                return unknowns.Count > 0 ? unknowns[Random.Shared.Next(unknowns.Count)].UserId : null;
            }

            if (bot.Role == RoleType.SerialKiller)
            {
                var targets = alive.Where(p => p.UserId != bot.UserId).ToList();
                return targets.Count > 0 ? targets[Random.Shared.Next(targets.Count)].UserId : null;
            }

            return null;
        }
    }

    public string GenerateBotChatMessage(string roomCode, RoomPlayer bot, IEnumerable<RoomPlayer> alivePlayers, string language, int currentDay)
    {
        var state = GetOrCreate(roomCode);
        bool isSinglish = language.Equals("Singlish", StringComparison.OrdinalIgnoreCase);
        var others = alivePlayers.Where(p => p.IsAlive && p.UserId != bot.UserId).ToList();

        lock (state.SyncRoot)
        {
            // 1. Police Bot reveals a confirmed Mafia!
            if (bot.Role == RoleType.Police && state.PoliceKnownMafia.TryGetValue(bot.UserId, out var foundMafia))
            {
                var mafiaPlayer = others.FirstOrDefault(p => foundMafia.Contains(p.UserId));
                if (mafiaPlayer != null)
                {
                    return isSinglish
                        ? $"Guys mata 100% sure! 🔍 {mafiaPlayer.User.Username} thamai Mafia! Okkoma eyata vote karamu!"
                        : $"Guys I investigated last night — 🔍 {mafiaPlayer.User.Username} IS MAFIA! Everyone vote them out!";
                }
            }

            // 2. Defend self if someone already voted for this bot!
            bool isBeingVoted = state.DayVotes.Values.Any(v => v == bot.UserId);
            var suspect = PickDialogueSuspect(state, bot, others);
            var suspectName = suspect?.User.Username ?? "kawuruhari";

            if (isBeingVoted)
            {
                if (isSinglish)
                {
                    var lines = new[]
                    {
                        $"Adoo mama nemei mafia! 😅 Boruwata mage pitin danna epa, {suspectName} wa poddak balapan!",
                        $"Machan mama innocent! Eka para mata vote karanna epa, {suspectName} thamai saka!",
                        $"Ai ban mata vote karanne? 😭 Mama Town ekata wada karanne, {suspectName} ganai balanna one!"
                    };
                    return lines[Random.Shared.Next(lines.Length)];
                }
                else
                {
                    var lines = new[]
                    {
                        $"Wait, I'm NOT Mafia! 😅 Don't waste votes on me, check {suspectName} instead!",
                        $"Why are you voting for me?! I'm innocent — {suspectName} is acting way more sus!",
                        $"Trust me guys, voting me out helps the Mafia. Look closely at {suspectName}."
                    };
                    return lines[Random.Shared.Next(lines.Length)];
                }
            }

            // 3. Majority consensus bandwagon reaction!
            var voteCounts = new Dictionary<Guid, int>();
            foreach (var v in state.DayVotes.Values)
            {
                if (v.HasValue) voteCounts[v.Value] = voteCounts.GetValueOrDefault(v.Value, 0) + 1;
            }
            var leader = voteCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (leader.Value >= 1 && leader.Key != bot.UserId)
            {
                var leaderPlayer = others.FirstOrDefault(p => p.UserId == leader.Key);
                if (leaderPlayer != null && !(bot.Role == RoleType.Mafia && leaderPlayer.Role == RoleType.Mafia))
                {
                    if (isSinglish)
                    {
                        var lines = new[]
                        {
                            $"Ow ow matath hithenawa {leaderPlayer.User.Username} saka wage 👀 Mamath vote karanawa!",
                            $"Hari hari, {leaderPlayer.User.Username} thama mafia wenna puluwan. Wadi manaapeta mamath ekagai!",
                            $"Poddak inna... {leaderPlayer.User.Username} da naththan {suspectName} da? 🤔 Kamak na {leaderPlayer.User.Username} ta vote karamu."
                        };
                        return lines[Random.Shared.Next(lines.Length)];
                    }
                    else
                    {
                        var lines = new[]
                        {
                            $"Agreed! {leaderPlayer.User.Username} looks super sus 👀 I'm voting with the town!",
                            $"Yeah, I'm backing the majority on {leaderPlayer.User.Username}. Let's vote them out.",
                            $"Hmm, {leaderPlayer.User.Username} has been quiet... I'll join the vote!"
                        };
                        return lines[Random.Shared.Next(lines.Length)];
                    }
                }
            }

            // 4. Morning outcome reaction + Personality dialogue
            string? killedName = state.LastKilledUsername;
            bool saved = state.LastSavedOccurred;

            if (isSinglish)
            {
                if (saved && Random.Shared.Next(0, 100) < 50)
                    return $"Doctor supiriyata save karala ne! 🩺🔥 Eath {suspectName} poddak amuthu widiyata inne...";

                if (!string.IsNullOrEmpty(killedName) && Random.Shared.Next(0, 100) < 55)
                    return $"Adoo {killedName} marila neh! 😭 Mata nan {suspectName} thamai godakma saka!";

                return bot.BotPersonality switch
                {
                    "Aggressive" => $"Machan nikan inna epa! {suspectName} thamai Mafia wage mata penne 🔫 Vote karamuda?",
                    "Analyst" => $"Day {currentDay} wenakan baluwama {suspectName} hariyata katha kareth na 🤔 Poddak check karamu.",
                    "Defensive" => $"Guys parissamen vote karanna, waradi kenek maruwoth Mafia dinanawa! Mata nan {suspectName} poddak sakai.",
                    _ => $"Hmm... kauruda kiyala hariyatama sure na, eath {suspectName} tikak saka wage 👀"
                };
            }
            else
            {
                if (saved && Random.Shared.Next(0, 100) < 50)
                    return $"Huge save by the Doctor last night! 🩺🔥 Still, {suspectName} seems really suspicious...";

                if (!string.IsNullOrEmpty(killedName) && Random.Shared.Next(0, 100) < 55)
                    return $"RIP {killedName} 😭 Honestly, {suspectName} is my #1 suspect right now!";

                return bot.BotPersonality switch
                {
                    "Aggressive" => $"Stop stalling guys — {suspectName} is definitely giving Mafia vibes! 🔫 Let's vote!",
                    "Analyst" => $"Analyzing Day {currentDay} so far... {suspectName} has been super quiet. Worth voting?",
                    "Defensive" => $"Be careful with votes everyone! Don't mislynch. Personally I suspect {suspectName}.",
                    _ => $"Not 100% sure yet, maybe {suspectName}? Let's see what everyone thinks 👀"
                };
            }
        }
    }

    public void SetLastNightOutcome(string roomCode, string? killedUsername, bool savedByDoctor)
    {
        var state = GetOrCreate(roomCode);
        lock (state.SyncRoot)
        {
            state.LastKilledUsername = killedUsername;
            state.LastSavedOccurred = savedByDoctor;
        }
    }

    private RoomPlayer? PickDialogueSuspect(RoomMatchState state, RoomPlayer bot, List<RoomPlayer> others)
    {
        var pool = bot.Role == RoleType.Mafia
            ? others.Where(o => o.Role != RoleType.Mafia).ToList()
            : others;

        if (pool.Count == 0) pool = others;
        if (pool.Count == 0) return null;

        var scores = GetSuspicionMap(state, bot.UserId);
        return pool.OrderByDescending(p => scores.GetValueOrDefault(p.UserId, 0) + Random.Shared.Next(0, 10)).First();
    }

    private static Dictionary<Guid, int> GetSuspicionMap(RoomMatchState state, Guid botId)
    {
        if (!state.BotSuspicion.TryGetValue(botId, out var map))
        {
            map = new Dictionary<Guid, int>();
            state.BotSuspicion[botId] = map;
        }
        return map;
    }

    private class RoomMatchState
    {
        public object SyncRoot { get; } = new();
        public Dictionary<Guid, Guid?> DayVotes { get; } = new();
        public Dictionary<Guid, Guid> NightActions { get; } = new();
        public Dictionary<Guid, HashSet<Guid>> PoliceKnownMafia { get; } = new();
        public Dictionary<Guid, HashSet<Guid>> PoliceKnownInnocent { get; } = new();
        public Dictionary<Guid, Dictionary<Guid, int>> BotSuspicion { get; } = new();
        public HashSet<Guid> BotChattedThisPhase { get; } = new();
        public Dictionary<Guid, DateTime> BotChatUnlockTimes { get; } = new();
        public Dictionary<Guid, DateTime> BotActionUnlockTimes { get; } = new();
        public string? LastKilledUsername { get; set; }
        public bool LastSavedOccurred { get; set; }
    }
}
