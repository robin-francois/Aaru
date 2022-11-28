﻿// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : CBM.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Commodore file system plugin.
//
// --[ Description ] ----------------------------------------------------------
//
//     Identifies the Commodore file system and shows information.
//
// --[ License ] --------------------------------------------------------------
//
//     This library is free software; you can redistribute it and/or modify
//     it under the terms of the GNU Lesser General Public License as
//     published by the Free Software Foundation; either version 2.1 of the
//     License, or (at your option) any later version.
//
//     This library is distributed in the hope that it will be useful, but
//     WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//     Lesser General Public License for more details.
//
//     You should have received a copy of the GNU Lesser General Public
//     License along with this library; if not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2022 Natalia Portillo
// ****************************************************************************/

using System;
using System.Runtime.InteropServices;
using System.Text;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.Helpers;
using Claunia.Encoding;
using Schemas;
using Encoding = System.Text.Encoding;
using Marshal = Aaru.Helpers.Marshal;

namespace Aaru.Filesystems;

/// <inheritdoc />
/// <summary>Implements detection of the filesystem used in 8-bit Commodore microcomputers</summary>
public sealed class CBM : IFilesystem
{
    const string FS_TYPE = "cbmfs";
    /// <inheritdoc />
    public FileSystemType XmlFsType { get; private set; }
    /// <inheritdoc />
    public string Name => Localization.CBM_Name;
    /// <inheritdoc />
    public Guid Id => new("D104744E-A376-450C-BAC0-1347C93F983B");
    /// <inheritdoc />
    public Encoding Encoding { get; private set; }
    /// <inheritdoc />
    public string Author => Authors.NataliaPortillo;

    /// <inheritdoc />
    public bool Identify(IMediaImage imagePlugin, Partition partition)
    {
        if(partition.Start > 0)
            return false;

        if(imagePlugin.Info.SectorSize != 256)
            return false;

        if(imagePlugin.Info.Sectors != 683  &&
           imagePlugin.Info.Sectors != 768  &&
           imagePlugin.Info.Sectors != 1366 &&
           imagePlugin.Info.Sectors != 3200)
            return false;

        byte[] sector;

        if(imagePlugin.Info.Sectors == 3200)
        {
            ErrorNumber errno = imagePlugin.ReadSector(1560, out sector);

            if(errno != ErrorNumber.NoError)
                return false;

            Header cbmHdr = Marshal.ByteArrayToStructureLittleEndian<Header>(sector);

            if(cbmHdr.diskDosVersion == 0x44 &&
               cbmHdr is { dosVersion: 0x33, diskVersion: 0x44 })
                return true;
        }
        else
        {
            ErrorNumber errno = imagePlugin.ReadSector(357, out sector);

            if(errno != ErrorNumber.NoError)
                return false;

            BAM cbmBam = Marshal.ByteArrayToStructureLittleEndian<BAM>(sector);

            if(cbmBam is { dosVersion: 0x41, doubleSided: 0x00 or 0x80 } and { unused1: 0x00, directoryTrack: 0x12 })
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void GetInformation(IMediaImage imagePlugin, Partition partition, out string information, Encoding encoding)
    {
        Encoding    = new PETSCII();
        information = "";
        byte[] sector;

        var sbInformation = new StringBuilder();

        sbInformation.AppendLine(Localization.Commodore_file_system);

        XmlFsType = new FileSystemType
        {
            Type        = FS_TYPE,
            Clusters    = imagePlugin.Info.Sectors,
            ClusterSize = 256
        };

        if(imagePlugin.Info.Sectors == 3200)
        {
            ErrorNumber errno = imagePlugin.ReadSector(1560, out sector);

            if(errno != ErrorNumber.NoError)
                return;

            Header cbmHdr = Marshal.ByteArrayToStructureLittleEndian<Header>(sector);

            sbInformation.AppendFormat(Localization.Directory_starts_at_track_0_sector_1, cbmHdr.directoryTrack,
                                       cbmHdr.directorySector).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_DOS_Version_0, Encoding.ASCII.GetString(new[]
            {
                cbmHdr.diskDosVersion
            })).AppendLine();

            sbInformation.AppendFormat(Localization.DOS_Version_0, Encoding.ASCII.GetString(new[]
            {
                cbmHdr.dosVersion
            })).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_Version_0, Encoding.ASCII.GetString(new[]
            {
                cbmHdr.diskVersion
            })).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_ID_0, cbmHdr.diskId).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_name_0, StringHandlers.CToString(cbmHdr.name, Encoding)).
                          AppendLine();

