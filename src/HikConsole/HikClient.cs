﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HikConsole.Abstraction;
using HikConsole.Data;
using HikConsole.Helpers;
using C = HikConsole.Helpers.ConsoleHelper;

namespace HikConsole
{
    public class HikClient
    {
        private const int ProgressBarMaximum = 100;
        private const int ProgressBarMinimum = 0;
        private const string DateTimePrintFormat = "yyyy.MM.dd HH:mm:ss";
        private const string TimeFormat = "HHmmss";
        private readonly CameraConfig config;
        private readonly ISDKWrapper sdk;
        private readonly IFilesHelper filesHelper;
        private readonly IProgressBarFactory progressFactory;
        private int downloadHandle = -1;
        private FindResult downloadFile;
        private int userId = -1;
        private int channel = -1;
        private IProgressBar progress;

        public HikClient(CameraConfig config, ISDKWrapper sdk, IFilesHelper filesHelper, IProgressBarFactory progressFactory)
        {
            this.config = config;
            this.sdk = sdk;
            this.filesHelper = filesHelper;
            this.progressFactory = progressFactory;
        }

        public bool IsDownloading => this.downloadHandle >= 0;

        public void Init()
        {
            this.sdk.Initialize();
            this.sdk.SetupSDKLogs(3, this.filesHelper.CombinePath(this.config.DestinationFolder, "SdkLog"), false);

            this.filesHelper.FolderCreateIfNotExist(this.config.DestinationFolder);
        }

        public bool Login()
        {
            if (this.userId < 0)
            {
                DeviceInfo deviceInfo = null;
                this.userId = this.sdk.Login(this.config.IpAddress, this.config.PortNumber, this.config.UserName, this.config.Password, ref deviceInfo);
                this.channel = deviceInfo.StartChannel;

                return true;
            }

            C.WriteLine("Already logged in", ConsoleColor.Red);
            return false;
        }

        public bool StartDownload(FindResult file)
        {
            if (this.IsDownloading)
            {
                C.WriteLine("Downloading, please stop firstly!");
                return false;
            }

            string directory = this.GetWorkingDirectory(file);
            this.filesHelper.FolderCreateIfNotExist(directory);

            this.PrintFileInfo(file);
            string fileName = this.GetFullPath(file, directory);

            if (this.filesHelper.FileExists(fileName, file.FileSize))
            {
                C.WriteLine($"- exist ", ConsoleColor.DarkYellow);
                return false;
            }

            this.downloadHandle = this.sdk.StartDownloadFile(this.userId, file.FileName, fileName);
            this.downloadFile = file;
            this.progress = this.progressFactory?.Create();
            return true;
        }

        public void StopDownload()
        {
            if (this.IsDownloading)
            {
                this.sdk.StopDownoloadFile(this.downloadHandle);
                this.ResetDownloadStatus();
            }
        }

        public void CheckProgress()
        {
            if (this.IsDownloading)
            {
                int barValue = this.sdk.GetDownloadPos(this.downloadHandle);

                if (barValue > ProgressBarMinimum && barValue < ProgressBarMaximum)
                {
                    this.progress?.Report((double)barValue / 100);
                }
                else if (barValue == 100)
                {
                    this.StopDownload();
                    this.downloadFile = null;

                    C.WriteLine("- downloaded", ConsoleColor.Green);
                }
                else if (barValue == 200)
                {
                    C.WriteLine("The downloading is abnormal for the abnormal network!", ConsoleColor.DarkRed);

                    this.ForceExit();
                }
            }
        }

        public void Logout()
        {
            if (this.userId >= 0)
            {
                C.WriteLine($"Logout the device", timeStamp: DateTime.Now);
                this.sdk.Logout(this.userId);
                this.sdk.Cleanup();
                this.userId = -1;
            }
        }

        public void ForceExit()
        {
            C.WriteLine("\r\nForce exit", ConsoleColor.DarkRed);
            this.StopDownload();
            this.DeleteCurrentFile();
            this.Logout();
        }

        public async Task<IList<FindResult>> Find(DateTime periodStart, DateTime periodEnd)
        {
            this.ValidateDateParameters(periodStart, periodEnd);

            return await this.Find(periodStart, periodEnd, this.userId, this.channel);
        }

        private void PrintFileInfo(FindResult file)
        {
            C.Write($"{file.FileName}, {file.StartTime.ToString(DateTimePrintFormat)}, {file.StopTime.ToString(DateTimePrintFormat)}, {Utils.FormatBytes(file.FileSize)} ");
        }

        private string GetWorkingDirectory(FindResult file)
        {
            return this.filesHelper.CombinePath(this.config.DestinationFolder, $"{file.StartTime.Year:0000}-{file.StartTime.Month:00}-{file.StartTime.Day:00}");
        }

        private string GetFullPath(FindResult file, string directory = null)
        {
            string folder = directory ?? this.GetWorkingDirectory(file);
            return this.filesHelper.CombinePath(folder, $"{file.StartTime.ToString(TimeFormat)}_{file.StopTime.ToString(TimeFormat)}_{file.FileName}.mp4");
        }

        private void ResetDownloadStatus()
        {
            this.downloadHandle = -1;
            this.progress?.Dispose();
            this.progress = null;
        }

        private void DeleteCurrentFile()
        {
            if (this.downloadFile != null)
            {
                string path = this.GetFullPath(this.downloadFile);
                C.WriteLine($"Removing file {path}", ConsoleColor.DarkRed);
                this.filesHelper.DeleteFile(path);

                this.downloadFile = null;
            }
        }

        private void ValidateDateParameters(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                throw new ArgumentException("Start period grather than end");
            }
        }

        private async Task<IList<FindResult>> Find(DateTime periodStart, DateTime periodEnd, int userId, int channel)
        {
            return await this.sdk.Find(periodStart, periodEnd, userId, channel);
        }
    }
}