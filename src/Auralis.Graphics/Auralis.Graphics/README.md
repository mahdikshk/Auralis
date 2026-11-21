# Auralis.Graphics

`Auralis.Graphics` is the platform-independent graphics contract library. It targets .NET 10 and has no dependency on a native graphics binding, windowing system, rendering layer, ECS, or editor.

## Project organization

The implementation uses the directories already declared by `Auralis.Graphics.csproj`:

- `Abstractions`: backend identifiers, common exceptions, and device-object lifetime.
- `Devices`: provider registration, device creation options, adapter information, and device capabilities.
- `Resources`: buffer, texture/view, sampler, shader, and format descriptions/contracts.
- `Commands`: command-list lifecycle and command queue/recording contracts.
- `Synchronization`: timeline-style fences and backend-neutral resource dependencies.
- `Pipelines`: shader-stage, vertex-input, descriptor-layout, and pipeline descriptions.
- `Presentation`: native target descriptors, presentation surfaces, image acquisition, and swapchains.
- `Backends/OpenGL` contains the desktop OpenGL implementation and its Silk.NET context-provider integration.
- `Backends/Direct3D12`, `Backends/Vulkan`, and `Backends/Metal` remain reserved for future implementations.

The OpenGL provider is included in this assembly but is never registered automatically. Applications opt into it by registering an `OpenGLGraphicsBackendProvider` explicitly.

## Backend registration and device creation

Backend implementations provide `IGraphicsBackendProvider` and register explicitly:

```csharp
var providers = new GraphicsBackendProviderRegistry();
providers.Register(new MyDirect3D12Provider());
providers.Register(new MyVulkanProvider());

using IGraphicsDevice device = await GraphicsDeviceFactory.CreateAsync(
    providers,
    new GraphicsDeviceOptions
    {
        Backend = GraphicsBackend.Vulkan,
        RequiredQueues = GraphicsQueueCapabilities.Graphics |
                         GraphicsQueueCapabilities.Compute
    });
```

The Direct3D 12 and Vulkan provider types in this example are supplied by future backend implementations; the OpenGL provider is included in this assembly. No reflection, assembly scanning, or native API type discovery is performed. When `Backend` is omitted, available providers are tried in registration order. Enumerate adapter information through `GraphicsDeviceFactory.EnumerateAdaptersAsync`, then use the selected provider's opaque `GraphicsAdapterId` in device options. An adapter ID is backend-specific, so adapter selection requires an explicit backend.

Device creation is independent of presentation. A device may be created and used for compute, resource uploads, or offscreen work without creating a surface or swapchain.

## Capabilities

`GraphicsDeviceCapabilities` contains the actual feature set, queue capabilities, reported limits, and per-format support reported by the selected backend. The common layer does not infer support from `GraphicsBackend` and does not provide fabricated defaults. Core operations represented by the common API form the baseline contract; `GraphicsFeature` describes optional capabilities that may be requested at device creation and queried later.

Texture format support and sample counts are queried from the capability snapshot. Format support is distinct from texture usage flags: a texture description expresses intended use, while the device decides whether that format/usage combination is valid.

The capability snapshot exposes device-level validation for buffers, textures, texture views, and vertex input layouts. Reported non-zero dimension, array-layer, buffer-size, and vertex-input limits are enforced; zero means the backend did not report a limit. Texture-view validation checks the effective view format against the device's reported format support. Surface presentation remains a separate queue/surface query and is not inferred from format support.

## Backend translation guidance

The common interfaces intentionally describe semantic operations rather than exposing one API's object model. Future providers should translate them as follows, while preserving each native API's actual constraints:

| Common concept | Direct3D 12 | Vulkan | Metal |
| --- | --- | --- | --- |
| Device and adapter | DXGI adapter + D3D12 device | physical device + logical device | `MTLDevice` |
| Logical queues | direct/compute/copy command queues; roles may share a queue | queues selected from compatible queue families; roles may share a queue | `MTLCommandQueue`; use command-buffer/encoder semantics for work classes |
| Command list | command allocator + graphics command list | command pool + command buffer | command buffer with render/compute/blit encoders |
| Resource dependencies | transition, UAV, and aliasing barriers | pipeline barriers, access masks, stages, and image layouts | encoder/resource-use ordering and hazard-tracking mechanisms |
| Binding layout and sets | root signatures and descriptor heaps/tables | descriptor set layouts and descriptor sets | resource slots and argument buffers |
| Presentation | DXGI swapchain and Win32 target data | platform surface plus swapchain and present-capable queue | drawable/layer presentation, commonly through a `CAMetalLayer` |
| Shader artifacts | DXIL | SPIR-V | Metal library or supported source compilation path |

This table is guidance, not a claim that the native concepts have identical semantics. In particular:

- A logical graphics, compute, or copy queue does not guarantee a physically separate native queue. Vulkan queue-family compatibility and Metal queue behavior differ from D3D12 queue types. A backend may return the same queue for multiple logical roles when that matches its native model.
- `GraphicsResourceBarrier` is intentionally not a universal D3D12 resource-state enum. Vulkan requires explicit execution/access/layout dependencies in more cases; Metal may express hazards through resource tracking and encoder boundaries. Backends must preserve the required visibility and ordering and may expose optional extension interfaces for native-only synchronization.
- `GraphicsMemoryAccess` and `GraphicsMemoryPreference` express intent, not a promise about heaps or storage modes. D3D12 heap placement, Vulkan memory-type selection, and Metal storage modes need backend-specific selection.
- Descriptor flags such as partially-bound, update-after-bind, and variable-count are not universally supported. Providers must validate them against actual device capabilities rather than emulate them silently.
- Shader format identifiers are opaque. DXIL, SPIR-V, Metal libraries, and source text must not be treated as interchangeable bytecode.
- Presentation support is surface-specific. A queue that can present to one native target may not present to another; surface creation must validate the chosen queue and native handle combination.

