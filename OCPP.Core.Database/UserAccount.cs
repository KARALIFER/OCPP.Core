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

#nullable disable

namespace OCPP.Core.Database
{
    public partial class UserAccount
    {
        public int UserId { get; set; }

        public string LoginName { get; set; }

        public string Password { get; set; }

        public bool IsAdmin { get; set; }

        public Guid PublicId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual ChargeTag ChargeTag { get; set; }

        public virtual ICollection<UserChargePoint> UserChargePoints { get; set; } = new List<UserChargePoint>();
    }
}
