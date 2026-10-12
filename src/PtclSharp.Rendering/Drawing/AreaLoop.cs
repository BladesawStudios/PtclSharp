using System.Numerics;
using System.Runtime.InteropServices;

namespace PtclSharp.Rendering.Drawing;

/// <summary>
/// The area loop plugin (EP04, nn::vfx AreaLoopSystem): the emitter is drawn several times with a growing offset, and the shader wraps
/// particles into a virtual box. Nothing is simulated; only the draw-time uniform block is built (EmitterDrawCallback, vfx_AreaLoop.cpp).
/// </summary>
public sealed class AreaLoop
{
    private readonly Vector3 _repeatOffset, _areaSize, _areaPos, _alphaRatio, _areaRotate;
    private readonly float _repeatNum, _clippingType, _isCameraLoop, _clippingHeight;

    public int RepeatCount => (int)(_repeatNum + 1);

    public AreaLoop(byte[] d)
    {
        static float F(byte[] b, int at) => BitConverter.ToSingle(b, at);
        Vector3 V(int at) => new(F(d, at), F(d, at + 4), F(d, at + 8));
        _repeatOffset = V(0x00);
        _repeatNum = F(d, 0x0C);
        _areaSize = V(0x10);
        _areaPos = V(0x20);
        _clippingType = BitConverter.ToInt32(d, 0x2C);
        _alphaRatio = V(0x30);
        _isCameraLoop = F(d, 0x3C);
        _areaRotate = V(0x40);
        _clippingHeight = d.Length >= 0x50 ? F(d, 0x4C) : 0f;
    }

    private static void StoreMatrix(Span<Vector4> v, int at, in Matrix4x4 m)
    {
        v[at] = new Vector4(m.M11, m.M12, m.M13, m.M14);
        v[at + 1] = new Vector4(m.M21, m.M22, m.M23, m.M24);
        v[at + 2] = new Vector4(m.M31, m.M32, m.M33, m.M34);
        v[at + 3] = new Vector4(m.M41, m.M42, m.M43, m.M44);
    }

    /// <summary>The block for repeat number <paramref name="repeat"/>.</summary>
    public byte[] Block(Matrix4x4 emitterSrt, Vector3 eye, Matrix4x4 billboard, int repeat)
    {
        Matrix4x4 box = Matrix4x4.CreateRotationX(_areaRotate.X) * Matrix4x4.CreateRotationY(_areaRotate.Y) * Matrix4x4.CreateRotationZ(_areaRotate.Z);
        Matrix4x4 mat;
        if (_isCameraLoop == 0f)
        {
            box.M41 += _areaPos.X; box.M42 += _areaPos.Y; box.M43 += _areaPos.Z;
            mat = box * emitterSrt;
        }
        else
        {
            Matrix4x4 camera = Matrix4x4.Identity;
            camera.M41 = eye.X; camera.M42 = eye.Y; camera.M43 = eye.Z;
            // the area position, rotated into camera space with the billboard matrix rows
            float tx = (_areaPos.X * billboard.M11) + (_areaPos.Y * billboard.M21) + (_areaPos.Z * billboard.M31);
            float ty = (_areaPos.X * billboard.M12) + (_areaPos.Y * billboard.M22) + (_areaPos.Z * billboard.M32);
            float tz = (_areaPos.X * billboard.M13) + (_areaPos.Y * billboard.M23) + (_areaPos.Z * billboard.M33);
            box.M41 += tx; box.M42 += ty; box.M43 += tz;
            mat = box * camera;
        }
        Matrix4x4.Invert(mat, out Matrix4x4 inv);

        var v = new Vector4[12];
        v[0] = new Vector4(_repeatOffset * repeat, _isCameraLoop);
        v[1] = new Vector4(_alphaRatio, 0);
        StoreMatrix(v, 2, mat);
        StoreMatrix(v, 6, inv);
        v[10] = new Vector4(_clippingType, _clippingHeight, 0, 0);
        v[11] = new Vector4(_areaSize, 0);
        return MemoryMarshal.AsBytes(v.AsSpan()).ToArray();
    }
}
