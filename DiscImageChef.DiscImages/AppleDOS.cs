// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : AppleDOS.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Disk image plugins.
//
// --[ Description ] ----------------------------------------------------------
//
//     Manages interleaved Apple ][ disk images.
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
// Copyright © 2011-2018 Natalia Portillo
// ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using DiscImageChef.CommonTypes;
using DiscImageChef.Filters;

namespace DiscImageChef.DiscImages
{
    // Checked using several images and strings inside Apple's DiskImages.framework
    public class AppleDos : ImagePlugin
    {
        readonly int[] dosOffsets = {0, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 15};
        readonly int[] prodosOffsets = {0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15};

        public AppleDos()
        {
            Name = "Apple ][ Interleaved Disk Image";
            PluginUuid = new Guid("A5828AC0-62C9-4304-81D4-EFD4AAE47360");
            ImageInfo = new ImageInfo
            {
                ReadableSectorTags = new List<SectorTagType>(),
                ReadableMediaTags = new List<MediaTagType>(),
                ImageHasPartitions = false,
                ImageHasSessions = false,
                ImageVersion = null,
                ImageApplication = null,
                ImageApplicationVersion = null,
                ImageCreator = null,
                ImageComments = null,
                MediaManufacturer = null,
                MediaModel = null,
                MediaSerialNumber = null,
                MediaBarcode = null,
                MediaPartNumber = null,
                MediaSequence = 0,
                LastMediaSequence = 0,
                DriveManufacturer = null,
                DriveModel = null,
                DriveSerialNumber = null,
                DriveFirmwareRevision = null
            };
        }

        public override bool IdentifyImage(Filter imageFilter)
        {
            extension = Path.GetExtension(imageFilter.GetFilename())?.ToLower();

            return imageFilter.GetDataForkLength() == 143360 && (extension == ".po" || extension == ".do");
        }

        public override bool OpenImage(Filter imageFilter)
        {
            Stream stream = imageFilter.GetDataForkStream();
            stream.Seek(0, SeekOrigin.Begin);

            byte[] tmp = new byte[imageFilter.GetDataForkLength()];
            stream.Read(tmp, 0, tmp.Length);

            deinterleaved = new byte[tmp.Length];

            extension = Path.GetExtension(imageFilter.GetFilename())?.ToLower();

            int[] offsets = extension == ".do" ? dosOffsets : prodosOffsets;

            for(int t = 0; t < 35; t++)
            {
                for(int s = 0; s < 16; s++)
                    Array.Copy(tmp, t * 16 * 256 + s * 256, deinterleaved, t * 16 * 256 + offsets[s] * 256, 256);
            }

            ImageInfo.SectorSize = 256;
            ImageInfo.ImageSize = (ulong)imageFilter.GetDataForkLength();
            ImageInfo.ImageCreationTime = imageFilter.GetCreationTime();
            ImageInfo.ImageLastModificationTime = imageFilter.GetLastWriteTime();
            ImageInfo.ImageName = Path.GetFileNameWithoutExtension(imageFilter.GetFilename());
            ImageInfo.Sectors = 560;
            ImageInfo.MediaType = MediaType.Apple33SS;
            ImageInfo.XmlMediaType = XmlMediaType.BlockMedia;
            ImageInfo.Cylinders = 35;
            ImageInfo.Heads = 2;
            ImageInfo.SectorsPerTrack = 16;

            return true;
        }

        public override bool ImageHasPartitions()
        {
            return ImageInfo.ImageHasPartitions;
        }

        public override ulong GetImageSize()
        {
            return ImageInfo.ImageSize;
        }

        public override ulong GetSectors()
        {
            return ImageInfo.Sectors;
        }

        public override uint GetSectorSize()
        {
            return ImageInfo.SectorSize;
        }

        public override byte[] ReadSector(ulong sectorAddress)
        {
            return ReadSectors(sectorAddress, 1);
        }

        public override byte[] ReadSectors(ulong sectorAddress, uint length)
        {
            if(sectorAddress > ImageInfo.Sectors - 1)
                throw new ArgumentOutOfRangeException(nameof(sectorAddress), "Sector address not found");

            if(sectorAddress + length > ImageInfo.Sectors)
                throw new ArgumentOutOfRangeException(nameof(length), "Requested more sectors than available");

            byte[] buffer = new byte[length * ImageInfo.SectorSize];

            Array.Copy(deinterleaved, (int)(sectorAddress * ImageInfo.SectorSize), buffer, 0, buffer.Length);

            return buffer;
        }

