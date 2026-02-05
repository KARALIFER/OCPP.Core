/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        private const char UserExportCsvSeparator = ';';

        [Authorize]
        public IActionResult MyTransactions()
        {
            try
            {
                int? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                MyTransactionsViewModel viewModel = LoadMyTransactionsViewModel();
                return View("MyTransactions", viewModel);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MyTransactions: Error loading transactions");
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        public IActionResult MyTransactionsExport()
        {
            try
            {
                MyTransactionsViewModel viewModel = LoadMyTransactionsViewModel();
                var workbook = CreateMyTransactionsSpreadsheet(viewModel.Transactions);

                using var memoryStream = new MemoryStream();
                IEnumerable<string> lines = workbook.Worksheet(1).RowsUsed().Select(row =>
                    string.Join(UserExportCsvSeparator, row.Cells(1, row.LastCellUsed(XLCellsUsedOptions.AllContents).Address.ColumnNumber)
                        .Select(cell => EscapeCsvValue(cell.GetValue<string>(), UserExportCsvSeparator))));

                using (var writer = new StreamWriter(memoryStream, Encoding.GetEncoding("ISO-8859-1"), 4096, true))
                {
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }

                memoryStream.Position = 0;
                return File(memoryStream.ToArray(), "text/csv", "MyTransactions.csv");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MyTransactionsExport: Error exporting CSV");
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        public IActionResult MyTransactionsExportXlsx()
        {
            try
            {
                MyTransactionsViewModel viewModel = LoadMyTransactionsViewModel();
                var workbook = CreateMyTransactionsSpreadsheet(viewModel.Transactions);

                using var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);
                memoryStream.Position = 0;

                return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MyTransactions.xlsx");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MyTransactionsExportXlsx: Error exporting XLSX");
                return StatusCode(500, "Internal server error");
            }
        }

        private MyTransactionsViewModel LoadMyTransactionsViewModel()
        {
            HashSet<string> permittedChargeTagIds = GetPermittedChargeTagIds() ?? new HashSet<string>();

            string selectedChargePointId = Request.Query["chargePointId"].ToString();
            string selectedStatus = Request.Query["status"].ToString();
            string fromValue = Request.Query["from"].ToString();
            string toValue = Request.Query["to"].ToString();

            DateTime? dateFrom = DateTime.TryParse(fromValue, out DateTime parsedFrom) ? parsedFrom.Date : null;
            DateTime? dateTo = DateTime.TryParse(toValue, out DateTime parsedTo) ? parsedTo.Date : null;

            IQueryable<Transaction> baseQuery = DbContext.Transactions.AsNoTracking();
            if (permittedChargeTagIds.Count > 0)
            {
                baseQuery = baseQuery.Where(t =>
                    permittedChargeTagIds.Contains(t.StartTagId) ||
                    permittedChargeTagIds.Contains(t.StopTagId));
            }
            else
            {
                baseQuery = baseQuery.Where(t => false);
            }

            List<string> permittedChargePointIds = baseQuery
                .Select(t => t.ChargePointId)
                .Distinct()
                .ToList();

            IQueryable<Transaction> filteredQuery = baseQuery;
            if (!string.IsNullOrWhiteSpace(selectedChargePointId))
            {
                filteredQuery = filteredQuery.Where(t => t.ChargePointId == selectedChargePointId);
            }

            if (dateFrom.HasValue)
            {
                filteredQuery = filteredQuery.Where(t => t.StartTime >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                DateTime endExclusive = dateTo.Value.AddDays(1);
                filteredQuery = filteredQuery.Where(t => t.StartTime < endExclusive);
            }

            if (string.Equals(selectedStatus, "active", StringComparison.InvariantCultureIgnoreCase))
            {
                filteredQuery = filteredQuery.Where(t => t.StopTime == null);
            }
            else if (string.Equals(selectedStatus, "completed", StringComparison.InvariantCultureIgnoreCase))
            {
                filteredQuery = filteredQuery.Where(t => t.StopTime != null);
            }

            List<TransactionExtended> transactions = (from t in filteredQuery
                                                      join startCT in DbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                                                      from startCT in ft_tmp.DefaultIfEmpty()
                                                      join stopCT in DbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                                                      from stopCT in ft.DefaultIfEmpty()
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
                                                      })
                                                      .OrderByDescending(t => t.TransactionId)
                                                      .ToList();

            List<ChargePoint> chargePoints = DbContext.ChargePoints
                .Where(cp => permittedChargePointIds.Contains(cp.ChargePointId))
                .OrderBy(cp => cp.Name)
                .ToList();

            return new MyTransactionsViewModel
            {
                Transactions = transactions,
                ChargePoints = chargePoints,
                SelectedChargePointId = selectedChargePointId,
                SelectedStatus = string.IsNullOrWhiteSpace(selectedStatus) ? "all" : selectedStatus,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
        }

        private XLWorkbook CreateMyTransactionsSpreadsheet(IEnumerable<TransactionExtended> transactions)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("MyTransactions");

            worksheet.Cell(1, 1).Value = _localizer["ChargePointLabel"].ToString();
            worksheet.Cell(1, 2).Value = _localizer["ConnectorLabel"].ToString();
            worksheet.Cell(1, 3).Value = _localizer["StartTime"].ToString();
            worksheet.Cell(1, 4).Value = _localizer["StopTime"].ToString();
            worksheet.Cell(1, 5).Value = _localizer["StartTag"].ToString();
            worksheet.Cell(1, 6).Value = _localizer["StopTag"].ToString();
            worksheet.Cell(1, 7).Value = _localizer["StartMeter"].ToString();
            worksheet.Cell(1, 8).Value = _localizer["StopMeter"].ToString();
            worksheet.Cell(1, 9).Value = _localizer["ChargeSum"].ToString();

            if (transactions != null)
            {
                int row = 2;
                foreach (TransactionExtended transaction in transactions)
                {
                    worksheet.Cell(row, 1).Value = transaction.ChargePointId;
                    worksheet.Cell(row, 2).Value = transaction.ConnectorId;
                    worksheet.Cell(row, 3).SetValue(transaction.StartTime.ToLocalTime());
                    worksheet.Cell(row, 4).SetValue(transaction.StopTime?.ToLocalTime());
                    worksheet.Cell(row, 5).Value = string.IsNullOrEmpty(transaction.StartTagName)
                        ? transaction.StartTagId
                        : transaction.StartTagName;
                    worksheet.Cell(row, 6).Value = string.IsNullOrEmpty(transaction.StopTagName)
                        ? transaction.StopTagId
                        : transaction.StopTagName;
                    worksheet.Cell(row, 7).SetValue(transaction.MeterStart);
                    if (transaction.MeterStop.HasValue)
                    {
                        worksheet.Cell(row, 8).SetValue(transaction.MeterStop);
                        worksheet.Cell(row, 9).SetValue(transaction.MeterStop - transaction.MeterStart);
                    }
                    row++;
                }
            }

            worksheet.Columns().AdjustToContents();
            return workbook;
        }
    }
}
