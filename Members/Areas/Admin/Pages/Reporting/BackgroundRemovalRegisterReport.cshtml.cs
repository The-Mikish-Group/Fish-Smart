using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Members.Data;
using Members.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Members.Areas.Admin.Pages.Reporting
{
    [Authorize(Roles = "Admin,Manager")]
    public partial class BackgroundRemovalRegisterReportModel(ApplicationDbContext context,
                                  UserManager<IdentityUser> userManager,
                                  ILogger<BackgroundRemovalRegisterReportModel> logger) : PageModel
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<IdentityUser> _userManager = userManager;
        private readonly ILogger<BackgroundRemovalRegisterReportModel> _logger = logger;

        [BindProperty(SupportsGet = true)]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date:")]
        public DateTime StartDate { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        [BindProperty(SupportsGet = true)]
        [DataType(DataType.Date)]
        [Display(Name = "End Date:")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        public IList<BackgroundRemovalRegisterItemViewModel> ReportData { get; set; } = [];
        public BackgroundRemovalRegisterSummaryViewModel Totals { get; set; } = new BackgroundRemovalRegisterSummaryViewModel();

        private async Task GenerateReportDataAsync()
        {
            ReportData = [];
            Totals = new BackgroundRemovalRegisterSummaryViewModel();

            DateTime effectiveStartDate = StartDate.Date;
            DateTime effectiveEndDate = EndDate.Date.AddDays(1).AddTicks(-1);

            // Get user profiles for display names
            var users = await _context.UserProfile.ToDictionaryAsync(up => up.UserId, up => $"{up.FirstName} {up.LastName}".Trim());

            // Get all background removal usage in the date range
            var backgroundRemovalUsage = await _context.BackgroundRemovalUsage
                .Include(bru => bru.Invoice)
                .Where(bru => bru.UsageDate >= effectiveStartDate &&
                              bru.UsageDate <= effectiveEndDate)
                .OrderBy(bru => bru.UsageDate)
                .ThenBy(bru => bru.UsageId)
                .ToListAsync();

            foreach (var usage in backgroundRemovalUsage)
            {
                string customerName = users.TryGetValue(usage.UserId, out var name) ? (string.IsNullOrEmpty(name) ? "N/A" : name) : "N/A";

                var itemVM = new BackgroundRemovalRegisterItemViewModel
                {
                    UsageId = usage.UsageId,
                    CustomerName = customerName,
                    UsageDate = usage.UsageDate,
                    ServiceUsed = usage.ServiceUsed,
                    ApiCost = usage.Cost,
                    ChargeAmount = usage.ChargeAmount,
                    IsWithinFreeLimit = usage.IsWithinFreeLimit,
                    HasBeenInvoiced = usage.HasBeenInvoiced,
                    InvoiceId = usage.InvoiceId.HasValue ? $"BR-INV-{usage.InvoiceId.Value:D5}" : "N/A",
                    InvoiceStatus = usage.Invoice?.Status.ToString() ?? "N/A",
                    AmountPaidOnInvoice = usage.Invoice?.AmountPaid ?? 0m,
                    Notes = usage.Notes ?? string.Empty,
                    UsageMonth = usage.UsageMonth,
                    UsageYear = usage.UsageYear
                };

                ReportData.Add(itemVM);

                // Update totals
                Totals.TotalUsageCount++;
                Totals.TotalApiCosts += usage.Cost;
                Totals.TotalChargedAmount += usage.ChargeAmount;
                
                if (usage.IsWithinFreeLimit)
                {
                    Totals.FreeUsageCount++;
                }
                else
                {
                    Totals.BillableUsageCount++;
                    Totals.TotalBillableAmount += usage.ChargeAmount;
                }

                if (usage.Invoice != null)
                {
                    Totals.TotalInvoicedAmount += usage.Invoice.AmountDue;
                    Totals.TotalPaidAmount += usage.Invoice.AmountPaid;
                    
                    if (usage.Invoice.Status != InvoiceStatus.Paid && usage.Invoice.Status != InvoiceStatus.Cancelled)
                    {
                        Totals.TotalOutstandingAmount += (usage.Invoice.AmountDue - usage.Invoice.AmountPaid);
                    }
                }
            }

            _logger.LogInformation("Generated Background Removal Register data for {StartDate} to {EndDate}. Count: {Count}", 
                StartDate, EndDate, ReportData.Count);
        }

        public async Task OnGetAsync()
        {
            await GenerateReportDataAsync();
        }

        public async Task<IActionResult> OnGetExportToCsvAsync()
        {
            await GenerateReportDataAsync();
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("UsageId,CustomerName,UsageDate,ServiceUsed,ApiCost,ChargeAmount,IsWithinFreeLimit,HasBeenInvoiced,InvoiceId,InvoiceStatus,AmountPaidOnInvoice,Notes,UsageMonth,UsageYear");

            foreach (var item in ReportData)
            {
                builder.AppendLine(
                    $"{item.UsageId}," +
                    $"\"{EscapeCsvField(item.CustomerName)}\"," +
                    $"{item.UsageDate:yyyy-MM-dd HH:mm:ss}," +
                    $"\"{EscapeCsvField(item.ServiceUsed)}\"," +
                    $"{item.ApiCost:F3}," +
                    $"{item.ChargeAmount:F2}," +
                    $"{item.IsWithinFreeLimit}," +
                    $"{item.HasBeenInvoiced}," +
                    $"\"{EscapeCsvField(item.InvoiceId)}\"," +
                    $"\"{EscapeCsvField(item.InvoiceStatus)}\"," +
                    $"{item.AmountPaidOnInvoice:F2}," +
                    $"\"{EscapeCsvField(item.Notes)}\"," +
                    $"{item.UsageMonth}," +
                    $"{item.UsageYear}"
                );
            }
            
            builder.AppendLine();
            builder.AppendLine($",,,,Total API Costs:,{Totals.TotalApiCosts:F3}");
            builder.AppendLine($",,,,Total Charged:,{Totals.TotalChargedAmount:F2}");
            builder.AppendLine($",,,,Total Invoiced:,{Totals.TotalInvoicedAmount:F2}");
            builder.AppendLine($",,,,Total Paid:,{Totals.TotalPaidAmount:F2}");
            builder.AppendLine($",,,,Total Outstanding:,{Totals.TotalOutstandingAmount:F2}");
            builder.AppendLine($",,,,Free Usage Count:,{Totals.FreeUsageCount}");
            builder.AppendLine($",,,,Billable Usage Count:,{Totals.BillableUsageCount}");

            string fileName = $"BackgroundRemovalRegister_{StartDate:yyyy-MM-dd}_to_{EndDate:yyyy-MM-dd}.csv";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
            return File(buffer, "text/csv", fileName);
        }

        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
            return field.Replace("\"", "\"\"");
        }

        public class BackgroundRemovalRegisterItemViewModel
        {
            public int UsageId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public DateTime UsageDate { get; set; }
            public string ServiceUsed { get; set; } = string.Empty;
            public decimal ApiCost { get; set; }
            public decimal ChargeAmount { get; set; }
            public bool IsWithinFreeLimit { get; set; }
            public bool HasBeenInvoiced { get; set; }
            public string InvoiceId { get; set; } = string.Empty;
            public string InvoiceStatus { get; set; } = string.Empty;
            public decimal AmountPaidOnInvoice { get; set; }
            public string Notes { get; set; } = string.Empty;
            public int UsageMonth { get; set; }
            public int UsageYear { get; set; }
        }

        public class BackgroundRemovalRegisterSummaryViewModel
        {
            public int TotalUsageCount { get; set; }
            public int FreeUsageCount { get; set; }
            public int BillableUsageCount { get; set; }
            public decimal TotalApiCosts { get; set; }
            public decimal TotalChargedAmount { get; set; }
            public decimal TotalBillableAmount { get; set; }
            public decimal TotalInvoicedAmount { get; set; }
            public decimal TotalPaidAmount { get; set; }
            public decimal TotalOutstandingAmount { get; set; }
        }
    }
}