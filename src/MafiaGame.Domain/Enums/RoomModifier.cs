namespace MafiaGame.Domain.Enums;

[Flags]
public enum RoomModifier
{
    None = 0,
    BlindVoting = 1,
    NoReveal = 2
}
