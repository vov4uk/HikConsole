﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Hik.Api;
using Hik.Api.Abstraction;
using Hik.Api.Data;
using Hik.Client.Abstraction;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Hik.Helpers.Abstraction;
using Serilog;

namespace Hik.Client
{
    public abstract class HikBaseClient : IDownloaderClient
    {
        protected const int ProgressBarMaximum = 100;
        protected const int ProgressBarMinimum = 0;

        private bool disposedValue = false;
        protected readonly CameraConfig config;
        protected readonly IDirectoryHelper dirHelper;
        protected int downloadId = -1;
        protected readonly IFilesHelper filesHelper;
        protected readonly IHikApi hikApi;
        protected readonly ILogger logger;
        protected Session session;

        protected HikBaseClient(
            CameraConfig config,
            IHikApi hikApi,
            IFilesHelper filesHelper,
            IDirectoryHelper directoryHelper,
            IMapper mapper,
            ILogger logger)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.hikApi = hikApi;
            this.filesHelper = filesHelper;
            this.dirHelper = directoryHelper;
            this.Mapper = mapper;
            this.logger = logger;
        }

        private string GetWorkingDirectory(MediaFileDto file)
        {
            return filesHelper.CombinePath(config.DestinationFolder, ToDirectoryNameString(file));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                logger.Information("Logout the device");
                if (session != null)
                {
                    hikApi.Logout(session.UserId);
                }

                session = null;

                hikApi.Cleanup();
                disposedValue = true;
            }
        }

        protected abstract Task<bool> DownloadFileInternalAsync(MediaFileDto remoteFile, CancellationToken token);

        protected string GetPathSafety(MediaFileDto remoteFile)
        {
            string workingDirectory = GetWorkingDirectory(remoteFile);
            dirHelper.CreateDirIfNotExist(workingDirectory);

            return filesHelper.CombinePath(workingDirectory, ToFileNameString(remoteFile));
        }

        protected void ResetDownloadStatus()
        {
            downloadId = -1;
        }

        protected abstract void StopDownload();

        protected abstract string ToDirectoryNameString(MediaFileDto file);

        protected abstract string ToFileNameString(MediaFileDto file);

        protected void ValidateDateParameters(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                throw new ArgumentException("Start period grater than end");
            }
        }

        protected IMapper Mapper { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task<bool> DownloadFileAsync(MediaFileDto remoteFile, CancellationToken token)
        {
            try
            {
                return await DownloadFileInternalAsync(remoteFile, token);
            }
            catch (HikException e)
            {
                var msg = $"Failed to download {remoteFile.Name} : {remoteFile.Path}. Code : {e.ErrorCode}; {e.ErrorMessage}";
                this.logger.Error("ErrorMsg: {errorMsg}; Trace: {trace}", msg, e.ToStringDemystified());
            }
            catch (Exception e)
            {
                this.logger.Error("ErrorMsg: {errorMsg}; Trace: {trace}", $"Failed to download {remoteFile.Name} : {remoteFile.Path}", e.ToStringDemystified());
            }

            return false;
        }

        public void ForceExit()
        {
            logger.Warning("Force Exit");
            StopDownload();
            Dispose(true);
        }

        public abstract Task<IReadOnlyCollection<MediaFileDto>> GetFilesListAsync(DateTime periodStart, DateTime periodEnd);

        public void InitializeClient()
        {
            string sdkLogsPath = filesHelper.CombinePath(Environment.CurrentDirectory, "logs", config.Alias + "_SdkLog");
            dirHelper.CreateDirIfNotExist(sdkLogsPath);
            dirHelper.CreateDirIfNotExist(config.DestinationFolder);

            logger.Information("SDK Logs : {sdkLogsPath}", sdkLogsPath);
            hikApi.Initialize();
            hikApi.SetupLogs(3, sdkLogsPath, false);
            hikApi.SetConnectTime(3000, 3);
            hikApi.SetReconnect(10000, 1);
        }

        public bool Login()
        {
            if (session == null)
            {
                session = hikApi.Login(config.Camera.IpAddress, config.Camera.PortNumber, config.Camera.UserName, config.Camera.Password);
                logger.Information("Sucessfull login to {IpAdress}", config.Camera.IpAddress);
                var status = hikApi.GetHddStatus(session.UserId);

                logger.Information(status?.ToString());

                if (status is { IsErrorStatus: true })
                {
                    throw new InvalidOperationException("HD error");
                }

                return true;
            }
            else
            {
                logger.Warning("Already logged in");
                return false;
            }
        }

        public void SyncTime()
        {
            if (config.SyncTime)
            {
                var cameraTime = hikApi.GetTime(session.UserId);
                logger.Information("Camera time :{cameraTime}", cameraTime);
                var currentTime = DateTime.Now;
                if (Math.Abs((currentTime - cameraTime).TotalSeconds) > config.SyncTimeDeltaSeconds)
                {
                    hikApi.SetTime(currentTime, session.UserId);
                    logger.Warning("Camera time updated :{currentTime}", currentTime);
                }
            }
        }
    }
}
