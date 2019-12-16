﻿using HikConsole.DataAccess.Data;
using Microsoft.EntityFrameworkCore;

namespace HikConsole.DataAccess
{
    public class DataContext : DbContext
    {
        private readonly string ConnectionString;
        public DataContext(DbContextOptions<DataContext> options)
            : base(options) { }

        public DataContext(string connection)
            : base()
        {
            ConnectionString = connection;
        }

        public DbSet<Job> Jobs { get; set; }
        public DbSet<Camera> Cameras { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<HardDriveStatus> HDStatus { get; set; }

        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer(ConnectionString);
        }
    }
}