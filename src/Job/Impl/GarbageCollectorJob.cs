﻿using CSharpFunctionalExtensions;
using Hik.Client.FileProviders;
using Hik.DataAccess.Abstractions;
using Hik.DataAccess.Data;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Hik.Helpers.Abstraction;
using Hik.Helpers.Email;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Job.Impl
{
    public class GarbageCollectorJob : JobProcessBase<GarbageCollectorConfig>
    {
        protected readonly IDirectoryHelper directoryHelper;
        protected readonly IFilesHelper filesHelper;
        protected readonly IFileProvider filesProvider;

        public GarbageCollectorJob(JobTrigger trigger,
            IDirectoryHelper directory,
            IFilesHelper files,
            IFileProvider provider,
            IHikDatabase db,
            IEmailHelper email,
            ILogger logger)
            : base(trigger, db, email, logger)
        {

            this.directoryHelper = directory;
            this.filesHelper = files;
            this.filesProvider = provider;
            this.configValidator = new GarbageCollectorConfigValidator();
        }

        protected override async Task<Result<IReadOnlyCollection<MediaFileDto>>> RunAsync()
        {
            IReadOnlyCollection<MediaFileDto> deleteFilesResult;

            if (Config.RetentionPeriodDays > 0)
            {
                var period = TimeSpan.FromDays(Config.RetentionPeriodDays);
                var cutOff = DateTime.Today.Subtract(period);
                filesProvider.Initialize(new[] { Config.DestinationFolder });
                deleteFilesResult = filesProvider.GetFilesOlderThan(Config.FileExtention, cutOff);

                this.DeleteFiles(deleteFilesResult);
            }
            else
            {
                var triggers = await db.GetJobTriggersAsync(Config.Triggers);
                var topFolders = triggers.Select(x => JObject.Parse(x.Config).Value<string>("DestinationFolder")).ToArray();
                deleteFilesResult = PercentageDelete(Config, topFolders);
            }

            directoryHelper.DeleteEmptyDirs(Config.DestinationFolder);
            return Result.Success(deleteFilesResult);
        }

        protected override async Task SaveResultsAsync(IReadOnlyCollection<MediaFileDto> files)
        {
            JobInstance.PeriodStart = files.Min(x => x.Date);
            JobInstance.PeriodEnd = files.Max(x => x.Date);
            JobInstance.FilesCount = files.Count;

            await db.UpdateDailyStatisticsAsync(jobTrigger.Id, files);

            if (JobInstance.PeriodEnd.HasValue && JobInstance.FilesCount > 0 && Config.Triggers?.Length > 0)
            {
                await db.DeleteObsoleteJobsAsync(Config.Triggers, JobInstance.PeriodEnd.Value);
            }
        }

        private void DeleteFiles(IReadOnlyCollection<MediaFileDto> filesToDelete)
        {
            if (Debugger.IsAttached)
            {
                return;
            }

            foreach (var file in filesToDelete)
            {
                file.Size = filesHelper.FileSize(file.Path);
                this.filesHelper.DeleteFile(file.Path);
            }
        }
        private List<MediaFileDto> PercentageDelete(GarbageCollectorConfig gcConfig, string[] topFolders)
        {
            List<MediaFileDto> deletedFiles = new();
            var destination = gcConfig.DestinationFolder;
            var totalSpace = this.directoryHelper.GetTotalSpaceBytes(destination) * 1.0;
            do
            {
                var freeSpace = this.directoryHelper.GetTotalFreeSpaceBytes(destination) * 1.0;

                var freePercentage = 100 * freeSpace / totalSpace;
                string freePercentageString = $"Destination: {destination} Free Percentage: {freePercentage,2} %";
                this.logger.Information("{freePercentage}", freePercentageString);

                if (freePercentage < gcConfig.FreeSpacePercentage)
                {
                    filesProvider.Initialize(topFolders);
                    var filesToDelete = filesProvider.GetNextBatch(gcConfig.FileExtention);
                    if (filesToDelete.Count == 0)
                    {
                        break;
                    }
                    this.DeleteFiles(filesToDelete);
                    deletedFiles.AddRange(filesToDelete);
                }
                else
                {
                    break;
                }
            }
            while (true);
            return deletedFiles;
        }
    }
}