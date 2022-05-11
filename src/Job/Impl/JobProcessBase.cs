﻿using Hik.Api;
using Hik.Client.Events;
using Hik.DataAccess.Abstractions;
using Hik.DataAccess.Data;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Job.Email;
using Job.Extensions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Job.Impl
{
    public abstract class JobProcessBase<T> : IJobProcess
        where T : BaseConfig
    {
        protected readonly ILogger logger;
        protected readonly IHikDatabase db;
        protected readonly IEmailHelper email;
        protected JobTrigger jobTrigger;

        protected JobProcessBase(string trigger, T config, IHikDatabase db, IEmailHelper email, ILogger logger)
        {
            TriggerKey = trigger;
            this.logger = logger;
            this.db = db;
            this.email = email;
            Config = config;
            LogInfo(Config.ToString());
        }

        public T Config { get; protected set; }
        public string TriggerKey { get; private set; }
        internal HikJob JobInstance { get; private set; }

        public async Task ExecuteAsync()
        {
            try
            {
                await CreateJobInstanceAsync();
                Config.Alias = TriggerKey;
                var result = await RunAsync();
                await SaveResultsInternalAsync(result);
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        protected void ExceptionFired(object sender, ExceptionEventArgs e)
        {
            HandleException(e.Exception);
        }

        protected void LogInfo(string msg)
        {
            logger.Info($"{TriggerKey} - {msg}");
        }

        protected abstract Task<IReadOnlyCollection<MediaFileDTO>> RunAsync();

        protected virtual async Task SaveResultsAsync(IReadOnlyCollection<MediaFileDTO> files)
        {
            JobInstance.FilesCount = files.Count;
            var mediaFiles = await db.SaveFilesAsync(JobInstance, files);
            await db.UpdateDailyStatisticsAsync(JobInstance, files);
            await db.SaveDownloadHistoryFilesAsync(JobInstance, mediaFiles);
        }

        private void HandleException(Exception e)
        {
            this.JobInstance.Success = false;
            try
            {
                Task.WaitAll(LogExceptionToDB(e), db.SaveJobResultAsync(JobInstance));
            }
            catch (Exception ex) { logger.Error(ex.ToString()); }

            if (Config.SentEmailOnError)
            {
                var details = JobInstance.ToHtmlTable(Config);
                email.Send(e, Config.Alias, details);
            }
            else
            {
                logger.Error(e.ToString());
            }
        }

        private async Task CreateJobInstanceAsync()
        {
            this.jobTrigger = await db.GetOrCreateJobTriggerAsync(TriggerKey);

            this.JobInstance = new HikJob
            {
                Started = DateTime.Now,
                JobTriggerId = jobTrigger.Id
            };

            await db.CreateJobInstanceAsync(JobInstance);
        }

        private async Task LogExceptionToDB(Exception e)
        {
            await db.LogExceptionToAsync(JobInstance.Id, (e as HikException)?.ErrorMessage ?? e.ToString(), e.StackTrace, (e as HikException)?.ErrorCode);
        }

        private async Task SaveResultsInternalAsync(IReadOnlyCollection<MediaFileDTO> files)
        {
            if (files?.Any() == true)
            {
                await SaveResultsAsync(files);
            }
            else
            {
                logger.Warn($"{TriggerKey} - Results Empty");
            }

            if (this.JobInstance.Success)
            {
                await db.SaveJobResultAsync(JobInstance);
            }
        }
    }
}