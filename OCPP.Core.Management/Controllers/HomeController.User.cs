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
using System.Linq;
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
        [Authorize]
        [ActionName("User")]
        public IActionResult UserManagement(string Id, UserViewModel uvm)
        {
            try
            {
                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    Logger.LogWarning("User: Request by non-administrator: {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                List<UserAccount> dbUsers = DbContext.UserAccounts
                    .Include(user => user.ChargeTag)
                    .OrderBy(x => x.LoginName)
                    .ToList();
                List<ChargeTag> dbChargeTags = DbContext.ChargeTags.OrderBy(x => x.TagName).ToList();
                List<ChargePoint> dbChargePoints = DbContext.ChargePoints.OrderBy(x => x.Name).ToList();
                UserAccount currentUser = null;
                if (!string.IsNullOrEmpty(Id) && Id != "@")
                {
                    if (int.TryParse(Id, out int userId))
                    {
                        currentUser = dbUsers.FirstOrDefault(user => user.UserId == userId);
                    }
                }

                if (Request.Method == "POST")
                {
                    string errorMsg = null;

                    if (Id == "@")
                    {
                        if (string.IsNullOrWhiteSpace(uvm.Username))
                        {
                            errorMsg = _localizer["UserNameRequired"].Value;
                        }
                        else if (string.IsNullOrWhiteSpace(uvm.Password))
                        {
                            errorMsg = _localizer["UserPasswordRequired"].Value;
                        }
                        else if (dbUsers.Any(user => user.LoginName.Equals(uvm.Username, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            errorMsg = _localizer["UserNameExists"].Value;
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            bool useExplicitIds = DbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
                            int nextUserId = 1;
                            if (useExplicitIds)
                            {
                                nextUserId = (dbUsers.Max(user => (int?)user.UserId) ?? 0) + 1;
                            }

                            UserAccount newUser = new UserAccount
                            {
                                LoginName = uvm.Username,
                                Password = uvm.Password,
                                IsAdmin = uvm.IsAdmin,
                                PublicId = Guid.NewGuid(),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            if (useExplicitIds)
                            {
                                newUser.UserId = nextUserId;
                            }
                            DbContext.UserAccounts.Add(newUser);
                            DbContext.SaveChanges();

                            string assignmentError = UpdateUserChargeTag(newUser.UserId, uvm.SelectedChargeTagId);
                            if (!string.IsNullOrEmpty(assignmentError))
                            {
                                ViewBag.ErrorMsg = assignmentError;
                                uvm.UserId = newUser.UserId;
                                uvm.Users = DbContext.UserAccounts
                                    .Include(user => user.ChargeTag)
                                    .OrderBy(user => user.LoginName)
                                    .ToList();
                                uvm.ChargeTags = BuildChargeTagList(dbChargeTags, newUser.UserId);
                                return View("UserDetail", uvm);
                            }

                            UpdateUserChargePoints(newUser.UserId, uvm.ChargePoints);
                            try
                            {
                                DbContext.SaveChanges();
                            }
                            catch (DbUpdateException dbEx)
                            {
                                Logger.LogWarning(dbEx, "User: Error assigning charge tag for new user {0}", newUser.LoginName);
                                ViewBag.ErrorMsg = _localizer["ChargeTagAssignmentConflict"].Value;
                                uvm.UserId = newUser.UserId;
                                uvm.Users = DbContext.UserAccounts
                                    .Include(user => user.ChargeTag)
                                    .OrderBy(user => user.LoginName)
                                    .ToList();
                                uvm.ChargeTags = BuildChargeTagList(dbChargeTags, newUser.UserId);
                                return View("UserDetail", uvm);
                            }
                        }
                        else
                        {
                            uvm.ChargeTags = BuildChargeTagList(dbChargeTags, currentUser?.UserId);
                            uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, uvm.ChargePoints);
                            ViewBag.ErrorMsg = errorMsg;
                            return View("UserDetail", uvm);
                        }
                    }
                    else if (currentUser != null)
                    {
                        if (Request.Form["action"] == "Delete")
                        {
                            DbContext.Remove<UserAccount>(currentUser);
                            DbContext.SaveChanges();
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(uvm.Username))
                            {
                                errorMsg = _localizer["UserNameRequired"].Value;
                            }
                            else if (dbUsers.Any(user => user.UserId != currentUser.UserId && user.LoginName.Equals(uvm.Username, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                errorMsg = _localizer["UserNameExists"].Value;
                            }

                            if (string.IsNullOrEmpty(errorMsg))
                            {
                                currentUser.LoginName = uvm.Username;
                                if (!string.IsNullOrWhiteSpace(uvm.Password))
                                {
                                    currentUser.Password = uvm.Password;
                                }
                                currentUser.IsAdmin = uvm.IsAdmin;
                                currentUser.UpdatedAt = DateTime.UtcNow;
                                DbContext.SaveChanges();

                                string assignmentError = UpdateUserChargeTag(currentUser.UserId, uvm.SelectedChargeTagId);
                                if (!string.IsNullOrEmpty(assignmentError))
                                {
                                    ViewBag.ErrorMsg = assignmentError;
                                    uvm.UserId = currentUser.UserId;
                                    uvm.ChargeTags = BuildChargeTagList(dbChargeTags, currentUser.UserId);
                                    uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, uvm.ChargePoints);
                                    return View("UserDetail", uvm);
                                }
                                UpdateUserChargePoints(currentUser.UserId, uvm.ChargePoints);
                                try
                                {
                                    DbContext.SaveChanges();
                                }
                                catch (DbUpdateException dbEx)
                                {
                                    Logger.LogWarning(dbEx, "User: Error assigning charge tag for user {0}", currentUser.LoginName);
                                    ViewBag.ErrorMsg = _localizer["ChargeTagAssignmentConflict"].Value;
                                    uvm.UserId = currentUser.UserId;
                                    uvm.ChargeTags = BuildChargeTagList(dbChargeTags, currentUser.UserId);
                                    uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, uvm.ChargePoints);
                                    return View("UserDetail", uvm);
                                }
                            }
                            else
                            {
                                uvm.UserId = currentUser.UserId;
                                uvm.ChargeTags = BuildChargeTagList(dbChargeTags, currentUser.UserId);
                                uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, uvm.ChargePoints);
                                ViewBag.ErrorMsg = errorMsg;
                                return View("UserDetail", uvm);
                            }
                        }
                    }

                    return RedirectToAction("User", new { Id = "" });
                }
                else
                {
                    uvm = new UserViewModel
                    {
                        Users = dbUsers
                    };

                    if (currentUser != null)
                    {
                        uvm.UserId = currentUser.UserId;
                        uvm.Username = currentUser.LoginName;
                        uvm.IsAdmin = currentUser.IsAdmin;
                        uvm.SelectedChargeTagId = currentUser.ChargeTag?.TagId;
                        uvm.ChargeTags = BuildChargeTagList(dbChargeTags, currentUser.UserId);

                        List<UserChargePoint> userChargePoints = DbContext.UserChargePoints
                            .Where(point => point.UserAccountId == currentUser.UserId)
                            .ToList();
                        uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, userChargePoints);
                    }
                    else
                    {
                        uvm.ChargeTags = BuildChargeTagList(dbChargeTags, null);
                        uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, new List<UserChargePoint>());
                    }

                    string viewName = (!string.IsNullOrEmpty(Id) || Id == "@") ? "UserDetail" : "UserList";
                    return View(viewName, uvm);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "User: Error loading or saving users from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult MyChargeTags()
        {
            try
            {
                int? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    Logger.LogWarning("MyChargeTags: Missing user id claim for {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                List<ChargeTag> chargeTags = DbContext.ChargeTags
                    .Where(tag => tag.UserAccountId == userId.Value)
                    .OrderBy(tag => tag.TagName)
                    .ToList();

                MyChargeTagsViewModel viewModel = new MyChargeTagsViewModel
                {
                    ChargeTags = chargeTags
                };

                return View("MyChargeTags", viewModel);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MyChargeTags: Error loading charge tags from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        private List<ChargeTag> BuildChargeTagList(IEnumerable<ChargeTag> chargeTags, int? currentUserId)
        {
            return chargeTags
                .Where(tag => tag.UserAccountId == null || (currentUserId.HasValue && tag.UserAccountId == currentUserId))
                .OrderBy(tag => tag.TagName)
                .ToList();
        }

        private List<UserChargePointAssignmentViewModel> BuildChargePointAssignments(IEnumerable<ChargePoint> chargePoints, IEnumerable<UserChargePointAssignmentViewModel> selectedAssignments)
        {
            HashSet<string> selectedChargePointIds = selectedAssignments == null
                ? new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                : selectedAssignments.Where(point => point.IsAssigned)
                    .Select(point => point.ChargePointId)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            Dictionary<string, bool> hiddenSelections = selectedAssignments == null
                ? new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase)
                : selectedAssignments.ToDictionary(point => point.ChargePointId, point => point.IsHidden, StringComparer.InvariantCultureIgnoreCase);

            return BuildChargePointAssignments(chargePoints, selectedChargePointIds, hiddenSelections);
        }

        private List<UserChargePointAssignmentViewModel> BuildChargePointAssignments(IEnumerable<ChargePoint> chargePoints, List<UserChargePoint> assignments)
        {
            HashSet<string> assignedChargePointIds = assignments
                .Select(point => point.ChargePointId)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            Dictionary<string, bool> hiddenAssignments = assignments
                .ToDictionary(point => point.ChargePointId, point => point.IsHidden, StringComparer.InvariantCultureIgnoreCase);

            return BuildChargePointAssignments(chargePoints, assignedChargePointIds, hiddenAssignments);
        }

        private List<UserChargePointAssignmentViewModel> BuildChargePointAssignments(
            IEnumerable<ChargePoint> chargePoints,
            HashSet<string> assignedChargePointIds,
            Dictionary<string, bool> hiddenAssignments)
        {
            List<UserChargePointAssignmentViewModel> assignments = new List<UserChargePointAssignmentViewModel>();

            foreach (ChargePoint chargePoint in chargePoints)
            {
                bool isHidden = hiddenAssignments != null &&
                    hiddenAssignments.TryGetValue(chargePoint.ChargePointId, out bool hiddenValue) &&
                    hiddenValue;

                assignments.Add(new UserChargePointAssignmentViewModel
                {
                    ChargePointId = chargePoint.ChargePointId,
                    ChargePointName = chargePoint.Name,
                    IsAssigned = assignedChargePointIds.Contains(chargePoint.ChargePointId),
                    IsHidden = isHidden
                });
            }

            return assignments;
        }

        private string UpdateUserChargeTag(int userId, string selectedTagId)
        {
            ChargeTag existingTag = DbContext.ChargeTags.FirstOrDefault(tag => tag.UserAccountId == userId);

            if (string.IsNullOrWhiteSpace(selectedTagId))
            {
                if (existingTag != null)
                {
                    existingTag.UserAccountId = null;
                }
                return null;
            }

            ChargeTag selectedTag = DbContext.ChargeTags.FirstOrDefault(tag => tag.TagId == selectedTagId);
            if (selectedTag == null)
            {
                return _localizer["ChargeTagIdRequired"].Value;
            }

            if (existingTag != null && !string.Equals(existingTag.TagId, selectedTagId, StringComparison.InvariantCultureIgnoreCase))
            {
                return _localizer["UserHasChargeTag"].Value;
            }

            if (selectedTag.UserAccountId.HasValue && selectedTag.UserAccountId != userId)
            {
                return _localizer["ChargeTagAlreadyAssigned"].Value;
            }

            selectedTag.UserAccountId = userId;
            return null;
        }

        private void UpdateUserChargePoints(int userId, IEnumerable<UserChargePointAssignmentViewModel> assignments)
        {
            if (assignments == null)
            {
                return;
            }

            HashSet<string> assignedChargePointIds = assignments
                .Where(point => point.IsAssigned)
                .Select(point => point.ChargePointId)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            List<UserChargePoint> existingAssignments = DbContext.UserChargePoints
                .Where(point => point.UserAccountId == userId)
                .ToList();

            foreach (UserChargePoint assignment in existingAssignments)
            {
                if (!assignedChargePointIds.Contains(assignment.ChargePointId))
                {
                    DbContext.UserChargePoints.Remove(assignment);
                }
            }

            foreach (string chargePointId in assignedChargePointIds)
            {
                bool isHidden = assignments
                    .FirstOrDefault(point => point.ChargePointId.Equals(chargePointId, StringComparison.InvariantCultureIgnoreCase))
                    ?.IsHidden ?? false;

                UserChargePoint existingAssignment = existingAssignments
                    .FirstOrDefault(point => point.ChargePointId.Equals(chargePointId, StringComparison.InvariantCultureIgnoreCase));

                if (existingAssignment == null)
                {
                    DbContext.UserChargePoints.Add(new UserChargePoint
                    {
                        UserAccountId = userId,
                        ChargePointId = chargePointId,
                        IsHidden = isHidden
                    });
                }
                else
                {
                    existingAssignment.IsHidden = isHidden;
                }
            }
        }
    }
}
