using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
namespace BoidsVulkan;


[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Vertex : IVertexData<Vertex>
{
    [VertexAttributeDescription(0, Format.R32G32Sfloat)]
    public Vector2D<float> position;

    [VertexAttributeDescription(1, Format.R32G32B32A32Sfloat)]
    public Vector4D<float> color;
}