## Resources and lifetime

Descriptors expose logical intent rather than native heap or resource-state details. For example, `GraphicsMemoryPreference` is a placement hint; it does not promise that a resource occupies a particular physical heap.

Texture views may select one mip/layer from a 2D array texture or one cube face as a 2D view. Format reinterpretation is opt-in through GraphicsResourceCreationOptions.MutableFormat and is restricted by the common layer to explicit linear/sRGB compatibility pairs (RGBA8, BGRA8, BC1, BC3, BC7, and ASTC 4x4). The device capability validator checks that the effective view format supports the parent texture's declared usages. Native backends may impose additional format-view restrictions and must validate the corresponding native mutable-format requirements; this flag does not promise arbitrary format casts.

GraphicsTextureUsage.Present marks textures intended for a presentation surface. Its support is validated through the surface/queue compatibility contract rather than a per-format GraphicsFormatSupport bit.

`IGraphicsResource` objects are owned by one device and are disposed deterministically. The common `GraphicsDeviceObject` base class provides idempotent disposal and no finalizer. A backend must defer physical destruction of resources that are still referenced by submitted GPU work. Disposal releases the application's reference; it must not invalidate work already accepted by the GPU.

CPU mapping is explicit and transient, not a persistent allocation. `Read` and `ReadWrite` preserve existing contents; `Write` is discard/write-only, so the previous contents of the mapped range are undefined and callers must initialize every byte they rely on. Use `ReadWrite` or another read-preserving strategy for partial updates that need old bytes. A `MemoryHandle` pin must be released before `Unmap` or resource disposal can finish, and previously acquired `Span<byte>` values must not be used after `Unmap` or disposal. Backends perform required cache maintenance and may reject mapping when the chosen access intent is not CPU-visible.

Shader descriptions carry an artifact-format identifier and opaque bytes. DXIL, SPIR-V, Metal libraries, source artifacts, and application-defined formats are not assumed to be interchangeable. Shader compilation is outside this library.

## Commands and synchronization

A command list is created from a queue, then moves through `Initial → Recording → Executable → Pending`. The backend must enforce the legal transition rules and report completion before a pending list is reset or reused. `GraphicsCommandListLifecycle` provides a small, allocation-free state tracker for implementations and hardware-free validation. Command recording is caller-thread-owned; separate command lists may be recorded concurrently. Queue submission validates that lists belong to the same device and a compatible queue.

Graphics, compute, and copy queues are requested through `IGraphicsDevice.GetQueue` and are represented by `IGraphicsCommandQueue`. `IGraphicsQueue` is the shared device-owned queue identity (`Device` and `Index`). Presentation candidates are obtained through `IGraphicsDevice.GetPresentationQueue` and may implement only `IGraphicsQueue`; they do not promise command-list creation, submission, or queue waits. Always query `IGraphicsPresentationSurface.SupportsPresentation` for the actual target before passing a candidate to `CreateSwapChain`. A backend may return a command-capable queue as a presentation candidate when appropriate. `IGraphicsCommandList` defines render, compute, copy, and indirect operations; implementations reject operations illegal for the queue's command role or current recording state.

`GraphicsResourceBarrier` describes source/destination execution stages and access intents. GraphicsPipelineStage.AllGraphics includes indirect drawing, vertex input, all declared graphics shader stages, depth/stencil, and color attachment output; each stage is also individually selectable. The barrier validator uses an explicit compatibility table to check access flags against resource kind, declared buffer/texture usage, and source/destination stages. Host buffer access is also checked against the buffer's CPU memory-access intent.

Texture barriers specify semantic layouts. Layout transitions are checked against declared texture usage: sampled, color/depth-stencil attachment, copy-source/destination, and presentation layouts require their corresponding usage flags. General has no additional usage requirement. These are deliberately not Direct3D 12 resource states; backends translate them into their own hazard, layout, visibility, and synchronization model.

Barriers may target an entire resource or a precise range: GraphicsBufferRange selects a byte interval, while GraphicsTextureSubresourceRange selects aspects, mip levels, and array layers. A null range means the entire resource. Range bounds are validated against the resource description, including format-aware color/depth/stencil aspect validation. Barriers currently accept buffers and underlying textures only; texture views, samplers, and shaders are rejected. To synchronize a view's subresources, specify its parent texture and the corresponding subresource range. A backend may conservatively widen a valid range when required by its native API, but must preserve the requested dependency. Fences expose timeline-style CPU waits/signals and queue-side GPU waits. CPU `Signal(value)` is monotonic and cannot overtake an earlier pending GPU signal: an attempted CPU signal greater than a pending GPU signal is rejected without deleting or superseding that GPU sync. GPU completion values advance only after the corresponding native sync reports completion. Callers must not use a CPU signal as a shortcut to claim that still-pending GPU work has completed. Backend-specific extension interfaces may be added later when a native capability cannot be represented faithfully.

## Pipelines and descriptors

Binding layouts are immutable descriptions with explicit binding indices, descriptor types, counts, shader stages, and optional flags. A variable-count binding is required to have the highest binding number; input order is otherwise unrestricted. Binding sets hold resource references and are bound by set index.

Render pipeline descriptions accept a shader-stage set rather than requiring a fixed vertex/fragment pair. Conventional pipelines require a vertex shader and may include geometry, fragment, and a paired tessellation-control/evaluation stage set. Tessellation uses PatchList topology and an explicit patch-control-point count. Mesh pipelines require a mesh shader, may include a task shader and fragment shader, and cannot include vertex input, vertex, geometry, or tessellation stages. Optional geometry, tessellation, mesh, and task stages are checked against their corresponding independent device features; a task shader additionally requires a mesh shader stage. Compute pipeline validation requires GraphicsFeature.ComputeShaders.

Pipeline validation also checks vertex attribute formats and vertex-input limits against device capabilities. Enabling blending requires the target format to report GraphicsFormatSupport.Blend. Compute pipelines remain independent of graphics fixed-function state.

