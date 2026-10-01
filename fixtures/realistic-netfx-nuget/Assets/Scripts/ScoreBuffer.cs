using System;
using System.Buffers;
using UnityEngine;

// Uses the System.Memory and System.Buffers NuGet DLLs from Assets/Plugins, as many projects do.
public class ScoreBuffer : MonoBehaviour
{
    private readonly int[] scores = new int[16];

    public int Capacity()
    {
        Span<int> view = scores.AsSpan();
        byte[] scratch = ArrayPool<byte>.Shared.Rent(64);
        ArrayPool<byte>.Shared.Return(scratch);
        return view.Length + "scores".AsSpan().Length;
    }
}
