namespace CodeMaster.Core.Models;

public enum ScheduleType
{
    Always,
    WeeklyRecurring,
    DateRange,
    OneTime
}

public class AccessPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Always;

    /// <summary>
    /// Bitmask for days of the week:
    /// 1 = Monday, 2 = Tuesday, 4 = Wednesday, 8 = Thursday, 16 = Friday, 32 = Saturday, 64 = Sunday.
    /// Default is 127 (all days enabled).
    /// </summary>
    public int DaysOfWeek { get; set; } = 127;

    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int? RemainingUses { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string>? DoorIds { get; set; }

    /// <summary>
    /// Evaluates if the policy is active at the specified UTC time.
    /// </summary>
    public bool IsActiveAt(DateTime utcTime)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (ValidFrom.HasValue && utcTime < ValidFrom.Value)
        {
            return false;
        }

        if (ValidUntil.HasValue && utcTime > ValidUntil.Value)
        {
            return false;
        }

        return ScheduleType switch
        {
            ScheduleType.Always => true,

            ScheduleType.OneTime => !RemainingUses.HasValue || RemainingUses.Value > 0,

            ScheduleType.DateRange => IsTimeWindowActive(utcTime),

            ScheduleType.WeeklyRecurring => IsDayOfWeekActive(utcTime) && IsTimeWindowActive(utcTime),

            _ => false
        };
    }

    private bool IsDayOfWeekActive(DateTime utcTime)
    {
        var dayBit = utcTime.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 4,
            DayOfWeek.Thursday => 8,
            DayOfWeek.Friday => 16,
            DayOfWeek.Saturday => 32,
            DayOfWeek.Sunday => 64,
            _ => 0
        };

        return (DaysOfWeek & dayBit) != 0;
    }

    private bool IsTimeWindowActive(DateTime utcTime)
    {
        if (!StartTime.HasValue && !EndTime.HasValue)
        {
            return true;
        }

        var currentTime = TimeOnly.FromDateTime(utcTime);

        if (StartTime.HasValue && EndTime.HasValue)
        {
            if (StartTime.Value <= EndTime.Value)
            {
                return currentTime >= StartTime.Value && currentTime <= EndTime.Value;
            }

            // Window spans overnight / midnight (e.g., 22:00 to 06:00)
            return currentTime >= StartTime.Value || currentTime <= EndTime.Value;
        }

        if (StartTime.HasValue)
        {
            return currentTime >= StartTime.Value;
        }

        return currentTime <= EndTime!.Value;
    }
}
