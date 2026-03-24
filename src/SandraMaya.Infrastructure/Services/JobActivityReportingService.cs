using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Services;

public sealed class JobActivityReportingService(IMemoryQueryService memoryQueryService) : IJobActivityReportingService
{
    public Task<JobActivitySummary> GetWeeklySummaryAsync(
        Guid userId,
        DateTimeOffset? asOfUtc = null,
        CancellationToken cancellationToken = default) =>
        BuildSummaryAsync(userId, JobActivitySummaryPeriod.Weekly, asOfUtc, cancellationToken);

    public Task<JobActivitySummary> GetMonthlySummaryAsync(
        Guid userId,
        DateTimeOffset? asOfUtc = null,
        CancellationToken cancellationToken = default) =>
        BuildSummaryAsync(userId, JobActivitySummaryPeriod.Monthly, asOfUtc, cancellationToken);

    private async Task<JobActivitySummary> BuildSummaryAsync(
        Guid userId,
        JobActivitySummaryPeriod period,
        DateTimeOffset? asOfUtc,
        CancellationToken cancellationToken)
    {
        var user = await memoryQueryService.GetUserAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        var timeZone = ResolveTimeZone(user.TimeZone);
        var referenceUtc = asOfUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var (rangeStartUtc, rangeEndUtc) = GetRange(period, referenceUtc, timeZone);

        var postings = await memoryQueryService.GetJobPostingsAsync(userId, cancellationToken);
        var applicationStatuses = await memoryQueryService.GetJobApplicationStatusesAsync(userId, cancellationToken);

        var recentDiscoveries = postings
            .Where(posting => IsInRange(posting.FirstSeenAtUtc, rangeStartUtc, rangeEndUtc))
            .OrderByDescending(posting => posting.FirstSeenAtUtc)
            .Take(10)
            .Select(static posting => new JobDiscoveryActivity(
                posting.Id,
                posting.Title,
                posting.CompanyName,
                posting.SourceUrl,
                posting.FirstSeenAtUtc))
            .ToList();

        var recentApplicationUpdates = applicationStatuses
            .Where(status => IsInRange(status.UpdatedAtUtc, rangeStartUtc, rangeEndUtc))
            .OrderByDescending(status => status.UpdatedAtUtc)
            .Take(10)
            .Select(static status => new JobApplicationActivity(
                status.JobPostingId,
                status.JobPosting.Title,
                status.JobPosting.CompanyName,
                status.Status,
                status.UpdatedAtUtc,
                status.AppliedAtUtc))
            .ToList();

        var currentStatusCounts = applicationStatuses
            .GroupBy(status => status.Status)
            .Select(static group => new JobStatusCount(group.Key, group.Count()))
            .OrderBy(count => GetStatusSortOrder(count.Status))
            .ThenBy(count => count.Status.ToString(), StringComparer.Ordinal)
            .ToList();

        return new JobActivitySummary(
            period,
            rangeStartUtc,
            rangeEndUtc,
            JobsDiscovered: postings.Count(posting => IsInRange(posting.FirstSeenAtUtc, rangeStartUtc, rangeEndUtc)),
            JobsTracked: applicationStatuses.Count(status => IsInRange(status.CreatedAtUtc, rangeStartUtc, rangeEndUtc)),
            ApplicationsSubmitted: applicationStatuses.Count(status => status.AppliedAtUtc is { } appliedAtUtc && IsInRange(appliedAtUtc, rangeStartUtc, rangeEndUtc)),
            InterviewsAdvanced: applicationStatuses.Count(status => status.Status == JobApplicationStatus.Interviewing && IsInRange(status.UpdatedAtUtc, rangeStartUtc, rangeEndUtc)),
            OffersReceived: applicationStatuses.Count(status => status.Status == JobApplicationStatus.Offer && IsInRange(status.UpdatedAtUtc, rangeStartUtc, rangeEndUtc)),
            RejectionsLogged: applicationStatuses.Count(status => status.Status == JobApplicationStatus.Rejected && IsInRange(status.UpdatedAtUtc, rangeStartUtc, rangeEndUtc)),
            WithdrawalsLogged: applicationStatuses.Count(status => status.Status == JobApplicationStatus.Withdrawn && IsInRange(status.UpdatedAtUtc, rangeStartUtc, rangeEndUtc)),
            CurrentStatusCounts: currentStatusCounts,
            RecentDiscoveries: recentDiscoveries,
            RecentApplicationUpdates: recentApplicationUpdates);
    }

    private static (DateTimeOffset RangeStartUtc, DateTimeOffset RangeEndUtc) GetRange(
        JobActivitySummaryPeriod period,
        DateTimeOffset referenceUtc,
        TimeZoneInfo timeZone)
    {
        var localReference = TimeZoneInfo.ConvertTime(referenceUtc, timeZone);
        var localDate = localReference.Date;

        var localStart = period switch
        {
            JobActivitySummaryPeriod.Weekly => localDate.AddDays(-GetDaysSinceMonday(localReference.DayOfWeek)),
            JobActivitySummaryPeriod.Monthly => new DateTime(localReference.Year, localReference.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported reporting period.")
        };

        var localEnd = period switch
        {
            JobActivitySummaryPeriod.Weekly => localStart.AddDays(7),
            JobActivitySummaryPeriod.Monthly => localStart.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported reporting period.")
        };

        return (
            ConvertLocalToUtc(localStart, timeZone),
            ConvertLocalToUtc(localEnd, timeZone));
    }

    private static DateTimeOffset ConvertLocalToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
            timeZone);

        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static int GetDaysSinceMonday(DayOfWeek dayOfWeek) =>
        dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;

    private static bool IsInRange(DateTimeOffset value, DateTimeOffset rangeStartUtc, DateTimeOffset rangeEndUtc) =>
        value >= rangeStartUtc && value < rangeEndUtc;

    private static int GetStatusSortOrder(JobApplicationStatus status) => status switch
    {
        JobApplicationStatus.Interested => 0,
        JobApplicationStatus.Saved => 1,
        JobApplicationStatus.Draft => 2,
        JobApplicationStatus.Applied => 3,
        JobApplicationStatus.Interviewing => 4,
        JobApplicationStatus.Offer => 5,
        JobApplicationStatus.Rejected => 6,
        JobApplicationStatus.Withdrawn => 7,
        JobApplicationStatus.Archived => 8,
        _ => 9
    };
}
