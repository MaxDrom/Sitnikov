using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkDescriptorSetUpdater
{
    private readonly
        List<(WriteDescriptorSet, DescriptorBufferInfo[])>
        _buffersWrites = [];

    private readonly VkContext _ctx;
    private readonly VkDevice _device;

    private readonly List<(WriteDescriptorSet, DescriptorImageInfo[])>
        _imageWrites = [];

    public VkDescriptorSetUpdater(VkContext ctx, VkDevice device)
    {
        _ctx = ctx;
        _device = device;
    }

    public VkDescriptorSetUpdater AppendWrite(
        DescriptorSet descriptorSet,
        int binding,
        DescriptorType descriptorType,
        DescriptorBufferInfo[] descriptorInfos,
        int arrayElement = 0)
    {
        WriteDescriptorSet writeDescriptor = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstBinding = (uint)binding,
            DescriptorType = descriptorType,
            DstSet = descriptorSet,
            DstArrayElement = (uint)arrayElement,
        };
        _buffersWrites.Add((writeDescriptor, descriptorInfos));
        return this;
    }

    public VkDescriptorSetUpdater AppendWrite(
        DescriptorSet descriptorSet,
        int binding,
        DescriptorType descriptorType,
        DescriptorImageInfo[] descriptorInfos,
        int arrayElement = 0)
    {
        WriteDescriptorSet writeDescriptor = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstBinding = (uint)binding,
            DescriptorType = descriptorType,
            DstSet = descriptorSet,
            DstArrayElement = (uint)arrayElement,
        };
        _imageWrites.Add((writeDescriptor, descriptorInfos));
        return this;
    }

    public VkDescriptorSetUpdater AppendCopy()
    {
        return this;
    }

    public unsafe void Update()
    {
        var writeDescriptors =
            stackalloc WriteDescriptorSet[_buffersWrites.Count +
                                          _imageWrites.Count];
        var currentDescr = 0;
        var pbufferInfos =
            stackalloc DescriptorBufferInfo[_buffersWrites.Sum(z =>
                z.Item2.Length)];
        for (var i = 0; i < _buffersWrites.Count; i++)
        {
            var (writeDescriptor, infos) = _buffersWrites[i];
            writeDescriptor.PBufferInfo = pbufferInfos;
            writeDescriptor.DescriptorCount = (uint)infos.Length;
            for (var j = 0; j < infos.Length; j++)
            {
                *pbufferInfos = infos[j];
                pbufferInfos++;
            }

            writeDescriptors[currentDescr] = writeDescriptor;
            currentDescr++;
        }

        var pImageInfos =
            stackalloc DescriptorImageInfo[_imageWrites.Sum(z =>
                z.Item2.Length)];
        for (var i = 0; i < _imageWrites.Count; i++)
        {
            var (writeDescriptor, infos) = _imageWrites[i];
            writeDescriptor.PImageInfo = pImageInfos;
            writeDescriptor.DescriptorCount = (uint)infos.Length;
            for (var j = 0; j < infos.Length; j++)
            {
                *pImageInfos = infos[j];
                pImageInfos++;
            }

            writeDescriptors[currentDescr] = writeDescriptor;
            currentDescr++;
        }

        _ctx.Api.UpdateDescriptorSets(_device.Device,
            (uint)(_buffersWrites.Count + _imageWrites.Count),
            writeDescriptors, null);
    }
}