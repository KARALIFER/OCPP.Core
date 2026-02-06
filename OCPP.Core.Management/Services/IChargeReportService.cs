using System;
using System.Collections.Generic;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Services
{
    public interface IChargeReportService
    {
        ChargeReportViewModel GenerateReport(DateTime? startDate, DateTime? stopDate, HashSet<string> permittedChargeTagIds, bool isAdmin);

        TransactionListViewModel GetTransactions(DateTime? startDate, DateTime? stopDate, HashSet<string> permittedChargeTagIds, HashSet<string> permittedChargePointIds, bool isAdmin);
    }
}
