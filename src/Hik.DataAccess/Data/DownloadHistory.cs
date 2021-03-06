﻿using Hik.DataAccess.Abstractions;
using Hik.DataAccess.Metadata;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hik.DataAccess.Data
{
    [Table(Tables.DownloadHistory)]
    public class DownloadHistory : IAuditable, IHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int MediaFileId { get; set; }

        public int JobId { get; set; }

        public virtual HikJob Job { get; set; }

        public virtual MediaFile MediaFile { get; set; }
    }
}
