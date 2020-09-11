﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using AutoMapper;
using HikApi;
using HikApi.Data;
using HikConsole.Abstraction;
using HikConsole.Config;
using HikConsole.Infrastructure;
using HikConsole.Scheduler;
using Moq;
using NLog;
using Xunit;

namespace HikConsole.Tests
{
    public class HikDownloaderTests
    {
        private readonly Mock<IDirectoryHelper> directoryMock;
        private readonly Fixture fixture;
        private readonly Mock<IHikClient> clientMock;
        private readonly Mock<IHikClientFactory> clientFactoryMock;
        private readonly Mock<IProgressBarFactory> progressMock;
        private readonly Mock<IHikConfig> configMock;
        private readonly IMapper mapper;

        public HikDownloaderTests()
        {
            this.directoryMock = new Mock<IDirectoryHelper>(MockBehavior.Strict);
            this.clientMock = new Mock<IHikClient>(MockBehavior.Strict);
            this.clientFactoryMock = new Mock<IHikClientFactory>(MockBehavior.Strict);
            this.progressMock = new Mock<IProgressBarFactory>();

            // temp fix, till not cover photo download with UT
            this.clientMock.Setup(x => x.FindPhotosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<RemotePhotoFile>());

            this.fixture = new Fixture();
            var status = this.fixture.Build<HdInfo>().With(x => x.HdStatus, 0U).Create();
            this.clientMock.Setup(x => x.CheckHardDriveStatus()).Returns(status);
            this.configMock = new Mock<IHikConfig>();

            Action<IMapperConfigurationExpression> configureAutoMapper = x =>
            {
                x.AddProfile<HikConsoleProfile>();
            };

            var mapperConfig = new MapperConfiguration(configureAutoMapper);
            this.mapper = mapperConfig.CreateMapper();
        }

        [Fact]
        public async Task DownloadAsync_EmptyConfig_NothingToDo()
        {
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, Array.Empty<CameraConfig>())
                    .Create();
            var downloader = this.CreateHikDownloader(appConfig);

            await downloader.DownloadAsync(string.Empty);

            this.clientMock.Verify(x => x.InitializeClient(), Times.Never);
        }

        [Fact]
        public async Task DownloadAsync_LoginFailed_LogWarning()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();

