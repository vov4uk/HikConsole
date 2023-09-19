﻿using System.Collections;
using System.Globalization;
using System.Resources;
using System;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Hik.DataAccess.SQL
{
    public class MigrationTools
    {
        public static async Task RunMigration(string connectionString)
        {
            var db = new DataContext(new DbConfiguration { ConnectionString = connectionString });

            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

            await db.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS MigrationHistory (
     ScriptName         TEXT NOT NULL,
     ExecutionDate      DATETIME2 (0) NOT NULL,
     CONSTRAINT         PK_MigrationHistory PRIMARY KEY(ScriptName)
);");

            ResourceSet create = SQL.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
            foreach (DictionaryEntry entry in create)
            {
                var script = await db.MigrationHistory.FindAsync(entry.Key);
                if (script == null)
                {
                    await db.Database.ExecuteSqlRawAsync(Convert.ToString(entry.Value));
                    await db.MigrationHistory.AddAsync(new Data.MigrationHistory { ScriptName = Convert.ToString(entry.Key), ExecutionDate = DateTime.Now });
                }
            }
            await db.SaveChangesAsync();
        }
    }
}