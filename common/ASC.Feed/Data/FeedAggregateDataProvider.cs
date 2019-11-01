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
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ASC.Common.Data;
using ASC.Common.Data.Sql;
using ASC.Common.Data.Sql.Expressions;
using ASC.Core;
using ASC.Core.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace ASC.Feed.Data
{
    public class FeedAggregateDataProvider
    {
        public AuthContext AuthContext { get; }
        public TenantManager TenantManager { get; }
        public TenantUtil TenantUtil { get; }
        public DbOptionsManager DbOptionsManager { get; }

        public FeedAggregateDataProvider(DbOptionsManager dbOptionsManager)
        {
            DbOptionsManager = dbOptionsManager;
        }
        public FeedAggregateDataProvider(AuthContext authContext, TenantManager tenantManager, TenantUtil tenantUtil, DbOptionsManager dbOptionsManager)
        {
            AuthContext = authContext;
            TenantManager = tenantManager;
            TenantUtil = tenantUtil;
            DbOptionsManager = dbOptionsManager;
        }

        public DateTime GetLastTimeAggregate(string key)
        {
            var q = new SqlQuery("feed_last")
                .Select("last_date")
                .Where("last_key", key);

            var db = GetDb();
            var value = db.ExecuteScalar<DateTime>(q);
            return value != default ? value.AddSeconds(1) : value;
        }

        public void SaveFeeds(IEnumerable<FeedRow> feeds, string key, DateTime value)
        {
            var db = GetDb();
            db.ExecuteNonQuery(new SqlInsert("feed_last", true).InColumnValue("last_key", key).InColumnValue("last_date", value));

            const int feedsPortionSize = 1000;
            var aggregatedDate = DateTime.UtcNow;

            var feedsPortion = new List<FeedRow>();
            foreach (var feed in feeds)
            {
                feedsPortion.Add(feed);
                if (feedsPortion.Sum(f => f.Users.Count) <= feedsPortionSize) continue;

                SaveFeedsPortion(feedsPortion, aggregatedDate);
                feedsPortion.Clear();
            }
            if (feedsPortion.Any())
            {
                SaveFeedsPortion(feedsPortion, aggregatedDate);
            }
        }

        private void SaveFeedsPortion(IEnumerable<FeedRow> feeds, DateTime aggregatedDate)
        {
            var db = GetDb();
            using var tx = db.BeginTransaction();
            var i = new SqlInsert("feed_aggregate", true)
.InColumns("id", "tenant", "product", "module", "author", "modified_by", "group_id", "created_date",
"modified_date", "json", "keywords", "aggregated_date");
            var i2 = new SqlInsert("feed_users", true).InColumns("feed_id", "user_id");

            foreach (var f in feeds)
            {
                if (0 >= f.Users.Count) continue;

                i.Values(f.Id, f.Tenant, f.ProductId, f.ModuleId, f.AuthorId, f.ModifiedById, f.GroupId, f.CreatedDate, f.ModifiedDate, f.Json, f.Keywords, aggregatedDate);



                if (f.ClearRightsBeforeInsert)
                {
                    db.ExecuteNonQuery(
                        new SqlDelete("feed_users")
                            .Where("feed_id", f.Id)
                        );
                }

                foreach (var u in f.Users)
                {
                    i2.Values(f.Id, u.ToString());
                }
            }

            db.ExecuteNonQuery(i);
            db.ExecuteNonQuery(i2);

            tx.Commit();
        }

        public void RemoveFeedAggregate(DateTime fromTime)
        {
            var db = GetDb();
            using var command = db.Connection.CreateCommand();
            using var tx = db.Connection.BeginTransaction(IsolationLevel.ReadUncommitted);
            command.Transaction = tx;
            command.CommandTimeout = 60 * 60; // a hour
            var dialect = db.GetSqlDialect(Constants.FeedDbId);
            if (dialect.SupportMultiTableUpdate)
            {
                command.ExecuteNonQuery("delete from feed_aggregate, feed_users using feed_aggregate, feed_users where id = feed_id and aggregated_date < @date", new { date = fromTime });
            }
            else
            {
                command.ExecuteNonQuery(new SqlDelete("feed_users").Where(Exp.In("feed_id", new SqlQuery("feed_aggregate").Select("id").Where(Exp.Lt("aggregated_date", fromTime)))), dialect);
                command.ExecuteNonQuery(new SqlDelete("feed_aggregate").Where(Exp.Lt("aggregated_date", fromTime)), dialect);
            }
            tx.Commit();
        }

        public List<FeedResultItem> GetFeeds(FeedApiFilter filter)
        {
            var filterOffset = filter.Offset;
            var filterLimit = filter.Max > 0 && filter.Max < 1000 ? filter.Max : 1000;

            var feeds = new Dictionary<string, List<FeedResultItem>>();

            var tryCount = 0;
            List<FeedResultItem> feedsIteration;
            do
            {
                feedsIteration = GetFeedsInternal(filter);
                foreach (var feed in feedsIteration)
                {
                    if (feeds.ContainsKey(feed.GroupId))
                    {
                        feeds[feed.GroupId].Add(feed);
                    }
                    else
                    {
                        feeds[feed.GroupId] = new List<FeedResultItem> { feed };
                    }
                }
                filter.Offset += feedsIteration.Count;
            } while (feeds.Count < filterLimit
                     && feedsIteration.Count == filterLimit
                     && tryCount++ < 5);

            filter.Offset = filterOffset;
            return feeds.Take(filterLimit).SelectMany(group => group.Value).ToList();
        }

        private List<FeedResultItem> GetFeedsInternal(FeedApiFilter filter)
        {
            var query = new SqlQuery("feed_aggregate a")
                .InnerJoin("feed_users u", Exp.EqColumns("a.id", "u.feed_id"))
                .Select("a.json, a.module, a.author, a.modified_by, a.group_id, a.created_date, a.modified_date, a.aggregated_date")
                .Where("a.tenant", TenantManager.GetCurrentTenant().TenantId)
                .Where(
                    !Exp.Eq("a.modified_by", AuthContext.CurrentAccount.ID) &
                    Exp.Eq("u.user_id", AuthContext.CurrentAccount.ID)
                )
                .OrderBy("a.modified_date", false)
                .SetFirstResult(filter.Offset)
                .SetMaxResults(filter.Max);

            if (filter.OnlyNew)
            {
                query.Where(Exp.Ge("a.aggregated_date", filter.From));
            }
            else
            {
                if (1 < filter.From.Year)
                {
                    query.Where(Exp.Ge("a.modified_date", filter.From));
                }
                if (filter.To.Year < 9999)
                {
                    query.Where(Exp.Le("a.modified_date", filter.To));
                }
            }

            if (!string.IsNullOrEmpty(filter.Product))
            {
                query.Where("a.product", filter.Product);
            }
            if (filter.Author != Guid.Empty)
            {
                query.Where("a.modified_by", filter.Author);
            }
            if (filter.SearchKeys != null && filter.SearchKeys.Length > 0)
            {
                var exp = filter.SearchKeys
                                .Where(s => !string.IsNullOrEmpty(s))
                                .Select(s => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_"))
                                .Aggregate(Exp.False, (cur, s) => cur | Exp.Like("a.keywords", s, SqlLike.AnyWhere));
                query.Where(exp);
            }

            var db = GetDb();
            var news = db
                        .ExecuteList(query)
                        .ConvertAll(r => new FeedResultItem(
                        Convert.ToString(r[0]),
                        Convert.ToString(r[1]),
                        new Guid(Convert.ToString(r[2])),
                        new Guid(Convert.ToString(r[3])),
                        Convert.ToString(r[4]),
                        TenantUtil.DateTimeFromUtc(Convert.ToDateTime(r[5])),
                        TenantUtil.DateTimeFromUtc(Convert.ToDateTime(r[6])),
                        TenantUtil.DateTimeFromUtc(Convert.ToDateTime(r[7])),
                        TenantUtil));
            return news;
        }

        public int GetNewFeedsCount(DateTime lastReadedTime, AuthContext authContext, TenantManager tenantManager)
        {
            var q = new SqlQuery("feed_aggregate a")
                .Select("id")
                .Where("a.tenant", tenantManager.GetCurrentTenant().TenantId)
                .Where(!Exp.Eq("a.modified_by", authContext.CurrentAccount.ID))
                .InnerJoin("feed_users u", Exp.EqColumns("a.id", "u.feed_id"))
                .Where("u.user_id", authContext.CurrentAccount.ID)
                .SetMaxResults(1001);

            if (1 < lastReadedTime.Year)
            {
                q.Where(Exp.Ge("a.aggregated_date", lastReadedTime));
            }

            var db = GetDb();
            return db.ExecuteList(q).Count();
        }

        public IEnumerable<int> GetTenants(TimeInterval interval)
        {
            var db = GetDb();
            var q = new SqlQuery("feed_aggregate")
.Select("tenant")
.Where(Exp.Between("aggregated_date", interval.From, interval.To))
.GroupBy(1);
            return db.ExecuteList(q).ConvertAll(r => Convert.ToInt32(r[0]));
        }

        public FeedResultItem GetFeedItem(string id, TenantUtil tenantUtil)
        {
            var query = new SqlQuery("feed_aggregate a")
                .Select("a.json, a.module, a.author, a.modified_by, a.group_id, a.created_date, a.modified_date, a.aggregated_date")
                .Where("a.id", id);

            var db = GetDb();
            var news = db
                    .ExecuteList(query)
                    .ConvertAll(r => new FeedResultItem(
                    Convert.ToString(r[0]),
                    Convert.ToString(r[1]),
                    new Guid(Convert.ToString(r[2])),
                    new Guid(Convert.ToString(r[3])),
                    Convert.ToString(r[4]),
                    tenantUtil.DateTimeFromUtc(Convert.ToDateTime(r[5])),
                    tenantUtil.DateTimeFromUtc(Convert.ToDateTime(r[6])),
                    tenantUtil.DateTimeFromUtc(Convert.ToDateTime(r[7])),
                    tenantUtil));

            return news.FirstOrDefault();
        }

        public void RemoveFeedItem(string id)
        {
            var db = GetDb();
            using var command = db.Connection.CreateCommand();
            using var tx = db.Connection.BeginTransaction(IsolationLevel.ReadUncommitted);
            command.Transaction = tx;
            command.CommandTimeout = 60 * 60; // a hour

            var dialect = db.GetSqlDialect(Constants.FeedDbId);

            command.ExecuteNonQuery(new SqlDelete("feed_users").Where("feed_id", id), dialect);
            command.ExecuteNonQuery(new SqlDelete("feed_aggregate").Where("id", id), dialect);

            tx.Commit();
        }

        private IDbManager GetDb() => DbOptionsManager.Get(Constants.FeedDbId);
    }


    public class FeedResultItem
    {
        public FeedResultItem(
            string json,
            string module,
            Guid authorId,
            Guid modifiedById,
            string groupId,
            DateTime createdDate,
            DateTime modifiedDate,
            DateTime aggregatedDate,
            TenantUtil tenantUtil)
        {
            var now = tenantUtil.DateTimeFromUtc(DateTime.UtcNow);

            Json = json;
            Module = module;

            AuthorId = authorId;
            ModifiedById = modifiedById;

            GroupId = groupId;

            if (now.Year == createdDate.Year && now.Date == createdDate.Date)
            {
                IsToday = true;
            }
            else if (now.Year == createdDate.Year && now.Date == createdDate.Date.AddDays(1))
            {
                IsYesterday = true;
            }

            CreatedDate = createdDate;
            ModifiedDate = modifiedDate;
            AggregatedDate = aggregatedDate;
        }

        public string Json { get; private set; }

        public string Module { get; private set; }

        public Guid AuthorId { get; private set; }

        public Guid ModifiedById { get; private set; }

        public string GroupId { get; private set; }

        public bool IsToday { get; private set; }

        public bool IsYesterday { get; private set; }

        public DateTime CreatedDate { get; private set; }

        public DateTime ModifiedDate { get; private set; }

        public DateTime AggregatedDate { get; private set; }

        public FeedMin ToFeedMin(UserManager userManager)
        {
            var feedMin = JsonConvert.DeserializeObject<FeedMin>(Json);
            feedMin.Author = new FeedMinUser { UserInfo = userManager.GetUsers(feedMin.AuthorId) };
            feedMin.CreatedDate = CreatedDate;

            if (feedMin.Comments == null) return feedMin;

            foreach (var comment in feedMin.Comments)
            {
                comment.Author = new FeedMinUser { UserInfo = userManager.GetUsers(comment.AuthorId) };
            }
            return feedMin;
        }
    }

    public static class FeedAggregateDataProviderFactory
    {
        public static IServiceCollection AddFeedAggregateDataProvider(this IServiceCollection services)
        {
            return services
                .AddAuthContextService()
                .AddTenantManagerService()
                .AddTenantUtilService()
                .AddDbManagerService();
        }
    }
}