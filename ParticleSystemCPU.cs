using BoidsVulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkAllocatorSystem;

namespace SymplecticIntegrators;

public class ParticleSystemCpuFactory : IParticleSystemFactory
{
    private readonly SymplecticIntegrator<double, Vector<double>>
        _integrator;

    public ParticleSystemCpuFactory(
        SymplecticIntegrator<double, Vector<double>> integrator)
    {
        _integrator = integrator;
    }

    public IParticleSystem Create(VkContext ctx,
        VkDevice device,
        VkCommandPool commandPool,
        VkAllocator allocator,
        VkAllocator staggingAllocator,
        Instance[] initialData)
    {
        return new ParticleSystemCpu(staggingAllocator, initialData,
            _integrator);
    }
}

public class ParticleSystemCpu : IParticleSystem
{
    private readonly Instance[] _data;

    private readonly SymplecticIntegrator<double, Vector<double>>
        _integrator;

    private readonly VkMappedMemory<Instance> _mapped;
    private readonly VkAllocator _staggingAllocator;
    private bool _disposedValue;

    public ParticleSystemCpu(VkAllocator staggingAllocator,
        Instance[] initialData,
        SymplecticIntegrator<double, Vector<double>> integrator)
    {
        _data = new Instance[initialData.Length];
        Array.Copy(initialData, _data, initialData.Length);
        _integrator = integrator;
        _staggingAllocator = staggingAllocator;
        Buffer = new VkBuffer<Instance>(_data.Length,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _staggingAllocator);
        _mapped = Buffer.Map(0, _data.Length);
    }

    public VkBuffer<Instance> Buffer { get; }

    public async Task Update(double delta, double totalTime)
    {
        var taskList = new List<Task>();
        for (var i = 0; i < _data.Length - 1; i++)
        {
            var pos = _data[i].position;
            var tmpi = i;
            taskList.Add(Task.Run(() =>
            {
                var (q, p) = _integrator.Step(
                    new Vector<double>([pos.X, totalTime]),
                    new Vector<double>([pos.Y, 0]), delta);
                var newpos =
                    new Vector2D<float>((float)q[0], (float)p[0]);
                _data[tmpi].position = newpos;
                _data[tmpi].offset = newpos - pos;
            }));
        }

        await Task.WhenAll(taskList);

        for (var i = 0; i < _data.Length; i++) _mapped[i] = _data[i];
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _mapped.Dispose();
                Buffer.Dispose();
            }

            _disposedValue = true;
        }
    }
}