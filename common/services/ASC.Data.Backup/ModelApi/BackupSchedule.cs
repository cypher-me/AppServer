﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ASC.Api.Collections;
using ASC.Data.Backup.Contracts;

using static ASC.Data.Backup.BackupAjaxHandler;

namespace ASC.Data.Backup.ModelApi
{
    public class BackupSchedule
    {
        public string StorageType { get; set; }
        public IEnumerable<ItemKeyValuePair<object, object>> StorageParams { get; set; }
        public string BackupsStored { get; set; }
        public Cron CronParams { get; set; }
        public bool BackupMail { get; set; }
    }

    public class Cron
    {
        public string Period { get; set; }
        public string Hour { get; set; }
        public string Day { get; set; }
    }
}
