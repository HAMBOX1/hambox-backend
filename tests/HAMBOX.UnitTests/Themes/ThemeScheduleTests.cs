using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.UnitTests.Themes;

public class ThemeScheduleTests
{
    [Fact]
    public void Create_WithUtcStart_Succeeds()
    {
        var schedule = ThemeSchedule.Create(Guid.NewGuid(), DateTime.UtcNow, null, null);

        Assert.True(schedule.IsActive);
        Assert.Equal(0, schedule.Priority);
    }

    [Fact]
    public void Create_WithLocalKindStart_Throws()
    {
        // The resolver compares StartsAtUtc/EndsAtUtc directly against DateTime.UtcNow — a
        // non-UTC value would silently activate/deactivate the schedule hours off from intent.
        var localStart = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            ThemeSchedule.Create(Guid.NewGuid(), localStart, null, null));
    }

    [Fact]
    public void Create_WithUnspecifiedKindStart_Throws()
    {
        var unspecifiedStart = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() =>
            ThemeSchedule.Create(Guid.NewGuid(), unspecifiedStart, null, null));
    }

    [Fact]
    public void Create_WithNonUtcEnd_Throws()
    {
        var start = DateTime.UtcNow;
        var localEnd = DateTime.SpecifyKind(start.AddDays(1), DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            ThemeSchedule.Create(Guid.NewGuid(), start, localEnd, null));
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        var start = DateTime.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            ThemeSchedule.Create(Guid.NewGuid(), start, start.AddMinutes(-1), null));
    }

    [Fact]
    public void Create_WithExplicitPriority_IsStored()
    {
        var schedule = ThemeSchedule.Create(Guid.NewGuid(), DateTime.UtcNow, null, null, priority: 10);

        Assert.Equal(10, schedule.Priority);
    }
}
