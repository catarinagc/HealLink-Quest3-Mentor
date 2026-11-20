using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PassthroughFrameReceiver : MonoBehaviour
{
    public int listenPort = 5057;
    [Tooltip("The Quad_PV renderer under the billboard.")]
    public Renderer targetRenderer;
    [Tooltip("Flip the image vertically if it shows upside down.")]
    public bool flipVertical = false;
    public bool logStatus = true;

    private UdpClient udp;
    private Thread thread;
    private volatile bool running;
    private readonly object frameLock = new object();
    private byte[] latestJpeg;
    private Texture2D texture;
    private bool materialConfigured;
    private int framesShown;
    private float nextLogTime;

    // reassembly state (receive thread only)
    private uint currentFrame;
    private bool hasFrame;
    private byte[][] parts;
    private int expected;
    private int received;

    public bool stereo = true;
    public Material stereoMaterial;

    //private Texture2D scratch;
    //private Material activeMaterial;
    //private int decodeFailures;
    //private Texture2D NewTex()
    //{
    //    var t = new Texture2D(2, 2, TextureFormat.RGB24, false);
    //    t.wrapMode = TextureWrapMode.Clamp;
    //    t.filterMode = FilterMode.Bilinear;
    //    return t;
    //}

    private void OnEnable()
    {
        try
        {
            udp = new UdpClient(listenPort);
            udp.Client.ReceiveBufferSize = 8 * 1024 * 1024;
            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true };
            thread.Start();
            if (logStatus) Debug.Log("PassthroughFrameReceiver listening on UDP " + listenPort);
        }
        catch (Exception e)
        {
            running = false;
            Debug.LogError("PassthroughFrameReceiver could not bind port " + listenPort + ": " + e.Message);
        }
    }

    private void OnDisable()
    {
        running = false;
        if (udp != null) { udp.Close(); udp = null; }
        thread = null;
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    private void Update()
    {
        byte[] jpg = null;
        lock (frameLock) { jpg = latestJpeg; latestJpeg = null; }
        if (jpg == null || targetRenderer == null) return;

        if (texture == null)
        {
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
        }

        if (!texture.LoadImage(jpg, false)) return;

        if (!materialConfigured)
        {
            if (stereo && stereoMaterial != null)
            {
                Material m = new Material(stereoMaterial);
                targetRenderer.material = m;
                m.mainTexture = texture;
                m.SetFloat("_FlipY", flipVertical ? 1f : 0f);
            }
            else
            {
                Material m = targetRenderer.material;
                m.mainTexture = texture;
                m.mainTextureScale = new Vector2(1f, flipVertical ? -1f : 1f);
                m.mainTextureOffset = new Vector2(0f, flipVertical ? 1f : 0f);
            }
            materialConfigured = true;
        }

        framesShown++;
        if (logStatus && Time.unscaledTime >= nextLogTime)
        {
            nextLogTime = Time.unscaledTime + 3f;
            Debug.Log("PassthroughFrameReceiver shown frames=" + framesShown + ", size=" + texture.width + "x" + texture.height);
        }
    }

    //private void Update()
    //{
    //    byte[] jpg = null;
    //    lock (frameLock) { jpg = latestJpeg; latestJpeg = null; }
    //    if (jpg == null || targetRenderer == null) return;

    //    // reject obviously broken JPEGs (must start FFD8 and end FFD9)
    //    if (jpg.Length < 4 || jpg[0] != 0xFF || jpg[1] != 0xD8 ||
    //        jpg[jpg.Length - 2] != 0xFF || jpg[jpg.Length - 1] != 0xD9)
    //    {
    //        decodeFailures++;
    //        return;
    //    }

    //    if (texture == null) texture = NewTex();
    //    if (scratch == null) scratch = NewTex();

    //    // decode off-screen, only swap on success
    //    if (!scratch.LoadImage(jpg, false) || scratch.width < 16 || scratch.height < 16)
    //    {
    //        decodeFailures++;
    //        return;
    //    }
    //    Texture2D tmp = texture; texture = scratch; scratch = tmp;

    //    if (!materialConfigured)
    //    {
    //        if (stereo && stereoMaterial != null)
    //        {
    //            activeMaterial = new Material(stereoMaterial);
    //            targetRenderer.material = activeMaterial;
    //            activeMaterial.SetFloat("_FlipY", flipVertical ? 1f : 0f);
    //        }
    //        else
    //        {
    //            activeMaterial = targetRenderer.material;
    //            activeMaterial.mainTextureScale = new Vector2(1f, flipVertical ? -1f : 1f);
    //            activeMaterial.mainTextureOffset = new Vector2(0f, flipVertical ? 1f : 0f);
    //        }
    //        materialConfigured = true;
    //    }
    //    activeMaterial.mainTexture = texture;

    //    framesShown++;
    //    if (logStatus && Time.unscaledTime >= nextLogTime)
    //    {
    //        nextLogTime = Time.unscaledTime + 3f;
    //        Debug.Log("PassthroughFrameReceiver shown=" + framesShown + ", failed=" + decodeFailures +
    //                  ", size=" + texture.width + "x" + texture.height);
    //    }
    //}

    private void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref any);
                HandlePacket(data);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
            catch (Exception e) { Debug.LogWarning("PassthroughFrameReceiver: " + e.Message); }
        }
    }

    private void HandlePacket(byte[] data)
    {
        if (data == null || data.Length <= 10 || data[0] != (byte)'P' || data[1] != (byte)'C') return;

        uint frameId = (uint)(data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24));
        int index = data[6] | (data[7] << 8);
        int count = data[8] | (data[9] << 8);
        if (count <= 0 || count > 512 || index >= count) return;

        if (!hasFrame || frameId != currentFrame)
        {
            if (hasFrame && (int)(frameId - currentFrame) < 0) return;  // older frame, ignore
            currentFrame = frameId;
            hasFrame = true;
            parts = new byte[count][];
            expected = count;
            received = 0;
        }

        if (parts == null || count != expected || parts[index] != null) return;

        byte[] payload = new byte[data.Length - 10];
        Buffer.BlockCopy(data, 10, payload, 0, payload.Length);
        parts[index] = payload;
        received++;

        if (received < expected) return;

        int total = 0;
        for (int i = 0; i < expected; i++) total += parts[i].Length;
        byte[] jpg = new byte[total];
        int pos = 0;
        for (int i = 0; i < expected; i++)
        {
            Buffer.BlockCopy(parts[i], 0, jpg, pos, parts[i].Length);
            pos += parts[i].Length;
        }

        parts = null;   // later duplicates of this frame are ignored
        lock (frameLock) { latestJpeg = jpg; }
    }
}