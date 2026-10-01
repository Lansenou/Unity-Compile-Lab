namespace Game.Imaging
{
    public sealed class PixelBuffer
    {
        private readonly byte[] pixels;

        public PixelBuffer(int width, int height)
        {
            pixels = new byte[width * height * 4];
        }

        public unsafe void Clear(byte value)
        {
            fixed (byte* start = pixels)
            {
                for (var p = start; p < start + pixels.Length; p++)
                {
                    *p = value;
                }
            }
        }
    }
}
