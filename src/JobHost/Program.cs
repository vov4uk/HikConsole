﻿using System;
using Job;
using Job.Email;
using NLog;

namespace JobHost
{
    static class Program
    {
        private static ILogger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            try
            {
                var parameters = Parameters.Parse(args);
                System.Diagnostics.Trace.CorrelationManager.ActivityId = parameters.ActivityId;
                logger.Info($"JobHost. Parameters resolved. {parameters}. Activity started execution.");

                Type jobType = Type.GetType(parameters.ClassName);

                if (jobType == null)
                {
                    throw new ArgumentException($"No such type exist '{parameters.ClassName}'");
                }

                Job.Impl.JobProcessBase job = (Job.Impl.JobProcessBase)Activator.CreateInstance(
                    jobType, 
                    $"{parameters.Group}.{parameters.TriggerKey}", 
                    parameters.ConfigFilePath, 
                    parameters.ConnectionString, 
                    parameters.ActivityId);
                job.ExecuteAsync().GetAwaiter().GetResult();
                logger.Info("JobHost. Activity completed execution.");
            }
            catch (Exception exception)
            {
                logger.Error($"JobHost. Exception : {exception}");
                EmailHelper.Send(exception);
                Environment.ExitCode = -1;
            }
        }
    }
}