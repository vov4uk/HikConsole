using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Autofac;
using HikConsole.Abstraction;
using HikConsole.Config;
using HikConsole.DataAccess;
using HikConsole.DataAccess.Data;
using HikConsole.Infrastructure;
using HikConsole.Scheduler;

namespace HikConsole
{
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        public static void Main()
        {
            var container = AppBootstrapper.ConfigureIoc();
            AppConfig appConfig = container.Resolve<IHikConfig>().Config;
            ILogger logger = container.Resolve<ILogger>();
            logger.Info(appConfig.ToString());

            var job = new HikJob
            {
                Started = DateTime.Now,
                JobType = nameof(HikDownloader),
            };

            using (var unitOfWork = new UnitOfWorkFactory().CreateUnitOfWork(appConfig.ConnectionString))
            {
                var jobRepo = unitOfWork.GetRepository<HikJob>();
                jobRepo.Add(job).GetAwaiter().GetResult();
                unitOfWork.SaveChangesAsync().GetAwaiter().GetResult();
            }

            var downloader = container.Resolve<HikDownloader>(new TypedParameter(typeof(AppConfig), appConfig));
            job.Finished = DateTime.Now;
            var result = downloader.DownloadAsync().GetAwaiter().GetResult();
            var jobResultSaver = new JobResultsSaver(appConfig.ConnectionString, job, result, logger);
            jobResultSaver.SaveAsync().GetAwaiter().GetResult();

            if (appConfig.Mode == "Recurring")
            {
                logger.Info("Starting Recurring");
                var interval = appConfig.Interval * 60 * 1000;
                using (Timer timer = new Timer(async (o) => await downloader.DownloadAsync(), null, interval, interval))
                {
                    WaitForExit();
                    downloader?.Cancel();
                }
            }

            WaitForExit();
        }

        private static void WaitForExit()
        {
            Console.WriteLine("Press \'q\' to quit");
            while (Console.ReadKey() != new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false))
            {
                // do nothing
            }
        }
    }
}
