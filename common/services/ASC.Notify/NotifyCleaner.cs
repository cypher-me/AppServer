/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.Threading;
using System.Threading.Tasks;
using ASC.Common.Data;
using ASC.Common.Logging;
using ASC.Notify.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ASC.Notify
{
    public class NotifyCleaner
    {
        private readonly ILog log;
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        public NotifyServiceCfg NotifyServiceCfg { get; }
        public IServiceProvider ServiceProvider { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public NotifyCleaner(IOptions<NotifyServiceCfg> notifyServiceCfg, IServiceProvider serviceProvider, IOptionsMonitor<LogNLog> options)
        {
            log = options.Get("ASC.Notify");
            NotifyServiceCfg = notifyServiceCfg.Value;
            ServiceProvider = serviceProvider;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            var t = new Task(Clear, CancellationTokenSource.Token, TaskCreationOptions.LongRunning);
            t.Start(TaskScheduler.Default);
        }

        public void Stop()
        {
            stop.Set();
            CancellationTokenSource.Cancel();
        }


        private void Clear()
        {
            while (true)
            {
                try
                {
                    var date = DateTime.UtcNow.AddDays(-NotifyServiceCfg.StoreMessagesDays);
                    using var scope = ServiceProvider.CreateScope();
                    using var db = scope.ServiceProvider.GetService<DbOptionsManager>().Get(NotifyServiceCfg.ConnectionStringName);
                    using var d1 = db.Connection.CreateCommand("delete from notify_info where modify_date < ? and state = 4", date);
                    using var d2 = db.Connection.CreateCommand("delete from notify_queue where creation_date < ?", date);
                    d1.CommandTimeout = 60 * 60; // hour
                    d2.CommandTimeout = 60 * 60; // hour

                    var affected1 = d1.ExecuteNonQuery();
                    var affected2 = d2.ExecuteNonQuery();

                    log.InfoFormat("Clear notify messages: notify_info({0}), notify_queue ({1})", affected1, affected2);
                }
                catch (ThreadAbortException)
                {
                    // ignore
                }
                catch (Exception err)
                {
                    log.Error(err);
                }
                if (stop.WaitOne(TimeSpan.FromHours(8)))
                {
                    break;
                }
            }
        }
    }

    public static class NotifyCleanerFactory
    {
        public static IServiceCollection AddNotifyCleaner(this IServiceCollection services)
        {
            services.TryAddSingleton<NotifyCleaner>();

            return services;
        }
    }
}