        public override string GetImageFormat()
        {
            return extension == ".po" ? "Apple ][ Interleaved Disk Image (ProDOS order)" : "Apple ][ Interleaved Disk Image (DOS order)";
        }

        public override DateTime GetImageCreationTime()
        {
            return ImageInfo.ImageCreationTime;
        }

        public override DateTime GetImageLastModificationTime()
        {
            return ImageInfo.ImageLastModificationTime;
        }

        public override string GetImageName()
        {
            return ImageInfo.ImageName;
        }

        public override MediaType GetMediaType()
        {
            return ImageInfo.MediaType;
        }

        public override bool? VerifySector(ulong sectorAddress)
        {
            return null;
        }

        public override bool? VerifySector(ulong sectorAddress, uint track)
        {
            return null;
        }

        public override bool? VerifySectors(ulong sectorAddress, uint length, out List<ulong> failingLbas,
                                            out List<ulong> unknownLbas)
        {
            failingLbas = new List<ulong>();
            unknownLbas = new List<ulong>();

            for(ulong i = sectorAddress; i < sectorAddress + length; i++) unknownLbas.Add(i);

            return null;
        }

        public override bool? VerifySectors(ulong sectorAddress, uint length, uint track, out List<ulong> failingLbas,
                                            out List<ulong> unknownLbas)
        {
            failingLbas = new List<ulong>();
            unknownLbas = new List<ulong>();

            for(ulong i = sectorAddress; i < sectorAddress + length; i++) unknownLbas.Add(i);

            return null;
        }

        public override bool? VerifyMediaImage()
        {
            return null;
        }

        public override List<Track> GetTracks()
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override List<Track> GetSessionTracks(Session session)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override List<Track> GetSessionTracks(ushort session)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override List<Session> GetSessions()
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSector(ulong sectorAddress, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectors(ulong sectorAddress, uint length, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorLong(ulong sectorAddress, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsLong(ulong sectorAddress, uint length, uint track)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        #region Internal variables
        byte[] deinterleaved;
        string extension;
        #endregion

        #region Unsupported features
        public override byte[] ReadSectorTag(ulong sectorAddress, SectorTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsTag(ulong sectorAddress, uint length, SectorTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorLong(ulong sectorAddress)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsLong(ulong sectorAddress, uint length)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override string GetImageVersion()
        {
            return ImageInfo.ImageVersion;
        }

        public override string GetImageApplication()
        {
            return ImageInfo.ImageApplication;
        }

        public override string GetImageApplicationVersion()
        {
            return ImageInfo.ImageApplicationVersion;
        }

        public override byte[] ReadDiskTag(MediaTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override string GetImageCreator()
        {
            return ImageInfo.ImageCreator;
        }

        public override string GetImageComments()
        {
            return ImageInfo.ImageComments;
        }

        public override string GetMediaManufacturer()
        {
            return ImageInfo.MediaManufacturer;
        }

        public override string GetMediaModel()
        {
            return ImageInfo.MediaModel;
        }

        public override string GetMediaSerialNumber()
        {
            return ImageInfo.MediaSerialNumber;
        }

        public override string GetMediaBarcode()
        {
            return ImageInfo.MediaBarcode;
        }

        public override string GetMediaPartNumber()
        {
            return ImageInfo.MediaPartNumber;
        }

        public override int GetMediaSequence()
        {
            return ImageInfo.MediaSequence;
        }

        public override int GetLastDiskSequence()
        {
            return ImageInfo.LastMediaSequence;
        }

        public override string GetDriveManufacturer()
        {
            return ImageInfo.DriveManufacturer;
        }

        public override string GetDriveModel()
        {
            return ImageInfo.DriveModel;
        }

        public override string GetDriveSerialNumber()
        {
            return ImageInfo.DriveSerialNumber;
        }

        public override List<Partition> GetPartitions()
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorTag(ulong sectorAddress, uint track, SectorTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }

        public override byte[] ReadSectorsTag(ulong sectorAddress, uint length, uint track, SectorTagType tag)
        {
            throw new FeatureUnsupportedImageException("Feature not supported by image format");
        }
        #endregion
    }
}