﻿namespace Hik.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using AutoMapper;
    using Hik.Api.Abstraction;
    using Hik.Api.Data;
    using Hik.Client;
    using Hik.Client.Infrastructure;
    using Hik.DTO.Config;
    using Hik.DTO.Contracts;
    using Hik.Helpers.Abstraction;
    using Serilog;
    using Moq;
    using Xunit;

    public class HikVideoClientTests
    {
        private readonly Mock<IHikSDK> sdkMock;
        private readonly Mock<IHikApi> apiMock;
        private readonly Mock<IVideoService> videoServiceMock;
        private readonly Mock<IConfigService> configServiceMock;
        private readonly Mock<IFilesHelper> filesMock;
        private readonly Mock<IDirectoryHelper> dirMock;
        private readonly Mock<ILogger> loggerMock;
        private readonly Fixture fixture;
        private readonly IMapper mapper;

        public HikVideoClientTests()
        {
            this.videoServiceMock = new (MockBehavior.Strict);
            this.configServiceMock = new (MockBehavior.Strict);
            this.loggerMock = new ();
            this.sdkMock = new (MockBehavior.Strict);
            this.apiMock = new(MockBehavior.Strict);
            this.apiMock.SetupGet(x => x.VideoService)
                .Returns(this.videoServiceMock.Object);
            this.apiMock.SetupGet(x => x.ConfigService)
                .Returns(this.configServiceMock.Object);

            this.filesMock = new(MockBehavior.Strict);
            this.dirMock = new(MockBehavior.Strict);
            this.fixture = new();
            Action<IMapperConfigurationExpression> configureAutoMapper = x =>
            {
                x.AddProfile<HikConsoleProfile>();
            };

            var mapperConfig = new MapperConfiguration(configureAutoMapper);
            this.mapper = mapperConfig.CreateMapper();
        }

        [Fact]
        public void Constructor_PutEmptyConfig_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new HikVideoClient(null, this.sdkMock.Object, this.filesMock.Object, this.dirMock.Object, this.mapper, this.loggerMock.Object));
        }

        #region InitializeClient
        [Fact]
        public void InitializeClient_CallInitializeClient_ClientInitialized()
        {
            this.filesMock.Setup(x => x.CombinePath(It.IsAny<string[]>()))
                .Returns(string.Empty);
            this.dirMock.Setup(x => x.CreateDirIfNotExist(It.IsAny<string>()));
            this.sdkMock.Setup(x => x.Initialize(3, It.IsAny<string>(), true, 2000, 1, 10000, true));

            this.GetHikClient().InitializeClient();

            this.sdkMock.Verify(x => x.Initialize(3, It.IsAny<string>(), true, 2000, 1, 10000, true), Times.Once);
        }

        #endregion InitializeClient

        #region Login
        [Fact]
        public void Login_CallLogin_LoginSuccessfully()
        {
            this.SetupLoginAndHddStatusCheck();
            var client = this.GetHikClient();
            bool loginResult = client.Login();

            this.sdkMock.Verify(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            this.configServiceMock.Verify(x => x.GetHddStatus(It.IsAny<int>()), Times.Once);
            Assert.True(loginResult);
        }

        [Fact]
        public void Login_HardDriveStatusError_ThrowsException()
        {
            this.sdkMock.Setup(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(apiMock.Object);
            this.configServiceMock.Setup(x => x.GetHddStatus(It.IsAny<int>()))
                .Returns(new HdInfo { HdStatus = 2 });

            var client = this.GetHikClient();

            Assert.Throws<InvalidOperationException>(() => { client.Login(); });
            this.sdkMock.Verify(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            this.configServiceMock.Verify(x => x.GetHddStatus(It.IsAny<int>()), Times.Once());
        }

        [Fact]
        public void Login_HardDriveStatusNull_ReturnTrue()
        {
            this.sdkMock.Setup(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(apiMock.Object);
            this.configServiceMock.Setup(x => x.GetHddStatus(It.IsAny<int>()))
                .Returns(default(HdInfo));

            var clientLoggedIn = this.GetHikClient().Login();

            Assert.True(clientLoggedIn);
            this.sdkMock.Verify(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            this.configServiceMock.Verify(x => x.GetHddStatus(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void Login_CallLoginTwice_LoginOnce()
        {
            this.SetupLoginAndHddStatusCheck();

            var client = this.GetHikClient();
            var first = client.Login();
            var second = client.Login();

            Assert.True(first);
            Assert.False(second);
            this.sdkMock.Verify(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            this.configServiceMock.Verify(x => x.GetHddStatus(It.IsAny<int>()), Times.Once);
        }

        #endregion Login

        #region Dispose
        [Fact]
        public void Dispose_CallLogin_LogoutSuccess()
        {
            this.SetupLoginAndHddStatusCheck();

            this.apiMock.Setup(x => x.Logout());
            this.sdkMock.Setup(x => x.Cleanup());

            bool loginResult = false;
            using (var client = this.GetHikClient())
            {
                loginResult = client.Login();
            }

            Assert.True(loginResult);
            this.sdkMock.Verify(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            this.apiMock.Verify(x => x.Logout(), Times.Once);
            this.sdkMock.Verify(x => x.Cleanup(), Times.Once);
        }

        [Fact]
        public void Dispose_DoNotLogin_LogoutNotCall()
        {
            this.sdkMock.Setup(x => x.Cleanup()).Verifiable();
            using (var client = this.GetHikClient())
            {
                // Do nothing
            }

            this.sdkMock.Verify();
            this.apiMock.Verify(x => x.Logout(), Times.Never);
        }

        #endregion Dispose

        #region GetFilesListAsync
        [Fact]
        public async Task GetFilesListAsync_CallWithValidParameters_ReturnMappedFiles()
        {
            DateTime start = default(DateTime);
            DateTime end = start.AddSeconds(1);
            var remoteFile = fixture.Create<HikRemoteFile>();

            this.SetupLoginAndHddStatusCheck();
            this.videoServiceMock.Setup(x => x.FindFilesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<HikRemoteFile>() { remoteFile });

            var client = this.GetHikClient();
            client.Login();
            var mediaFiles = await client.GetFilesListAsync(start, end);

            this.videoServiceMock.Verify(x => x.FindFilesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            Assert.Single(mediaFiles);
            var firstFile = mediaFiles.First();
            Assert.Equal(remoteFile.Name, firstFile.Name);
            Assert.Equal(remoteFile.Date, firstFile.Date);
            Assert.Equal(remoteFile.Duration, firstFile.Duration);
            Assert.Equal(remoteFile.Size, firstFile.Size);
        }

        [Fact]
        public async Task GetFilesListAsync_CallWithInvalidParameters_ThrowsException()
        {
            var client = this.GetHikClient();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                var date = new DateTime(1970, 1, 1);
                await client.GetFilesListAsync(date, date);
            });
        }

        #endregion GetFilesListAsync

        #region DownloadFileAsync

        [Theory]
        [InlineData(1991, 05, 31, 60, "video", "C:\\1991-05\\31\\00\\19910531_000000_000100.mp4")]
        [InlineData(2020, 12, 31, 3600, "ch000000001", "C:\\2020-12\\31\\00\\20201231_000000_010000.mp4")]
        public async Task DownloadFileAsync_CallDownload_ProperFilesStored(int y, int m, int d, int duration, string name, string fileName)
        {
            var cameraConfig = new CameraConfig { ClientType = ClientType.HikVisionVideo, DestinationFolder = "C:\\", Camera = new DTO.Config.DeviceConfig() };

            int downloadHandler = 1;
            this.SetupLoginAndHddStatusCheck();
            MediaFileDto remoteFile = new MediaFileDto { Date = new DateTime(y, m, d), Duration = duration, Name = name };

            var tempName = fileName + ".tmp";
            var targetName = fileName;

            this.filesMock.Setup(x => x.CombinePath(It.IsAny<string[]>()))
                .Returns((string[] arg) => Path.Combine(arg));
            this.dirMock.Setup(x => x.CreateDirIfNotExist(It.IsAny<string>()));
            this.filesMock.Setup(x => x.FileSize(targetName))
                .Returns(1);
            this.filesMock.Setup(x => x.RenameFile(tempName + ".mp4", targetName));
            this.filesMock.Setup(x => x.FileExists(targetName))
                .Returns(false);
            this.filesMock.Setup(x => x.GetTempFileName())
                .Returns(tempName);

            this.videoServiceMock.Setup(x => x.StartDownloadFile(remoteFile.Name, tempName + ".mp4"))
                .Returns(downloadHandler);
            this.videoServiceMock.Setup(x => x.GetDownloadPosition(It.IsAny<int>()))
                .Returns(100);
            this.videoServiceMock.Setup(x => x.StopDownloadFile(It.IsAny<int>()));

            var client = new HikVideoClient(cameraConfig, this.sdkMock.Object, this.filesMock.Object, this.dirMock.Object, this.mapper, loggerMock.Object);
            client.Login();
            var isDownloaded = await client.DownloadFileAsync(remoteFile, CancellationToken.None);

            Assert.True(isDownloaded);
            this.filesMock.Verify(x => x.CombinePath(It.IsAny<string[]>()), Times.Exactly(2));
            this.dirMock.Verify(x => x.CreateDirIfNotExist(It.IsAny<string>()), Times.Once);
            this.filesMock.Verify(x => x.FileExists(targetName), Times.Once);
            this.filesMock.Verify(x => x.GetTempFileName(), Times.Once);
            this.filesMock.Verify(x => x.RenameFile(tempName + ".mp4", targetName), Times.Once);
            this.videoServiceMock.Verify(x => x.StartDownloadFile(remoteFile.Name, tempName + ".mp4"), Times.Once);
        }

        [Fact]
        public async Task DownloadFileAsync_FileAlreadyExist_ReturnFalse()
        {
            this.filesMock.Setup(x => x.CombinePath(It.IsAny<string[]>()))
                .Returns(string.Empty);
            this.dirMock.Setup(x => x.CreateDirIfNotExist(It.IsAny<string>()));
            this.filesMock.Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns(true);
            this.filesMock.Setup(x => x.GetTempFileName())
                .Returns(string.Empty);

            var client = this.GetHikClient();
            var isDownloaded = await client.DownloadFileAsync(this.fixture.Create<MediaFileDto>(), CancellationToken.None);

            Assert.False(isDownloaded);
        }

        [Fact]
        public async Task DownloadFileAsync_AbnormalProgress_StopDownloadFile()
        {
            int downloadHandler = 1;
            this.SetupLoginAndHddStatusCheck();

            this.SetupFilesMockForDownload();

            this.filesMock.Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns(false);
            this.filesMock.Setup(x => x.GetTempFileName())
                .Returns(string.Empty);
            this.videoServiceMock
                .Setup(x => x.StartDownloadFile(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(downloadHandler);
            this.videoServiceMock.Setup(x => x.StopDownloadFile(downloadHandler));
            this.videoServiceMock.Setup(x => x.GetDownloadPosition(It.IsAny<int>()))
                .Returns(200);
            this.apiMock.Setup(x => x.Logout());
            this.sdkMock.Setup(x => x.Cleanup());

            HikVideoClient client = null;
            using (client = this.GetHikClient())
            {
                client.Login();
                await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadFileAsync(this.fixture.Create<MediaFileDto>(), CancellationToken.None));
            }

            this.videoServiceMock.Verify(x => x.StopDownloadFile(downloadHandler), Times.Once);
            this.apiMock.Verify(x => x.Logout(), Times.Once);
            this.sdkMock.Verify(x => x.Cleanup(), Times.Once);
        }

        #endregion DownloadFileAsync

        #region ForceExit
        [Fact]
        public void ForceExit_FilesNotDownloading_DoNotDeleteFile()
        {
            this.SetupLoginAndHddStatusCheck();

            this.apiMock.Setup(x => x.Logout());
            this.sdkMock.Setup(x => x.Cleanup());

            var client = this.GetHikClient();
            client.Login();
            client.ForceExit();

            this.apiMock.Verify(x => x.Logout(), Times.Once);
            this.sdkMock.Verify(x => x.Cleanup(), Times.Once);
        }

        [Fact]
        public async Task ForceExit_FileIsDownloading_DoStop()
        {
            int downloadHandler = 1;
            var client = this.GetHikClient();
            this.SetupLoginAndHddStatusCheck();
            this.SetupFilesMockForDownload();

            this.videoServiceMock
                .Setup(x => x.StartDownloadFile(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(downloadHandler);
            this.videoServiceMock.Setup(x => x.StopDownloadFile(downloadHandler));
            this.videoServiceMock.Setup(x => x.GetDownloadPosition(It.IsAny<int>())).Callback(client.ForceExit)
                .Returns(10);
            this.apiMock.Setup(x => x.Logout());
            this.sdkMock.Setup(x => x.Cleanup());
            this.filesMock.Setup(x => x.GetTempFileName())
                .Returns(string.Empty);


            client.Login();
            var isDownloadingStarted = await client.DownloadFileAsync(this.fixture.Create<MediaFileDto>(), CancellationToken.None);
            
            this.apiMock.Verify(x => x.Logout(), Times.Once);
            this.sdkMock.Verify(x => x.Cleanup(), Times.Once);

            Assert.True(isDownloadingStarted);
            this.videoServiceMock.Verify(x => x.StopDownloadFile(downloadHandler), Times.Once);
        }

        #endregion ForceExit

        private void SetupLoginAndHddStatusCheck()
        {
            var status = new HdInfo { HdStatus = 0 };
            this.sdkMock.Setup(x => x.Login(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(apiMock.Object);
            this.configServiceMock.Setup(x => x.GetHddStatus(It.IsAny<int>()))
                .Returns(status);
        }

        private void SetupFilesMockForDownload()
        {
            this.filesMock.Setup(x => x.CombinePath(It.IsAny<string[]>()))
                .Returns(string.Empty);
            this.dirMock.Setup(x => x.CreateDirIfNotExist(It.IsAny<string>()));
            this.filesMock.Setup(x => x.FileSize(It.IsAny<string>()))
                .Returns(1);
            this.filesMock.Setup(x => x.RenameFile(It.IsAny<string>(), It.IsAny<string>()));
            this.filesMock.Setup(x => x.FileExists(It.IsAny<string>()))
                .Returns(false);
        }

        private HikVideoClient GetHikClient() =>
            new HikVideoClient(this.fixture.Create<CameraConfig>(), this.sdkMock.Object, this.filesMock.Object, this.dirMock.Object, this.mapper, loggerMock.Object);
     }
}
