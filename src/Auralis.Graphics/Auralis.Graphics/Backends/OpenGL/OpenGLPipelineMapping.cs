using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

internal readonly record struct OpenGLVertexFormat(
    int ComponentCount,
    VertexAttribType Type,
    VertexAttribIType IntegerType,
    bool IsInteger,
    bool Normalized);

internal static class OpenGLPipelineMapping
{
    internal static PrimitiveType ToPrimitiveType(GraphicsPrimitiveTopology topology) => topology switch
    {
        GraphicsPrimitiveTopology.PointList => PrimitiveType.Points,
        GraphicsPrimitiveTopology.LineList => PrimitiveType.Lines,
        GraphicsPrimitiveTopology.LineStrip => PrimitiveType.LineStrip,
        GraphicsPrimitiveTopology.TriangleList => PrimitiveType.Triangles,
        GraphicsPrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
        GraphicsPrimitiveTopology.TriangleFan => PrimitiveType.TriangleFan,
        GraphicsPrimitiveTopology.LineListAdjacency => PrimitiveType.LinesAdjacency,
        GraphicsPrimitiveTopology.LineStripAdjacency => PrimitiveType.LineStripAdjacency,
        GraphicsPrimitiveTopology.TriangleListAdjacency => PrimitiveType.TrianglesAdjacency,
        GraphicsPrimitiveTopology.TriangleStripAdjacency => PrimitiveType.TriangleStripAdjacency,
        GraphicsPrimitiveTopology.PatchList => PrimitiveType.Patches,
        _ => throw new ArgumentOutOfRangeException(nameof(topology))
    };

