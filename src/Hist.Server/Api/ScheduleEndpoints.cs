using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Hist.Server.Api;

public static class ScheduleEndpoints
{
    public static RouteGroupBuilder MapScheduleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetSchedules);
        group.MapPatch("/{id}", UpdateSchedule);
        return group;
    }

    private static async Task<IResult> GetSchedules(ISchedulerFactory schedulerFactory)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup());

        var results = new List<object>();
        foreach (var key in jobKeys.OrderBy(k => k.Name))
        {
            var triggers = await scheduler.GetTriggersOfJob(key);
            var trigger = triggers.FirstOrDefault();
            var state = trigger is null
                ? TriggerState.None
                : await scheduler.GetTriggerState(trigger.Key);

            results.Add(new
            {
                id = key.Name,
                group = key.Group,
                cron = (trigger as ICronTrigger)?.CronExpressionString,
                next_fire = trigger?.GetNextFireTimeUtc(),
                enabled = state != TriggerState.Paused,
                state = state.ToString().ToLowerInvariant()
            });
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> UpdateSchedule(
        string id,
        [FromBody] UpdateScheduleRequest req,
        ISchedulerFactory schedulerFactory)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey = new JobKey(id);

        if (!await scheduler.CheckExists(jobKey))
            return Results.NotFound();

        var triggers = await scheduler.GetTriggersOfJob(jobKey);
        var trigger = triggers.FirstOrDefault();
        if (trigger is null) return Results.NotFound();

        // Toggle enabled/disabled
        if (req.Enabled.HasValue)
        {
            if (req.Enabled.Value)
                await scheduler.ResumeTrigger(trigger.Key);
            else
                await scheduler.PauseTrigger(trigger.Key);
        }

        // Update cron expression
        if (req.Cron is not null)
        {
            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(trigger.Key)
                .ForJob(jobKey)
                .WithCronSchedule(req.Cron)
                .Build();

            await scheduler.RescheduleJob(trigger.Key, newTrigger);
        }

        return Results.Ok();
    }

    record UpdateScheduleRequest(bool? Enabled, string? Cron);
}