This API intentionally omits render graphs, materials, render passes, and renderer scheduling. Unsupported optional pipeline features must be reported by a backend rather than silently approximated.

## Native presentation interoperability

A host or platform integration implements `IGraphicsPresentationTarget` and returns a snapshot containing:

- A platform identifier.
- A collection of typed opaque handles (not one universal pointer).
- Per-handle ownership (`Borrowed` or `OwnedByGraphics`).
- The drawable extent, revision, and suspended state.

For example, a Win32 target can supply an HWND and HINSTANCE; X11 can supply a Display and Window; Wayland can supply a display and surface; Android can supply an ANativeWindow; Apple platforms can supply a Metal layer. These types are represented only by opaque `nint` values and handle-kind identifiers in this project. Native handle extraction and conversion belong to platform integrations.

The application remains responsible for the lifetime of every borrowed native object and of the `IGraphicsPresentationTarget` itself. The graphics surface borrows the target and must not destroy borrowed handles. Handles marked `OwnedByGraphics` transfer release responsibility to the graphics surface/backend.

Surface creation occurs after device creation. Device-level GraphicsQueueCapabilities.Present is only a coarse indication that the device may expose presentation queues; it does not prove that a specific queue can present to a specific target. Request command-capable queues through IGraphicsDevice.GetQueue and presentation candidates through IGraphicsDevice.GetPresentationQueue. Query IGraphicsPresentationSurface.SupportsPresentation(queue) for the actual target, then pass the selected compatible IGraphicsQueue explicitly to CreateSwapChain(queue, description). A backend may return a command-capable queue or a dedicated presentation-only queue; the latter implements IGraphicsQueue but not IGraphicsCommandQueue. The swapchain exposes the queue it uses for presentation. This keeps headless/offscreen device creation independent of presentation and accommodates surface-specific queue-family support such as Vulkan.

Resizing recreates the swapchain image set and invalidates outstanding acquired-image values. When a target is suspended (for example, a minimized window), the application should skip acquisition/presentation until a non-zero extent is reported. When native surface identity changes, create a new surface from the updated target snapshot and dispose the old surface after its work is complete.

## Threading

Provider registration is a startup operation and the registry is not intended for concurrent mutation. Device/resource objects are not universally thread-safe; callers must synchronize mutations and disposal unless an implementation explicitly documents otherwise. Independent command lists may be recorded concurrently. Queue submission and CPU/GPU synchronization are backend-defined but must follow the command-list lifecycle contract.

## Implementation status

This project provides backend-neutral graphics descriptors, validation, device/command/pipeline/synchronization contracts, presentation interop, and a desktop OpenGL implementation in the same `Auralis.Graphics` assembly. The OpenGL backend creates and manages OpenGL resources and executes recorded commands through an injected, host-owned context provider. It does not create native windows or contexts. Direct3D 12, Vulkan, and Metal implementations are outside the scope of this project state.


## OpenGL backend

The backend is implemented in `Backends/OpenGL` in the existing `Auralis.Graphics` assembly. It uses the official Silk.NET packages `Silk.NET.OpenGL`, `Silk.NET.OpenGL.Extensions.ARB`, `Silk.NET.OpenGL.Legacy.Extensions.EXT`, and `Silk.NET.OpenGL.Extensions.EXT`, all pinned to version **2.23.0**. No OpenTK, alternative GL binding, handwritten P/Invoke entry points, or Silk.NET.Windowing dependency is used. The supported baseline is **desktop OpenGL 4.5 Core Profile**. OpenGL ES contexts are not supported.

### Registration and context integration

The application creates its own context through its platform integration and supplies it using the backend-specific `IOpenGLContextProvider` contract. The backend does not create windows or contexts and does not interpret `GraphicsNativeSurfaceDescription` as a context descriptor.

```csharp
var registry = new GraphicsBackendProviderRegistry();
registry.Register(new OpenGLGraphicsBackendProvider(contextProvider));

using IGraphicsDevice device = await GraphicsDeviceFactory.CreateAsync(
    registry,
    new GraphicsDeviceOptions
    {
        Backend = GraphicsBackend.OpenGL,
        RequiredQueues = GraphicsQueueCapabilities.Graphics |
                         GraphicsQueueCapabilities.Copy
    });

IGraphicsCommandQueue graphicsQueue = device.GetQueue(GraphicsQueueType.Graphics);
IGraphicsCommandQueue copyQueue = device.GetQueue(GraphicsQueueType.Copy);
```

The context provider supplies a stable context identity, function-pointer resolver, `MakeCurrent`/`ReleaseCurrent`, ownership policy, and optional buffer swapping. It may wrap a host-created WGL, GLX, EGL, or native Apple desktop OpenGL context. Those native APIs and context creation remain the host/platform integration's responsibility. A headless/offscreen context is valid; no drawable is required for device creation or offscreen resource/command work.

The hot `WithContext` paths use inline `MakeCurrent`/`ReleaseCurrent` try/finally scopes, avoiding per-operation context-lease allocations; the `Action<GL>` overload no longer creates an intermediate `Func` wrapper. The device makes the supplied context current before loading Silk.NET's `GL` dispatch. It checks the actual version and Core Profile before constructing the Silk.NET extension wrappers. A context lease calls `ReleaseCurrent` only after `MakeCurrent` returns successfully, and releases the context even when the guarded GL operation throws. The same provider is used to reactivate the context for each native operation and submission; context access is serialized by the device. A dispatch object is never shared with an unrelated context.

Context-object ownership and GL-state ownership are separate:
- `OpenGLContextOwnership.Borrowed`: Auralis never disposes the host context. The provider is not disposed by the device.
- `OpenGLContextOwnership.Owned`: the provider has transferred its lifetime to the device; device disposal calls the provider's `Dispose`.
- `OpenGLContextStateOwnership.Exclusive` is required. The initial backend does not save/restore all GL state. The host must not issue unrelated GL commands on the context while it is attached to Auralis. `Shared` state ownership is rejected instead of silently corrupting host state.

