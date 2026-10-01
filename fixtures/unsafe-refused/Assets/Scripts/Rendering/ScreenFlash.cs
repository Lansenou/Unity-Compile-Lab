using Game.Rendering;
using UnityEngine;

public class ScreenFlash : MonoBehaviour
{
    private readonly byte[] pixels = new byte[64 * 64 * 4];

    public void Flash()
    {
        PixelBuffer.Fill(pixels, 255);
    }
}
