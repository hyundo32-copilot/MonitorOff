namespace MonitorOff.Kit;

public enum ClickOutcome
{
    Single,
    Double,
}

/// <summary>Sources/MonitorOffKit/ClickDiscriminator.swift 의 포트.</summary>
public struct ClickDiscriminator
{
    public double Interval { get; }

    private DateTime? _pendingDate;

    public ClickDiscriminator(double interval = 0.28)
    {
        Interval = interval;
        _pendingDate = null;
    }

    public ClickOutcome? Click(DateTime date)
    {
        if (_pendingDate is not DateTime pending)
        {
            _pendingDate = date;
            return null;
        }

        if (date - pending <= TimeSpan.FromSeconds(Interval))
        {
            _pendingDate = null;
            return ClickOutcome.Double;
        }

        _pendingDate = date;
        return null;
    }

    public ClickOutcome? Timeout()
    {
        if (_pendingDate is null)
        {
            return null;
        }

        _pendingDate = null;
        return ClickOutcome.Single;
    }
}