The context provider owns the meaning of its context identity and must guarantee that its resolver and activation methods refer to that same context. A drawable is optional. For presentation, the provider must additionally verify that a `GraphicsPresentationTargetSnapshot` identifies the drawable belonging to this exact context, expose the presentation modes it can honor, apply the selected mode, and swap that drawable's buffers. The backend rejects a target that the provider does not recognize.

### Implemented functionality

| Area | Current implementation |
| --- | --- |
| Device/provider | Explicit registration, 4.5 Core validation, vendor/renderer/version/driver strings, queried limits and capabilities |
| Formats | Silk.NET internal-format queries and per-format operation/sample-count capability snapshots; unsupported formats remain unsupported |
| Buffers | Immutable DSA storage, direct `MapNamedBufferRange`/`UnmapNamedBuffer` views over driver memory, explicit per-access storage permissions, invalidated views after unmap/disposal, and buffer copy |
| Textures | Immutable 1D/2D/3D/cube storage, arrays and multisample 2D storage; texture views via `ARB_texture_view` |
| Samplers | Sampler objects with filtering, addressing, compare, LOD, border color, and extension-gated anisotropy |
| Shaders | GLSL source compilation and extension-gated SPIR-V ingestion/specialization through Silk.NET; driver compile/specialization logs surfaced; requested SPIR-V entry point and specialization constants are passed to the driver |
| Descriptors | Uniform/storage buffers, sampled/storage textures, samplers, and combined texture/sampler sets; explicit deterministic binding-point mapping |
| Graphics/compute pipelines | Linked monolithic graphics or compute program; DSA vertex-array object for conventional pipelines; geometry/tessellation stages; conditional EXT or NV mesh/task stages with their distinct GLSL dialects and dispatch contracts; fixed raster/depth/stencil/blend state |
| Rendering/compute commands | Deferred render-target binding, viewport/scissor, typed clears, vertex/index binding, direct/indexed/indirect draws, direct/indirect mesh-task draws, and direct/indirect compute dispatch |
| Copy/barriers | Deferred buffer and whole-texture copies plus destination-access-based OpenGL memory barriers |
| Synchronization | GL sync objects, allocation-efficient FIFO fence tracking, CPU wait/polling, and non-blocking GPU-side waits for already-created fence values |
| Presentation | Acquired images are offscreen color textures; present blits to the provider-verified default drawable and swaps buffers |
| Queues | Logical graphics/compute/copy queues sharing one serialized context command stream; no physical parallel queues are claimed |

The backend reports OpenGL implementation strings in `GraphicsDeviceInfo`. The injected context is already selected, so system-wide physical adapter enumeration is unavailable: `EnumerateAdaptersAsync` returns an empty list rather than fabricating adapter IDs, PCI identifiers, memory sizes, or adapter types. Unknown limits remain zero. Format support is derived from the active context's internal-format operation queries and advertised extensions; it is not inferred from the enum alone.

### Binding-point mapping

OpenGL has no Vulkan-style descriptor-set object. Auralis maps common sets and bindings to OpenGL binding points deterministically:
1. Process descriptor sets in ascending set index.
2. Process bindings within each set in ascending binding number, independent of the order used to construct the common description.
3. Maintain independent slot sequences for uniform-buffer bindings, storage-buffer bindings, texture units (sampled textures, samplers, and combined texture/sampler entries), and image units (storage textures).
4. A descriptor array consumes consecutive slots in its namespace.

GLSL must use the resulting numeric binding in its `layout(binding = N)` declaration. This backend does not rewrite GLSL source to translate a `set` qualifier. For example, UBO binding points are assigned independently from texture units; the same integer may therefore occur in two different namespaces. `PartiallyBound` is supported for fixed-count bindings: empty array elements are permitted in the recorded snapshot and are explicitly unbound at execution (UBO/SSBO binding base, texture unit, sampler, or image unit as appropriate). Partially-bound bindings do not permit a half-populated combined texture/sampler element; each element must be wholly populated or wholly empty. Shaders must not access array elements left unpopulated by the application. `UpdateAfterBind` and `VariableCount` remain unsupported and are rejected. A descriptor set snapshot is still immutable with respect to later host updates.

### Command execution and queue model

OpenGL does not expose native command buffers. Auralis uses CPU-side typed deferred command records: recording methods validate and retain arguments without invoking GL; submission activates the provider's context and executes commands in order. Each command list keeps one atomic native reference for every distinct GL resource referenced by its recorded commands or captured descriptor snapshots. Public resource disposal closes the owner reference and rejects new retains, but an already-recorded list can still use the retained native handle. Reset and disposal of an unsubmitted list release those references; a pending list cannot be reset, and disposing it waits for GPU completion. Submission command-list reference arrays are rented and returned only after the associated GL completion sync has signaled. The executable-to-pending transition is serialized with command-list disposal under the device context gate. Submission failure after issuing GL work finishes that work before returning lists to an executable state and releasing submission ownership. Device disposal finishes and drains outstanding submissions, releases registered native objects while the context is still current, and only then tears down GL dispatch/context ownership.

The graphics, compute, and copy queue objects are logical roles over one context's ordered command stream. They are serialized and do not promise independent graphics, compute, or transfer hardware queues. Compute pipeline creation, direct dispatch, and indirect dispatch are implemented when the required OpenGL 4.3+ compute functionality is present. The backend advertises a logical compute queue in that case, but does not advertise separate compute/transfer queue features.

The fence abstraction is implemented with ordered GL sync objects stored in a reusable FIFO queue; monotonic values make a tree structure unnecessary. CPU waits use client waits; queue waits enqueue a GL server-side wait and do not block the CPU. A queue wait requires the requested fence value to have already been signaled/submitted, because OpenGL cannot enqueue a wait on a future sync object that does not yet exist. The implementation provides monotonic timeline-style values on top of individual GL sync objects; it does not claim the native Vulkan timeline-semaphore feature.

