using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Services
{
    public class ChargeReportService : IChargeReportService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ChargeReportService> _logger;

        public ChargeReportService(OCPPCoreContext dbContext, ILogger<ChargeReportService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public ChargeReportViewModel GenerateReport(DateTime? startDate, DateTime? stopDate, HashSet<string> permittedChargeTagIds, HashSet<string> permittedChargePointIds, bool isAdmin)
        {
            LoggerTrace("GenerateReport", startDate, stopDate);
            var range = ResolveDateRange(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);

            bool hasAssignedChargeTag = permittedChargeTagIds == null || permittedChargeTagIds.Count > 0;
            if (!hasAssignedChargeTag)
            {
                return new ChargeReportViewModel
                {
                    StartDate = range.StartDate,
                    StopDate = range.StopDate,
                    HasAssignedChargeTag = false,
                    Groups = new List<GroupReport>()
                };
            }

            DateTime dbStartDate = range.StartDate.ToUniversalTime();
            DateTime dbStopDate = range.StopDate.AddDays(1).ToUniversalTime();

            var transactionQuery = _dbContext.Transactions.AsQueryable();
            if (!isAdmin && permittedChargePointIds != null && permittedChargePointIds.Count > 0)
            {
                transactionQuery = transactionQuery.Where(t => permittedChargePointIds.Contains(t.ChargePointId));
            }
            if (!isAdmin || (permittedChargeTagIds != null && permittedChargeTagIds.Count > 0))
            {
                transactionQuery = transactionQuery.Where(t =>
                    permittedChargeTagIds != null &&
                    (permittedChargeTagIds.Contains(t.StartTagId) || permittedChargeTagIds.Contains(t.StopTagId)));
            }

            var transactions = (from t in transactionQuery
                                join startCT in _dbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                                from startCT in ft_tmp.DefaultIfEmpty()
                                join stopCT in _dbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                                from stopCT in ft.DefaultIfEmpty()
                                where (t.StartTime >= dbStartDate &&
                                       t.StartTime <= dbStopDate &&
                                       (!t.StopTime.HasValue || t.StopTime < dbStopDate))
                                select new TransactionExtended
                                {
                                    TransactionId = t.TransactionId,
                                    Uid = t.Uid,
                                    ChargePointId = t.ChargePointId,
                                    ConnectorId = t.ConnectorId,
                                    StartTagId = t.StartTagId,
                                    StartTime = t.StartTime,
                                    MeterStart = t.MeterStart,
                                    StartResult = t.StartResult,
                                    StopTagId = t.StopTagId,
                                    StopTime = t.StopTime,
                                    MeterStop = t.MeterStop,
                                    StopReason = t.StopReason,
                                    StartTagName = startCT.TagName,
                                    StartTagParentId = startCT.ParentTagId,
                                    StopTagName = stopCT.TagName,
                                    StopTagParentId = stopCT.ParentTagId
                                }).AsNoTracking()
                                  .ToList();

            return new ChargeReportViewModel
            {
                StartDate = range.StartDate,
                StopDate = range.StopDate,
                HasAssignedChargeTag = true,
                Groups = transactions
                    .GroupBy(t => t.StartTagParentId)
                    .OrderBy(g => g.Key)
                    .Select(g => new GroupReport
                    {
                        GroupName = g.Key,
                        Tags = g.GroupBy(t => (string.IsNullOrEmpty(t.StartTagName) ? t.StartTagId : t.StartTagName))
                                .OrderBy(tg => tg.Key)
                                .Select(tg => new TagReport
                                {
                                    TagName = tg.Key,
                                    Transactions = tg.Select(t => new TransactionReport
                                    {
                                        TransactionId = t.TransactionId,
                                        ChargePointId = t.ChargePointId,
                                        ConnectorId = t.ConnectorId,
                                        StartTagId = string.IsNullOrEmpty(t.StartTagName) ? t.StartTagId : t.StartTagName,
                                        StartTime = t.StartTime,
                                        MeterStart = t.MeterStart,
                                        StartResult = t.StartResult,
                                        StopTagId = string.IsNullOrEmpty(t.StopTagName) ? t.StopTagId : t.StopTagName,
                                        StopTime = t.StopTime,
                                        MeterStop = t.MeterStop,
                                        StopReason = t.StopReason
                                    }).ToList()
                                }).ToList()
                    }).ToList()
            };
        }

        public TransactionListViewModel GetTransactions(DateTime? startDate, DateTime? stopDate, HashSet<string> permittedChargeTagIds, HashSet<string> permittedChargePointIds, bool isAdmin)
        {
            var range = ResolveDateRange(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);
            DateTime dbStartDate = range.StartDate.ToUniversalTime();
            DateTime dbStopDate = range.StopDate.AddDays(1).ToUniversalTime();

            var tlvm = new TransactionListViewModel
            {
                ConnectorStatuses = new List<ConnectorStatus>(),
                Transactions = new List<TransactionExtended>()
            };

            _logger.LogTrace("ChargeReport: Loading charge points and connectors...");
            tlvm.ConnectorStatuses = _dbContext.ConnectorStatuses.Include(cs => cs.ChargePoint).ToList();
            if (permittedChargePointIds != null && permittedChargePointIds.Count > 0 && !isAdmin)
            {
                tlvm.ConnectorStatuses = tlvm.ConnectorStatuses
                    .Where(connector => permittedChargePointIds.Contains(connector.ChargePointId))
                    .ToList();
            }

            var transactionQuery = _dbContext.Transactions.AsQueryable();
            if (permittedChargePointIds != null && permittedChargePointIds.Count > 0 && !isAdmin)
            {
                transactionQuery = transactionQuery.Where(t => permittedChargePointIds.Contains(t.ChargePointId));
            }

            if (!isAdmin || (permittedChargeTagIds != null && permittedChargeTagIds.Count > 0))
            {
                transactionQuery = transactionQuery.Where(t =>
                    permittedChargeTagIds != null &&
                    (permittedChargeTagIds.Contains(t.StartTagId) || permittedChargeTagIds.Contains(t.StopTagId)));
            }

            _logger.LogTrace("ChargeReport: Loading transactions...");
            tlvm.Transactions = (from t in transactionQuery
                                 join startCT in _dbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                                 from startCT in ft_tmp.DefaultIfEmpty()
                                 join stopCT in _dbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                                 from stopCT in ft.DefaultIfEmpty()
                                 where (t.StartTime >= dbStartDate &&
                                        t.StartTime <= dbStopDate &&
                                        (!t.StopTime.HasValue || t.StopTime < dbStopDate))
                                 select new TransactionExtended
                                 {
                                     TransactionId = t.TransactionId,
                                     Uid = t.Uid,
                                     ChargePointId = t.ChargePointId,
                                     ConnectorId = t.ConnectorId,
                                     StartTagId = t.StartTagId,
                                     StartTime = t.StartTime,
                                     MeterStart = t.MeterStart,
                                     StartResult = t.StartResult,
                                     StopTagId = t.StopTagId,
                                     StopTime = t.StopTime,
                                     MeterStop = t.MeterStop,
                                     StopReason = t.StopReason,
                                     StartTagName = startCT.TagName,
                                     StartTagParentId = startCT.ParentTagId,
                                     StopTagName = stopCT.TagName,
                                     StopTagParentId = stopCT.ParentTagId
                                 }).AsNoTracking()
                                 .ToList();

            return tlvm;
        }

        private (DateTime StartDate, DateTime StopDate) ResolveDateRange(DateTime? startDate, DateTime? stopDate, HashSet<string> permittedChargeTagIds, HashSet<string> permittedChargePointIds, bool isAdmin)
        {
            if (!startDate.HasValue || !stopDate.HasValue)
            {
                if (!isAdmin && permittedChargeTagIds != null && permittedChargeTagIds.Count > 0)
                {
                    var tagScopedQuery = _dbContext.Transactions
                        .Where(t => permittedChargeTagIds.Contains(t.StartTagId) || permittedChargeTagIds.Contains(t.StopTagId));
                    if (permittedChargePointIds != null && permittedChargePointIds.Count > 0)
                    {
                        tagScopedQuery = tagScopedQuery.Where(t => permittedChargePointIds.Contains(t.ChargePointId));
                    }

                    DateTime? minStart = tagScopedQuery.Min(t => (DateTime?)t.StartTime);
                    DateTime? maxEnd = tagScopedQuery.Max(t => (DateTime?)(t.StopTime ?? t.StartTime));

                    if (minStart.HasValue && maxEnd.HasValue)
                    {
                        startDate = minStart.Value.Date;
                        stopDate = maxEnd.Value.Date;
                    }
                }
            }

            startDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
            stopDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1);

            return (startDate.Value.Date, stopDate.Value.Date);
        }

        private void LoggerTrace(string action, DateTime? startDate, DateTime? stopDate)
        {
            _logger.LogTrace("ChargeReportService: {Action}({Start}, {Stop})", action, startDate?.ToString("s"), stopDate?.ToString("s"));
        }
    }
}
