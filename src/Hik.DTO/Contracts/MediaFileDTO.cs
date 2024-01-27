﻿using System;
using System.ComponentModel.DataAnnotations;

namespace Hik.DTO.Contracts
{
    public class MediaFileDto
    {
        private static readonly DateTime StartOfAges = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public int Id { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public string Objects { get; set; }

        public int? EventId { get; set; }

        public long Size { get; set; }

        [Display(Name = "Started"), DisplayFormat(DataFormatString = Consts.DisplayDateTimeFormat), DataType(DataType.DateTime)]
        public DateTime? DownloadStarted { get; set; }

        [Display(Name = "Downloaded")]
        public int? DownloadDuration { get; set; }

        [Display(Name = "Date"), DisplayFormat(DataFormatString = Consts.DisplayDateTimeFormat), DataType(DataType.DateTime)]
        public DateTime Date { get; set; }

        public int? Duration { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public static DateTime? EventDate(int? eventId)
        {
            if (eventId.HasValue)
            {
                int days = eventId.Value / 100000;
                int seconds = eventId.Value - days * 100000;
                var date = StartOfAges.AddDays(days).AddSeconds(seconds);
                return date;
            }
            return null;
        }
    }
}
