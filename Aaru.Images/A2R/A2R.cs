// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : A2R.cs
// Author(s)      : Rebecca Wallander <sakcheen@gmail.com>
//
// Component      : Disk image plugins.
//
// --[ Description ] ----------------------------------------------------------
//
//     Manages A2R flux images.
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
// Copyright © 2011-2023 Rebecca Wallander
// ****************************************************************************/

// Version 2 documentation: https://web.archive.org/web/20200325131633/https://applesaucefdc.com/a2r/
// Version 3 documentation: https://web.archive.org/web/20220526215820/https://applesaucefdc.com/a2r/

using System.Collections.Generic;
using System.IO;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.CommonTypes.Structs;

namespace Aaru.DiscImages;

/// <inheritdoc cref="Aaru.CommonTypes.Interfaces.IMediaImage" />
/// <summary>Implements reading A2R flux images</summary>
public sealed partial class A2R : IFluxImage, IMediaImage, IWritableImage, IWritableFluxImage
{
    ImageInfo                  _imageInfo;
    Stream                     _a2rStream;
    IFilter                    _a2rFilter;
    FileStream                 _writingStream;
    A2rHeader                  Header;
    Dictionary<string, string> Meta;
    InfoChunkV2                _infoChunkV2;
    InfoChunkV3                _infoChunkV3;
    List<StreamCapture>        _a2rCaptures;
    uint                       _currentResolution;

    // Offset from the start of the current RWCP to the next capture
    uint _currentCaptureOffset = 16;

    // 53 = A2R header, INFO header, INFO data
    long _currentRwcpStart = 53;

    public A2R()
    {
        _imageInfo = new ImageInfo
        {
            ReadableSectorTags    = new List<SectorTagType>(),
            ReadableMediaTags     = new List<MediaTagType>(),
            HasPartitions         = false,
            HasSessions           = false,
            Version               = null,
            Application           = null,
            ApplicationVersion    = null,
            Creator               = null,
            Comments              = null,
            MediaManufacturer     = null,
            MediaModel            = null,
            MediaSerialNumber     = null,
            MediaBarcode          = null,
            MediaPartNumber       = null,
            MediaSequence         = 0,
            LastMediaSequence     = 0,
            DriveManufacturer     = null,
            DriveModel            = null,
            DriveSerialNumber     = null,
            DriveFirmwareRevision = null
        };
    }
}