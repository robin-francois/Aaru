﻿// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Remote.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Core algorithms.
//
// --[ Description ] ----------------------------------------------------------
//
//     Handles connections to Aaru.Server.
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2023 Natalia Portillo
// ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Aaru.CommonTypes.Metadata;
using Aaru.Console;
using Aaru.Database;
using Aaru.Database.Models;
using Aaru.Dto;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using CdOffset = Aaru.Database.Models.CdOffset;
using Version = Aaru.CommonTypes.Metadata.Version;

namespace Aaru.Core;

/// <summary>Handles connections to Aaru.Server</summary>
public static class Remote
{
    /// <summary>Submits a device report</summary>
    /// <param name="report">Device report</param>
    public static void SubmitReport(DeviceReport report)
    {
        var submitThread = new Thread(() =>
        {
            Spectre.ProgressSingleSpinner(ctx =>
            {
                ctx.AddTask(Localization.Core.Uploading_device_report).IsIndeterminate();

                try
                {
                    string json = JsonSerializer.Serialize(report, typeof(DeviceReport), DeviceReportContext.Default);

                    var httpClient = new HttpClient();

                    httpClient.DefaultRequestHeaders.Add("User-Agent",
                                                         $"Aaru {typeof(Version).Assembly.GetName().Version}");

                    httpClient.BaseAddress = new Uri("https://www.aaru.app");

                    HttpResponseMessage response = httpClient.
                                                   PostAsync("/api/uploadreportv2",
                                                             new StringContent(json, Encoding.UTF8,
                                                                               "application/json")).GetAwaiter().
                                                   GetResult();

                    if(!response.IsSuccessStatusCode)
                        return;

                    Stream data   = response.Content.ReadAsStream();
                    var    reader = new StreamReader(data);

                    reader.ReadToEnd();
                    data.Close();
                }
                catch(WebException)
                {
                    // Can't connect to the server, do nothing
                }

                // ReSharper disable once RedundantCatchClause
                catch
                {
                #if DEBUG
                    if(Debugger.IsAttached)
                        throw;
                #endif
                }
            });
        });

        submitThread.Start();
    }

    /// <summary>Updates the main database</summary>
    /// <param name="create">If <c>true</c> creates the database from scratch, otherwise updates an existing database</param>
    public static void UpdateMainDatabase(bool create)
    {
        var mctx = AaruContext.Create(Settings.Settings.MainDbPath);

        if(create)
        {
            mctx.Database.EnsureCreated();

            mctx.Database.
                 ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT PRIMARY KEY, \"ProductVersion\" TEXT)");

            foreach(string migration in mctx.Database.GetPendingMigrations())
                mctx.Database.
                     ExecuteSqlRaw($"INSERT INTO \"__EFMigrationsHistory\" (MigrationId, ProductVersion) VALUES ('{
                         migration}', '0.0.0')");
        }
        else
            mctx.Database.Migrate();

        mctx.SaveChanges();

        try
        {
            long     lastUpdate = 0;
            DateTime latest     = DateTime.MinValue;

            if(!create)
            {
                List<DateTime> latestAll = new();

                if(mctx.UsbVendors.Any())
                    latestAll.Add(mctx.UsbVendors.Max(v => v.ModifiedWhen));

                if(mctx.UsbProducts.Any())
                    latestAll.Add(mctx.UsbProducts.Max(p => p.ModifiedWhen));

                if(mctx.CdOffsets.Any())
                    latestAll.Add(mctx.CdOffsets.Max(o => o.ModifiedWhen));

                if(mctx.Devices.Any())
                    latestAll.Add(mctx.Devices.Max(d => d.LastSynchronized));

                if(latestAll.Any())
                {
                    latest     = latestAll.Max(t => t);
                    lastUpdate = (latest.ToFileTimeUtc() - new DateTime(1970, 1, 1).ToFileTimeUtc()) / 10000000;
                }
            }

            if(lastUpdate == 0)
            {
                create = true;
                AaruConsole.WriteLine(Localization.Core.Creating_main_database);
            }
            else
            {
                AaruConsole.WriteLine(Localization.Core.Updating_main_database);
                AaruConsole.WriteLine(Localization.Core.Last_update_0, latest);
            }

            DateTime updateStart = DateTime.UtcNow;

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"Aaru {typeof(Version).Assembly.GetName().Version}");
            httpClient.BaseAddress = new Uri("https://www.aaru.app");

