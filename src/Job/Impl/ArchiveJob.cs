﻿using Hik.Client.Abstraction;
using Hik.DataAccess.Abstractions;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Job.Email;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Job.Impl
{
    public class ArchiveJob : JobProcessBase<ArchiveConfig>
    {
        private const string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss";
        private readonly IArchiveService worker;

        public ArchiveJob(string trigger, ArchiveConfig config, IArchiveService worker, IHikDatabase db, IEmailHelper email, ILogger logger)
            : base(trigger, config, db, email, logger)
        {
            this.worker = worker;
        }

        protected override async Task<IReadOnlyCollection<MediaFileDTO>> RunAsync()
        {
            worker.ExceptionFired += base.ExceptionFired;
            return await worker.ExecuteAsync(Config, DateTime.MinValue, DateTime.MaxValue);
        }

        protected override async Task SaveResultsAsync(IReadOnlyCollection<MediaFileDTO> files)
        {
            JobInstance.PeriodStart = files.Min(x => x.Date);
            JobInstance.PeriodEnd = files.Max(x => x.Date);
            JobInstance.FilesCount = files.Count;

            var abnormalFilesCount = Config.AbnormalFilesCount;
            if (abnormalFilesCount > 0 && files.Count > abnormalFilesCount)
            {
                email.Send(
                    $"{files.Count} - {TriggerKey}: Abnormal activity detected",
                    $"{files.Count} taken in period from {JobInstance.PeriodStart?.ToString(DateTimeFormat)} to {JobInstance.PeriodEnd?.ToString(DateTimeFormat)}");
            }

            await db.UpdateDailyStatisticsAsync(JobInstance, files);

            if (files.Sum(x => x.Duration ?? 0) > 0)
            {
                var mediaFiles = await db.SaveFilesAsync(JobInstance, files);
                await db.SaveDownloadHistoryFilesAsync(JobInstance, mediaFiles);
            }
        }
    }
}