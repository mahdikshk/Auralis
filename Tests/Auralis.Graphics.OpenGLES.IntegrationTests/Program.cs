using System.Text;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Backends.OpenGLES;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Auralis.Graphics.OpenGL.TestSupport;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.OpenGLES.IntegrationTests;

internal static class Program
{
    private const string RunVariable = "AURALIS_RUN_OPENGLES_HARDWARE_TESTS";

    private static async Task<int> Main()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.Ordinal))
        {
            Console.WriteLine($"SKIP: set {RunVariable}=1 to execute tests against an actual OpenGL ES 3.1+ context.");
            return 77;
        }

        GlfwOpenGLESContextProvider context;
        try
        {
            context = GlfwOpenGLESContextProvider.Create();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"SKIP: GLFW could not create a host OpenGL ES context: {exception.Message}");
            return 77;
        }

        IGraphicsDevice? device = null;
        try
        {
            GraphicsBackendProviderRegistry registry = new();
            registry.Register(new OpenGLESGraphicsBackendProvider(context));

            device = await GraphicsDeviceFactory.CreateAsync(
                registry,
                new GraphicsDeviceOptions
                {
                    Backend = GraphicsBackend.OpenGLES,
                    RequiredQueues = GraphicsQueueCapabilities.Graphics | GraphicsQueueCapabilities.Compute
                });

            Console.WriteLine($"OpenGL ES driver: {device.Info.Name}; API: {device.Info.ApiVersion}; driver: {device.Info.DriverVersion}");

            int passed = 0;
            int skipped = 0;
            int failed = 0;

            void RunTest(string name, string requirements, Action test)
            {
                try
                {
                    test();
                    passed++;
                    Console.WriteLine($"PASS: {name} (requires {requirements}).");
                }
                catch (Exception exception)
                {
                    failed++;
                    Console.Error.WriteLine($"FAIL: {name} (requires {requirements}): {exception}");
                }
            }

            void SkipTest(string name, string requirements, string reason)
            {
                skipped++;
                Console.WriteLine($"SKIP: {name} (requires {requirements}): {reason}");
            }

            RunTest("buffer copy, mapping, and fence", "OpenGL ES 3.1", () => RunBufferCopyAndFence(device));
            RunTest("compute dispatch and SSBO readback", "OpenGL ES 3.1", () => RunComputeDispatch(device));
            RunTest("descriptor types, recording snapshots, and GPU output", "OpenGL ES 3.1", () => RunComputeDescriptorTypesAndSnapshots(device, context));
            RunTest("compute pipeline-layout switching", "OpenGL ES 3.1", () => RunComputePipelineLayoutSwitch(device));
            RunTest("texture and sampler allocation", "OpenGL ES 3.1", () => RunTextureAndSamplerResources(device));
            RunTest("resource retention after recording", "OpenGL ES 3.1", () => RunResourceRetentionAfterRecording(device));
            RunTest("reusable command-list resubmission", "OpenGL ES 3.1", () => RunReusableCommandListAfterOwnerDisposal(device));
            RunTest("offscreen graphics pipeline", "OpenGL ES 3.1", () => RunOffscreenGraphicsPipeline(device, context));
            RunTest("MRT rendering and attachment ordering", "OpenGL ES 3.1, at least two color attachments", () => RunMrtRendering(device, context));
            RunTest("typed signed/unsigned integer color clears", "OpenGL ES 3.1 integer color attachments", () => RunIntegerColorClears(device, context));
            RunTest("multi-command array and indexed indirect drawing", "OpenGL ES 3.1 indirect drawing", () => RunMultiDrawIndirect(device, context));

            uint multisampleCount =
                device.Capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UNorm, 4) ? 4u :
                device.Capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UNorm, 2) ? 2u : 1u;
            if (multisampleCount > 1)
                RunTest("multisample framebuffer clear and shader sample readback", $"RGBA8 {multisampleCount}x sample support", () => RunMultisampleFramebuffer(device, context, multisampleCount));
            else
                SkipTest("multisample framebuffer clear and shader sample readback", "RGBA8 multisample sample-count support", "the driver reports only single-sample RGBA8 storage");

            Console.WriteLine($"SUMMARY: {passed} passed, {skipped} skipped, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }
        catch (GraphicsBackendUnavailableException exception)
        {
            Console.WriteLine($"SKIP: the created context does not meet the OpenGL ES backend requirements: {exception.Message}");
            return 77;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: OpenGL ES hardware integration failed: {exception}");
            return 1;
        }
        finally
        {
            if (device is not null)
                device.Dispose();
            else
                context.Dispose();
        }
    }

    private static void RunMrtRendering(IGraphicsDevice device, GlfwOpenGLESContextProvider context)
    {
        if (device.Capabilities.Limits.MaxColorAttachments < 2 ||
            device.Capabilities.Limits.MaxColorDrawBuffers < 2)
            throw new GraphicsValidationException("The GLES driver does not expose at least two color attachments and draw buffers.");

        using IGraphicsTexture first = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 1, 1, GraphicsTextureUsage.ColorAttachment | GraphicsTextureUsage.Sampled));
        using IGraphicsTexture second = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 1, 1, GraphicsTextureUsage.ColorAttachment | GraphicsTextureUsage.Sampled));
        using IGraphicsTextureView firstView = first.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsTextureView secondView = second.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));

        IGraphicsTextureView[] attachments = [firstView, secondView];
        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Graphics);
        using (IGraphicsCommandList clear = queue.CreateCommandList())
        using (IGraphicsFence fence = device.CreateFence())
        {
            clear.Begin();
            clear.SetRenderTargets(attachments);
            clear.SetViewport(new GraphicsViewport(0, 0, 64, 64));
            clear.SetScissor(new GraphicsScissorRect(0, 0, 64, 64));
            clear.ClearColor(0, new GraphicsClearColor(1, 0, 0, 1));
            clear.ClearColor(1, new GraphicsClearColor(0, 1, 0, 1));
            clear.End();
            queue.Submit([clear], fence, 1);
            if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The GLES MRT clear fence did not complete.");
        }

        AssertPixelNear(ReadRgba8Pixel(device, context, firstView), 255, 0, 0, 255);
        AssertPixelNear(ReadRgba8Pixel(device, context, secondView), 0, 255, 0, 255);

        using IGraphicsPipelineLayout layout = device.CreatePipelineLayout(Array.Empty<IGraphicsBindingLayout>());
        using IGraphicsShader vertex = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Vertex,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                const vec2 positions[3] = vec2[3](
                    vec2(-1.0, -1.0),
                    vec2( 3.0, -1.0),
                    vec2(-1.0,  3.0));
                void main() { gl_Position = vec4(positions[gl_VertexID], 0.0, 1.0); }
                """)
        });
        using IGraphicsShader fragment = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Fragment,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                precision highp float;
                layout(location = 0) out vec4 firstOutput;
                layout(location = 1) out vec4 secondOutput;
                void main()
                {
                    firstOutput = vec4(0.25, 0.5, 0.75, 1.0);
                    secondOutput = vec4(0.75, 0.25, 0.125, 1.0);
                }
                """)
        });
        IGraphicsShader[] shaders = [vertex, fragment];
        GraphicsFormat[] formats = [GraphicsFormat.RGBA8UNorm, GraphicsFormat.RGBA8UNorm];
        using IGraphicsRenderPipeline pipeline = device.CreateRenderPipeline(
            new GraphicsRenderPipelineDescription(
                layout, shaders, GraphicsPrimitiveTopology.TriangleList, formats));

        using (IGraphicsCommandList draw = queue.CreateCommandList())
        using (IGraphicsFence fence = device.CreateFence())
        {
            draw.Begin();
            draw.SetRenderTargets(attachments);
            draw.SetViewport(new GraphicsViewport(0, 0, 64, 64));
            draw.SetScissor(new GraphicsScissorRect(0, 0, 64, 64));
            draw.SetRenderPipeline(pipeline);
            draw.Draw(3);
            draw.End();
            queue.Submit([draw], fence, 1);
            if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The GLES MRT rendering fence did not complete.");
        }

        AssertPixelNear(ReadRgba8Pixel(device, context, firstView), 64, 128, 191, 255);
        AssertPixelNear(ReadRgba8Pixel(device, context, secondView), 191, 64, 32, 255);

        uint rejectedCount = Math.Max(
            device.Capabilities.Limits.MaxColorAttachments,
            device.Capabilities.Limits.MaxColorDrawBuffers) + 1;
        IGraphicsTextureView[] tooMany = new IGraphicsTextureView[checked((int)rejectedCount)];
        Array.Fill(tooMany, firstView);
        using IGraphicsCommandList invalid = queue.CreateCommandList();
        invalid.Begin();
        ExpectThrows<GraphicsValidationException>(
            () => invalid.SetRenderTargets(tooMany),
            "MRT attachment count must respect both queried limits");
        invalid.End();

        Console.WriteLine("Validated MRT attachment-indexed clears, fragment output locations, attachment ordering, GPU pixel readback, and attachment-count rejection.");
    }

    private static void RunIntegerColorClears(IGraphicsDevice device, GlfwOpenGLESContextProvider context)
    {
        using IGraphicsTexture unsignedTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UInt,
            32, 32, 1, 1, 1, 1, GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTexture signedTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8SInt,
            32, 32, 1, 1, 1, 1, GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTexture normalizedTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            32, 32, 1, 1, 1, 1, GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTextureView unsignedView = unsignedTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsTextureView signedView = signedTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsTextureView normalizedView = normalizedTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));

        IGraphicsTextureView[] attachments = [unsignedView, signedView, normalizedView];
        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Graphics);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();
        commandList.Begin();
        commandList.SetRenderTargets(attachments);
        commandList.SetViewport(new GraphicsViewport(0, 0, 32, 32));
        commandList.SetScissor(new GraphicsScissorRect(0, 0, 32, 32));

        ExpectThrows<GraphicsValidationException>(
            () => commandList.ClearColorUInt(1, new GraphicsClearColorUInt(1, 2, 3, 4)),
            "unsigned clear must reject a signed integer attachment");
        ExpectThrows<GraphicsValidationException>(
            () => commandList.ClearColorSInt(0, new GraphicsClearColorSInt(1, 2, 3, 4)),
            "signed clear must reject an unsigned integer attachment");
        ExpectThrows<GraphicsValidationException>(
            () => commandList.ClearColorUInt(2, new GraphicsClearColorUInt(1, 2, 3, 4)),
            "integer clear must reject a normalized attachment");
        ExpectThrows<GraphicsValidationException>(
            () => commandList.ClearColorSInt(2, new GraphicsClearColorSInt(1, 2, 3, 4)),
            "signed integer clear must reject a normalized attachment");
        ExpectThrows<GraphicsValidationException>(
            () => commandList.ClearColor(0, new GraphicsClearColor(1, 1, 1, 1)),
            "floating-point clear must reject an integer attachment");

        commandList.ClearColorUInt(0, new GraphicsClearColorUInt(17, 33, 65, 129));
        commandList.ClearColorSInt(1, new GraphicsClearColorSInt(-11, 22, -33, 44));
        commandList.ClearColor(2, new GraphicsClearColor(0.25f, 0.5f, 0.75f, 1));
        commandList.End();
        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES integer color-clear fence did not complete.");

        uint[] unsigned = ReadIntegerPixel<uint>(device, context, unsignedView, (PixelType)0x1405);
        int[] signed = ReadIntegerPixel<int>(device, context, signedView, (PixelType)0x1404);
        if (!unsigned.AsSpan().SequenceEqual(new uint[] { 17, 33, 65, 129 }))
            throw new InvalidOperationException($"Unsigned integer clear readback was [{string.Join(", ", unsigned)}].");
        if (!signed.AsSpan().SequenceEqual(new int[] { -11, 22, -33, 44 }))
            throw new InvalidOperationException($"Signed integer clear readback was [{string.Join(", ", signed)}].");

        AssertPixelNear(ReadRgba8Pixel(device, context, normalizedView), 64, 128, 191, 255);
        Console.WriteLine("Validated signed and unsigned attachment-indexed integer clears and exact typed GPU readback.");
    }

    private static void RunMultiDrawIndirect(IGraphicsDevice device, GlfwOpenGLESContextProvider context)
    {
        using IGraphicsTexture color = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 1, 1, GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTextureView colorView = color.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsPipelineLayout layout = device.CreatePipelineLayout(Array.Empty<IGraphicsBindingLayout>());
        using IGraphicsShader vertex = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Vertex,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                const vec2 positions[6] = vec2[6](
                    vec2(-1.0, -1.0), vec2( 0.0, -1.0), vec2(-0.5, 1.0),
                    vec2( 0.0, -1.0), vec2( 1.0, -1.0), vec2( 0.5, 1.0));
                flat out vec3 vertexColor;
                void main()
                {
                    gl_Position = vec4(positions[gl_VertexID], 0.0, 1.0);
                    vertexColor = gl_VertexID < 3 ? vec3(1.0, 0.0, 0.0) : vec3(0.0, 1.0, 0.0);
                }
                """)
        });
        using IGraphicsShader fragment = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Fragment,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                precision highp float;
                flat in vec3 vertexColor;
                layout(location = 0) out vec4 outputColor;
                void main() { outputColor = vec4(vertexColor, 1.0); }
                """)
        });
        IGraphicsShader[] shaders = [vertex, fragment];
        GraphicsFormat[] formats = [GraphicsFormat.RGBA8UNorm];
        using IGraphicsRenderPipeline pipeline = device.CreateRenderPipeline(
            new GraphicsRenderPipelineDescription(layout, shaders, GraphicsPrimitiveTopology.TriangleList, formats));

        using IGraphicsBuffer arguments = device.CreateBuffer(new GraphicsBufferDescription(
            188,
            GraphicsBufferUsage.IndirectArguments,
            GraphicsMemoryAccess.CpuToGpu));
        uint[] words = new uint[47];
        // DrawArraysIndirect: two tightly packed 16-byte commands at offset 0.
        words[0] = 3; words[1] = 1; words[2] = 0; words[3] = 0;
        words[4] = 3; words[5] = 1; words[6] = 3; words[7] = 0;
        // DrawArraysIndirect: two 16-byte commands separated by an 8-byte stride gap.
        words[8] = 3; words[9] = 1; words[10] = 0; words[11] = 0;
        words[14] = 3; words[15] = 1; words[16] = 3; words[17] = 0;
        // DrawElementsIndirect: tightly packed 20-byte commands.
        words[20] = 3; words[21] = 1; words[22] = 0; words[23] = 0; words[24] = 0;
        words[25] = 3; words[26] = 1; words[27] = 3; words[28] = 0; words[29] = 0;
        // DrawElementsIndirect: two 20-byte commands with a 4-byte stride gap.
        words[36] = 3; words[37] = 1; words[38] = 0; words[39] = 0; words[40] = 0;
        words[42] = 3; words[43] = 1; words[44] = 3; words[45] = 0; words[46] = 0;
        WriteUInts(arguments, words);

        using IGraphicsBuffer indices = device.CreateBuffer(new GraphicsBufferDescription(
            6 * sizeof(uint),
            GraphicsBufferUsage.Index,
            GraphicsMemoryAccess.CpuToGpu));
        WriteUInts(indices, 0, 1, 2, 3, 4, 5);

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Graphics);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();
        IGraphicsTextureView[] targets = [colorView];
        commandList.Begin();
        commandList.SetRenderTargets(targets);
        commandList.SetViewport(new GraphicsViewport(0, 0, 64, 64));
        commandList.SetScissor(new GraphicsScissorRect(0, 0, 64, 64));
        commandList.ClearColor(0, new GraphicsClearColor(0, 0, 0, 1));
        commandList.SetRenderPipeline(pipeline);
        commandList.DrawIndirect(arguments, 0, drawCount: 2, stride: 0);
        commandList.DrawIndirect(arguments, 32, drawCount: 2, stride: 24);
        commandList.SetIndexBuffer(indices, GraphicsIndexFormat.UInt32);
        commandList.DrawIndexedIndirect(arguments, 80, drawCount: 2, stride: 0);
        commandList.DrawIndexedIndirect(arguments, 144, drawCount: 2, stride: 24);
        ExpectThrows<GraphicsValidationException>(
            () => commandList.DrawIndirect(arguments, 184, drawCount: 2, stride: 0),
            "array indirect arguments must be range checked");
        ExpectThrows<GraphicsValidationException>(
            () => commandList.DrawIndexedIndirect(arguments, 184, drawCount: 2, stride: 0),
            "indexed indirect arguments must be range checked");
        commandList.End();
        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES multi-draw-indirect fence did not complete.");

        AssertPixelNear(ReadRgba8Pixel(device, context, colorView, 16, 32), 255, 0, 0, 255);
        AssertPixelNear(ReadRgba8Pixel(device, context, colorView, 48, 32), 0, 255, 0, 255);
        Console.WriteLine("Validated multiple DrawIndirect and DrawIndexedIndirect commands, default and explicit strides, nonzero argument offsets, range rejection, and GPU-visible output from each draw.");
    }

    private static void RunMultisampleFramebuffer(
        IGraphicsDevice device,
        GlfwOpenGLESContextProvider context,
        uint sampleCount)
    {
        using IGraphicsTexture multisample = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            32, 32, 1, 1, 1, sampleCount,
            GraphicsTextureUsage.Sampled | GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTextureView multisampleView = multisample.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2DMultisample, 0, 1, 0, 1));

        IGraphicsCommandQueue graphicsQueue = device.GetQueue(GraphicsQueueType.Graphics);
        using (IGraphicsCommandList clear = graphicsQueue.CreateCommandList())
        using (IGraphicsFence fence = device.CreateFence())
        {
            IGraphicsTextureView[] targets = [multisampleView];
            clear.Begin();
            clear.SetRenderTargets(targets);
            clear.SetViewport(new GraphicsViewport(0, 0, 32, 32));
            clear.SetScissor(new GraphicsScissorRect(0, 0, 32, 32));
            clear.ClearColor(0, new GraphicsClearColor(0.25f, 0.5f, 0.75f, 1));
            clear.End();
            graphicsQueue.Submit([clear], fence, 1);
            if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The GLES multisample framebuffer-clear fence did not complete.");
        }

        using IGraphicsBuffer output = device.CreateBuffer(new GraphicsBufferDescription(
            4 * sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.GpuToCpu));
        using IGraphicsShader computeShader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Compute,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                layout(local_size_x = 1) in;
                layout(binding = 0) uniform highp sampler2DMS sourceTexture;
                layout(std430, binding = 0) buffer Result { uvec4 value; } resultBuffer;
                void main()
                {
                    vec4 sampleValue = texelFetch(sourceTexture, ivec2(0, 0), 0);
                    resultBuffer.value = uvec4(round(sampleValue * 255.0));
                }
                """)
        });
        using IGraphicsBindingLayout layout = device.CreateBindingLayout(new GraphicsBindingLayoutDescription(
        [
            new GraphicsBindingLayoutEntry(0, GraphicsDescriptorType.SampledTexture, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(1, GraphicsDescriptorType.StorageBuffer, 1, GraphicsShaderStages.Compute)
        ]));
        using IGraphicsBindingSet set = device.CreateBindingSet(layout);
        set.SetTexture(0, 0, multisampleView);
        set.SetBuffer(1, 0, output, 0, 4 * sizeof(uint));
        IGraphicsBindingLayout[] layouts = [layout];
        using IGraphicsPipelineLayout pipelineLayout = device.CreatePipelineLayout(layouts);
        using IGraphicsComputePipeline pipeline = device.CreateComputePipeline(
            new GraphicsComputePipelineDescription(pipelineLayout, computeShader));

        IGraphicsCommandQueue computeQueue = device.GetQueue(GraphicsQueueType.Compute);
        using IGraphicsCommandList compute = computeQueue.CreateCommandList();
        using IGraphicsFence computeFence = device.CreateFence();
        compute.Begin();
        compute.SetComputePipeline(pipeline);
        compute.SetBindingSet(0, set);
        compute.Dispatch(1);
        compute.MemoryBarrier(
        [
            new GraphicsResourceBarrier(
                output,
                GraphicsPipelineStage.ComputeShader,
                GraphicsPipelineStage.Host,
                GraphicsResourceAccess.ShaderWrite,
                GraphicsResourceAccess.HostRead)
        ]);
        compute.End();
        computeQueue.Submit([compute], computeFence, 1);
        if (!computeFence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES multisample shader-readback fence did not complete.");

        Memory<byte> readback = output.Map(GraphicsMapMode.Read, 0, 4 * sizeof(uint));
        uint[] actual = new uint[4];
        for (int i = 0; i < actual.Length; i++)
            actual[i] = BitConverter.ToUInt32(readback.Span.Slice(i * sizeof(uint), sizeof(uint)));
        output.Unmap();
        if (!actual.AsSpan().SequenceEqual(new uint[] { 64, 128, 191, 255 }))
            throw new InvalidOperationException($"The multisample texture sample readback was [{string.Join(", ", actual)}].");

        Console.WriteLine($"Validated {sampleCount}x framebuffer attachment, clear, and shader sampling through sampler2DMS; no resolve operation is implied.");
    }

    private static unsafe byte[] ReadRgba8Pixel(
        IGraphicsDevice device,
        GlfwOpenGLESContextProvider context,
        IGraphicsTextureView view,
        int x = 32,
        int y = 32)
    {
        OpenGLESGraphicsDevice gles = (OpenGLESGraphicsDevice)device;
        OpenGLESGraphicsTextureView nativeView = (OpenGLESGraphicsTextureView)view;
        byte[] pixel = new byte[4];
        context.MakeCurrent();
        uint framebuffer = 0;
        try
        {
            GL gl = gles.Api;
            framebuffer = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            gl.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                GLEnum.ColorAttachment0,
                nativeView.Target,
                nativeView.Handle,
                0);
            if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                throw new GraphicsException("The GLES pixel-readback framebuffer is incomplete.");
            gl.ReadBuffer(GLEnum.ColorAttachment0);
            fixed (byte* destination = pixel)
                gl.ReadPixels(x, y, 1, 1, (PixelFormat)0x1908, (PixelType)0x1401, destination);
            GLEnum error = gl.GetError();
            if (error != GLEnum.NoError)
                throw new GraphicsException($"GLES RGBA8 pixel readback failed with {error}.");
        }
        finally
        {
            gles.Api.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (framebuffer != 0)
                gles.Api.DeleteFramebuffer(framebuffer);
            context.ReleaseCurrent();
        }

        return pixel;
    }

    private static unsafe T[] ReadIntegerPixel<T>(
        IGraphicsDevice device,
        GlfwOpenGLESContextProvider context,
        IGraphicsTextureView view,
        PixelType pixelType) where T : unmanaged
    {
        OpenGLESGraphicsDevice gles = (OpenGLESGraphicsDevice)device;
        OpenGLESGraphicsTextureView nativeView = (OpenGLESGraphicsTextureView)view;
        T[] pixel = new T[4];
        context.MakeCurrent();
        uint framebuffer = 0;
        try
        {
            GL gl = gles.Api;
            framebuffer = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            gl.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                GLEnum.ColorAttachment0,
                nativeView.Target,
                nativeView.Handle,
                0);
            if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                throw new GraphicsException("The GLES integer pixel-readback framebuffer is incomplete.");
            gl.ReadBuffer(GLEnum.ColorAttachment0);
            fixed (T* destination = pixel)
                gl.ReadPixels(0, 0, 1, 1, (PixelFormat)0x8D99, pixelType, destination);
            GLEnum error = gl.GetError();
            if (error != GLEnum.NoError)
                throw new GraphicsException($"GLES integer pixel readback failed with {error}.");
        }
        finally
        {
            gles.Api.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (framebuffer != 0)
                gles.Api.DeleteFramebuffer(framebuffer);
            context.ReleaseCurrent();
        }

        return pixel;
    }

    private static void AssertPixelNear(byte[] actual, byte red, byte green, byte blue, byte alpha)
    {
        if (actual.Length < 4 ||
            Math.Abs(actual[0] - red) > 1 ||
            Math.Abs(actual[1] - green) > 1 ||
            Math.Abs(actual[2] - blue) > 1 ||
            Math.Abs(actual[3] - alpha) > 1)
            throw new InvalidOperationException(
                $"Pixel readback [{string.Join(", ", actual)}] did not match [{red}, {green}, {blue}, {alpha}].");
    }

    private static void ExpectThrows<TException>(Action action, string description)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}: {description}.");
    }

    private static void RunBufferCopyAndFence(IGraphicsDevice device)
    {
        const int size = 1024;
        using IGraphicsBuffer source = device.CreateBuffer(new GraphicsBufferDescription(
            size,
            GraphicsBufferUsage.CopySource | GraphicsBufferUsage.CopyDestination,
            GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer destination = device.CreateBuffer(new GraphicsBufferDescription(
            size,
            GraphicsBufferUsage.CopySource | GraphicsBufferUsage.CopyDestination,
            GraphicsMemoryAccess.CpuReadWrite));

        Memory<byte> writeMapping = source.Map(GraphicsMapMode.Write);
        Span<byte> bytes = writeMapping.Span;
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)((i * 37 + 11) & 0xFF);
        source.Unmap();

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Copy);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();

        commandList.Begin();
        commandList.CopyBuffer(source, 0, destination, 0, size);
        commandList.End();
        queue.Submit([commandList], fence, 1);

        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES buffer-copy fence did not complete within 10 seconds.");

        Memory<byte> readback = destination.Map(GraphicsMapMode.Read);
        for (int i = 0; i < size; i++)
        {
            byte expected = (byte)((i * 37 + 11) & 0xFF);
            if (readback.Span[i] != expected)
                throw new InvalidOperationException($"GLES buffer-copy mismatch at byte {i}.");
        }
        destination.Unmap();
        Console.WriteLine("PASS: GLES buffer allocation, map/unmap, copy, and GPU fence.");
    }


    private static void RunTextureAndSamplerResources(IGraphicsDevice device)
    {
        using IGraphicsTexture texture2D = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 1, 1, GraphicsTextureUsage.Sampled));
        using IGraphicsTexture textureArray = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 4, 1, GraphicsTextureUsage.Sampled));
        using IGraphicsTexture texture3D = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture3D, GraphicsFormat.RGBA8UNorm,
            32, 32, 8, 1, 1, 1, GraphicsTextureUsage.Sampled));
        using IGraphicsTexture cube = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Cube, GraphicsFormat.RGBA8UNorm,
            32, 32, 1, 1, 6, 1, GraphicsTextureUsage.Sampled));

        uint multisampleCount =
            device.Capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UNorm, 4) ? 4u :
            device.Capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UNorm, 2) ? 2u : 1u;
        if (multisampleCount > 1)
        {
            using IGraphicsTexture multisample = device.CreateTexture(new GraphicsTextureDescription(
                GraphicsTextureDimension.Texture2D,
                GraphicsFormat.RGBA8UNorm,
                64, 64, 1, 1, 1, multisampleCount,
                GraphicsTextureUsage.Sampled | GraphicsTextureUsage.ColorAttachment));
            using IGraphicsTextureView multisampleView = multisample.CreateView(
                new GraphicsTextureViewDescription(
                    GraphicsFormat.Unknown,
                    GraphicsTextureViewType.Texture2DMultisample,
                    0, 1, 0, 1));
            Console.WriteLine($"PASS: GLES {multisampleCount}x multisample texture allocation and identity view.");
        }
        else
        {
            Console.WriteLine("SKIP: no multisample sample count is reported for RGBA8Unorm.");
        }
        using IGraphicsSampler sampler = device.CreateSampler(new GraphicsSamplerDescription(
            GraphicsFilter.Linear, GraphicsFilter.Linear, GraphicsFilter.Linear,
            GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge,
            0, 16));

        if (device.Capabilities.Features.Supports(GraphicsFeature.TextureViews))
        {
            using IGraphicsTexture viewSource = device.CreateTexture(new GraphicsTextureDescription(
                GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
                64, 64, 1, 4, 4, 1, GraphicsTextureUsage.Sampled));
            using IGraphicsTextureView subresourceView = viewSource.CreateView(
                new GraphicsTextureViewDescription(
                    GraphicsFormat.Unknown,
                    GraphicsTextureViewType.Texture2DArray,
                    1, 2, 1, 2));
            Console.WriteLine("PASS: GL_EXT_texture_view creates a mip- and layer-restricted 2D-array view.");
        }
        else
        {
            Console.WriteLine("SKIP: GL_EXT_texture_view or glTextureViewEXT is unavailable.");
        }

        Console.WriteLine("PASS: GLES 2D, 2D-array, 3D and cube texture allocation plus sampler creation/disposal.");
    }

    private static void RunResourceRetentionAfterRecording(IGraphicsDevice device)
    {
        IGraphicsBuffer source = device.CreateBuffer(new GraphicsBufferDescription(
            256,
            GraphicsBufferUsage.CopySource | GraphicsBufferUsage.CopyDestination,
            GraphicsMemoryAccess.GpuOnly));
        IGraphicsBuffer destination = device.CreateBuffer(new GraphicsBufferDescription(
            256,
            GraphicsBufferUsage.CopySource | GraphicsBufferUsage.CopyDestination,
            GraphicsMemoryAccess.GpuOnly));

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Copy);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();

        commandList.Begin();
        commandList.CopyBuffer(source, 0, destination, 0, 256);
        commandList.End();

        // Public disposal must not delete either native buffer while the executable command list retains it.
        source.Dispose();
        destination.Dispose();

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES resource-retention fence did not complete within 10 seconds.");

        Console.WriteLine("PASS: GLES native buffers remain alive after public disposal while recorded work retains them.");
    }


    private static void RunReusableCommandListAfterOwnerDisposal(IGraphicsDevice device)
    {
        const int size = 4096;
        using IGraphicsBuffer source = device.CreateBuffer(new GraphicsBufferDescription(
            size,
            GraphicsBufferUsage.CopySource | GraphicsBufferUsage.CopyDestination,
            GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer destination = device.CreateBuffer(new GraphicsBufferDescription(
            size,
            GraphicsBufferUsage.CopySource | GraphicsBufferUsage.CopyDestination,
            GraphicsMemoryAccess.CpuReadWrite));

        Memory<byte> input = source.Map(GraphicsMapMode.Write, 0, size);
        for (int i = 0; i < input.Length; i++)
            input.Span[i] = (byte)((i * 29 + 3) & 0xFF);
        source.Unmap();

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Copy);
        using IGraphicsCommandList commandList = queue.CreateCommandList(GraphicsCommandListUsage.Reusable);
        using IGraphicsFence fence = device.CreateFence();

        commandList.Begin();
        commandList.CopyBuffer(source, 0, destination, 0, size);
        commandList.End();

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The first GLES reusable-command-list submission did not complete.");

        // Creating a list polls the queue's completion sync so the reusable list becomes Executable.
        using (IGraphicsCommandList completionPoll = queue.CreateCommandList())
        {
        }

        source.Dispose();
        queue.Submit([commandList], fence, 2);
        if (!fence.Wait(2, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The second GLES reusable-command-list submission did not complete.");

        using (IGraphicsCommandList completionPoll = queue.CreateCommandList())
        {
        }

        Memory<byte> actual = destination.Map(GraphicsMapMode.Read, 0, size);
        for (int i = 0; i < actual.Length; i++)
        {
            byte expected = (byte)((i * 29 + 3) & 0xFF);
            if (actual.Span[i] != expected)
                throw new InvalidOperationException(
                    $"GLES reusable-command-list copy mismatch at byte {i}: expected 0x{expected:X2}, received 0x{actual.Span[i]:X2}.");
        }
        destination.Unmap();

        Console.WriteLine("PASS: GLES reusable command list retains a disposed source buffer across repeated submissions and fence completion.");
    }

    private static void RunOffscreenGraphicsPipeline(IGraphicsDevice device, GlfwOpenGLESContextProvider context)
    {
        using IGraphicsTexture color = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 1, 1,
            GraphicsTextureUsage.ColorAttachment | GraphicsTextureUsage.Sampled |
            GraphicsTextureUsage.CopySource));
        using IGraphicsTexture copyDestination = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 1, 1,
            GraphicsTextureUsage.ColorAttachment | GraphicsTextureUsage.CopyDestination));
        using IGraphicsTexture depthStencil = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.D24UNormS8UInt,
            64, 64, 1, 1, 1, 1, GraphicsTextureUsage.DepthStencilAttachment));
        using IGraphicsTexture sampledTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D, GraphicsFormat.RGBA8UNorm,
            32, 32, 1, 1, 1, 1, GraphicsTextureUsage.Sampled));
        using IGraphicsSampler sampler = device.CreateSampler(new GraphicsSamplerDescription(
            GraphicsFilter.Linear, GraphicsFilter.Linear, GraphicsFilter.Linear,
            GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge,
            0, 8));

        using IGraphicsTextureView colorView = color.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsTextureView depthView = depthStencil.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsTextureView sampledView = sampledTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsBindingLayout sampledLayout = device.CreateBindingLayout(new GraphicsBindingLayoutDescription(
        [
            new GraphicsBindingLayoutEntry(0, GraphicsDescriptorType.CombinedTextureSampler, 1, GraphicsShaderStages.Fragment)
        ]));
        using IGraphicsBindingSet sampledSet = device.CreateBindingSet(sampledLayout);
        sampledSet.SetTexture(0, 0, sampledView);
        sampledSet.SetSampler(0, 0, sampler);
        IGraphicsBindingLayout[] bindingLayouts = [sampledLayout];
        using IGraphicsPipelineLayout pipelineLayout = device.CreatePipelineLayout(bindingLayouts);

        using IGraphicsShader vertexShader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Vertex,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                const vec2 positions[3] = vec2[3](
                    vec2(-0.75, -0.75),
                    vec2( 0.75, -0.75),
                    vec2( 0.00,  0.75));
                void main()
                {
                    gl_Position = vec4(positions[gl_VertexID], 0.0, 1.0);
                }
                """)
        });
        using IGraphicsShader fragmentShader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Fragment,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                precision highp float;
                layout(binding = 0) uniform highp sampler2D sourceTexture;
                layout(location = 0) out vec4 outputColor;
                void main()
                {
                    outputColor = texture(sourceTexture, vec2(0.5, 0.5));
                }
                """)
        });

        IGraphicsShader[] shaders = [vertexShader, fragmentShader];
        GraphicsFormat[] colorFormats = [GraphicsFormat.RGBA8UNorm];
        using IGraphicsRenderPipeline pipeline = device.CreateRenderPipeline(
            new GraphicsRenderPipelineDescription(
                pipelineLayout, shaders, GraphicsPrimitiveTopology.TriangleList,
                colorFormats, GraphicsFormat.D24UNormS8UInt));

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Graphics);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();
        IGraphicsTextureView[] colors = [colorView];

        commandList.Begin();
        commandList.SetRenderTargets(colors, depthView);
        commandList.SetViewport(new GraphicsViewport(0, 0, 64, 64));
        commandList.SetScissor(new GraphicsScissorRect(0, 0, 64, 64));
        commandList.ClearColor(0, new GraphicsClearColor(0, 0, 0, 1));
        commandList.ClearDepthStencil(1, 0xFF, clearDepth: true, clearStencil: true);
        commandList.SetRenderPipeline(pipeline);
        commandList.SetBindingSet(0, sampledSet);
        commandList.Draw(3);
        commandList.CopyTexture(color, copyDestination);
        commandList.End();

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES offscreen rendering fence did not complete within 10 seconds.");

        OpenGLESGraphicsDevice glesDevice = (OpenGLESGraphicsDevice)device;
        context.MakeCurrent();
        try
        {
            GLEnum error = glesDevice.Api.GetError();
            if (error != GLEnum.NoError)
                throw new GraphicsException($"The GLES driver reported {error} after offscreen clear and draw execution.");
        }
        finally
        {
            context.ReleaseCurrent();
        }

        Console.WriteLine("PASS: GLES vertex/fragment compilation, graphics pipeline link, FBO attachments, viewport/scissor, color/depth/stencil clear, combined sampled-texture/sampler binding, draw, and validated color-texture blit copy.");
    }


    private static void RunComputeDescriptorTypesAndSnapshots(
        IGraphicsDevice device,
        GlfwOpenGLESContextProvider context)
    {
        // Two explicitly-cleared texels make the linear sampler's interpolation observable through SSBO readback.
        using IGraphicsTexture sampledTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            2, 1, 1, 1, 1, 1,
            GraphicsTextureUsage.Sampled | GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTextureView sampledView = sampledTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        ClearTwoTexelTexture(device, sampledView);

        using IGraphicsTexture alternativeSampledTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            2, 1, 1, 1, 1, 1,
            GraphicsTextureUsage.Sampled | GraphicsTextureUsage.ColorAttachment));
        using IGraphicsTextureView alternativeSampledView = alternativeSampledTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        ClearTexture(device, alternativeSampledView, 2, 1, new GraphicsClearColor(0, 1, 0, 1));

        using IGraphicsTexture storageTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            1, 1, 1, 1, 1, 1,
            GraphicsTextureUsage.Storage));
        uint originalTextureHandle = ((OpenGLESGraphicsTexture)sampledTexture).Handle;
        uint originalSamplerHandle;
        uint originalStorageImageHandle;
        using IGraphicsTextureView storageView = storageTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));
        using IGraphicsTexture alternativeStorageTexture = device.CreateTexture(new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            1, 1, 1, 1, 1, 1,
            GraphicsTextureUsage.Storage));
        using IGraphicsTextureView alternativeStorageView = alternativeStorageTexture.CreateView(new GraphicsTextureViewDescription(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1));

        using IGraphicsBuffer parameters = device.CreateBuffer(new GraphicsBufferDescription(
            4 * sizeof(uint), GraphicsBufferUsage.Constant, GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer alternativeParameters = device.CreateBuffer(new GraphicsBufferDescription(
            4 * sizeof(uint), GraphicsBufferUsage.Constant, GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer output = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer alternativeOutput = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer partialOutput = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer alternativePartialOutput = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.CpuReadWrite));

        WriteUInts(parameters, 7u, 0x00A11CEu, 0, 0);
        WriteUInts(alternativeParameters, 0x22u, 0xBADu, 0, 0);
        WriteUInts(output, 0xDEADBEEFu);
        WriteUInts(alternativeOutput, 0xDEADBEEFu);
        WriteUInts(partialOutput, 0xDEADBEEFu);
        WriteUInts(alternativePartialOutput, 0xDEADBEEFu);

        using IGraphicsSampler linearSampler = device.CreateSampler(new GraphicsSamplerDescription(
            GraphicsFilter.Linear, GraphicsFilter.Linear, GraphicsFilter.Linear,
            GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge,
            0, 0));
        using IGraphicsSampler nearestSampler = device.CreateSampler(new GraphicsSamplerDescription(
            GraphicsFilter.Nearest, GraphicsFilter.Nearest, GraphicsFilter.Nearest,
            GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge, GraphicsAddressMode.ClampToEdge,
            0, 0));
        originalSamplerHandle = ((OpenGLESGraphicsSampler)linearSampler).Handle;
        originalStorageImageHandle = ((OpenGLESGraphicsTexture)storageTexture).Handle;

        using IGraphicsBindingLayout bindingLayout = device.CreateBindingLayout(new GraphicsBindingLayoutDescription(
        [
            new GraphicsBindingLayoutEntry(0, GraphicsDescriptorType.UniformBuffer, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(1, GraphicsDescriptorType.StorageBuffer, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(2, GraphicsDescriptorType.SampledTexture, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(3, GraphicsDescriptorType.Sampler, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(4, GraphicsDescriptorType.CombinedTextureSampler, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(5, GraphicsDescriptorType.StorageTexture, 1, GraphicsShaderStages.Compute),
            new GraphicsBindingLayoutEntry(
                6, GraphicsDescriptorType.StorageBuffer, 2, GraphicsShaderStages.Compute,
                GraphicsBindingFlags.PartiallyBound)
        ]));
        using IGraphicsBindingSet bindingSet = device.CreateBindingSet(bindingLayout);
        bindingSet.SetBuffer(0, 0, parameters, 0, 4 * sizeof(uint));
        bindingSet.SetBuffer(1, 0, output, 0, sizeof(uint));
        bindingSet.SetTexture(2, 0, sampledView);
        bindingSet.SetSampler(3, 0, linearSampler);
        bindingSet.SetTexture(4, 0, sampledView);
        bindingSet.SetSampler(4, 0, linearSampler);
        bindingSet.SetTexture(5, 0, storageView);
        // Array element zero remains unbound; element one is the only populated element.
        bindingSet.SetBuffer(6, 1, partialOutput, 0, sizeof(uint));

        IGraphicsBindingLayout[] bindingLayouts = [bindingLayout];
        using IGraphicsPipelineLayout pipelineLayout = device.CreatePipelineLayout(bindingLayouts);
        using IGraphicsShader shader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Compute,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                layout(local_size_x = 1) in;
                layout(std140, binding = 0) uniform Parameters { uvec4 values; } parameters;
                layout(std430, binding = 0) buffer Result { uint value; } resultBuffer;
                layout(binding = 0) uniform highp sampler2D sampledOnly;
                layout(binding = 2) uniform highp sampler2D sampledWithSampler;
                layout(rgba8, binding = 0) coherent uniform highp image2D storageOutput;
                layout(std430, binding = 1) buffer PartialResult { uint marker; } partialResults[2];

                void main()
                {
                    vec4 exactTexel = texelFetch(sampledOnly, ivec2(0, 0), 0);
                    vec4 filteredTexel = texture(sampledWithSampler, vec2(0.375, 0.5));
                    imageStore(storageOutput, ivec2(0, 0), filteredTexel);
                    memoryBarrierImage();
                    vec4 imageReadback = imageLoad(storageOutput, ivec2(0, 0));

                    resultBuffer.value =
                        (uint(round(exactTexel.r * 255.0)) << 24u) |
                        (uint(round(imageReadback.r * 255.0)) << 16u) |
                        (uint(round(imageReadback.b * 255.0)) << 8u) |
                        parameters.values.x;
                    partialResults[1].marker = parameters.values.y;
                }
                """)
        });
        using IGraphicsComputePipeline pipeline = device.CreateComputePipeline(
            new GraphicsComputePipelineDescription(pipelineLayout, shader));

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Compute);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();

        commandList.Begin();
        commandList.SetComputePipeline(pipeline);
        commandList.SetBindingSet(0, bindingSet);
        commandList.Dispatch(1);
        commandList.End();

        // Mutate every descriptor category after recording. The executable command list must keep
        // the original UBO, SSBOs, sampled view, sampler, storage image, and partially-bound slot snapshot.
        bindingSet.SetBuffer(0, 0, alternativeParameters, 0, 4 * sizeof(uint));
        bindingSet.SetBuffer(1, 0, alternativeOutput, 0, sizeof(uint));
        bindingSet.SetTexture(2, 0, alternativeSampledView);
        bindingSet.SetSampler(3, 0, nearestSampler);
        bindingSet.SetTexture(4, 0, alternativeSampledView);
        bindingSet.SetSampler(4, 0, nearestSampler);
        bindingSet.SetTexture(5, 0, alternativeStorageView);
        bindingSet.SetBuffer(6, 1, alternativePartialOutput, 0, sizeof(uint));
        bindingSet.Dispose();

        // Dispose the original texture, sampler, UBO and image owners before submission. The
        // recorded descriptor snapshot must still keep the native handles alive through execution.
        sampledTexture.Dispose();
        linearSampler.Dispose();
        parameters.Dispose();
        storageTexture.Dispose();

        OpenGLESGraphicsDevice glesDevice = (OpenGLESGraphicsDevice)device;
        using IGraphicsBuffer sentinelBufferResource = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint),
            GraphicsBufferUsage.Storage,
            GraphicsMemoryAccess.GpuOnly));
        uint sentinelBuffer = ((OpenGLESGraphicsBuffer)sentinelBufferResource).Handle;
        context.MakeCurrent();
        try
        {
            glesDevice.Api.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, sentinelBuffer);
            glesDevice.Api.GetInteger(GLEnum.ShaderStorageBufferBinding, 1, out int seededBinding);
            if (seededBinding != (int)sentinelBuffer)
                throw new GraphicsException("Could not seed the GLES partially-bound SSBO slot with the sentinel buffer.");
        }
        finally
        {
            context.ReleaseCurrent();
        }

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES full-descriptor integration fence did not complete within 10 seconds.");

        context.MakeCurrent();
        try
        {
            glesDevice.Api.GetInteger(GLEnum.ShaderStorageBufferBinding, 1, out int missingArrayElementBinding);
            if (missingArrayElementBinding != 0)
                throw new InvalidOperationException(
                    $"Partially-bound GLES SSBO array element zero was not explicitly unbound (binding={missingArrayElementBinding}).");
            glesDevice.Api.GetInteger(GLEnum.ImageBindingName, 0, out int imageBinding);
            if (imageBinding != (int)originalStorageImageHandle)
                throw new InvalidOperationException(
                    $"The storage-image descriptor snapshot bound {imageBinding}, expected {originalStorageImageHandle}.");
            glesDevice.Api.GetInteger(GLEnum.SamplerBinding, 1, out int separateSamplerBinding);
            if (separateSamplerBinding != (int)originalSamplerHandle)
                throw new InvalidOperationException(
                    $"The standalone sampler descriptor bound {separateSamplerBinding}, expected {originalSamplerHandle}.");
            glesDevice.Api.GetInteger(GLEnum.SamplerBinding, 2, out int samplerBinding);
            if (samplerBinding != (int)originalSamplerHandle)
                throw new InvalidOperationException(
                    $"The combined-sampler descriptor snapshot bound {samplerBinding}, expected {originalSamplerHandle}.");
            glesDevice.Api.ActiveTexture((TextureUnit)0x84C2);
            glesDevice.Api.GetInteger(GLEnum.TextureBinding2D, out int textureBinding);
            if (textureBinding != (int)originalTextureHandle)
                throw new InvalidOperationException(
                    $"The combined-texture descriptor snapshot bound {textureBinding}, expected {originalTextureHandle}.");
        }
        finally
        {
            context.ReleaseCurrent();
        }

        uint expectedResult = (0xFFu << 24) | (191u << 16) | (64u << 8) | 7u;
        uint actualResult = ReadUInt(output);
        if (actualResult != expectedResult)
            throw new InvalidOperationException(
                $"GLES descriptor snapshot result was 0x{actualResult:X8}, expected 0x{expectedResult:X8}.");
        AssertUInt(ReadUInt(partialOutput), 0x00A11CEu);
        AssertUInt(ReadUInt(alternativeOutput), 0xDEADBEEFu);
        AssertUInt(ReadUInt(alternativePartialOutput), 0xDEADBEEFu);

        Console.WriteLine(
            "PASS: GLES UBO, SSBO, sampled texture, sampler, combined texture/sampler, storage image, partially-bound SSBO array unbinding, descriptor-set mutation snapshots, texture/sampler disposal-after-recording, and GPU readback.");
    }

    private static void ClearTwoTexelTexture(IGraphicsDevice device, IGraphicsTextureView textureView) =>
        ClearTexture(
            device,
            textureView,
            2,
            1,
            new GraphicsClearColor(1, 0, 0, 1),
            new GraphicsClearColor(0, 0, 1, 1));

    private static void ClearTexture(
        IGraphicsDevice device,
        IGraphicsTextureView textureView,
        uint width,
        uint height,
        GraphicsClearColor firstColor,
        GraphicsClearColor? secondColor = null)
    {
        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Graphics);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();
        IGraphicsTextureView[] targets = [textureView];

        commandList.Begin();
        commandList.SetRenderTargets(targets);
        commandList.SetViewport(new GraphicsViewport(0, 0, width, height));
        if (secondColor is { } nextColor)
        {
            commandList.SetScissor(new GraphicsScissorRect(0, 0, 1, height));
            commandList.ClearColor(0, firstColor);
            commandList.SetScissor(new GraphicsScissorRect(1, 0, width - 1, height));
            commandList.ClearColor(0, nextColor);
        }
        else
        {
            commandList.SetScissor(new GraphicsScissorRect(0, 0, width, height));
            commandList.ClearColor(0, firstColor);
        }
        commandList.End();

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES texture-initialization fence did not complete within 10 seconds.");
    }

    private static void WriteUInts(IGraphicsBuffer buffer, params uint[] values)
    {
        Memory<byte> memory = buffer.Map(GraphicsMapMode.Write, 0, checked((ulong)(values.Length * sizeof(uint))));
        for (int i = 0; i < values.Length; i++)
            BitConverter.TryWriteBytes(memory.Span.Slice(i * sizeof(uint), sizeof(uint)), values[i]);
        buffer.Unmap();
    }


    private static void AssertUInt(uint actual, uint expected)
    {
        if (actual != expected)
            throw new InvalidOperationException(
                $"GPU readback was 0x{actual:X8}; expected 0x{expected:X8}.");
    }

    private static uint ReadUInt(IGraphicsBuffer buffer)
    {
        Memory<byte> memory = buffer.Map(GraphicsMapMode.Read, 0, sizeof(uint));
        uint value = BitConverter.ToUInt32(memory.Span);
        buffer.Unmap();
        return value;
    }


    private static void RunComputePipelineLayoutSwitch(IGraphicsDevice device)
    {
        using IGraphicsBuffer firstOutput = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.CpuReadWrite));
        using IGraphicsBuffer secondOutput = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint), GraphicsBufferUsage.Storage, GraphicsMemoryAccess.CpuReadWrite));

        using IGraphicsBindingLayout firstBindingLayout = device.CreateBindingLayout(
            new GraphicsBindingLayoutDescription(
            [
                new GraphicsBindingLayoutEntry(0, GraphicsDescriptorType.StorageBuffer, 1, GraphicsShaderStages.Compute)
            ]));
        using IGraphicsBindingLayout secondBindingLayout = device.CreateBindingLayout(
            new GraphicsBindingLayoutDescription(
            [
                new GraphicsBindingLayoutEntry(7, GraphicsDescriptorType.StorageBuffer, 1, GraphicsShaderStages.Compute)
            ]));
        using IGraphicsBindingSet firstSet = device.CreateBindingSet(firstBindingLayout);
        using IGraphicsBindingSet secondSet = device.CreateBindingSet(secondBindingLayout);
        firstSet.SetBuffer(0, 0, firstOutput, 0, sizeof(uint));
        secondSet.SetBuffer(7, 0, secondOutput, 0, sizeof(uint));

        IGraphicsBindingLayout[] firstLayouts = [firstBindingLayout];
        IGraphicsBindingLayout[] secondLayouts = [secondBindingLayout];
        using IGraphicsPipelineLayout firstPipelineLayout = device.CreatePipelineLayout(firstLayouts);
        using IGraphicsPipelineLayout secondPipelineLayout = device.CreatePipelineLayout(secondLayouts);

        using IGraphicsShader firstShader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Compute,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                layout(local_size_x = 1) in;
                layout(std430, binding = 0) buffer FirstResult { uint value; } resultBuffer;
                void main() { resultBuffer.value = 0x13572468u; }
                """)
        });
        using IGraphicsShader secondShader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Compute,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                layout(local_size_x = 1) in;
                layout(std430, binding = 0) buffer SecondResult { uint value; } resultBuffer;
                void main() { resultBuffer.value = 0x24681357u; }
                """)
        });
        using IGraphicsComputePipeline firstPipeline = device.CreateComputePipeline(
            new GraphicsComputePipelineDescription(firstPipelineLayout, firstShader));
        using IGraphicsComputePipeline secondPipeline = device.CreateComputePipeline(
            new GraphicsComputePipelineDescription(secondPipelineLayout, secondShader));

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Compute);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();

        commandList.Begin();
        commandList.SetComputePipeline(firstPipeline);
        commandList.SetBindingSet(0, firstSet);
        commandList.Dispatch(1);
        commandList.SetComputePipeline(secondPipeline);
        commandList.SetBindingSet(0, secondSet);
        commandList.Dispatch(1);
        commandList.End();

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES pipeline-layout-switch fence did not complete within 10 seconds.");

        uint firstValue = ReadUInt(firstOutput);
        uint secondValue = ReadUInt(secondOutput);
        if (firstValue != 0x13572468u || secondValue != 0x24681357u)
            throw new InvalidOperationException(
                $"GLES pipeline-layout switch returned 0x{firstValue:X8}/0x{secondValue:X8} instead of 0x13572468/0x24681357.");

        Console.WriteLine("PASS: GLES compute pipeline switching between distinct descriptor layouts and GPU-visible SSBO results.");
    }

    private static void RunComputeDispatch(IGraphicsDevice device)
    {
        if (!device.Capabilities.Features.Supports(GraphicsFeature.ComputeShaders))
            throw new NotSupportedException("The actual GLES context did not expose compute shaders.");

        using IGraphicsBuffer output = device.CreateBuffer(new GraphicsBufferDescription(
            sizeof(uint),
            GraphicsBufferUsage.Storage,
            GraphicsMemoryAccess.GpuToCpu));

        using IGraphicsShader shader = device.CreateShader(new GraphicsShaderDescription
        {
            Stage = GraphicsShaderStage.Compute,
            Format = GraphicsShaderFormat.GlslSource,
            Code = Encoding.UTF8.GetBytes(
                """
                #version 310 es
                layout(local_size_x = 1) in;
                layout(std430, binding = 0) buffer Result { uint value; } resultBuffer;
                void main() { resultBuffer.value = 0xA17E51u; }
                """)
        });

        using IGraphicsBindingLayout bindingLayout = device.CreateBindingLayout(
            new GraphicsBindingLayoutDescription(
            [
                new GraphicsBindingLayoutEntry(
                    0,
                    GraphicsDescriptorType.StorageBuffer,
                    1,
                    GraphicsShaderStages.Compute)
            ]));
        using IGraphicsBindingSet bindingSet = device.CreateBindingSet(bindingLayout);
        bindingSet.SetBuffer(0, 0, output, 0, sizeof(uint));

        IGraphicsBindingLayout[] layouts = [bindingLayout];
        using IGraphicsPipelineLayout pipelineLayout = device.CreatePipelineLayout(layouts);
        using IGraphicsComputePipeline pipeline = device.CreateComputePipeline(
            new GraphicsComputePipelineDescription(pipelineLayout, shader));

        IGraphicsCommandQueue queue = device.GetQueue(GraphicsQueueType.Compute);
        using IGraphicsCommandList commandList = queue.CreateCommandList();
        using IGraphicsFence fence = device.CreateFence();

        commandList.Begin();
        commandList.SetComputePipeline(pipeline);
        commandList.SetBindingSet(0, bindingSet);
        commandList.Dispatch(1);
        commandList.End();

        queue.Submit([commandList], fence, 1);
        if (!fence.Wait(1, TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The GLES compute fence did not complete within 10 seconds.");

        Memory<byte> readback = output.Map(GraphicsMapMode.Read);
        uint actual = BitConverter.ToUInt32(readback.Span);
        output.Unmap();
        if (actual != 0xA17E51u)
            throw new InvalidOperationException($"GLES compute result was 0x{actual:X8}, expected 0x00A17E51.");

        Console.WriteLine("PASS: GLES GLSL ES compute compilation, pipeline, SSBO binding, dispatch, and readback.");
    }
}
