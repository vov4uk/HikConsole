﻿using System.Diagnostics.CodeAnalysis;

namespace Hik.DataAccess.Metadata
{
    [ExcludeFromCodeCoverage]
    public static class Tables
    {
        public const string Video = nameof(Video);
        public const string Photo = nameof(Photo);
        public const string File = nameof(File);
        public const string Job = nameof(Job);
        public const string JobTrigger = nameof(JobTrigger);
        public const string ExceptionLog = nameof(ExceptionLog);
        public const string DailyStatistics = nameof(DailyStatistics);
        public const string DeletedFiles = nameof(DeletedFiles);
    }
}
