﻿using System.Diagnostics.CodeAnalysis;

namespace Hik.Helpers.Email
{
    [ExcludeFromCodeCoverage]
    public class EmailConfig
    {
        public string UserName { get; set; }

        public string Password { get; set; }

        public string Server { get; set; }

        public int Port { get; set; }

        public string Receiver { get; set; }
    }
}