Resource barriers are translated into GL memory-barrier bits based on destination access. Texture layouts in the common barrier description remain semantic hints, not native OpenGL image layouts. The backend uses broad `AllBarrierBits` only for generic memory read/write intents where a narrower consumer cannot be identified.

### Explicit limitations

- No OpenGL ES support.
- No native context/window creation or platform integration is included.
- The backend does not expose the native default framebuffer as an `IGraphicsTexture`. Swapchain images are Auralis-owned offscreen textures; presentation blits one to framebuffer 0 and then calls the provider's swap operation.
- The injected provider/platform integration must correctly identify the exact drawable, support and apply the requested present mode, and implement buffer swapping. No WGL/GLX/EGL/native Apple context or window creation code is included.
- The current swapchain presents a color-only offscreen image ring; it does not expose default framebuffer depth/stencil attachments or native swapchain image handles.
- Mesh/task shader pipelines are conditionally supported through either `GL_EXT_mesh_shader` or `GL_NV_mesh_shader`. Each GLSL mesh/task shader must enable exactly one matching extension dialect, and a pipeline cannot mix EXT and NV stages. EXT dispatch uses the native three-dimensional API and queried per-axis/total limits. NV direct dispatch uses `glDrawMeshTasksNV(first, count)`, validates the one-dimensional count against `GL_MAX_DRAW_MESH_TASKS_COUNT_NV`, and remains available even when both extensions are present. NV indirect dispatch is deliberately rejected: its native two-field `{ count, first }` record is not the common Auralis three-dimensional `{ groupCountX, groupCountY, groupCountZ }` payload. Mesh/task SPIR-V is rejected until its EXT-versus-NV execution model can be identified and validated rather than guessed.
- No separate native compute/transfer queue. Compute work is serialized on the same context stream as graphics and copies.
- Descriptor indexing, update-after-bind, and variable-count descriptors are unsupported. `PartiallyBound` is implemented for fixed descriptor counts: missing UBO/SSBO slots are cleared with `glBindBufferBase(..., 0)`, texture/sampler slots with zero texture/sampler bindings, and storage-image slots with a zero image binding. A partially populated combined texture/sampler element is rejected; an element must be either fully populated or entirely empty.
- Independent blend enable and color-write masks use optional `GL_EXT_draw_buffers2` entry points; per-attachment blend factors/equations use the official Silk.NET ARB draw-buffers-blend wrapper. Independent state is reported only when indexed-state entry points are available.
- Wide lines are reported only when the driver-reported `GL_ALIASED_LINE_WIDTH_RANGE` includes a non-unit width; pipeline widths outside that range are rejected.
- Depth-bounds testing is optional and requires `GL_EXT_depth_bounds_test`.
- Depth-bias clamping is optional and requires `GL_ARB_polygon_offset_clamp`; non-zero clamps remain rejected without it.
- SPIR-V ingestion is optional and requires `GL_ARB_gl_spirv`; GLSL source compilation remains available independently.
- EXT mesh/task stages require `GL_EXT_mesh_shader`, its direct/indirect entry points, and non-zero mesh/task per-axis and total work-group limits. NV mesh/task stages require `GL_NV_mesh_shader`, the native direct entry point, and non-zero `GL_MAX_DRAW_MESH_TASKS_COUNT_NV`. GLSL source must explicitly enable the matching extension; the desktop native stage tokens have the same values, but the GLSL built-ins and dispatch semantics do not. A pipeline cannot mix EXT and NV mesh/task stages. EXT pipelines support direct and 3D indirect dispatch (including the native multi-indirect function when present). NV pipelines support direct one-dimensional dispatch only; the NV indirect record is `{ count, first }`, unlike Auralis's common 3D `{ groupCountX, groupCountY, groupCountZ }` record, and is therefore rejected rather than reinterpreted. Mesh/task SPIR-V is currently rejected because this path does not yet inspect the module execution model to distinguish EXT from NV.
- Debug output uses `GL_KHR_debug` (core in modern desktop OpenGL); it is diagnostic output, not GPU-based validation. Callback output, notification filtering, and command debug groups are implemented. Object labels are applied to buffers, textures, texture views, samplers, shaders, and graphics/compute pipeline program objects (plus graphics vertex arrays).
- Sparse resources, timestamp-query reporting, and multi-viewport are not advertised: they require complete resource residency/query-result/common-command contracts, not just extension detection.
- Shared host GL-state preservation is not implemented; exclusive GL-state ownership is required. Command execution owns framebuffer, copy/indirect-buffer, and scissor state while it runs; temporary bindings are reset to a clean baseline (framebuffer and transient buffer bindings zero, scissor disabled) rather than restoring arbitrary host bindings. A host must not interleave its own GL state mutations with backend command execution. Binding framebuffer zero is only a cleanup baseline; offscreen rendering always binds and validates its own framebuffer and does not depend on a usable default framebuffer.
- Physical adapter IDs, PCI IDs, dedicated memory size, and system-wide adapter enumeration are unknown through the injected-context contract.

### Tests and verification

The ordinary graphics test suite includes hardware-independent tests for provider registration/availability, presentation drawable/present-mode validation, texture targets, sampler translations, descriptor slot allocation, pipeline enum/vertex-format mappings, compute dispatch limit validation, resource barriers, effective-mip render-target validation, mapped-memory lifetime, SPIR-V validation, debug-option validation, line-width validation, blend-state validation, partially-bound descriptor snapshots, unsupported descriptor-flag rejection, GLES format-to-capability mapping consistency, EXT/NV mesh-shader source-dialect and stage-token validation, mesh/task per-axis and total work-group limit validation, optional pipeline-feature validation, and the native resource-reference state machine. The unit tests use deterministic instrumented resources where a live device is unnecessary; they are not actual-driver integration tests.

