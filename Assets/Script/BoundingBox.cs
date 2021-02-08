using System.Runtime.InteropServices;

namespace TinyYoloV2 {

//
// The layout of this structure must be matched with one defined in Common.hlsl
//
[StructLayout(LayoutKind.Sequential)]
readonly struct BoundingBox
{
    public readonly float x, y, w, h;
    public readonly uint classIndex;
    public readonly float score;

    // sizeof(BoundingBox)
    public static int Size = 6 * sizeof(int);
};

} // namespace TinyYoloV2
