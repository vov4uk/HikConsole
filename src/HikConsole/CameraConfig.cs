﻿using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace HikConsole
{
    [ExcludeFromCodeCoverage]
    public class CameraConfig
    {
        [JsonProperty("Allias")]
        public string Allias { get; set; }

        [JsonProperty("DestinationFolder")]
        public string DestinationFolder { get; set; }

        [JsonProperty("IPAddress")]
        public string IpAddress { get; set; }

        [JsonProperty("PortNumber")]
        public int PortNumber { get; set; } = 8000;

        [JsonProperty("UserName")]
        public string UserName { get; set; }

        [JsonProperty("Password")]
        public string Password { get; set; }

        public override string ToString()
        {
            return $@"Allias      : {this.Allias}
Destination : {this.DestinationFolder}
IP Address  : {this.IpAddress}:{this.PortNumber}
User name   : {this.UserName}";
        }
    }
}
