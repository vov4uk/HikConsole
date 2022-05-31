﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hik.Client.Abstraction;
using Hik.Client.Events;
using Hik.Client.Helpers;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Hik.DTO.Message;
using Hik.Helpers;
using Hik.Helpers.Abstraction;

namespace Hik.Client.Service
{
    public class ArchiveService : RecurrentJobBase, IArchiveService
    {
        private readonly IFilesHelper filesHelper;
        private readonly IVideoHelper videoHelper;
        private readonly IImageHelper imageHelper;
        private Regex regex;

        public ArchiveService(
            IDirectoryHelper directoryHelper,
            IFilesHelper filesHelper,
            IVideoHelper videoHelper,
            IImageHelper imageHelper)
            : base(directoryHelper)
        {
            this.filesHelper = filesHelper;
            this.videoHelper = videoHelper;
            this.imageHelper = imageHelper;
        }

        protected override async Task<IReadOnlyCollection<MediaFileDto>> RunAsync(BaseConfig config, DateTime from, DateTime to)
        {
            var aConfig = config as ArchiveConfig;
            PrepareRegex(aConfig.FileNamePattern);

            var result = new List<MediaFileDto>();
            var allFiles = this.directoryHelper.EnumerateFiles(aConfig.SourceFolder, aConfig.AllowedFileExtentions).SkipLast(aConfig.SkipLast);

            try
            {
                if (aConfig.DetectPeopleConfig != null && aConfig.DetectPeopleConfig.DetectPeoples)
                {
                    var detectConfig = aConfig.DetectPeopleConfig;
                    var mqConfig = detectConfig.RabbitMQConfig;
                    if (mqConfig != null)
                    {
                        using var rabbitMq = new RabbitMQSender(mqConfig.HostName, mqConfig.QueueName, mqConfig.RoutingKey);
                        foreach (var oldFile in allFiles)
                        {
                            DateTime date = GetCreationDate(aConfig.FileNameDateTimeFormat, oldFile);
                            var duration = -1;
                            string fileExt = filesHelper.GetExtension(oldFile);
                            string newFileName = date.ToArchiveFileString(duration, fileExt);
                            string newFilePath = GetPathSafety(newFileName, GetWorkingDirectory(aConfig.DestinationFolder, date));
                            string junkFilePath = GetPathSafety(newFileName, GetWorkingDirectory(detectConfig.JunkFolder, date));
                            string guid = Guid.NewGuid().ToString();
                            var msg = new DetectPeopleMessage()
                            {
                                UniqueId = guid,
                                OldFilePath = oldFile,
                                NewFilePath = newFilePath,
                                NewFileName = newFileName,
                                JunkFilePath = junkFilePath,
                                DeleteJunk = detectConfig.DeletePhotosWithoutPeoples,
                            };

                            rabbitMq.Sent(msg.ToString());

                            result.Add(new MediaFileDto
                            {
                                Date = date,
                                Name = guid,
                                Path = string.Empty,
                                Duration = duration,
                                Size = filesHelper.FileSize(oldFile),
                            });
                        }
                    }
                }
                else
                {
                    foreach (var oldFile in allFiles)
                    {
                        DateTime date = GetCreationDate(aConfig.FileNameDateTimeFormat, oldFile);
                        int duration = await this.videoHelper.GetDuration(oldFile);

                        string fileExt = filesHelper.GetExtension(oldFile);

                        string newFileName = date.ToArchiveFileString(duration, fileExt);
                        string newFilePath = MoveFile(aConfig.DestinationFolder, oldFile, date, newFileName);

                        result.Add(new MediaFileDto
                        {
                            Date = date,
                            Name = filesHelper.GetFileName(oldFile),
                            Path = newFilePath,
                            Duration = duration,
                            Size = filesHelper.FileSize(newFilePath),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                this.OnExceptionFired(new ExceptionEventArgs(ex), aConfig);
            }

            return result.AsReadOnly();
        }

        private string MoveFile(string destinationFolder, string oldFilePath, DateTime date, string newFileName)
        {
            string newFilePath = GetPathSafety(newFileName, GetWorkingDirectory(destinationFolder, date));
            string fileExt = filesHelper.GetExtension(oldFilePath);
            if (fileExt == ".jpg")
            {
                this.imageHelper.SetDate(oldFilePath, newFilePath, date);
                this.filesHelper.DeleteFile(oldFilePath);
            }
            else
            {
                this.filesHelper.RenameFile(oldFilePath, newFilePath);
            }

            return newFilePath;
        }

        private DateTime GetCreationDate(string dateTimeFormat, string oldFile)
        {
            string fileName = filesHelper.GetFileNameWithoutExtension(oldFile);
            List<string> nameParts = ReverseStringFormat(fileName);
            DateTime date = default;
            bool nameParsed = false;
            if (nameParts != null && nameParts.Any())
            {
                foreach (var name in nameParts)
                {
                    if (DateTime.TryParseExact(
                    name,
                    dateTimeFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                    {
                        nameParsed = true;
                        break;
                    }
                }
            }

            if (!nameParsed || date.Year == 1970 || date.Year == 1)
            {
                date = filesHelper.GetCreationDate(oldFile);
            }

            return date;
        }

        private string GetWorkingDirectory(string destinationFolder, DateTime date)
        {
            return filesHelper.CombinePath(destinationFolder, date.ToPhotoDirectoryNameString());
        }

        private string GetPathSafety(string file, string directory)
        {
            directoryHelper.CreateDirIfNotExist(directory);
            return filesHelper.CombinePath(directory, file);
        }

        private List<string> ReverseStringFormat(string str)
        {
            return this.regex.Match(str).Groups.Select(x => x.Value).ToList();
        }

        private void PrepareRegex(string template)
        {
            // Handels regex special characters.
            template = Regex.Replace(template, @"[\\\^\$\.\|\?\*\+\(\)]", m => @"\" + m.Value);
            string pattern = "^" + Regex.Replace(template, @"\{[0-9]+\}", "(.*?)") + "$";
            this.regex = new Regex(pattern);
        }
    }
}
