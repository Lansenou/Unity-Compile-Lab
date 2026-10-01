namespace Game.Rendering
{
    public static class PixelBuffer
    {
        public static unsafe void Fill(byte[] pixels, byte value)
        {
            fixed (byte* p = pixels)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    p[i] = value;
                }
            }
        }
    }
}
