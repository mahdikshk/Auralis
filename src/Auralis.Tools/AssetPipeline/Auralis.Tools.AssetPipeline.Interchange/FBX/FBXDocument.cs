using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace Auralis.Tools.AssetPipeline.Interchange.FBX;

public sealed class FBXDocument
{
    public FBXHeader Header { get; set; }


    private FBXDocument()
    {

    }




    public static async ValueTask<FBXDocument> ParseTextAsync(Stream stream)
    {

        return new();
    }
    public static async ValueTask<FBXDocument> ParseBinaryAsync(Stream stream)
    {
        
        return new();
    }

    public static async ValueTask<FBXDocument> ParseTextAsync(ReadOnlyMemory<byte> data)
    {
        return new();
    }

    public static async ValueTask<FBXDocument> ParseBinaryAsync(ReadOnlyMemory<byte> data)
    {
        return new();
    }

    public static async ValueTask<FBXDocument> ParseAsync(Stream stream)
    {
        return new();
    }

    public static async ValueTask<FBXDocument> ParseAsync(ReadOnlyMemory<byte> data)
    {
        return new();
    }
}
