using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Auralis.Tools.AssetPipeline.Interchange.FBX;

public class FBXHeader
{
    public byte[] Magic { get; set; }
    public byte[] Reserved { get; set; }
    public uint Version { get; set; }
}
