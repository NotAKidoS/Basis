
using DarkRift;
using UnityEngine;

public static class BasisCompressionOfRotation
{
    public static void CompressQuaternion(DarkRiftWriter Packer, Quaternion Rotation)
    {
        Packer.Write(BasisCompression.CompressQuaternion(Rotation));
    }
    public static void DecompressQuaternion(DarkRiftReader bitPacker, out Quaternion Quaternion)
    {
        //checked
        bitPacker.Read(out uint data);
        Quaternion = BasisCompression.DecompressQuaternion(data);
    }
}