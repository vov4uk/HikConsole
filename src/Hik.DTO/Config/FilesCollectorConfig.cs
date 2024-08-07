﻿using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace Hik.DTO.Config
{
    public class FilesCollectorConfig : BaseConfig
    {
        [Display(Name = "Source folder")]
        public string SourceFolder { get; set; }

        [Display(Name = "Sent email if processed more than n files (if 0 not sent)")]
        public int AbnormalFilesCount { get; set; } = 0;

        [Display(Name = "File name pattern")]
        public string FileNamePattern { get; set; }

        [Display(Name = "File name dateTime format")]
        public string FileNameDateTimeFormat { get; set; }

        [Display(Name = "Skip last n files")]
        public int SkipLast { get; set; } = 0;

        [Display(Name = "Allowed file extentions")]
        public string AllowedFileExtentions { get; set; } = ".jpg;";
    }

    public class FilesConfigValidator : AbstractValidator<FilesCollectorConfig>
    {
        public FilesConfigValidator()
        {
            RuleFor(x => x.FileNamePattern).NotEmpty();
            RuleFor(x => x.FileNameDateTimeFormat).NotEmpty();
            RuleFor(x => x.DestinationFolder).NotNull();
            RuleFor(x => x.SourceFolder).NotNull();
            RuleFor(x => x.AllowedFileExtentions).NotEmpty();
        }
    }
}
