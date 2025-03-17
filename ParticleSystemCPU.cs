using System.Numerics;
using System.Runtime.InteropServices;
using BoidsVulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkAllocatorSystem;

namespace SymplecticIntegrators;

public class ParticleSystemCPUFactory : IParticleSystemFactory
{
    private SymplecticIntegrator<double, Vector<double>> _integrator;
    public ParticleSystemCPUFactory(SymplecticIntegrator<double, Vector<double>> integrator)
    {
        _integrator = integrator;
    }
    public IParticleSystem Create(VkContext ctx, VkDevice device, VkCommandPool commandPool, IVkAllocator allocator,
                                  IVkAllocator staggingAllocator, Instance[] initialData)
    {
        return new ParticleSystemCPU(ctx, device, staggingAllocator, initialData, _integrator);
    }
}

public class ParticleSystemCPU : IParticleSystem
{
    public VkBuffer<Instance> Buffer => _buffer;

    private VkContext _ctx;
    private VkDevice _device;
    private Instance[] _data;
    private IVkAllocator _staggingAllocator;
    private VkBuffer<Instance> _buffer;
    private VkMappedMemory<Instance> _mapped;
    private bool _disposedValue;
    private SymplecticIntegrator<double, Vector<double>> _integrator;

    public ParticleSystemCPU(VkContext ctx, VkDevice device, IVkAllocator staggingAllocator,
                             Instance[] initialData, SymplecticIntegrator<double, Vector<double>> integrator)
    {
        _data = new Instance[initialData.Length];
        Array.Copy(initialData, _data, initialData.Length);
        _integrator = integrator;
        _ctx = ctx;
        _device = device;
        _staggingAllocator = staggingAllocator;
        _buffer = new VkBuffer<Instance>(_data.Length, BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
                               _staggingAllocator);
        _mapped = _buffer.Map(0, _data.Length);
    }

    public async Task Update(double delta, double totalTime)
    {
        var taskList = new List<Task>();
        for (var i = 0; i < _data.Length - 1; i++)
        {
            var pos = _data[i].position;
            var tmpi = i;
            taskList.Add(Task.Run(() =>
                                  {
                                      var (q, p) = _integrator.Step(new Vector<double>([pos.X, totalTime]),
                                                                    new Vector<double>([pos.Y, 0]), delta);
                                      var newpos = new Vector2D<float>((float)q[0], (float)p[0]);
                                      _data[tmpi].position = newpos;
                                      _data[tmpi].offset = newpos - pos;
                                  }));
        }
        await Task.WhenAll(taskList);

        for (var i = 0; i < _data.Length; i++)
            _mapped[i] = _data[i];
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _mapped.Dispose();
                _buffer.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}