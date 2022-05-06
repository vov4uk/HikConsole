using AutoFixture.Xunit2;
using Hik.DataAccess.Abstractions;
using Hik.DataAccess.Data;
using Hik.DTO.Contracts;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace Hik.DataAccess.Tests
{
    public class HikDatabaseTests
    {
        protected readonly Mock<IUnitOfWorkFactory> uowFactory;
        protected readonly Mock<IUnitOfWork> uow;

        public HikDatabaseTests()
        {
            this.uow = new(MockBehavior.Strict);
            this.uowFactory = new(MockBehavior.Strict);
            this.uowFactory.Setup(x => x.CreateUnitOfWork())
                .Returns(uow.Object);
            this.uow.Setup(x => x.Dispose());
        }

        [Fact]
        public async Task CreateJobInstanceAsync_Add_SaveChanges()
        {
            var hikJobRepo = new Mock<IBaseRepository<HikJob>>(MockBehavior.Strict);
            hikJobRepo.Setup(x => x.AddAsync(It.IsAny<HikJob>()))
                .Returns(new ValueTask<HikJob>(new HikJob()));
            uow.Setup(x => x.GetRepository<HikJob>())
                .Returns(hikJobRepo.Object);
            uow.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            await db.CreateJobInstanceAsync(new HikJob());

            uow.VerifyAll();
        }

        [Theory]
        [AutoData]
        public async Task LogExceptionToAsync_Add_SaveChanges(int jobId, string message, string callStack, uint? errorCode)
        {
            ExceptionLog? actual = null;
            var exRepo = new Mock<IBaseRepository<ExceptionLog>>(MockBehavior.Strict);
            exRepo.Setup(x => x.AddAsync(It.IsAny<ExceptionLog>()))
                .Callback<ExceptionLog>((x) => actual = x)
                .Returns(new ValueTask<ExceptionLog>(new ExceptionLog()));
            uow.Setup(x => x.GetRepository<ExceptionLog>())
                .Returns(exRepo.Object);
            uow.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            await db.LogExceptionToAsync(jobId, message, callStack, errorCode);

            uow.VerifyAll();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            Assert.Equal(message, actual.Message);
            Assert.Equal(callStack, actual.CallStack);
            Assert.Equal(jobId, actual.JobId);
            Assert.Equal(errorCode, actual.HikErrorCode);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        [Fact]
        public async Task GetOrCreateJobTriggerAsync_FoundJob_ReturnJob()
        {
            var jobRepo = new Mock<IBaseRepository<JobTrigger>>(MockBehavior.Strict);
            jobRepo.Setup(x => x.FindByAsync(It.IsAny<Expression<Func<JobTrigger, bool>>>(), It.IsAny<Expression<Func<JobTrigger, object>>[]>()))
                .ReturnsAsync(new JobTrigger());
            uow.Setup(x => x.GetRepository<JobTrigger>())
                .Returns(jobRepo.Object);

            var db = new HikDatabase(uowFactory.Object);

            var trigger = await db.GetOrCreateJobTriggerAsync("Key.Group");

            Assert.NotNull(trigger);
        }

        [Fact]
        public async Task GetOrCreateJobTriggerAsync_NotFoundJob_CreateJob()
        {
            JobTrigger? actual = null;
            var jobRepo = new Mock<IBaseRepository<JobTrigger>>(MockBehavior.Strict);

#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            jobRepo.Setup(x => x.FindByAsync(It.IsAny<Expression<Func<JobTrigger, bool>>>(), It.IsAny<Expression<Func<JobTrigger, object>>[]>()))
                .ReturnsAsync(default(JobTrigger));
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

            jobRepo.Setup(x => x.AddAsync(It.IsAny<JobTrigger>()))
                .Callback<JobTrigger>((x) => actual = x)
                .Returns(new ValueTask<JobTrigger>(new JobTrigger()));

            uow.Setup(x => x.GetRepository<JobTrigger>())
                .Returns(jobRepo.Object);
            uow.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            var trigger = await db.GetOrCreateJobTriggerAsync("Group.Key");

            Assert.NotNull(trigger);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            Assert.Equal("Key", actual.TriggerKey);
            Assert.Equal("Group", actual.Group);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        [Fact]
        public async Task SaveJobResultAsync_JobSuccess_LastSyncUpdated()
        {
            JobTrigger trigger = new JobTrigger();
            HikJob job = new HikJob() { Started = new (2000, 01, 01), Finished = new (2000, 01, 01), Success = true };
            var hikJobRepo = new Mock<IBaseRepository<HikJob>>(MockBehavior.Strict);
            var jobRepo = new Mock<IBaseRepository<JobTrigger>>(MockBehavior.Strict);

            hikJobRepo.Setup(x => x.Update(job));
            jobRepo.Setup(x => x.FindByAsync(It.IsAny<Expression<Func<JobTrigger, bool>>>(), It.IsAny<Expression<Func<JobTrigger, object>>[]>()))
                .ReturnsAsync(trigger);

            uow.Setup(x => x.GetRepository<JobTrigger>())
                .Returns(jobRepo.Object);
            uow.Setup(x => x.GetRepository<HikJob>())
                .Returns(hikJobRepo.Object);
            uow.Setup(x => x.SaveChangesAsync(job))
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            await db.SaveJobResultAsync(job);

            Assert.NotEqual(new DateTime(2000, 01, 01), job.Finished);
            Assert.Equal(new DateTime(2000, 01, 01), trigger.LastSync);
        }

        [Fact]
        public async Task SaveJobResultAsync_JobNotSuccess_LastSyncNotUpdated()
        {
            JobTrigger trigger = new JobTrigger() { LastSync = new (1999, 12, 31) };
            HikJob job = new HikJob() { Started = new (2000, 01, 01), Success = false };
            var hikJobRepo = new Mock<IBaseRepository<HikJob>>(MockBehavior.Strict);

            hikJobRepo.Setup(x => x.Update(job));

            uow.Setup(x => x.GetRepository<HikJob>())
                .Returns(hikJobRepo.Object);
            uow.Setup(x => x.SaveChangesAsync(job))
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            await db.SaveJobResultAsync(job);

            Assert.NotEqual(new DateTime(2000, 01, 01), job.Finished);
            Assert.Equal(new DateTime(1999, 12, 31), trigger.LastSync);
        }

        [Fact]
        public async Task SaveFilesAsync_HasDownloadDuration_DownloadDurationSaved()
        {
            HikJob job = new HikJob() { Started = new (2022, 01, 01), Success = true, JobTriggerId = 31 };

            List<MediaFileDTO> files = new List<MediaFileDTO>()
            {
                new () { Date = new (2022, 01,01), DownloadDuration = 1 },
                new () { Date = new (2022, 01,11), DownloadDuration = 1 },
                new () { Date = new (2022, 01,21), DownloadDuration = 1 },
                new () { Date = new (2022, 01,31), DownloadDuration = 1 },
            };

            var fileRepo = new Mock<IBaseRepository<MediaFile>>(MockBehavior.Strict);
            var downloadRepo = new Mock<IBaseRepository<DownloadDuration>>(MockBehavior.Strict);

            fileRepo.Setup(x => x.AddRangeAsync(It.IsAny<List<MediaFile>>()))
                .Returns(Task.CompletedTask);
            downloadRepo.Setup(x => x.AddRangeAsync(It.IsAny<List<DownloadDuration>>()))
                .Returns(Task.CompletedTask);

            uow.Setup(x => x.GetRepository<MediaFile>())
                .Returns(fileRepo.Object);
            uow.Setup(x => x.GetRepository<DownloadDuration>())
                .Returns(downloadRepo.Object);
            uow.Setup(x => x.SaveChangesAsync(job))
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            var actual = await db.SaveFilesAsync(job, files);
            Assert.Equal(4, actual.Count);
            Assert.True(actual.TrueForAll(x => x.JobTriggerId == 31));
        }

        [Fact]
        public async Task SaveFilesAsync_NoDownloadDuration_DownloadDurationNotSaved()
        {
            HikJob job = new HikJob() { Started = new DateTime(2022, 01, 01), Success = true };

            List<MediaFileDTO> files = new ()
            {
                new () { Date = new (2022, 01,01) },
                new () { Date = new (2022, 01,11) },
                new () { Date = new (2022, 01,21) },
                new () { Date = new (2022, 01,31) },
            };

            var fileRepo = new Mock<IBaseRepository<MediaFile>>(MockBehavior.Strict);

            fileRepo.Setup(x => x.AddRangeAsync(It.IsAny<List<MediaFile>>()))
                .Returns(Task.CompletedTask);

            uow.Setup(x => x.GetRepository<MediaFile>())
                .Returns(fileRepo.Object);
            uow.Setup(x => x.SaveChangesAsync(job))
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            var actual = await db.SaveFilesAsync(job, files);
            Assert.Equal(4, actual.Count);
        }

        [Fact]
        public async Task SaveDownloadHistoryFilesAsync_FilesSaved()
        {
            HikJob job = new () { Started = new (2022, 01, 01), Success = true };

            List<MediaFile> files = new List<MediaFile>()
            {
                new () { Date = new (2022, 01,01) },
                new () { Date = new (2022, 01,11) },
                new () { Date = new (2022, 01,21) },
                new () { Date = new (2022, 01,31) },
            };

            var fileRepo = new Mock<IBaseRepository<DownloadHistory>>(MockBehavior.Strict);

            fileRepo.Setup(x => x.AddRangeAsync(It.IsAny<List<DownloadHistory>>()))
                .Returns(Task.CompletedTask);

            uow.Setup(x => x.GetRepository<DownloadHistory>())
                .Returns(fileRepo.Object);
            uow.Setup(x => x.SaveChangesAsync(job))
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            await db.SaveDownloadHistoryFilesAsync(job, files);

            fileRepo.Verify(x => x.AddRangeAsync(It.IsAny<List<DownloadHistory>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateDailyStatisticsAsync_StatisticsUpdated()
        {
            HikJob job = new () { Started = new DateTime(2022, 01, 01), Success = true };

            var newStatistics = new List<DailyStatistic>();

            var statistics = new List<DailyStatistic>
            {
                new () { Period = new (2021, 12,31), FilesCount = 1, FilesSize = 1024 },
                new () { Period = new (2022, 01,01), FilesCount = 1, FilesSize = 1024 },
                new () { Period = new (2022, 01,02), FilesCount = 1, FilesSize = 1024 },
            };

            List<MediaFileDTO> files = new ()
            {
                new () { Date = new (2022, 01,01), Size = 1024, Duration = 60 },
                new () { Date = new (2022, 01,02), Size = 1024, Duration = 60 },
                new () { Date = new (2022, 01,03), Size = 1024, Duration = 60 },
                new () { Date = new (2022, 01,03), Size = 1024, Duration = 60 },
                new () { Date = new (2022, 01,03), Size = 1024, Duration = 60 },
                new () { Date = new (2022, 01,04), Size = 1024, Duration = 60 },
                new () { Date = new (2022, 01,04), Size = 1024, Duration = 60 },
            };

            var statRepo = new Mock<IBaseRepository<DailyStatistic>>(MockBehavior.Strict);

            statRepo.Setup(x => x.FindManyAsync(It.IsAny<Expression<Func<DailyStatistic, bool>>>(), It.IsAny<Expression<Func<DailyStatistic, object>>[]>()))
                .ReturnsAsync(statistics);
            statRepo.Setup(x => x.AddRangeAsync(It.IsAny<List<DailyStatistic>>()))
                .Callback<IEnumerable<DailyStatistic>>(x => newStatistics.AddRange(x))
                .Returns(Task.CompletedTask);

            uow.Setup(x => x.GetRepository<DailyStatistic>())
                .Returns(statRepo.Object);
            uow.Setup(x => x.SaveChangesAsync(job))
                .ReturnsAsync(0);

            var db = new HikDatabase(uowFactory.Object);

            await db.UpdateDailyStatisticsAsync(job, files);

            Assert.Equal(2, newStatistics.Count);
            Assert.Equal(5, statistics.Count);
            var thirdOfJan = newStatistics[0];
            var fourthOfJan = newStatistics[1];
            Assert.Equal(3, thirdOfJan.FilesCount);
            Assert.Equal(2, fourthOfJan.FilesCount);
            Assert.Equal(3072, thirdOfJan.FilesSize);
            Assert.Equal(2048, fourthOfJan.FilesSize);
            Assert.Equal(new DateTime(2022, 01, 03), thirdOfJan.Period);
            Assert.Equal(new DateTime(2022, 01, 04), fourthOfJan.Period);
        }

        [Fact]
        public async Task DeleteObsoleteJobsAsync_InvalidTrigger_NothingDeleted()
        {
            var jobRepo = new Mock<IBaseRepository<JobTrigger>>(MockBehavior.Strict);
            var fileRepo = new Mock<IBaseRepository<MediaFile>>(MockBehavior.Strict);
            var hikJobRepo = new Mock<IBaseRepository<HikJob>>(MockBehavior.Strict);
            uow.Setup(x => x.GetRepository<MediaFile>())
                .Returns(fileRepo.Object);
            uow.Setup(x => x.GetRepository<JobTrigger>())
                .Returns(jobRepo.Object);
            uow.Setup(x => x.GetRepository<HikJob>())
                .Returns(hikJobRepo.Object);
            uow.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(0);

#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            jobRepo.Setup(x => x.FindByAsync(It.IsAny<Expression<Func<JobTrigger, bool>>>(), It.IsAny<Expression<Func<JobTrigger, object>>[]>()))
                .ReturnsAsync(default(JobTrigger));
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

            var db = new HikDatabase(uowFactory.Object);

            await db.DeleteObsoleteJobsAsync(new string[] {"Test.Job"}, DateTime.Now);

            fileRepo.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<MediaFile>>()), Times.Never);
        }

        [Fact]
        public async Task DeleteObsoleteJobsAsync_ValidTrigger_FilesDeleted()
        {
            var jobRepo = new Mock<IBaseRepository<JobTrigger>>(MockBehavior.Strict);
            var fileRepo = new Mock<IBaseRepository<MediaFile>>(MockBehavior.Strict);
            var hikJobRepo = new Mock<IBaseRepository<HikJob>>(MockBehavior.Strict);
            uow.Setup(x => x.GetRepository<MediaFile>())
                .Returns(fileRepo.Object);
            uow.Setup(x => x.GetRepository<JobTrigger>())
                .Returns(jobRepo.Object);
            uow.Setup(x => x.GetRepository<HikJob>())
                .Returns(hikJobRepo.Object);
            uow.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(0);

            jobRepo.Setup(x => x.FindByAsync(It.IsAny<Expression<Func<JobTrigger, bool>>>(), It.IsAny<Expression<Func<JobTrigger, object>>[]>()))
                .ReturnsAsync(new JobTrigger());
            fileRepo.Setup(x => x.FindManyAsync(It.IsAny<Expression<Func<MediaFile, bool>>>(), It.IsAny<Expression<Func<MediaFile, object>>[]>()))
                .ReturnsAsync(new List<MediaFile>());
            fileRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<MediaFile>>()));
            hikJobRepo.Setup(x => x.FindManyAsync(It.IsAny<Expression<Func<HikJob, bool>>>(), It.IsAny<Expression<Func<HikJob, object>>[]>()))
                .ReturnsAsync(new List<HikJob>());
            hikJobRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<HikJob>>()));

            var db = new HikDatabase(uowFactory.Object);

            await db.DeleteObsoleteJobsAsync(new string[] { "Test.Job" }, DateTime.Now);

            fileRepo.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<MediaFile>>()), Times.Once);
            hikJobRepo.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<HikJob>>()), Times.Once);
        }
    }
}