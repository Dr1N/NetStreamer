using SharpDX.DXGI;

namespace ScreenDublicator
{
    public class PointerInfo
    {
        public byte[] PtrShapeBuffer;
        public OutputDuplicatePointerShapeInformation ShapeInfo;
        public SharpDX.Mathematics.Interop.RawPoint Position;
        public bool Visible;
        public int BufferSize;
        public int WhoUpdatedPositionLast;
        public long LastTimeStamp;
     }
}