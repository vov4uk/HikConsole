﻿using System.Collections;
using System.Globalization;
using System;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Hik.DataAccess.SQL
{
    [ExcludeFromCodeCoverage]
    public static class MigrationTools
    {
        public static async Task RunMigration(DbConfiguration connection)
        {
            var db = new DataContext(connection);
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            Log.Information("SQLite : Migration Started");

            await db.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS MigrationHistory (
     ScriptName         TEXT NOT NULL,
     ExecutionDate      DATETIME2 (0) NOT NULL,
     CONSTRAINT         PK_MigrationHistory PRIMARY KEY(ScriptName)
);");
            await db.SaveChangesAsync();

            var scripts = SQL.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true).OfType<DictionaryEntry>()
                .OrderBy(i => i.Key);
            foreach (DictionaryEntry entry in scripts)
            {
                var script = await db.MigrationHistory.FindAsync(entry.Key);
                if (script == null)
                {
                    await db.Database.ExecuteSqlRawAsync(Convert.ToString(entry.Value));
                    await db.MigrationHistory.AddAsync(new Data.MigrationHistory { ScriptName = Convert.ToString(entry.Key), ExecutionDate = DateTime.Now });
                    Log.Information($"SQLite : {entry.Key} executed");
                    await db.SaveChangesAsync();
                }
            }

            Log.Information("SQLite : Migration Finished");
        }
    }
}
