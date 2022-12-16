﻿// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Audio.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Core algorithms.
//
// --[ Description ] ----------------------------------------------------------
//
//     Contains logic to create sidecar from an audio media dump.
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
using System.IO;
using System.Text;
using Aaru.CommonTypes;
using Aaru.CommonTypes.AaruMetadata;
using Aaru.CommonTypes.Interfaces;

namespace Aaru.Core;

public sealed partial class Sidecar
{
    // TODO: Complete it
    /// <summary>Creates a metadata sidecar for an audio media (e.g. cassette)</summary>
    /// <param name="image">Image</param>
    /// <param name="filterId">Filter uuid</param>
    /// <param name="imagePath">Image path</param>
    /// <param name="fi">Image file information</param>
    /// <param name="plugins">Image plugins</param>
    /// <param name="imgChecksums">List of image checksums</param>
    /// <param name="sidecar">Metadata sidecar</param>
    /// <param name="encoding">Encoding to be used for filesystem plugins</param>
    static void AudioMedia(IBaseImage image, Guid filterId, string imagePath, FileInfo fi, PluginBase plugins,
                           List<CommonTypes.AaruMetadata.Checksum> imgChecksums, ref Metadata sidecar,
                           Encoding encoding)
    {
        sidecar.AudioMedias = new List<AudioMedia>
        {
            new()
            {
                Checksums = imgChecksums,
                Image = new Image
                {
                    Format = image.Format,
                    Offset = 0,
                    Value  = Path.GetFileName(imagePath)
                },
                Size = (ulong)fi.Length,
                Sequence = new Sequence
                {
                    Title = image.Info.MediaTitle
                }
            }
        };

        if(image.Info.MediaSequence     != 0 &&
           image.Info.LastMediaSequence != 0)
        {
            sidecar.AudioMedias[0].Sequence.MediaSequence = (uint)image.Info.MediaSequence;
            sidecar.AudioMedias[0].Sequence.TotalMedia    = (uint)image.Info.LastMediaSequence;
        }
        else
        {
            sidecar.AudioMedias[0].Sequence.MediaSequence = 1;
            sidecar.AudioMedias[0].Sequence.TotalMedia    = 1;
        }
    }
}