            HttpResponseMessage response =
                httpClient.GetAsync($"/api/update?timestamp={lastUpdate}").GetAwaiter().GetResult();

            if(!response.IsSuccessStatusCode)
            {
                AaruConsole.ErrorWriteLine(Localization.Core.Error_0_when_trying_to_get_updated_entities,
                                           response.StatusCode);

                return;
            }

            Stream  data   = response.Content.ReadAsStream();
            var     reader = new StreamReader(data);
            SyncDto sync   = JsonSerializer.Deserialize<SyncDto>(reader.ReadToEnd())     ?? new SyncDto();

            if(create)
            {
                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Adding_USB_vendors);
                                task.MaxValue = sync.UsbVendors.Count;

                                foreach(UsbVendorDto vendor in sync.UsbVendors)
                                {
                                    task.Increment(1);
                                    mctx.UsbVendors.Add(new UsbVendor(vendor.VendorId, vendor.Vendor));
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_usb_vendors, sync.UsbVendors.Count);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Adding_USB_products);
                                task.MaxValue = sync.UsbProducts.Count;

                                foreach(UsbProductDto product in sync.UsbProducts)
                                {
                                    task.Increment(1);

                                    mctx.UsbProducts.Add(new UsbProduct(product.VendorId, product.ProductId,
                                                                        product.Product));
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_usb_products, sync.UsbProducts.Count);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Adding_CompactDisc_read_offsets);
                                task.MaxValue = sync.Offsets.Count;

                                foreach(CdOffsetDto offset in sync.Offsets)
                                {
                                    task.Increment(1);

                                    mctx.CdOffsets.Add(new CdOffset(offset)
                                    {
                                        Id = offset.Id
                                    });
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_CompactDisc_read_offsets, sync.Offsets.Count);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Adding_known_devices);
                                task.MaxValue = sync.Devices.Count;

                                foreach(DeviceDto device in sync.Devices)

                                {
                                    task.Increment(1);

                                    mctx.Devices.Add(new Device(device)
                                    {
                                        Id = device.Id
                                    });
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_known_devices, sync.Devices.Count);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Adding_known_iNES_NES_2_0_headers);
                                task.MaxValue = sync.NesHeaders?.Count ?? 0;

                                foreach(NesHeaderDto header in sync.NesHeaders ?? new List<NesHeaderDto>())
                                {
                                    task.Increment(1);

                                    mctx.NesHeaders.Add(new NesHeaderInfo
                                    {
                                        Id                     = header.Id,
                                        AddedWhen              = DateTime.UtcNow,
                                        BatteryPresent         = header.BatteryPresent,
                                        ConsoleType            = header.ConsoleType,
                                        DefaultExpansionDevice = header.DefaultExpansionDevice,
                                        ExtendedConsoleType    = header.ExtendedConsoleType,
                                        FourScreenMode         = header.FourScreenMode,
                                        Mapper                 = header.Mapper,
                                        ModifiedWhen           = DateTime.UtcNow,
                                        NametableMirroring     = header.NametableMirroring,
                                        Sha256                 = header.Sha256,
                                        Submapper              = header.Submapper,
                                        TimingMode             = header.TimingMode,
                                        VsHardwareType         = header.VsHardwareType,
                                        VsPpuType              = header.VsPpuType
                                    });
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_known_iNES_NES_2_0_headers,
                                      sync.NesHeaders?.Count ?? 0);
            }
            else
            {
                long addedVendors       = 0;
                long addedProducts      = 0;
                long addedOffsets       = 0;
                long addedDevices       = 0;
                long addedNesHeaders    = 0;
                long modifiedVendors    = 0;
                long modifiedProducts   = 0;
                long modifiedOffsets    = 0;
                long modifiedDevices    = 0;
                long modifiedNesHeaders = 0;

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Updating_USB_vendors);
                                task.MaxValue = sync.UsbVendors.Count;

                                foreach(UsbVendorDto vendor in sync.UsbVendors)
                                {
                                    task.Increment(1);

                                    UsbVendor existing = mctx.UsbVendors.FirstOrDefault(v => v.Id == vendor.VendorId);

                                    if(existing != null)
                                    {
                                        modifiedVendors++;
                                        existing.Vendor       = vendor.Vendor;
                                        existing.ModifiedWhen = updateStart;
                                        mctx.UsbVendors.Update(existing);
                                    }
                                    else
                                    {
                                        addedVendors++;
                                        mctx.UsbVendors.Add(new UsbVendor(vendor.VendorId, vendor.Vendor));
                                    }
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_usb_vendors, addedVendors);
                AaruConsole.WriteLine(Localization.Core.Modified_0_USB_vendors, modifiedVendors);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Updating_USB_products);
                                task.MaxValue = sync.UsbVendors.Count;

                                foreach(UsbProductDto product in sync.UsbProducts)
                                {
                                    task.Increment(1);

                                    UsbProduct existing =
                                        mctx.UsbProducts.FirstOrDefault(p => p.VendorId  == product.VendorId &&
                                                                             p.ProductId == product.ProductId);

                                    if(existing != null)
                                    {
                                        modifiedProducts++;
                                        existing.Product      = product.Product;
                                        existing.ModifiedWhen = updateStart;
                                        mctx.UsbProducts.Update(existing);
                                    }
                                    else
                                    {
                                        addedProducts++;

                                        mctx.UsbProducts.Add(new UsbProduct(product.VendorId, product.ProductId,
                                                                            product.Product));
                                    }
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_usb_products, addedProducts);
                AaruConsole.WriteLine(Localization.Core.Modified_0_USB_products, modifiedProducts);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Updating_CompactDisc_read_offsets);
                                task.MaxValue = sync.Offsets.Count;

                                foreach(CdOffsetDto offset in sync.Offsets)
                                {
                                    CdOffset existing = mctx.CdOffsets.FirstOrDefault(o => o.Id == offset.Id);
                                    task.Increment(1);

                                    if(existing != null)
                                    {
                                        modifiedOffsets++;
                                        existing.Agreement    = offset.Agreement;
                                        existing.Manufacturer = offset.Manufacturer;
                                        existing.Model        = offset.Model;
                                        existing.Submissions  = offset.Submissions;
                                        existing.Offset       = offset.Offset;
                                        existing.ModifiedWhen = updateStart;
                                        mctx.CdOffsets.Update(existing);
                                    }
                                    else
                                    {
                                        addedOffsets++;

                                        mctx.CdOffsets.Add(new CdOffset(offset)
                                        {
                                            Id = offset.Id
                                        });
                                    }
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_CompactDisc_read_offsets, addedOffsets);
                AaruConsole.WriteLine(Localization.Core.Modified_0_CompactDisc_read_offsets, modifiedOffsets);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Updating_known_devices);
                                task.MaxValue = sync.Offsets.Count;

                                foreach(DeviceDto device in sync.Devices)
                                {
                                    task.Increment(1);
                                    Device existing = mctx.Devices.FirstOrDefault(d => d.Id == device.Id);

                                    if(existing != null)
                                    {
                                        modifiedDevices++;

                                        mctx.Remove(existing);

                                        existing = new Device(device)
                                        {
                                            Id                         = device.Id,
                                            OptimalMultipleSectorsRead = device.OptimalMultipleSectorsRead,
                                            CanReadGdRomUsingSwapDisc  = device.CanReadGdRomUsingSwapDisc
                                        };

                                        mctx.Devices.Add(existing);
                                    }
                                    else
                                    {
                                        addedDevices++;

                                        mctx.Devices.Add(new Device(device)
                                        {
                                            Id                         = device.Id,
                                            OptimalMultipleSectorsRead = device.OptimalMultipleSectorsRead,
                                            CanReadGdRomUsingSwapDisc  = device.CanReadGdRomUsingSwapDisc
                                        });
                                    }
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_known_devices, addedDevices);
                AaruConsole.WriteLine(Localization.Core.Modified_0_known_devices, modifiedDevices);

                AnsiConsole.Progress().AutoClear(true).HideCompleted(true).
                            Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()).
                            Start(ctx =>
                            {
                                ProgressTask task = ctx.AddTask(Localization.Core.Updating_known_iNES_NES_2_0_headers);
                                task.MaxValue = sync.Offsets.Count;

                                sync.NesHeaders ??= new List<NesHeaderDto>();

                                foreach(NesHeaderDto header in sync.NesHeaders)
                                {
                                    task.Increment(1);
                                    NesHeaderInfo existing = mctx.NesHeaders.FirstOrDefault(d => d.Id == header.Id);

                                    if(existing != null)
                                    {
                                        modifiedNesHeaders++;
                                        DateTime addedDate = existing.AddedWhen;

                                        mctx.Remove(existing);

                                        existing = new NesHeaderInfo
                                        {
                                            Id                     = header.Id,
                                            AddedWhen              = addedDate,
                                            BatteryPresent         = header.BatteryPresent,
                                            ConsoleType            = header.ConsoleType,
                                            DefaultExpansionDevice = header.DefaultExpansionDevice,
                                            ExtendedConsoleType    = header.ExtendedConsoleType,
                                            FourScreenMode         = header.FourScreenMode,
                                            Mapper                 = header.Mapper,
                                            ModifiedWhen           = DateTime.UtcNow,
                                            NametableMirroring     = header.NametableMirroring,
                                            Sha256                 = header.Sha256,
                                            Submapper              = header.Submapper,
                                            TimingMode             = header.TimingMode,
                                            VsHardwareType         = header.VsHardwareType,
                                            VsPpuType              = header.VsPpuType
                                        };

                                        mctx.NesHeaders.Add(existing);
                                    }
                                    else
                                    {
                                        addedNesHeaders++;

                                        mctx.NesHeaders.Add(new NesHeaderInfo
                                        {
                                            Id                     = header.Id,
                                            AddedWhen              = DateTime.UtcNow,
                                            BatteryPresent         = header.BatteryPresent,
                                            ConsoleType            = header.ConsoleType,
                                            DefaultExpansionDevice = header.DefaultExpansionDevice,
                                            ExtendedConsoleType    = header.ExtendedConsoleType,
                                            FourScreenMode         = header.FourScreenMode,
                                            Mapper                 = header.Mapper,
                                            ModifiedWhen           = DateTime.UtcNow,
                                            NametableMirroring     = header.NametableMirroring,
                                            Sha256                 = header.Sha256,
                                            Submapper              = header.Submapper,
                                            TimingMode             = header.TimingMode,
                                            VsHardwareType         = header.VsHardwareType,
                                            VsPpuType              = header.VsPpuType
                                        });
                                    }
                                }
                            });

                AaruConsole.WriteLine(Localization.Core.Added_0_known_iNES_NES_2_0_headers, addedNesHeaders);
                AaruConsole.WriteLine(Localization.Core.Modified_0_known_iNES_NES_2_0_headers, modifiedNesHeaders);
            }
        }
        catch(Exception ex)
        {
            AaruConsole.ErrorWriteLine(Localization.Core.Exception_0_when_updating_database, ex);
        }
        finally
        {
            Spectre.ProgressSingleSpinner(ctx =>
            {
                ctx.AddTask(Localization.Core.Saving_changes).IsIndeterminate();
                mctx.SaveChanges();
            });
        }
    }
}