#### Opt-in hardware integration tests

A separate executable test harness lives in `Tests/Auralis.Graphics.OpenGL.IntegrationTests`. It uses the test-only `Tests/Auralis.Graphics.OpenGL.TestSupport` project, which creates a hidden desktop OpenGL 4.5 Core context through the official Silk.NET GLFW binding. No windowing or GLFW dependency is added to the `Auralis.Graphics` backend assembly.

Run it on a machine with a working desktop session, GLFW native library, and an OpenGL 4.5 Core driver:

```bash
AURALIS_RUN_OPENGL_HARDWARE_TESTS=1 dotnet run --project Tests/Auralis.Graphics.OpenGL.IntegrationTests/Auralis.Graphics.OpenGL.IntegrationTests.csproj --configuration Release
```

On Windows PowerShell, use:

```powershell
$env:AURALIS_RUN_OPENGL_HARDWARE_TESTS = "1"
dotnet run --project Tests/Auralis.Graphics.OpenGL.IntegrationTests/Auralis.Graphics.OpenGL.IntegrationTests.csproj --configuration Release
```

The runner reports each case as `PASS`, `SKIP`, or `FAIL`. It returns exit code **77** when the explicit opt-in is absent or a usable 4.5 Core context cannot be created; unsupported optional extensions and unsupported format/features are reported as individual skips. EXT mesh tests require the EXT API; NV mesh tests are separately gated by the NV API. The current cases exercise dispose-before-submit buffer/texture references, disposal after a GPU submission has been queued, reusable command-list re-submission after source-owner disposal, completion polling without a user-provided fence, GPU copy plus fence completion/readback, partially-bound storage-buffer descriptor switching across compute pipelines, indirect compute dispatch, conventional direct, instanced, indexed and indexed-indirect drawing plus non-indexed indirect drawing, and conditional EXT mesh/task direct/indirect drawing plus separate NV direct mesh and task+mesh shader pipelines with GPU pixel readback. NV indirect remains an expected unsupported operation because of its incompatible command-record layout. A separate offscreen graphics case compiles and links GLSL shaders, renders a deterministic triangle into an RGBA8 texture, copies that texture through the graphics command API, reads both images back with OpenGL 4.5 texture readback, compares the copied image byte-for-byte, and checks both a covered pixel and the cleared background pixel. A descriptor integration case additionally exercises UBO, SSBO, sampled-texture, standalone-sampler, combined texture/sampler, and storage-image bindings; a partially-bound SSBO array; descriptor-set mutation after recording; original-resource disposal before submission; explicit missing-slot unbinding; driver binding-state checks; and GPU-visible SSBO readback. The harness checks the GL error queue after each case. Mesh/task cases are skipped when the driver does not expose the required extension and valid limits.

These hardware tests are intentionally **not executed by CI**. The workflow only restores and builds the harness; it never invokes `dotnet run` or `dotnet test` for it. The existing hardware-independent unit test project remains the only test suite executed by CI.

**Actual-driver baseline (maintainer-verified before this capability-expansion continuation):** the OpenGL smoke test, device creation, shader compilation, GPU resource allocation, offscreen drawing, GPU resource copying, fence completion, presentation, swapchain resizing, and graphics-resource/device disposal have all successfully passed against an actual OpenGL driver. These are established results and are not described as untested. This documentation update does not claim that the current editing environment reran those tests. The backend has no mandatory windowing package; the host supplies the OpenGL 4.5 Core context provider.

### Allocation and performance notes

- Buffer mappings use `glMapNamedBufferRange`/`glUnmapNamedBuffer` and an invalidatable `MemoryManager<byte>` over the native mapping; the normal path no longer uses a per-map staging array and DSA readback/writeback copies.
- Command recording reuses command-list-owned `List<T>` storage for commands, barrier resource references, render-target views, and binding snapshots. Snapshot values are copied into command-list storage so later descriptor-set mutation does not rewrite recorded values. A deduplicated per-command-list retention set holds one atomic native reference per distinct native resource, and releases those references on reset/disposal after any pending submission has completed. The retention set uses a list plus a reference-identity hash set; no allocation or throughput benchmark has yet quantified its overhead.
- Submission rents command-list reference arrays from `ArrayPool<T>` and retains them until the completion sync is observed. Pending entries are value types held in a reusable list.
- The device tracks only live native resources in a reference-identity `HashSet<T>` protected by the existing context gate. A resource leaves that registry when its native deletion succeeds or ownership is handed to the deferred-deletion queue, avoiding an unbounded history of already-disposed managed wrappers.
- Fence sync points use a FIFO `Queue<T>` because values are monotonic; completed syncs are deleted from the head without a temporary cleanup collection.
- Context activation no longer allocates an `OpenGLContextLease` per `WithContext` call, and the `Action<GL>` overload no longer routes through an intermediate `Func<GL, T>`. Callback-based call sites can still allocate delegates/closures; zero-allocation context execution has not been established.
- A separate hardware-dependent BenchmarkDotNet project lives in `Benchmarks/Auralis.Graphics.OpenGL.Benchmarks`. Run it on a machine with a working OpenGL 4.5 Core driver using `dotnet run --project Benchmarks/Auralis.Graphics.OpenGL.Benchmarks/Auralis.Graphics.OpenGL.Benchmarks.csproj --configuration Release -- --filter '*OpenGLBackendBenchmarks*'`. The suite uses `MemoryDiagnoser` and measures command recording of one buffer copy, command submission plus GPU fence wait, completed fence-value polling, buffer creation/disposal, and texture creation/disposal. Results are specific to the selected driver, GPU, CPU, OS, context provider, and power/clock state.
- The benchmark project is **built but never executed by CI**. CI restores and compiles it only. No before/after allocation or throughput results are claimed until the benchmark suite has been run on named hardware; the initial harness does not itself constitute a measurement.


