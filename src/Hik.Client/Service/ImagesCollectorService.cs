﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hik.Client.Abstraction.Services;
using Hik.Client.Helpers;
using Hik.DTO.Config;
using Hik.DTO.Contracts;
using Hik.Helpers.Abstraction;
using Newtonsoft.Json;
using Serilog;

namespace Hik.Client.Service
{
    public class ImagesCollectorService : RecurrentJobBase, IImagesCollectorService
    {
        private const string OutFileName = "yyyyMMdd_HHmmssfff";
        private static readonly string[] Extentions = { ".jpg" };
        private readonly IFilesHelper filesHelper;

        public ImagesCollectorService(
            IDirectoryHelper directoryHelper,
            IFilesHelper filesHelper,
            ILogger logger)
            : base(directoryHelper, logger)
        {
            this.filesHelper = filesHelper;
        }

        protected override Task<IReadOnlyCollection<MediaFileDto>> RunAsync(BaseConfig config, DateTime from, DateTime to)
        {
            var iConfig = config as ImagesCollectorConfig;

            if (iConfig == null)
            {
                throw new ArgumentException("Invalid config");
            }

            var result = new List<MediaFileDto>();
            var allFiles = this.directoryHelper.EnumerateFiles(iConfig.SourceFolder, Extentions);

            foreach (var filePath in allFiles)
            {
                string fileName = filesHelper.GetFileNameWithoutExtension(filePath);

                string[] imgParts = fileName.Split("_");

                DateTime dateTaken = DateTime.ParseExact(
                    imgParts[0],
                    "yyyyMMddHHmmssfff",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None);

                int elapsedMilliseconds = imgParts.Length > 1 ? int.Parse(imgParts[1]) : 0;
                string label = imgParts.Length > 2 ? imgParts[2] : null;

                string newFilePath = MoveFile(iConfig.DestinationFolder, filePath, dateTaken);

                var dto = new MediaFileDto
                {
                    Date = dateTaken,
                    Name = dateTaken.ToString(OutFileName),
                    Path = newFilePath,
                    DownloadDuration = elapsedMilliseconds,
                    Size = filesHelper.FileSize(newFilePath),
                    Objects = label,
                };
                result.Add(dto);

                logger.Debug($"{filePath} -> {JsonConvert.SerializeObject(dto)}");
            }

            return Task.FromResult(result.AsReadOnly() as IReadOnlyCollection<MediaFileDto>);
        }

        private string MoveFile(string destinationFolder, string oldFilePath, DateTime date)
        {
            string newFileName = date.ToString(OutFileName) + ".jpg";
            var workingDir = filesHelper.CombinePath(destinationFolder, date.ToDirectoryName());
            directoryHelper.CreateDirIfNotExist(workingDir);

            string newFilePath = filesHelper.CombinePath(workingDir, newFileName);
            this.filesHelper.RenameFile(oldFilePath, newFilePath);
            return newFilePath;
        }
    }
}