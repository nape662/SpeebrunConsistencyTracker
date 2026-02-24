using System;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public class SessionManager
{
    private readonly PracticeSession _currentSession = new();
    private AttemptBuilder _currentAttemptBuilder = new();

    public void OnBeforeLoadState()
    {
        if (!HasActiveAttempt)
            return;

        if (RoomTimerIntegration.RoomTimerIsCompleted())
        {
            // Timer endpoint was reached → completed attempt
            EndCurrentAttempt();
        }
        else if (RoomTimerIntegration.GetRoomTime() > 0)
        {
            // Timer still running but player reset → DNF
            var dnfRoomIndex = _currentAttemptBuilder.Count;
            TimeTicks ticks = new(RoomTimerIntegration.GetRoomTime());
            _currentAttemptBuilder.SetDnf(dnfRoomIndex, ticks);
            EndCurrentAttempt();
        }
        // else: no progress at all, builder will be replaced by OnLoadState
    }

    public void OnLoadState()
    {
        _currentAttemptBuilder = new AttemptBuilder();
    }

    public long CurrentSplitTime()
    {
        return _currentAttemptBuilder.SegmentTime.Ticks;
    }

    public void CompleteRoom(long ticks)
    {
        if (!HasActiveAttempt)
            return;
        TimeTicks roomTime = new TimeTicks(ticks) - _currentAttemptBuilder.SegmentTime;
        if (roomTime > 0)
            _currentAttemptBuilder.CompleteRoom(roomTime);
    }


    private void EndCurrentAttempt()
    {
        if (!HasActiveAttempt)
            return;

        var attempt = _currentAttemptBuilder.Build();
        _currentSession.AddAttempt(attempt);

        // Always expand room count to the max rooms seen in any attempt
        _currentSession.RoomCount = Math.Max(_currentSession.RoomCount, attempt.CompletedRooms.Count);

        _currentAttemptBuilder = null;
    }

    public PracticeSession CurrentSession => _currentSession;
    public AttemptBuilder CurrentAttempt => _currentAttemptBuilder;
    public bool HasActiveAttempt => _currentAttemptBuilder != null;

    public void SetRoomCount(int count)
    {
        _currentSession.RoomCount = Math.Max(_currentSession.RoomCount, count);
    }

    public int DynamicRoomCount()
    {
        int fromAttempts = CurrentSession.Attempts
            .Select(a => a.CompletedRooms.Count)
            .DefaultIfEmpty(0)
            .Max();
        int inProgress = _currentAttemptBuilder?.Count ?? 0;

        return Math.Max(Math.Max(_currentSession.RoomCount, fromAttempts), inProgress);
    }
}
