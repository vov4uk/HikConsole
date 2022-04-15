﻿using Hik.Client.Abstraction;
using Hik.Client.Helpers;
using Hik.DataAccess;
using Hik.DataAccess.Data;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Job.Extensions;
using Job.FileProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Job.Impl
{
    public class GarbageCollectorJob : JobProcessBase
    {
        protected readonly IDirectoryHelper directoryHelper;
        protected readonly IFilesHelper filesHelper;

        public GarbageCollectorJob(string trigger, string configFilePath, string connectionString, Guid activityId)
            : base(trigger, configFilePath, connectionString, activityId)
        {
            Config = HikConfigExtensions.GetConfig<GarbageCollectorConfig>(configFilePath);
            LogInfo(Config?.ToString());
            this.directoryHelper = new DirectoryHelper();
            this.filesHelper = new FilesHelper();
        }

        public override void InitializeProcessingPeriod()
        {
        }

        public override Task<IReadOnlyCollection<MediaFileDTO>> RunAsync()
        {
            var gcConfig = Config as GarbageCollectorConfig;

            IReadOnlyCollection<MediaFileDTO> deleteFilesResult;
            var fileProvider = new WinFileProvider(gcConfig.FileExtention);

            if (gcConfig.RetentionPeriodDays > 0)
            {
                var period = TimeSpan.FromDays(gcConfig.RetentionPeriodDays);
                var cutOff = DateTime.Today.Subtract(period);
                fileProvider.Initialize(new[] { gcConfig.DestinationFolder });
                deleteFilesResult = fileProvider.GetFilesOlderThan(cutOff);

                this.DeleteFiles(deleteFilesResult);
            }
            else
            {
                deleteFilesResult = PersentageDelete(gcConfig, fileProvider);
            }

            directoryHelper.DeleteEmptyDirs(gcConfig.DestinationFolder);
            return Task.FromResult(deleteFilesResult);
        }

        private List<MediaFileDTO> PersentageDelete(GarbageCollectorConfig gcConfig, WinFileProvider fileProvider)
        {
            List<MediaFileDTO> deletedFiles = new();
            var destination = gcConfig.DestinationFolder;
            do
            {
                double totalSpace = this.directoryHelper.GetTotalSpaceGb(destination);
                double freeSpace = this.directoryHelper.GetTotalFreeSpaceGb(destination);

                var freePercentage = 100 * freeSpace / totalSpace;
                this.logger.Info($"Destination: {destination} Free Percentage: {freePercentage,2}");

                if (freePercentage < gcConfig.FreeSpacePercentage)
                {
                    fileProvider.Initialize(gcConfig.TopFolders);
                    var filesToDelete = fileProvider.GetNextBatch();
                    if (!filesToDelete.Any())
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

        protected void DeleteFiles(IReadOnlyCollection<MediaFileDTO> filesToDelete)
        {
            foreach (var file in filesToDelete)
            {
                this.logger.Debug($"Deleting: {file.Path}");
#if RELEASE
                file.Size = filesHelper.FileSize(file.Path);
                this.filesHelper.DeleteFile(file.Path);
#endif
            }
        }

        public override async Task SaveResultsAsync(IReadOnlyCollection<MediaFileDTO> files, JobService service)
        {
            JobInstance.PeriodStart = files.Min(x => x.Date);
            JobInstance.PeriodEnd = files.Max(x => x.Date);
            JobInstance.FilesCount = files.Count;

            await service.UpdateDailyStatisticsAsync(files);

            await service.DeleteObsoleteJobsAsync((Config as GarbageCollectorConfig).Triggers, JobInstance.PeriodEnd.Value);
        }

        public override Task SaveHistoryAsync(IReadOnlyCollection<MediaFile> files, JobService service)
        {
            return Task.CompletedTask;
        }
    }
}