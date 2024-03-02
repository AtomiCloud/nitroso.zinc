﻿// <auto-generated />
using System;
using App.StartUp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace App.Migrations
{
    [DbContext(typeof(MainDbContext))]
    [Migration("20231216094300_AddTicketField")]
    partial class AddTicketField
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("App.Modules.Bookings.Data.BookingData", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("NOW()");

                    b.Property<DateOnly>("Date")
                        .HasColumnType("date");

                    b.Property<int>("Direction")
                        .HasColumnType("integer");

                    b.Property<byte>("Status")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("smallint")
                        .HasDefaultValue((byte)0);

                    b.Property<string>("Ticket")
                        .HasColumnType("text");

                    b.Property<TimeOnly>("Time")
                        .HasColumnType("time without time zone");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Bookings");
                });

            modelBuilder.Entity("App.Modules.Passengers.Data.PassengerData", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<byte>("Gender")
                        .HasColumnType("smallint");

                    b.Property<DateOnly>("PassportExpiry")
                        .HasColumnType("date");

                    b.Property<string>("PassportNumber")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("UserId", "PassportNumber")
                        .IsUnique();

                    b.ToTable("Passengers");
                });

            modelBuilder.Entity("App.Modules.Schedules.Data.ScheduleData", b =>
                {
                    b.Property<DateOnly>("Date")
                        .HasColumnType("date");

                    b.Property<bool>("Confirmed")
                        .HasColumnType("boolean");

                    b.Property<TimeOnly[]>("JToWExcluded")
                        .IsRequired()
                        .HasColumnType("time without time zone[]");

                    b.Property<TimeOnly[]>("WToJExcluded")
                        .IsRequired()
                        .HasColumnType("time without time zone[]");

                    b.HasKey("Date");

                    b.ToTable("Schedules");
                });

            modelBuilder.Entity("App.Modules.Timings.Data.TimingData", b =>
                {
                    b.Property<int>("Direction")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Direction"));

                    b.Property<TimeOnly[]>("Timings")
                        .IsRequired()
                        .HasColumnType("time without time zone[]");

                    b.HasKey("Direction");

                    b.ToTable("Timings");

                    b.HasData(
                        new
                        {
                            Direction = 1,
                            Timings = new[] { new TimeOnly(5, 0, 0), new TimeOnly(5, 30, 0), new TimeOnly(6, 0, 0), new TimeOnly(6, 30, 0), new TimeOnly(7, 0, 0), new TimeOnly(7, 30, 0), new TimeOnly(8, 45, 0), new TimeOnly(10, 0, 0), new TimeOnly(11, 30, 0), new TimeOnly(12, 45, 0), new TimeOnly(14, 0, 0), new TimeOnly(15, 15, 0), new TimeOnly(16, 30, 0), new TimeOnly(17, 45, 0), new TimeOnly(19, 0, 0), new TimeOnly(20, 15, 0), new TimeOnly(21, 30, 0), new TimeOnly(22, 45, 0) }
                        },
                        new
                        {
                            Direction = 2,
                            Timings = new[] { new TimeOnly(8, 30, 0), new TimeOnly(9, 45, 0), new TimeOnly(11, 0, 0), new TimeOnly(12, 30, 0), new TimeOnly(13, 45, 0), new TimeOnly(15, 0, 0), new TimeOnly(16, 15, 0), new TimeOnly(17, 30, 0), new TimeOnly(18, 45, 0), new TimeOnly(20, 0, 0), new TimeOnly(21, 15, 0), new TimeOnly(22, 30, 0), new TimeOnly(23, 45, 0) }
                        });
                });

            modelBuilder.Entity("App.Modules.Users.Data.UserData", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("App.Modules.Bookings.Data.BookingData", b =>
                {
                    b.HasOne("App.Modules.Users.Data.UserData", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.OwnsMany("App.Modules.Bookings.Data.BookingPassengerData", "Passengers", b1 =>
                        {
                            b1.Property<Guid>("BookingDataId")
                                .HasColumnType("uuid");

                            b1.Property<int>("Id")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("integer");

                            b1.Property<string>("FullName")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.Property<byte>("Gender")
                                .HasColumnType("smallint");

                            b1.Property<DateOnly>("PassportExpiry")
                                .HasColumnType("date");

                            b1.Property<string>("PassportNumber")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.HasKey("BookingDataId", "Id");

                            b1.ToTable("Bookings");

                            b1.ToJson("Passengers");

                            b1.WithOwner()
                                .HasForeignKey("BookingDataId");
                        });

                    b.Navigation("Passengers");

                    b.Navigation("User");
                });

            modelBuilder.Entity("App.Modules.Passengers.Data.PassengerData", b =>
                {
                    b.HasOne("App.Modules.Users.Data.UserData", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });
#pragma warning restore 612, 618
        }
    }
}