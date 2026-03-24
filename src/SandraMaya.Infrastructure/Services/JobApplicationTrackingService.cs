using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Services;

public sealed class JobApplicationTrackingService(
    IMemoryCommandService memoryCommandService,
    IMemoryQueryService memoryQueryService) : IJobApplicationTrackingService
{
    public async Task<JobApplicationState> MarkStatusAsync(
        JobApplicationStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var jobPosting = await memoryQueryService.GetJobPostingAsync(
            request.UserProfileId,
            request.JobPostingId,
            cancellationToken);

        if (jobPosting is null)
        {
            throw new InvalidOperationException(
                $"Job posting '{request.JobPostingId}' was not found for user '{request.UserProfileId}'.");
        }

        await memoryCommandService.SaveJobApplicationStatusAsync(
            new JobApplicationStatusRecord
            {
                UserProfileId = request.UserProfileId,
                JobPostingId = request.JobPostingId,
                CvRevisionId = request.CvRevisionId,
                Status = request.Status,
                NotesMarkdown = request.NotesMarkdown,
                MetadataJson = request.MetadataJson,
                AppliedAtUtc = request.AppliedAtUtc
            },
            cancellationToken);

        var current = await memoryQueryService.GetJobApplicationStatusAsync(
            request.UserProfileId,
            request.JobPostingId,
            cancellationToken);

        return new JobApplicationState(jobPosting, current);
    }

    public async Task<JobApplicationState?> GetCurrentStateAsync(
        Guid userId,
        Guid jobPostingId,
        CancellationToken cancellationToken = default)
    {
        var jobPosting = await memoryQueryService.GetJobPostingAsync(userId, jobPostingId, cancellationToken);
        if (jobPosting is null)
        {
            return null;
        }

        var current = await memoryQueryService.GetJobApplicationStatusAsync(userId, jobPostingId, cancellationToken);
        return new JobApplicationState(jobPosting, current);
    }

    public async Task<IReadOnlyList<JobApplicationState>> ListApplicationsAsync(
        Guid userId,
        JobApplicationListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var statuses = await memoryQueryService.GetJobApplicationStatusesAsync(userId, cancellationToken);
        var filteredStatuses = statuses.AsEnumerable();

        if (query.Statuses is { Count: > 0 })
        {
            var requestedStatuses = query.Statuses.ToHashSet();
            filteredStatuses = filteredStatuses.Where(status => requestedStatuses.Contains(status.Status));
        }

        return filteredStatuses
            .OrderByDescending(status => status.UpdatedAtUtc)
            .Take(Math.Clamp(query.Limit, 1, 200))
            .Select(status => new JobApplicationState(status.JobPosting, status))
            .ToList();
    }
}
