using NordschleifeTrackdayPlugin.Session;

namespace NordschleifeTrackdayPlugin.Convoy;

public sealed class NordschleifeTrackdayConvoy
{
    private readonly NordschleifeTrackdaySession _leader;

    private long _startedTimeMs = 0;
    private List<ulong> _finishingDrivers = [];
    private List<ulong> _startingDrivers = [];

    private bool _isOnMove = false;

    public NordschleifeTrackdayConvoy(NordschleifeTrackdaySession leader, List<ulong> finishingDrivers, List<ulong> startingDrivers)
    {
        _leader = leader;
        _finishingDrivers = finishingDrivers;
        _startingDrivers = startingDrivers;
    }

    public NordschleifeTrackdaySession Leader()
    {
        return _leader;
    }

    public ulong LeaderGuid()
    {
        return _leader.Client().Guid;
    }

    public void SetStartedTimeMs(long l)
    {
        _startedTimeMs = l;
    }

    public bool HasStarted()
    {
        return _startedTimeMs > 0;
    }

    public long StartedTimeMs()
    {
        return _startedTimeMs;
    }

    public bool IsStartTimeValid(long otherStart)
    {
        return Math.Abs(_startedTimeMs - otherStart) <= 20000;
    }

    public void SetFinishingDrivers(List<ulong> list)
    {
        _finishingDrivers = list;
    }

    public void AddFinishingDriver(ulong guid)
    {
        _finishingDrivers.Add(guid);
    }

    public void RemoveFinishingDriver(ulong guid)
    {
        _finishingDrivers.Remove(guid);
    }

    public List<ulong> FinishingDrivers()
    {
        return _finishingDrivers;
    }

    public void SetStartingDrivers(List<ulong> list)
    {
        _startingDrivers = list;
    }

    public List<ulong> StartingDrivers()
    {
        return _startingDrivers;
    }

    public bool IsOnMove()
    {
        return _isOnMove;
    }

    public async void SetIsOnMove(bool b)
    {
        var inst = NordschleifeTrackdayPlugin.Instance();
        if (IsOnMove() || inst == null)
        {
            return;
        }

        _isOnMove = b;
        await inst.BroadcastWithDelayAsync($"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}@{_leader.Username()}'s convoy is on the move!");
    }
}
