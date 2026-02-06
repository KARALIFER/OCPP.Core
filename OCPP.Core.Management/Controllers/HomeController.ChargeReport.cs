using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OCPP.Core.Management.Models;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using System.IO;
using System.Text;
using System.Collections.Generic;
using OCPP.Core.Management.Services;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargeReport(DateTime? startDate, DateTime? stopDate)
        {
            try
            {
                Logger.LogTrace("ChargeReport: GenerateReport()...");
                bool isAdmin = User != null && User.IsInRole(Constants.AdminRoleName);
                HashSet<string> permittedChargeTagIds = GetPermittedChargeTagIds();
                HashSet<string> permittedChargePointIds = GetPermittedChargePointIds();
                var report = _chargeReportService.GenerateReport(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);
                return View(report);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error loading charge points from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult ChargeReportCsv(DateTime? startDate, DateTime? stopDate)
        {
            try
            {
                Logger.LogTrace("ChargeReport: ChargeReportCsv()...");
                bool isAdmin = User != null && User.IsInRole(Constants.AdminRoleName);
                HashSet<string> permittedChargeTagIds = GetPermittedChargeTagIds();
                HashSet<string> permittedChargePointIds = GetPermittedChargePointIds();
                var report = _chargeReportService.GenerateReport(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);
                var csv = new StringBuilder();

                csv.Append(_localizer["ChargeReportGroup"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["ChargeReportTag"]);
                csv.Append(DefaultCSVSeparator);
                csv.AppendLine(_localizer["ChargeReportEnergy"]);

                foreach (var group in report.Groups)
                {
                    foreach (var tag in group.Tags)
                    {
                        var totalEnergy = tag.Transactions
                            .Where(t => t.Energy.HasValue)
                            .Sum(t => t.Energy.Value);
                        csv.Append(EscapeCsvValue(group.GroupName, DefaultCSVSeparator));
                        csv.Append(DefaultCSVSeparator);
                        csv.Append(EscapeCsvValue(tag.TagName, DefaultCSVSeparator));
                        csv.Append(DefaultCSVSeparator);
                        csv.Append(Math.Round(totalEnergy, 3));
                        csv.AppendLine();
                    }
                }

                var fileName = $"ChargeReport_{DateTime.Now:yyyyMMddHHmmss}.csv";
                return File(Encoding.GetEncoding("ISO-8859-1").GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating CSV report");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult ChargeReportXlsx(DateTime? startDate, DateTime? stopDate)
        {
            try
            {
                Logger.LogTrace("ChargeReport: ChargeReportXslx()...");
                bool isAdmin = User != null && User.IsInRole(Constants.AdminRoleName);
                HashSet<string> permittedChargeTagIds = GetPermittedChargeTagIds();
                HashSet<string> permittedChargePointIds = GetPermittedChargePointIds();
                var report = _chargeReportService.GenerateReport(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(_localizer["ChargeReport"]);

                worksheet.Cell(1, 1).Value = _localizer["ChargeReportGroup"].ToString();
                worksheet.Cell(1, 2).Value = _localizer["ChargeReportTag"].ToString();
                worksheet.Cell(1, 3).Value = _localizer["ChargeReportEnergy"].ToString();

                var row = 2;
                foreach (var group in report.Groups)
                {
                    foreach (var tag in group.Tags)
                    {
                        var totalEnergy = tag.Transactions
                            .Where(t => t.Energy.HasValue)
                            .Sum(t => t.Energy.Value);

                        worksheet.Cell(row, 1).Value = group.GroupName;
                        worksheet.Cell(row, 2).Value = tag.TagName;
                        worksheet.Cell(row, 3).Value = Math.Round(totalEnergy, 3);
                        row++;
                    }
                }

                worksheet.Columns().AdjustToContents(); // Auto-scaling the column width

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                var fileName = $"ChargeReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating XLSX report");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult AllTransactionsCsv(DateTime? startDate, DateTime? stopDate)
        {
            try
            {
                Logger.LogTrace("ChargeReport: AllTransactionsCsv()...");
                bool isAdmin = User != null && User.IsInRole(Constants.AdminRoleName);
                HashSet<string> permittedChargeTagIds = GetPermittedChargeTagIds();
                HashSet<string> permittedChargePointIds = GetPermittedChargePointIds();
                var tlvm = _chargeReportService.GetTransactions(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);

                // Join transactions with chargepoints and connector names
                var fullTransactions =
                     from t in tlvm.Transactions
                     join cs in tlvm.ConnectorStatuses on new { t.ChargePointId, t.ConnectorId } equals new { cs.ChargePointId, cs.ConnectorId }
                     select new { t, cs };

                Logger.LogTrace("ChargeReport: AllTransactionsCsv() - Build Csv");

                var csv = new StringBuilder();
                csv.Append(_localizer["TransactionID"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["ChargePointId"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["Connector"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StartTagID"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StartTag"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["ChargeReportGroup"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StartTime"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StartMeter"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StopTagID"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StopTag"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StopTime"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["StopMeter"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["Energy-kWh"]);
                csv.Append(DefaultCSVSeparator);
                csv.AppendLine(_localizer["Power-kW"]);

                foreach (var ft in fullTransactions)
                {
                    double? energy = ft.t.MeterStop.HasValue ? (ft.t.MeterStop.Value - ft.t.MeterStart) : null;

                    csv.Append(ft.t.TransactionId);
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(string.IsNullOrEmpty(ft.cs?.ChargePoint?.Name) ? ft.t.ChargePointId : ft.cs.ChargePoint.Name, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(string.IsNullOrEmpty(ft.cs?.ConnectorName) ? ft.t.ConnectorId.ToString() : ft.cs.ConnectorName, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(ft.t.StartTagId, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(ft.t.StartTagName, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(ft.t.StartTagParentId, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(ft.t.StartTime.ToLocalTime());
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(ft.t.MeterStart);
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(ft.t.StopTagId, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(EscapeCsvValue(ft.t.StopTagName, DefaultCSVSeparator));
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(ft.t.StopTime?.ToLocalTime());
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(ft.t.MeterStop);
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(energy);
                    csv.Append(DefaultCSVSeparator);
                    csv.Append(CalculateAveragePowerKw(ft.t.MeterStart, ft.t.MeterStop, ft.t.StartTime, ft.t.StopTime));
                    csv.AppendLine();
                }

                var fileName = $"AllTransactions_{DateTime.Now:yyyyMMddHHmmss}.csv";
                return File(Encoding.GetEncoding("ISO-8859-1").GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating CSV for all transactions");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult AllTransactionsXlsx(DateTime? startDate, DateTime? stopDate)
        {
            try
            {
                Logger.LogTrace("ChargeReport: AllTransactionsXlsx()...");
                bool isAdmin = User != null && User.IsInRole(Constants.AdminRoleName);
                HashSet<string> permittedChargeTagIds = GetPermittedChargeTagIds();
                HashSet<string> permittedChargePointIds = GetPermittedChargePointIds();
                var tlvm = _chargeReportService.GetTransactions(startDate, stopDate, permittedChargeTagIds, permittedChargePointIds, isAdmin);

                // Join transactions with chargepoints and connector names
                var fullTransactions =
                     from t in tlvm.Transactions
                     join cs in tlvm.ConnectorStatuses on new { t.ChargePointId, t.ConnectorId } equals new { cs.ChargePointId, cs.ConnectorId }
                     select new { t, cs};

                Logger.LogTrace("ChargeReport: AllTransactionsXlsx() - Build XLS-Workbook");

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(_localizer["ReportSheetName"]);

                worksheet.Cell(1, 1).Value = _localizer["TransactionID"].Value;
                worksheet.Cell(1, 2).Value = _localizer["ChargePointId"].Value;
                worksheet.Cell(1, 3).Value = _localizer["Connector"].Value;
                worksheet.Cell(1, 4).Value = _localizer["StartTagID"].Value;
                worksheet.Cell(1, 5).Value = _localizer["StartTag"].Value;
                worksheet.Cell(1, 6).Value = _localizer["ChargeReportGroup"].Value;
                worksheet.Cell(1, 7).Value = _localizer["StartTime"].Value;
                worksheet.Cell(1, 8).Value = _localizer["StartMeter"].Value;
                worksheet.Cell(1, 9).Value = _localizer["StopTagID"].Value;
                worksheet.Cell(1, 10).Value = _localizer["StopTag"].Value;
                worksheet.Cell(1, 11).Value = _localizer["StopTime"].Value;
                worksheet.Cell(1, 12).Value = _localizer["StopMeter"].Value;
                worksheet.Cell(1, 13).Value = _localizer["Energy-kWh"].Value;
                worksheet.Cell(1, 14).Value = _localizer["Power-kW"].Value;

                var row = 2;
                foreach (var ft in fullTransactions)
                {
                    var energy = ft.t.MeterStop.HasValue ? (ft.t.MeterStop.Value - ft.t.MeterStart) : (double?)null;
                    worksheet.Cell(row, 1).Value = ft.t.TransactionId;
                    worksheet.Cell(row, 2).Value = (string.IsNullOrEmpty(ft.cs?.ChargePoint?.Name)) ? ft.t.ChargePointId : ft.cs.ChargePoint.Name;
                    worksheet.Cell(row, 3).Value = (string.IsNullOrEmpty(ft.cs?.ConnectorName)) ? ft.t.ConnectorId : ft.cs. ConnectorName;
                    worksheet.Cell(row, 4).Value = ft.t.StartTagId;
                    worksheet.Cell(row, 5).Value = ft.t.StartTagName;
                    worksheet.Cell(row, 6).Value = ft.t.StartTagParentId;
                    worksheet.Cell(row, 7).SetValue(ft.t.StartTime.ToLocalTime());
                    worksheet.Cell(row, 8).SetValue(ft.t.MeterStart);
                    if (ft.t.StopTagId != null)
                    {
                        worksheet.Cell(row, 9).Value = ft.t.StopTagId;
                        worksheet.Cell(row, 10).Value = ft.t.StopTagName;
                    }
                    if (ft.t.StopTime.HasValue)
                        worksheet.Cell(row, 11).SetValue(ft.t.StopTime.Value.ToLocalTime());
                    if (ft.t.MeterStop.HasValue)
                        worksheet.Cell(row, 12).SetValue(ft.t.MeterStop);
                    if (energy.HasValue)
                        worksheet.Cell(row, 13).SetValue(energy);
                    worksheet.Cell(row, 14).SetValue(CalculateAveragePowerKw(ft.t.MeterStart, ft.t.MeterStop, ft.t.StartTime, ft.t.StopTime));

                    row++;
                }
                worksheet.Columns().AdjustToContents(); // Auto-scaling the column width

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                var fileName = $"AllTransactions_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating XLSX for all transactions");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

    }
}