    internal static DepthFunction ToDepthFunction(GraphicsCompareOperation operation) => operation switch
    {
        GraphicsCompareOperation.Never => DepthFunction.Never,
        GraphicsCompareOperation.Less => DepthFunction.Less,
        GraphicsCompareOperation.Equal => DepthFunction.Equal,
        GraphicsCompareOperation.LessEqual => DepthFunction.Lequal,
        GraphicsCompareOperation.Greater => DepthFunction.Greater,
        GraphicsCompareOperation.NotEqual => DepthFunction.Notequal,
        GraphicsCompareOperation.GreaterEqual => DepthFunction.Gequal,
        GraphicsCompareOperation.Always => DepthFunction.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    internal static StencilOp ToStencilOperation(GraphicsStencilOperation operation) => operation switch
    {
        GraphicsStencilOperation.Keep => StencilOp.Keep,
        GraphicsStencilOperation.Zero => StencilOp.Zero,
        GraphicsStencilOperation.Replace => StencilOp.Replace,
        GraphicsStencilOperation.IncrementClamp => StencilOp.Incr,
        GraphicsStencilOperation.DecrementClamp => StencilOp.Decr,
        GraphicsStencilOperation.Invert => StencilOp.Invert,
        GraphicsStencilOperation.IncrementWrap => StencilOp.IncrWrap,
        GraphicsStencilOperation.DecrementWrap => StencilOp.DecrWrap,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    internal static BlendingFactor ToBlendFactor(GraphicsBlendFactor factor) => factor switch
    {
        GraphicsBlendFactor.Zero => BlendingFactor.Zero,
        GraphicsBlendFactor.One => BlendingFactor.One,
        GraphicsBlendFactor.SourceColor => BlendingFactor.SrcColor,
        GraphicsBlendFactor.OneMinusSourceColor => BlendingFactor.OneMinusSrcColor,
        GraphicsBlendFactor.DestinationColor => BlendingFactor.DstColor,
        GraphicsBlendFactor.OneMinusDestinationColor => BlendingFactor.OneMinusDstColor,
        GraphicsBlendFactor.SourceAlpha => BlendingFactor.SrcAlpha,
        GraphicsBlendFactor.OneMinusSourceAlpha => BlendingFactor.OneMinusSrcAlpha,
        GraphicsBlendFactor.DestinationAlpha => BlendingFactor.DstAlpha,
        GraphicsBlendFactor.OneMinusDestinationAlpha => BlendingFactor.OneMinusDstAlpha,
        GraphicsBlendFactor.ConstantColor => BlendingFactor.ConstantColor,
        GraphicsBlendFactor.OneMinusConstantColor => BlendingFactor.OneMinusConstantColor,
        GraphicsBlendFactor.ConstantAlpha => BlendingFactor.ConstantAlpha,
        GraphicsBlendFactor.OneMinusConstantAlpha => BlendingFactor.OneMinusConstantAlpha,
        GraphicsBlendFactor.SourceAlphaSaturate => BlendingFactor.SrcAlphaSaturate,
        _ => throw new ArgumentOutOfRangeException(nameof(factor))
    };

    internal static BlendEquationModeEXT ToBlendEquation(GraphicsBlendOperation operation) => operation switch
    {
        GraphicsBlendOperation.Add => BlendEquationModeEXT.FuncAdd,
        GraphicsBlendOperation.Subtract => BlendEquationModeEXT.FuncSubtract,
        GraphicsBlendOperation.ReverseSubtract => BlendEquationModeEXT.FuncReverseSubtract,
        GraphicsBlendOperation.Minimum => BlendEquationModeEXT.Min,
        GraphicsBlendOperation.Maximum => BlendEquationModeEXT.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    internal static uint GetVertexFormatByteSize(GraphicsFormat format) => format switch
    {
        GraphicsFormat.R8UNorm or GraphicsFormat.R8SNorm or GraphicsFormat.R8UInt or GraphicsFormat.R8SInt => 1,
        GraphicsFormat.RG8UNorm or GraphicsFormat.RG8SNorm or GraphicsFormat.RG8UInt or GraphicsFormat.RG8SInt => 2,
        GraphicsFormat.RGBA8UNorm or GraphicsFormat.RGBA8UNormSrgb or GraphicsFormat.RGBA8SNorm or
            GraphicsFormat.RGBA8UInt or GraphicsFormat.RGBA8SInt => 4,
        GraphicsFormat.R16Float or GraphicsFormat.R16UInt or GraphicsFormat.R16SInt => 2,
        GraphicsFormat.RG16Float or GraphicsFormat.RG16UInt or GraphicsFormat.RG16SInt => 4,
        GraphicsFormat.RGBA16Float or GraphicsFormat.RGBA16UInt or GraphicsFormat.RGBA16SInt => 8,
        GraphicsFormat.R32Float or GraphicsFormat.R32UInt or GraphicsFormat.R32SInt => 4,
        GraphicsFormat.RG32Float or GraphicsFormat.RG32UInt or GraphicsFormat.RG32SInt => 8,
        GraphicsFormat.RGB32Float => 12,
        GraphicsFormat.RGBA32Float or GraphicsFormat.RGBA32UInt or GraphicsFormat.RGBA32SInt => 16,
        _ => 0
    };

    internal static bool TryGetVertexFormat(GraphicsFormat format, out OpenGLVertexFormat result)
    {
        result = format switch
        {
            GraphicsFormat.R8UNorm => new(1, VertexAttribType.UnsignedByte, VertexAttribIType.UnsignedByte, false, true),
            GraphicsFormat.R8SNorm => new(1, VertexAttribType.Byte, VertexAttribIType.Byte, false, true),
            GraphicsFormat.R8UInt => new(1, VertexAttribType.UnsignedByte, VertexAttribIType.UnsignedByte, true, false),
            GraphicsFormat.R8SInt => new(1, VertexAttribType.Byte, VertexAttribIType.Byte, true, false),
            GraphicsFormat.RG8UNorm => new(2, VertexAttribType.UnsignedByte, VertexAttribIType.UnsignedByte, false, true),
            GraphicsFormat.RG8SNorm => new(2, VertexAttribType.Byte, VertexAttribIType.Byte, false, true),
            GraphicsFormat.RG8UInt => new(2, VertexAttribType.UnsignedByte, VertexAttribIType.UnsignedByte, true, false),
            GraphicsFormat.RG8SInt => new(2, VertexAttribType.Byte, VertexAttribIType.Byte, true, false),
            GraphicsFormat.RGBA8UNorm or GraphicsFormat.RGBA8UNormSrgb => new(4, VertexAttribType.UnsignedByte, VertexAttribIType.UnsignedByte, false, true),
            GraphicsFormat.RGBA8SNorm => new(4, VertexAttribType.Byte, VertexAttribIType.Byte, false, true),
            GraphicsFormat.RGBA8UInt => new(4, VertexAttribType.UnsignedByte, VertexAttribIType.UnsignedByte, true, false),
            GraphicsFormat.RGBA8SInt => new(4, VertexAttribType.Byte, VertexAttribIType.Byte, true, false),
            GraphicsFormat.R16Float => new(1, VertexAttribType.HalfFloat, VertexAttribIType.Short, false, false),
            GraphicsFormat.R16UInt => new(1, VertexAttribType.UnsignedShort, VertexAttribIType.UnsignedShort, true, false),
            GraphicsFormat.R16SInt => new(1, VertexAttribType.Short, VertexAttribIType.Short, true, false),
            GraphicsFormat.RG16Float => new(2, VertexAttribType.HalfFloat, VertexAttribIType.Short, false, false),
            GraphicsFormat.RG16UInt => new(2, VertexAttribType.UnsignedShort, VertexAttribIType.UnsignedShort, true, false),
            GraphicsFormat.RG16SInt => new(2, VertexAttribType.Short, VertexAttribIType.Short, true, false),
            GraphicsFormat.RGBA16Float => new(4, VertexAttribType.HalfFloat, VertexAttribIType.Short, false, false),
            GraphicsFormat.RGBA16UInt => new(4, VertexAttribType.UnsignedShort, VertexAttribIType.UnsignedShort, true, false),
            GraphicsFormat.RGBA16SInt => new(4, VertexAttribType.Short, VertexAttribIType.Short, true, false),
            GraphicsFormat.R32Float => new(1, VertexAttribType.Float, VertexAttribIType.Int, false, false),
            GraphicsFormat.R32UInt => new(1, VertexAttribType.UnsignedInt, VertexAttribIType.UnsignedInt, true, false),
            GraphicsFormat.R32SInt => new(1, VertexAttribType.Int, VertexAttribIType.Int, true, false),
            GraphicsFormat.RG32Float => new(2, VertexAttribType.Float, VertexAttribIType.Int, false, false),
            GraphicsFormat.RG32UInt => new(2, VertexAttribType.UnsignedInt, VertexAttribIType.UnsignedInt, true, false),
            GraphicsFormat.RG32SInt => new(2, VertexAttribType.Int, VertexAttribIType.Int, true, false),
            GraphicsFormat.RGB32Float => new(3, VertexAttribType.Float, VertexAttribIType.Int, false, false),
            GraphicsFormat.RGBA32Float => new(4, VertexAttribType.Float, VertexAttribIType.Int, false, false),
            GraphicsFormat.RGBA32UInt => new(4, VertexAttribType.UnsignedInt, VertexAttribIType.UnsignedInt, true, false),
            GraphicsFormat.RGBA32SInt => new(4, VertexAttribType.Int, VertexAttribIType.Int, true, false),
            _ => default
        };

        return result.ComponentCount != 0;
    }
}
