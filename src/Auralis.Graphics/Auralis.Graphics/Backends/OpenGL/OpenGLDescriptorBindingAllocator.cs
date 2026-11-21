using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Pipelines;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Assigns deterministic OpenGL binding points. Sets are processed in declared order and
/// bindings within each set in ascending binding order. Each GL binding namespace has its own
/// monotonically increasing slot sequence.
/// </summary>
internal static class OpenGLDescriptorBindingAllocator
{
    internal static OpenGLDescriptorMapping[] Allocate(
        ReadOnlySpan<GraphicsBindingLayoutDescription> layouts,
        uint maxUniformBufferBindings,
        uint maxStorageBufferBindings,
        uint maxTextureUnits,
        uint maxImageUnits)
    {
        uint uniformSlot = 0;
        uint storageSlot = 0;
        uint textureUnit = 0;
        uint imageUnit = 0;
        List<OpenGLDescriptorMapping> mappings = new();

        for (uint setIndex = 0; setIndex < layouts.Length; setIndex++)
        {
            ReadOnlySpan<GraphicsBindingLayoutEntry> entries = layouts[(int)setIndex].Entries;
            GraphicsBindingLayoutEntry[] ordered = entries.ToArray();
            Array.Sort(ordered, static (left, right) => left.Binding.CompareTo(right.Binding));

            for (int i = 0; i < ordered.Length; i++)
            {
                GraphicsBindingLayoutEntry entry = ordered[i];
                OpenGLDescriptorNamespace descriptorNamespace = GetNamespace(entry.Type);
                uint nextSlot = descriptorNamespace switch
                {
                    OpenGLDescriptorNamespace.UniformBuffer => uniformSlot,
                    OpenGLDescriptorNamespace.StorageBuffer => storageSlot,
                    OpenGLDescriptorNamespace.TextureUnit => textureUnit,
                    _ => imageUnit
                };

                uint maximum = descriptorNamespace switch
                {
                    OpenGLDescriptorNamespace.UniformBuffer => maxUniformBufferBindings,
                    OpenGLDescriptorNamespace.StorageBuffer => maxStorageBufferBindings,
                    OpenGLDescriptorNamespace.TextureUnit => maxTextureUnits,
                    _ => maxImageUnits
                };

                if (nextSlot > maximum || entry.Count > maximum - nextSlot)
                    throw new GraphicsValidationException(
                        $"Descriptor set {setIndex}, binding {entry.Binding} exceeds the OpenGL {descriptorNamespace} binding-point limit ({maximum}).");

                mappings.Add(new OpenGLDescriptorMapping(setIndex, entry, nextSlot));
                uint nextValue = checked(nextSlot + entry.Count);
                switch (descriptorNamespace)
                {
                    case OpenGLDescriptorNamespace.UniformBuffer: uniformSlot = nextValue; break;
                    case OpenGLDescriptorNamespace.StorageBuffer: storageSlot = nextValue; break;
                    case OpenGLDescriptorNamespace.TextureUnit: textureUnit = nextValue; break;
                    case OpenGLDescriptorNamespace.ImageUnit: imageUnit = nextValue; break;
                }
            }
        }

        return mappings.ToArray();
    }

    private static OpenGLDescriptorNamespace GetNamespace(GraphicsDescriptorType type) => type switch
    {
        GraphicsDescriptorType.UniformBuffer => OpenGLDescriptorNamespace.UniformBuffer,
        GraphicsDescriptorType.StorageBuffer => OpenGLDescriptorNamespace.StorageBuffer,
        GraphicsDescriptorType.SampledTexture or GraphicsDescriptorType.Sampler or
            GraphicsDescriptorType.CombinedTextureSampler => OpenGLDescriptorNamespace.TextureUnit,
        GraphicsDescriptorType.StorageTexture => OpenGLDescriptorNamespace.ImageUnit,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