## OpenGL ES backend

Auralis includes a separate OpenGL ES backend under `Backends/OpenGLES`. It implements the existing `IGraphicsBackendProvider`, `IGraphicsDevice`, resource, pipeline, command, synchronization, and presentation contracts. It does not add a second graphics API and does not change the desktop OpenGL implementation.

### Host context contract

The backend requires a host-created, currentable OpenGL ES context reporting **OpenGL ES 3.1 or newer**. The host implements `IOpenGLESContextProvider` and supplies:

The host supplies exclusive ownership of the GLES context state. Command execution resets its temporary framebuffer, copy/indirect-buffer bindings, and scissor-enable state to a clean baseline rather than preserving arbitrary host GL state.

- a stable context identity and ownership mode;
- exclusive GLES state ownership (the backend does not save and restore arbitrary external GL state);
- `MakeCurrent`, `ReleaseCurrent`, and a context-specific `GetProcAddress`;
- optional drawable compatibility, present-mode, and buffer-swap operations.

The backend does not create EGL contexts, windows, Android surfaces, or platform-specific objects. The same existing registry and device factory select the provider:

```csharp
var registry = new GraphicsBackendProviderRegistry();
registry.Register(new OpenGLESGraphicsBackendProvider(hostGlesContextProvider));

using IGraphicsDevice device = await GraphicsDeviceFactory.CreateAsync(
    registry,
    new GraphicsDeviceOptions
    {
        Backend = GraphicsBackend.OpenGLES,
        RequiredQueues = GraphicsQueueCapabilities.Graphics |
                         GraphicsQueueCapabilities.Compute
    });
```

`hostGlesContextProvider` is an application/platform object implementing `IOpenGLESContextProvider`; it is not supplied by the graphics assembly. A borrowed provider remains owned by the host. An owned provider is disposed by the device, including when device initialization fails.

### GLES capability model

- ES 3.1 is the minimum accepted context. The device reads the actual version, vendor, renderer, extension list, and implementation limits after activating the supplied context.
- Compute shaders, indirect drawing, shader-storage buffers, image load/store, memory barriers, and core 2D multisample texture storage use the ES 3.1 path. Multisample sample-count flags are queried per format with the internal-format query API; the backend does not infer support from power-of-two values.
- ES 3.2 adds core geometry and tessellation stages. Patch control points are applied through the GLES patch-parameter operation and checked against the queried `GL_MAX_PATCH_VERTICES`; devices without tessellation support continue to reject tessellation pipelines.
- The GLES core framebuffer path queries both `GL_MAX_COLOR_ATTACHMENTS` and `GL_MAX_DRAW_BUFFERS`. Multiple ordered color attachments and depth-only framebuffers are supported within those limits. One common blend state applies to every attachment; independent per-attachment blending requires `GL_EXT_draw_buffers_indexed` and its resolved native functions.
- Typed unsigned/signed integer color clears use the matching GLES integer clear operations and require the corresponding integer attachment format. Normalized and floating formats reject integer clears.
- Multi-draw indirect uses `GL_EXT_multi_draw_indirect` when both native entry points resolve. Without that extension, multiple indirect commands are executed as a CPU loop of native single-indirect calls over the GPU-resident argument buffer; the fallback does not read argument data back and does not advertise the native `MultiDrawIndirect` feature.
- Direct base-vertex drawing requires `GL_EXT_draw_elements_base_vertex`. Base-instance drawing requires `GL_EXT_base_instance` and its base-vertex dependency. These features are detected separately and unavailable entry points are never called.
- Cube-map arrays use core GLES 3.2 or `GL_OES_texture_cube_map_array`; the layer-face count must be a multiple of six. GLES has no native 1D texture target, so 1D textures remain unsupported.
- Texture views support identity/full-resource views without an extension. Subresource and format-reinterpreted views require `GL_EXT_texture_view` or `GL_OES_texture_view`, and the corresponding EXT/OES function address must resolve from the current context provider. Native view objects retain their parent texture.
- Texture image copies use `GL_EXT_copy_image` or `GL_OES_copy_image` when available. This path copies each mip and supports the array/3D/cube subresource layout handled by the native copy operation, without requiring color-attachment usage. When neither extension is available, a narrowly scoped framebuffer-blit fallback remains limited to compatible single-sample, single-layer, single-mip RGBA8 2D textures that really are framebuffer attachments.
- Format support is reported separately for sampling, filtering, color/depth attachment, storage, blending, copy use, and per-format sample counts. The mapping includes the normalized, signed-normalized, integer, float, sRGB, and depth formats that this implementation can allocate; extension-dependent float color attachments and float filtering remain gated. An internal format being mappable does not itself advertise every usage.
- Timeline fences use GLES sync objects. Logical graphics, compute, and copy queue roles are serialized over one GLES command stream; they are not physically independent hardware queues.
- Anisotropic filtering is advertised only when `GL_EXT_texture_filter_anisotropic` is present and the driver-reported maximum is queried.
- Presentation is advertised only when the injected context provider reports a compatible host drawable. A generic currentable context does not imply presentation support.
- GLES mesh/task shaders use the distinct `GL_EXT_mesh_shader` contract when the extension and all required direct/indirect entry points resolve. ESSL 3.10 or newer must enable `GL_EXT_mesh_shader`; mesh/task work-group count and total-count limits are queried from the driver. Native three-dimensional direct/indirect dispatch is supported. Native multi-draw mesh indirect is optional; without it, multiple native indirect calls are issued using GPU-resident offsets. This does not imply desktop NV semantics. GLES SPIR-V ingestion remains unsupported: desktop `GL_ARB_gl_spirv` is not a GLES ingestion mechanism, and this backend has no verified native GLES SPIR-V path.
- GLES does not claim descriptor indexing, update-after-bind, variable-count descriptors, persistent mapping, sparse residency, timestamp-query results, or indexed multi-viewport. Those features either lack a native implementation here or need common API semantics that the existing contracts do not yet expose.

