namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// A clock that moves only when the test calls <see cref="Advance"/>, timers included: a <c>Task.Delay(…, clock)</c>
/// completes then and never on its own. <see cref="TimeProvider"/>'s own timers run on the system clock, so a test on
/// <see cref="FixedClock"/> still had a background loop wake up whenever the machine got round to it.
/// </summary>
public sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    /// <summary>Timers armed and not yet due: a loop waiting on this clock.</summary>
    public int Pending
    {
        get { lock (_gate) return _timers.Count; }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the time on and runs, on the caller's thread, every timer that fell due.</summary>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.Where(t => t.DueAt <= _now)];
            foreach (var timer in due)
            {
                if (timer.Period <= TimeSpan.Zero) _timers.Remove(timer);
                else timer.DueAt += timer.Period;
            }
        }
        foreach (var timer in due) timer.Fire();
    }

    /// <summary>Waits, on real time, until something has armed a timer on this clock.</summary>
    public async Task WhenPendingAsync(TimeSpan? timeout = null)
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (Pending == 0)
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("No timer was armed on the manual clock.");
            await Task.Delay(1);
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset DueAt { get; set; }
        public TimeSpan Period { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                clock._timers.Remove(this);
                if (dueTime == Timeout.InfiniteTimeSpan) return true;
                DueAt = clock._now + dueTime;
                Period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;
                clock._timers.Add(this);
                return true;
            }
        }

        public void Dispose()
        {
            lock (clock._gate) clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
