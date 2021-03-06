﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using CronExpressionDescriptor;
using Hik.Client.Helpers;
using Hik.DataAccess;
using Hik.DataAccess.Data;
using Hik.DTO.Contracts;
using Job;
using Job.Extensions;
using Job.Extentions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Hik.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly DataContext dataContext;
        private readonly ActivityBag activities = new();
        private readonly Options options = new() { DayOfWeekStartIndexZero = false, Use24HourTimeFormat = true };

        public IndexModel(DataContext dataContext)
        {
            this.dataContext = dataContext;
            dataContext.Database.EnsureCreated();
            JobTriggers = dataContext.JobTriggers.AsQueryable().ToList();
        }

        private IList<JobTrigger> JobTriggers { get; }

        public Dictionary<string, IList<TriggerDTO>> TriggersDtOs { get; private set; }

        public string ResponseMsg { get; private set; }

        public async Task OnGet(string msg = null)
        {
            ResponseMsg = msg;
            TriggersDtOs = new Dictionary<string, IList<TriggerDTO>>();
            var jobs = await dataContext.JobTriggers
                .AsQueryable()
                .Include(x => x.Jobs)
                .Select(x => x.Jobs.OrderByDescending(y => y.Started)
                .FirstOrDefault())
                .ToListAsync();

            foreach (var item in QuartzTriggers.Instance)
            {
                var className = item.GetJobClass();
                var group = item.Key.Group;
                var name = item.Key.Name;
                var act = activities.FirstOrDefault(x => x.Parameters.TriggerKey == name && x.Parameters.Group == group);
                var tri = JobTriggers.FirstOrDefault(x => x.TriggerKey == name && x.Group == group);
                var job = jobs.FirstOrDefault(x => x.JobTriggerId == tri?.Id);
                var dto = new TriggerDTO
                {
                    Group = group,
                    Name = name,
                    TriggerStarted = item.StartTimeUtc.DateTime.ToLocalTime(),
                    ConfigPath = item.GetConfig(),
                    Next = item.GetNextFireTimeUtc().Value.DateTime.ToLocalTime(),
                    ActivityId = act?.Id,
                    CronSummary = ExpressionDescriptor.GetDescription(item.CronExpressionString, options),
                    CronString = item.CronExpressionString,
                    ProcessId = act?.ProcessId,
                    JobTriggerId = tri?.Id ?? -1,
                    JobId = job?.Id,
                    LastSync = tri?.LastSync,
                    Success = job?.Success == true,
                    LastJobPeriodEnd = job?.PeriodEnd,
                    LastJobPeriodStart = job?.PeriodStart,
                    LastJobFilesCount = job?.FilesCount,
                    LastJobStarted = job?.Started,
                    LastJobFinished = job?.Finished
                };

                TriggersDtOs.SafeAdd(className, dto);

            }
        }

        public IActionResult OnPostRun(string group, string name)
        {
            var trigger = QuartzTriggers.Instance.Single(t => t.Key.Group == group && t.Key.Name == name);

            string className = trigger.GetJobClass();
            string configPath = trigger.GetConfig();
            bool runAsTask = trigger.GetRunAsTask() == "true";

            var configuration = AutofacConfig.Container.Resolve<IConfiguration>();

            IConfigurationSection connStrings = configuration.GetSection("ConnectionStrings");
            string defaultConnection = connStrings.GetSection("HikConnectionString").Value;

            var parameters = new Parameters(className, group, name, configPath, defaultConnection, runAsTask);

            var activity = new Activity(parameters);
            Task.Run(() => activity.Start());
            return RedirectToPage("./Index", new { msg = $"Activity {group}.{name} started" });
        }

        public IActionResult OnPostKill(Guid activityId)
        {
            var activity = activities.SingleOrDefault(a => a.Id == activityId);

            activity?.Kill();
            return RedirectToPage("./Index", new { msg = $"Activity {activityId} dead" });
        }
    }
}
