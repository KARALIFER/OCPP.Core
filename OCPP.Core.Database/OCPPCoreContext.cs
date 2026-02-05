/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
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
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;

#nullable disable

namespace OCPP.Core.Database
{
    public partial class OCPPCoreContext : DbContext
    {
        public OCPPCoreContext(DbContextOptions options)
            : base(options)
        {
        }

        public virtual DbSet<ChargePoint> ChargePoints { get; set; }
        public virtual DbSet<ChargeTag> ChargeTags { get; set; }
        public virtual DbSet<ConnectorStatus> ConnectorStatuses { get; set; }
        public virtual DbSet<MessageLog> MessageLogs { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<UserAccount> UserAccounts { get; set; }
        public virtual DbSet<UserChargePoint> UserChargePoints { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChargePoint>(entity =>
            {
                entity.ToTable("ChargePoint");

                entity.HasIndex(e => e.ChargePointId, "ChargePoint_Identifier")
                    .IsUnique();

                entity.Property(e => e.ChargePointId).HasMaxLength(100);

                entity.Property(e => e.Comment).HasMaxLength(200);

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.Property(e => e.Username).HasMaxLength(50);

                entity.Property(e => e.Password).HasMaxLength(50);

                entity.Property(e => e.ClientCertThumb).HasMaxLength(100);
            });

            modelBuilder.Entity<ChargeTag>(entity =>
            {
                entity.HasKey(e => e.TagId)
                    .HasName("PK_ChargeKeys");

                entity.HasIndex(e => e.TagUid)
                    .IsUnique();

                entity.HasIndex(e => e.UserAccountId)
                    .IsUnique();

                entity.Property(e => e.TagId).HasMaxLength(50);

                entity.Property(e => e.TagUid)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ParentTagId).HasMaxLength(50);

                entity.Property(e => e.TagName).HasMaxLength(200);

                entity.HasOne(d => d.UserAccount)
                    .WithOne(p => p.ChargeTag)
                    .HasForeignKey<ChargeTag>(d => d.UserAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ConnectorStatus>(entity =>
            {
                entity.HasKey(e => new { e.ChargePointId, e.ConnectorId });

                entity.ToTable("ConnectorStatus");

                entity.Property(e => e.ChargePointId).HasMaxLength(100);

                entity.Property(e => e.ConnectorName).HasMaxLength(100);

                entity.Property(e => e.LastStatus).HasMaxLength(100);
            });

            modelBuilder.Entity<MessageLog>(entity =>
            {
                entity.HasKey(e => e.LogId);

                entity.ToTable("MessageLog");

                entity.HasIndex(e => e.LogTime, "IX_MessageLog_ChargePointId");

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ErrorCode).HasMaxLength(100);

                entity.Property(e => e.Message)
                    .IsRequired()
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(e => e.Uid).HasMaxLength(50);

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.StartTagId).HasMaxLength(50);

                entity.Property(e => e.StartResult).HasMaxLength(100);

                entity.Property(e => e.StopTagId).HasMaxLength(50);

                entity.Property(e => e.StopReason).HasMaxLength(100);

                entity.HasOne(d => d.ChargePoint)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.ChargePointId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Transactions_ChargePoint");

                entity.HasIndex(e => new { e.ChargePointId, e.ConnectorId });
            });

            modelBuilder.Entity<UserAccount>(entity =>
            {
                entity.ToTable("Users");

                entity.HasKey(e => e.UserId);

                entity.Property(e => e.UserId)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(e => e.LoginName)
                    .IsUnique();

                entity.HasIndex(e => e.PublicId)
                    .IsUnique();

                entity.Property(e => e.LoginName)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnName("Username");

                entity.Property(e => e.Password)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PublicId)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .IsRequired();

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
            });

            modelBuilder.Entity<UserChargePoint>(entity =>
            {
                entity.ToTable("UserChargePoints");

                entity.HasKey(e => new { e.UserAccountId, e.ChargePointId });

                entity.Property(e => e.UserAccountId)
                    .HasColumnName("UserId");

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.IsHidden)
                    .HasDefaultValue(false);

                entity.HasIndex(e => e.ChargePointId);

                entity.HasOne(d => d.UserAccount)
                    .WithMany(p => p.UserChargePoints)
                    .HasForeignKey(d => d.UserAccountId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.ChargePoint)
                    .WithMany(p => p.UserChargePoints)
                    .HasForeignKey(d => d.ChargePointId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
