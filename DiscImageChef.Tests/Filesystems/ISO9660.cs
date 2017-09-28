﻿// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : ISO9660.cs
// Version        : 1.0
// Author(s)      : Natalia Portillo
//
// Component      : Component
//
// Revision       : $Revision$
// Last change by : $Author$
// Date           : $Date$
//
// --[ Description ] ----------------------------------------------------------
//
// Description
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
// Copyright (C) 2011-2015 Claunia.com
// ****************************************************************************/
// //$Id$
using System.IO;
using DiscImageChef.CommonTypes;
using DiscImageChef.Filesystems;
using DiscImageChef.Filters;
using DiscImageChef.ImagePlugins;
using NUnit.Framework;

namespace DiscImageChef.Tests.Filesystems
{
    [TestFixture]
    public class ISO9660
    {
        readonly string[] testfiles = {
            // Toast 3.5.7
            "toast_3.5.7_iso9660_apple.iso.lz", "toast_3.5.7_iso9660_dos_apple.iso.lz", "toast_3.5.7_iso9660_dos.iso.lz", "toast_3.5.7_iso9660_hfs.iso.lz",
            "toast_3.5.7_iso9660.iso.lz", "toast_3.5.7_iso9660_joliet_apple.iso.lz", "toast_3.5.7_iso9660_joliet.iso.lz", "toast_3.5.7_iso9660_mac_apple.iso.lz",
            "toast_3.5.7_iso9660_mac.iso.lz", "toast_3.5.7_iso9660_ver_apple.iso.lz", "toast_3.5.7_iso9660_ver_dos_apple.iso.lz", "toast_3.5.7_iso9660_ver_dos.iso.lz",
            "toast_3.5.7_iso9660_ver.iso.lz", "toast_3.5.7_iso9660_ver_joliet_apple.iso.lz", "toast_3.5.7_iso9660_ver_joliet.iso.lz", "toast_3.5.7_iso9660.iso.lz",
            // Toast 4.1.3
            "toast_4.1.3_iso9660_hfs.iso.lz",
        };

        readonly MediaType[] mediatypes = {
            MediaType.CD, MediaType.CD, MediaType.CD, MediaType.CD,
            MediaType.CD, MediaType.CD, MediaType.CD, MediaType.CD,
            MediaType.CD, MediaType.CD, MediaType.CD, MediaType.CD,
            MediaType.CD, MediaType.CD, MediaType.CD, MediaType.CD,
            MediaType.CD,
        };

        readonly ulong[] sectors = {
            946, 946, 300, 1880,
            300, 951, 300, 946,
            300, 946, 946, 300,
            300, 951, 300, 300,
            1882,
        };

        readonly uint[] sectorsize = {
            2048, 2048, 2048, 2048,
            2048, 2048, 2048, 2048,
            2048, 2048, 2048, 2048,
            2048, 2048, 2048, 2048,
            2048,
        };

        readonly long[] clusters = {
            946, 946, 300, 1880,
            300, 951, 300, 946,
            300, 946, 946, 300,
            300, 951, 300, 300,
            1882,
        };

        readonly int[] clustersize = {
            2048, 2048, 2048, 2048,
            2048, 2048, 2048, 2048,
            2048, 2048, 2048, 2048,
            2048, 2048, 2048, 2048,
            2048,
        };

        readonly string[] volumename = {
            "DISK_UTILS", "DISK_UTILS", "DISK_UTILS", "DISK_UTILS",
            "DISK_UTILS", "Disk utils", "Disk utils", "Disk utils",
            "Disk utils", "DISK_UTILS", "DISK_UTILS", "DISK_UTILS",
            "DISK_UTILS", "Disk utils", "Disk utils", "DISK_UTILS",
            "DISK_UTILS",
        };

        readonly string[] volumeserial = {
            null, null, null, null,
            null, null, null, null,
            null, null, null, null,
            null, null, null, null,
            null,
        };

        readonly string[] sysid = {
            "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002",
            "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002",
            "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002",
            "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002", "APPLE COMPUTER, INC., TYPE: 0002",
            "APPLE COMPUTER, INC., TYPE: 0002",
        };

        readonly string[] appid = {
            "TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY",
            "TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY",
            "TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY",
            "TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY","TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY",
            "TOAST ISO 9660 BUILDER COPYRIGHT (C) 1997 ADAPTEC, INC. - HAVE A NICE DAY",
        };

        [Test]
        public void Test()
        {
            for(int i = 0; i < testfiles.Length; i++)
            {
                string location = Path.Combine(Consts.TestFilesRoot, "filesystems", "iso9660", testfiles[i]);
                Filter filter = new LZip();
                filter.Open(location);
                ImagePlugin image = new ZZZRawImage();
                Assert.AreEqual(true, image.OpenImage(filter), testfiles[i]);
                Assert.AreEqual(mediatypes[i], image.ImageInfo.mediaType, testfiles[i]);
                Assert.AreEqual(sectors[i], image.ImageInfo.sectors, testfiles[i]);
                Assert.AreEqual(sectorsize[i], image.ImageInfo.sectorSize, testfiles[i]);
                Filesystem fs = new ISO9660Plugin();
                Partition wholePart = new Partition
                {
                    Name = "Whole device",
                    Length = image.ImageInfo.sectors,
                    Size = image.ImageInfo.sectors * image.ImageInfo.sectorSize
                };
                Assert.AreEqual(true, fs.Identify(image, wholePart), testfiles[i]);
                fs.GetInformation(image, wholePart, out string information);
                Assert.AreEqual(clusters[i], fs.XmlFSType.Clusters, testfiles[i]);
                Assert.AreEqual(clustersize[i], fs.XmlFSType.ClusterSize, testfiles[i]);
                Assert.AreEqual("ISO9660", fs.XmlFSType.Type, testfiles[i]);
                Assert.AreEqual(volumename[i], fs.XmlFSType.VolumeName, testfiles[i]);
                Assert.AreEqual(volumeserial[i], fs.XmlFSType.VolumeSerial, testfiles[i]);
                Assert.AreEqual(sysid[i], fs.XmlFSType.SystemIdentifier, testfiles[i]);
                Assert.AreEqual(appid[i], fs.XmlFSType.ApplicationIdentifier, testfiles[i]);
            }
        }
    }
}
