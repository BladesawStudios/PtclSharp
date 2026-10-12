using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Drawing;

/// <summary>Creates and fills the fixed-size uniform buffers the particle shaders' blocks bind to.</summary>
internal sealed unsafe class UniformBuffers(GL gl)
{
    // The decompiled shaders declare every block as 4096 vectors, whatever the game actually fills.
    const int Size = 65536;

    public uint Create()
    {
        uint buffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
        gl.BufferData(BufferTargetARB.UniformBuffer, Size, null, BufferUsageARB.DynamicDraw);
        return buffer;
    }

    public void Upload(uint buffer, ReadOnlySpan<byte> data)
    {
        gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
        fixed (byte* p = data)
            gl.BufferSubData(BufferTargetARB.UniformBuffer, 0, (nuint)data.Length, p);
    }
}
