using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Vertex : IVertexData<Vertex>
{
    [VertexInputDescription(0, Format.R32G32Sfloat)]
    public Vector2D<float> position;

    [VertexInputDescription(1, Format.R32G32B32A32Sfloat)]
    public Vector4D<float> color;
}