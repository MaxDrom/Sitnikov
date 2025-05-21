using Autofac.Features.AttributeFilters;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Sitnikov.BoidsVulkan;
using Sitnikov.BoidsVulkan.VkAllocatorSystem;
using Sitnikov.symplecticIntegrators;

namespace Sitnikov;

public sealed class ParticleSystemCpu : IParticleSystem
{
    private Instance[] _data;
    private Instance[] _data2;

    private readonly SymplecticIntegrator<double, Vector<double>>
        _integrator;

    private readonly VkMappedMemory<Instance> _mapped;
    private bool _disposedValue;

    public ParticleSystemCpu([MetadataFilter("Type", "HostVisible")]VkAllocator stagingAllocator,
        Instance[] initialData,
        SymplecticIntegrator<double, Vector<double>> integrator)
    {
        _data = new Instance[initialData.Length];
        _data2 = new Instance[initialData.Length];
        Array.Copy(initialData, _data, initialData.Length);
        Array.Copy(initialData, _data2, initialData.Length);
        _integrator = integrator;
        _buffer = new VkBuffer<Instance>(_data.Length,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            stagingAllocator);
        _mapped = Buffer.Map(0, _data.Length);
    }

    public VkBuffer<Instance> Buffer
    {
        get
        {
            for (var i = 0; i < _data.Length; i++) _mapped[i] = _data[i];
            return _buffer;
        }
    }

    public IEnumerable<Instance> DataOnCpu => _data2;

    private VkBuffer<Instance> _buffer;

    public async Task Update(double delta, double totalTime)
    {
        var taskList = new List<Task>();
        for (var i = 0; i < _data.Length - 1; i++)
        {
            var pos = _data[i].position;
            var number = i;
            taskList.Add(Task.Run(() =>
            {
                var (q, p) = _integrator.Step(
                    new Vector<double>([pos.X, totalTime]),
                    new Vector<double>([pos.Y, 0]), delta);
                var newPosition =
                    new Vector2D<float>((float)q[0], (float)p[0]);
                _data[number].position = newPosition;
                _data[number].offset = newPosition - pos;
            }));
        }
        var tmp = _data;
        _data2 = _data;
        _data = tmp;
        await Task.WhenAll(taskList);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _mapped.Dispose();
            Buffer.Dispose();
        }

        _disposedValue = true;
    }
}