            XmlFsType.VolumeName   = StringHandlers.CToString(cbmHdr.name, Encoding);
            XmlFsType.VolumeSerial = $"{cbmHdr.diskId}";
        }
        else
        {
            ErrorNumber errno = imagePlugin.ReadSector(357, out sector);

            if(errno != ErrorNumber.NoError)
                return;

            BAM cbmBam = Marshal.ByteArrayToStructureLittleEndian<BAM>(sector);

            sbInformation.AppendFormat(Localization.Directory_starts_at_track_0_sector_1, cbmBam.directoryTrack,
                                       cbmBam.directorySector).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_DOS_type_0,
                                       Encoding.ASCII.GetString(BitConverter.GetBytes(cbmBam.dosType))).AppendLine();

            sbInformation.AppendFormat(Localization.DOS_Version_0, Encoding.ASCII.GetString(new[]
            {
                cbmBam.dosVersion
            })).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_ID_0, cbmBam.diskId).AppendLine();

            sbInformation.AppendFormat(Localization.Disk_name_0, StringHandlers.CToString(cbmBam.name, Encoding)).
                          AppendLine();

            XmlFsType.VolumeName   = StringHandlers.CToString(cbmBam.name, Encoding);
            XmlFsType.VolumeSerial = $"{cbmBam.diskId}";
        }

        information = sbInformation.ToString();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct BAM
    {
        /// <summary>Track where directory starts</summary>
        public readonly byte directoryTrack;
        /// <summary>Sector where directory starts</summary>
        public readonly byte directorySector;
        /// <summary>Disk DOS version, 0x41</summary>
        public readonly byte dosVersion;
        /// <summary>Set to 0x80 if 1571, 0x00 if not</summary>
        public readonly byte doubleSided;
        /// <summary>Block allocation map</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 140)]
        public readonly byte[] bam;
        /// <summary>Disk name</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly byte[] name;
        /// <summary>Filled with 0xA0</summary>
        public readonly ushort fill1;
        /// <summary>Disk ID</summary>
        public readonly ushort diskId;
        /// <summary>Filled with 0xA0</summary>
        public readonly byte fill2;
        /// <summary>DOS type</summary>
        public readonly ushort dosType;
        /// <summary>Filled with 0xA0</summary>
        public readonly uint fill3;
        /// <summary>Unused</summary>
        public readonly byte unused1;
        /// <summary>Block allocation map for Dolphin DOS extended tracks</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public readonly byte[] dolphinBam;
        /// <summary>Block allocation map for Speed DOS extended tracks</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public readonly byte[] speedBam;
        /// <summary>Unused</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public readonly byte[] unused2;
        /// <summary>Free sector count for second side in 1571</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public readonly byte[] freeCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    readonly struct Header
    {
        /// <summary>Track where directory starts</summary>
        public readonly byte directoryTrack;
        /// <summary>Sector where directory starts</summary>
        public readonly byte directorySector;
        /// <summary>Disk DOS version, 0x44</summary>
        public readonly byte diskDosVersion;
        /// <summary>Unusued</summary>
        public readonly byte unused1;
        /// <summary>Disk name</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly byte[] name;
        /// <summary>Filled with 0xA0</summary>
        public readonly ushort fill1;
        /// <summary>Disk ID</summary>
        public readonly ushort diskId;
        /// <summary>Filled with 0xA0</summary>
        public readonly byte fill2;
        /// <summary>DOS version ('3')</summary>
        public readonly byte dosVersion;
        /// <summary>Disk version ('D')</summary>
        public readonly byte diskVersion;
        /// <summary>Filled with 0xA0</summary>
        public readonly short fill3;
    }
}