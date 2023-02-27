﻿using Hik.Client.FileProviders;
using Hik.DataAccess.Data;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Job.Impl;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Job.Tests.Impl
{
    public class DbMigrationJobTests : JobBaseTest
    {
        protected readonly Mock<IFileProvider> filesProvider;

        public DbMigrationJobTests(ITestOutputHelper output) : base(output)
        {
            filesProvider = new(MockBehavior.Strict);
        }

        [Fact]
        public void Constructor_ValidConfig_ValidConfigType()
        {
            var job = CreateJob();
            Assert.IsType<MigrationConfig>(job.Config);
        }

        [Fact]
        public async Task RunAsync_FoundFiles_FilesSaved()
        {
            var topFolders = new[] { "C:\\FTP\\Floor0" };

            base.SetupGetOrCreateJobTriggerAsync();
            base.SetupCreateJobInstanceAsync();
            base.SetupSaveJobResultAsync();
            base.SetupUpdateDailyStatisticsAsync();
            dbMock.Setup(x => x.SaveFilesAsync(It.IsAny<HikJob>(), It.IsAny<IReadOnlyCollection<MediaFileDto>>()))
                .ReturnsAsync(new List<MediaFile>());
            dbMock.Setup(x => x.SaveDownloadHistoryFilesAsync(It.IsAny<HikJob>(), It.IsAny<IReadOnlyCollection<MediaFile>>()))
                .Returns(Task.CompletedTask);

            filesProvider.Setup(x => x.Initialize(topFolders))
                .Verifiable();
            filesProvider.SetupSequence(x => x.GetOldestFilesBatch(false))
                .ReturnsAsync(new List<MediaFileDto>()
                {
                    new () { Date = new (2022, 01,01)},
                    new () { Date = new (2022, 01,11)},
                    new () { Date = new (2022, 01,21)},
                    new () { Date = new (2022, 01,31)},
                })
                .ReturnsAsync(new List<MediaFileDto>()
                {
                    new () { Date = new (2022, 02,01)},
                    new () { Date = new (2022, 02,11)},
                    new () { Date = new (2022, 02,21)},
                    new () { Date = new (2022, 02,28)},
                })
                .ReturnsAsync(new List<MediaFileDto>());

            var job = CreateJob();
            await job.ExecuteAsync();
            filesProvider.VerifyAll();
            Assert.Equal(8, job.JobInstance.FilesCount);
        }
        private DbMigrationJob CreateJob(string configFileName = "DBMigrationTests.json")
        {
            var config = GetConfig<MigrationConfig>(configFileName);
            return new DbMigrationJob(config, filesProvider.Object, dbMock.Object, this.emailMock.Object, this.loggerMock);
        }
    }
}