### Implemented operations

- Buffers use GLES bind-to-edit allocation and transient `MapBufferRange`/unmap mapping. Read mappings copy into managed staging memory and write mappings upload with `BufferSubData`; persistent mapping and desktop buffer-storage APIs are not used.
- Immutable texture storage supports GLES 2D, 2D-array, 3D, cube, cube-array (3.2/OES), and eligible 2D multisample textures. The GLES view model supports identity views and extension-backed mip/layer/format subresource views; unsupported target/view combinations are rejected.
- Supported texture formats include normalized and sRGB formats, 8/16/32-bit signed and unsigned integer formats, signed-normalized formats, 16/32-bit floating-point formats, and D16/D24S8/D32F/D32FS8 depth formats. Per-format usages remain restricted by the GLES version, extension set, and actual implementation capabilities.
- Samplers use GLES sampler objects. Border-clamp modes are rejected; anisotropy is set only when the extension is available.
- Shader creation compiles GLSL ES source with driver diagnostics. Source must declare `#version 300 es`, `#version 310 es`, or `#version 320 es` as appropriate for the stage and context. Desktop GLSL source and SPIR-V are rejected.
- Binding layouts map uniform buffers, storage buffers, sampled textures, samplers, combined texture/sampler pairs, and storage images to GLES binding slots. Unsupported binding flags and incomplete non-partially-bound descriptors are rejected.
- Render pipelines validate attachment count, color/depth formats, and sample counts against the active render targets. Common blending is supported across MRT attachments; differing per-attachment state is accepted only with the indexed-blending extension. GLES 3.2 tessellation uses the native patch parameter and the queried patch-vertex maximum.
- Command lists support buffer copies, EXT/OES image copies and a restricted framebuffer-blit fallback, render-target binding, viewport/scissor, typed floating/unsigned/signed color clears, depth/stencil clears, vertex/index binding, direct/instanced drawing, native or emulated multi-command indirect drawing, extension-gated base-vertex/base-instance drawing, compute dispatch/indirect dispatch, descriptor binds, and memory barriers.
- Recorded and submitted command lists retain referenced native resources, including texture-view names and their parent textures, until reset/disposal or observed GPU completion. Device shutdown finishes the GLES stream and drains pending submissions before native resource deletion.
- When a host drawable is supplied, the presentation surface creates backend-owned color images and framebuffer objects, blits the selected image to the host default framebuffer, and delegates the actual buffer swap to the host provider.

### Remaining GLES limitations

- The GLES backend still uses one context command stream. Logical graphics/compute/copy roles do not represent separate physical hardware queues.
- 1D textures remain unsupported because GLES has no native 1D texture target.
- GLES EXT mesh/task support is extension-gated and depends on `GL_EXT_mesh_shader`, the resolved required entry points, and driver-reported limits. Desktop NV's one-dimensional dispatch and 8-byte indirect command are a separate contract and are not reused by GLES.
- SPIR-V ingestion remains unsupported because the backend has no verified native GLES ingestion extension/path.
- Native multi-draw-indirect is optional. Its CPU-loop fallback preserves GPU-resident indirect-buffer semantics but does not claim native multi-draw feature support.
- Independent per-attachment blending requires `GL_EXT_draw_buffers_indexed`; otherwise the same blend configuration applies across all MRT attachments.
- Multi-viewport remains unsupported because the common viewport/scissor commands have no viewport index.
- Descriptor indexing, update-after-bind, and variable descriptor counts remain unsupported by this descriptor model. The current descriptor snapshots are intentionally preserved.
- Sparse-resource residency remains unsupported because the common texture/resource API has no page-commit/decommit and residency contract.
- Timestamp-query result retrieval remains unsupported because the common API has no usable query-result lifecycle, availability, unit, or disjoint-result contract.
Run the actual-driver test executable explicitly on a machine with a usable GLES 3.1+ implementation and GLFW support:

```bash
AURALIS_RUN_OPENGLES_HARDWARE_TESTS=1 dotnet run --project Tests/Auralis.Graphics.OpenGLES.IntegrationTests/Auralis.Graphics.OpenGLES.IntegrationTests.csproj --configuration Release
```

Without the environment variable, or when GLFW cannot create an ES context, the runner reports `SKIP` and exits with code 77. The CI workflow restores and builds the GLES hardware integration project but deliberately does not execute it; CI runs only the hardware-independent graphics unit tests.

The opt-in GLES runner currently exercises buffer allocation/map/copy and fence completion; compute shader compilation, pipeline creation, SSBO dispatch and readback; UBO, SSBO, sampled texture, standalone sampler, combined texture/sampler and storage-image descriptors; partially-bound SSBO element unbinding; descriptor-set mutation after recording; disposal of original resources before submission; pipeline-layout switching; 2D, 2D-array, 3D and cube texture allocation; sampler creation/disposal; resource retention after recording; reusable command-list submissions after owner disposal; offscreen graphics rendering; two-attachment MRT clears and fragment outputs with separate attachment readback; signed and unsigned integer clear readback with type-mismatch rejection; array and indexed indirect draws with multiple commands, nonzero argument offsets and default/explicit strides; and multisample framebuffer clearing followed by shader sampling through `sampler2DMS` into an SSBO. The multisample case does not imply a common resolve operation. The descriptor case checks original texture, sampler and image binding state as well as GPU-visible output values. CI builds this runner but does not execute it. The GLES context and driver path has not been executed in this environment. The hardware-independent tests cover version/extension parsing, capability reporting, provider registration/failure behavior, GLSL ES stage/version validation, texture-format/description validation, compute-dispatch boundaries/no-op recording, texture-copy compatibility and the native resource-reference state machine. A successful build or unit-test run is not evidence of real-driver correctness. Actual-driver execution must be reported separately from build-only verification.
