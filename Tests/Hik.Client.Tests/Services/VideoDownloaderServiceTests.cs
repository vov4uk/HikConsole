﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Hik.Api;
using Hik.Client.Abstraction;
using Hik.Client.Service;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Hik.Helpers.Abstraction;
using Serilog;
using Moq;
using Xunit;

namespace Hik.Client.Tests.Services
{
    public class VideoDownloaderServiceTests
    {
        private readonly Mock<IDirectoryHelper> directoryMock;
        private readonly Fixture fixture;
        private readonly Mock<IDownloaderClient> clientMock;
        private readonly Mock<IClientFactory> clientFactoryMock;
        private readonly Mock<ILogger> loggerMock;

        public VideoDownloaderServiceTests()
        {
            this.directoryMock = new (MockBehavior.Strict);
            this.clientMock = new (MockBehavior.Strict);
            this.clientFactoryMock = new (MockBehavior.Strict);
            this.loggerMock = new ();
            this.fixture = new ();
        }

        [Fact]
        public async Task ExecuteAsync_LoginThrowsException_HandleException()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();

            this.SetupClientInitialize();
            this.clientMock.Setup(x => x.ForceExit());
            this.clientMock.Setup(x => x.Login()).Throws(new HikException("Login", "Error"));
            this.clientMock.Setup(x => x.Dispose());
            this.directoryMock.Setup(x => x.DirExist(It.IsAny<string>()))
                .Returns(true);

            // act
            var downloader = this.CreateHikDownloader();
            await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_FindNoFiles_NothingToDownload()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();

            this.clientMock.Setup(x => x.GetFilesListAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Array.Empty<MediaFileDto>());

            // act
            var downloader = this.CreateHikDownloader();
            await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_FindOneFile_FileDownloaded()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>().Create();
            var file = this.fixture.Build<MediaFileDto>().Create();
            bool fileDownloaded = false;

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.clientMock.Setup(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            this.clientMock.Setup(x => x.GetFilesListAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new MediaFileDto[] { file, file });

            // act
            var downloader = this.CreateHikDownloader();
            downloader.FileDownloaded += (object sender, Hik.Client.Events.FileDownloadedEventArgs e) =>
            {
                fileDownloaded = true;
            };

            await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            Assert.True(fileDownloaded);
        }

        [Fact]
        public async Task ExecuteAsync_FindManyFiles_LastFileNotDownloaded()
        {
            int filesCount = 5;

            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var files = this.fixture.Build<MediaFileDto>()
                .CreateMany(filesCount)
                .ToArray();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.clientMock.Setup(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            this.clientMock.Setup(x => x.GetFilesListAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(files);

            // act
            var downloader = this.CreateHikDownloader();
            await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()), Times.Exactly(filesCount - 1));
        }

        [Fact]
        public async Task ExecuteAsync_FindManyFiles_OnlyOneStartDownload()
        {
            int filesCount = 5;

            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var files = this.fixture.Build<MediaFileDto>()
                .CreateMany(filesCount)
                .ToArray();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.clientMock.SetupSequence(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)
                .ReturnsAsync(false)
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            this.clientMock.Setup(x => x.GetFilesListAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(files);

            // act
            var downloader = this.CreateHikDownloader();
            await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()), Times.Exactly(filesCount - 1));
        }

        [Fact]
        public async Task ExecuteAsync_CancellationOnClintInitialize_ClientDisposed()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();

            this.clientMock.Setup(x => x.InitializeClient());
            this.directoryMock.Setup(x => x.DirExist(It.IsAny<string>()))
                .Returns(true);
            this.SetupClientDispose();

            // act
            var downloader = this.CreateHikDownloader();

            this.clientMock.Setup(x => x.InitializeClient()).Callback(downloader.Cancel);

            await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_CancellationOnDownload_ExceptionFiredAfterFirstFileDownloaded()
        {
            int filesCount = 5;

            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();
            var files = this.fixture.Build<MediaFileDto>()
                .CreateMany(filesCount)
                .ToArray();

            this.SetupClientSuccessLogin();
            this.SetupDirectoryHelper();
            this.SetupClientDispose();
            this.clientMock.Setup(x => x.GetFilesListAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(files);

            // act
            var downloader = this.CreateHikDownloader();
            this.clientMock.Setup(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()))
                .Callback(downloader.Cancel)
                .ReturnsAsync(true);

            var result = await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            this.clientMock.Verify(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(result.IsFailure);
        }

        [Fact]
        public async Task ExecuteAsync_CancellationOnLogin_ExceptionFiredGetRemoteFilesListNotStarted()
        {
            var cameraConfig = this.fixture.Build<CameraConfig>()
                .Create();

            this.clientMock.Setup(x => x.InitializeClient());

            this.clientMock.Setup(x => x.Dispose());
            this.directoryMock.Setup(x => x.DirExist(It.IsAny<string>()))
                .Returns(true);
            this.SetupClientDispose();
            this.clientMock.Setup(x => x.InitializeClient());

            // act
            var downloader = this.CreateHikDownloader();
            this.clientMock.Setup(x => x.DownloadFileAsync(It.IsAny<MediaFileDto>(), It.IsAny<CancellationToken>()));
            this.clientMock.Setup(x => x.Login()).Callback(downloader.Cancel)
                .Returns(true);
            var result = await downloader.ExecuteAsync(cameraConfig, default(DateTime), default(DateTime));

            // assert
            this.clientMock.Verify(x => x.InitializeClient(), Times.Once);
            this.clientMock.Verify(x => x.Dispose(), Times.Once);
            this.clientMock.Verify(x => x.Login(), Times.Once);
            Assert.True(result.IsFailure);
        }

        private void SetupDirectoryHelper()
        {
            this.directoryMock.Setup(x => x.GetTotalFreeSpaceBytes(It.IsAny<string>()))
                .Returns(1024);
            this.directoryMock.Setup(x => x.DirSize(It.IsAny<string>()))
                .Returns(1024);
            this.directoryMock.Setup(x => x.DirExist(It.IsAny<string>()))
                .Returns(true);
        }

        private void SetupClientSuccessLogin()
        {
            this.SetupClientInitialize();
            this.clientMock.Setup(x => x.Login())
                .Returns(true);
        }

        private void SetupClientInitialize()
        {
            this.clientMock.Setup(x => x.InitializeClient());
        }

        private void SetupClientDispose()
        {
            this.clientMock.Setup(x => x.Dispose());
        }

        private VideoDownloaderService CreateHikDownloader()
        {
            this.clientFactoryMock.Setup(x => x.Create(It.IsAny<CameraConfig>(), It.IsAny<ILogger>()))
                .Returns(this.clientMock.Object);

            return new VideoDownloaderService(this.directoryMock.Object, this.clientFactoryMock.Object, loggerMock.Object);
        }
    }
}
