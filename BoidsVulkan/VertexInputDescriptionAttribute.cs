using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public interface IVertexData
{ }

public class VertexAttributeDescription : Attribute
{
    public int Location { get; init; }
    public Format Format { get; init; }

    public VertexAttributeDescription(int location, Format format)
    {
        Location = location;
        Format = format;
    }
}

public static class IVertexDataExtension
{
    public static (VertexInputAttributeDescription[], VertexInputBindingDescription) CreateVertexInputDescription<T>(this T vertexData, int binding, VertexInputRate inputRate = VertexInputRate.Vertex)
        where T : unmanaged, IVertexData
    {
        var result = new List<VertexInputAttributeDescription>();


        foreach (var (property, attribute) in typeof(T).GetFields()
                                .Select(z => (z, z.GetCustomAttribute<VertexAttributeDescription>()))
                                .Where(z => z.Item2 != null))
        {
            result.Add(new VertexInputAttributeDescription()
            {
                Binding = (uint)binding,
                Location = (uint)attribute.Location,
                Format = attribute.Format,
                Offset = (uint)Marshal.OffsetOf<T>(property.Name)
            });
        }

        var bind = new VertexInputBindingDescription()
        {
            Binding = (uint)binding,
            Stride = (uint)Marshal.SizeOf<T>(),
            InputRate = inputRate
        };

        return ([.. result], bind);
    }
}