            this.SetupClientInitialize();
            this.SetupClientDispose();
            this.clientMock.Setup(x => x.Login()).Returns(false);

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
        }

        [Fact]
        public async Task DownloadAsync_LoginThrowsException_HandleException()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();

            this.SetupClientInitialize();
            this.clientMock.Setup(x => x.ForceExit());
            this.clientMock.Setup(x => x.Login()).Throws(new HikException("Login", 7));

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.ForceExit(), Times.Once);
        }

        [Fact]
        public async Task DownloadAsync_FindNoFiles_NothingToDownload()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();

            this.clientMock.Setup(x => x.FindVideosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(Array.Empty<RemoteVideoFile>());

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.directoryMock.Verify(x => x.GetTotalFreeSpace(It.IsAny<string>()), Times.Once);
            this.directoryMock.Verify(x => x.DirSize(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DownloadAsync_FindOneFiles_StartDownload()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();
            var file = this.fixture.Build<RemoteVideoFile>()
                .Create();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.SetupUpdateProgress();
            this.clientMock.Setup(x => x.StartVideoDownload(file)).Returns(true);
            this.clientMock.SetupGet(x => x.IsDownloading).Returns(false);

            this.clientMock.Setup(x => x.FindVideosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new RemoteVideoFile[] { file, file });

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.StartVideoDownload(file), Times.Once);
            this.clientMock.Verify(x => x.UpdateVideoProgress(), Times.Once);
            this.VerifyStatisticWasPrinted();
        }

        [Fact]
        public async Task DownloadAsync_FindManyFiles_AllStartDownload()
        {
            int filesCount = 3;

            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();
            var files = this.fixture.Build<RemoteVideoFile>()
                .CreateMany(filesCount)
                .ToArray();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.SetupUpdateProgress();
            this.clientMock.Setup(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>())).Returns(true);
            this.clientMock.SetupGet(x => x.IsDownloading).Returns(false);

            this.clientMock.Setup(x => x.FindVideosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(files);

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>()), Times.Exactly(filesCount - 1));
            this.clientMock.Verify(x => x.UpdateVideoProgress(), Times.Exactly(filesCount - 1));
            this.VerifyStatisticWasPrinted();
        }

        [Fact]
        public async Task DownloadAsync_FindManyFiles_OnlyOneStartDownload()
        {
            int filesCount = 3;

            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();
            var files = this.fixture.Build<RemoteVideoFile>()
                .CreateMany(filesCount)
                .ToArray();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.SetupUpdateProgress();
            this.clientMock.SetupSequence(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>()))
                .Returns(false)
                .Returns(true);

            this.clientMock.Setup(x => x.IsDownloading).Returns(false);

            this.clientMock.Setup(x => x.FindVideosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(files);

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>()), Times.Exactly(filesCount - 1));
            this.clientMock.Verify(x => x.UpdateVideoProgress(), Times.Once);
            this.clientMock.VerifyGet(x => x.IsDownloading, Times.Once);
            this.VerifyStatisticWasPrinted();
        }

        [Fact]
        public async Task DownloadAsync_FindOneFiles_LongRunningDownload()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();
            var file = this.fixture.Build<RemoteVideoFile>()
                .Create();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.SetupUpdateProgress();
            this.clientMock.Setup(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>())).Returns(true).Verifiable();
            this.clientMock.SetupSequence(x => x.IsDownloading)
                .Returns(true)
                .Returns(true)
                .Returns(false);

            this.clientMock.Setup(x => x.FindVideosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new RemoteVideoFile[] { file, file });

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify();
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.StartVideoDownload(file), Times.Once);
            this.clientMock.Verify(x => x.UpdateVideoProgress(), Times.Exactly(3));
            this.VerifyStatisticWasPrinted();
        }

        [Fact]
        public async Task DownloadAsync_MultipleCameras_OneFilePerCameraToDownload()
        {
            int cameraCount = 3;
            var cameraConfigs = this.fixture.Build<CameraConfig>()
                .CreateMany(cameraCount)
                .ToArray();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, cameraConfigs)
                    .Create();
            var file = this.fixture.Build<RemoteVideoFile>()
                .Create();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.SetupUpdateProgress();
            this.clientMock.Setup(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>())).Returns(true).Verifiable();
            this.clientMock.SetupGet(x => x.IsDownloading).Returns(false);

            this.clientMock.Setup(x => x.FindVideosAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new RemoteVideoFile[] { file, file });

            // act
            var downloader = this.CreateHikDownloader(appConfig);
            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify();
            this.clientMock.Verify(x => x.InitializeClient(), Times.Exactly(cameraCount));
            this.clientMock.Verify(x => x.Login(), Times.Exactly(cameraCount));
            this.clientMock.Verify(x => x.Dispose(), Times.Exactly(cameraCount));
            this.clientMock.Verify(x => x.StartVideoDownload(It.IsAny<RemoteVideoFile>()), Times.Exactly(cameraCount));
            this.clientMock.Verify(x => x.UpdateVideoProgress(), Times.Exactly(cameraCount));
            this.directoryMock.Verify(x => x.GetTotalFreeSpace(It.IsAny<string>()), Times.Exactly(cameraCount));
            this.directoryMock.Verify(x => x.DirSize(It.IsAny<string>()), Times.Exactly(cameraCount));
        }

        [Fact]
        public async Task Cancel_CancelationOnClintItitialize_ClientForceExited()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var appConfig = this.fixture.Build<AppConfig>()
                    .With(x => x.Cameras, new CameraConfig[] { cameraConfig })
                    .Create();

            this.clientMock.Setup(x => x.InitializeClient());
            this.clientMock.Setup(x => x.ForceExit());
            this.SetupClientDispose();

            // act
            var downloader = this.CreateHikDownloader(appConfig);

            this.clientMock.Setup(x => x.InitializeClient()).Callback(downloader.Cancel);

            await downloader.DownloadAsync(string.Empty);

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.ForceExit(), Times.Once);
        }

        private void SetupDirectoryHelper()
        {
            this.directoryMock.Setup(x => x.GetTotalFreeSpace(It.IsAny<string>())).Returns(1024);
            this.directoryMock.Setup(x => x.DirSize(It.IsAny<string>())).Returns(1024);
        }

        private void SetupClientSuccessLogin()
        {
            this.SetupClientInitialize();
            this.clientMock.Setup(x => x.Login()).Returns(true);
        }

        private void SetupClientInitialize()
        {
            this.clientMock.Setup(x => x.InitializeClient());
        }

        private void SetupClientDispose()
        {
            this.clientMock.Setup(x => x.Dispose());
        }

        private void SetupUpdateProgress()
        {
            this.clientMock.Setup(x => x.UpdateVideoProgress());
        }

        private void VerifyStatisticWasPrinted()
        {
            this.directoryMock.Verify(x => x.GetTotalFreeSpace(It.IsAny<string>()), Times.Once);
            this.directoryMock.Verify(x => x.DirSize(It.IsAny<string>()), Times.Once);
        }

        private HikDownloader CreateHikDownloader(AppConfig appConfig)
        {
            this.clientFactoryMock.Setup(x => x.Create(It.IsAny<CameraConfig>())).Returns(this.clientMock.Object);
            this.configMock.Setup(x => x.GetConfig(It.IsAny<string>())).Returns(appConfig);

            return new HikDownloader(this.configMock.Object, this.directoryMock.Object, this.clientFactoryMock.Object, this.mapper)
            {
                ProgressCheckPeriodMilliseconds = 100,
            };
        }
    }
}
