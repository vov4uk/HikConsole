﻿using System;
using System.Collections.Generic;
using Hik.DTO.Contracts;

namespace Hik.Client.FileProviders
{
    public interface IFileProvider
    {
        void Initialize(string[] directories);

        IReadOnlyCollection<MediaFileDTO> GetNextBatch(string fileExtention, int batchSize = 100);

        IReadOnlyCollection<MediaFileDTO> GetFilesOlderThan(string fileExtention, DateTime date);
    }
}
