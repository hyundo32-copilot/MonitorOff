using System.Runtime.InteropServices;

namespace MonitorOff;

/// <summary>
/// 트레이에 표시할 초승달 아이콘을 GDI+ 로 직접 그린다. (macOS 의 moon.fill 대체)
/// </summary>
internal static class MoonIcon
{
    private const int Supersample = 4;

    // 아이콘 정규화 좌계: 큰 원에서 오프셋 원을 빼면 초승달이 된다.
    private const double CrescentCenterX = 0.46;
    private const double CrescentCenterY = 0.52;
    private const double CrescentRadius = 0.40;
    private const double CutoutCenterX = 0.70;
    private const double CutoutCenterY = 0.32;
    private const double CutoutRadius = 0.36;

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo piconinfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfoHeader pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    public static Icon Create(int size = 32)
    {
        var alpha = RenderCrescentAlpha(size);

        // 32bpp top-down DIB: 흰색(BGRA) + 알파 채널
        var header = new BitmapInfoHeader
        {
            biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = size,
            biHeight = -size,
            biPlanes = 1,
            biBitCount = 32,
        };

        IntPtr dibBits;
        var hColor = CreateDIBSection(IntPtr.Zero, ref header, 0, out dibBits, IntPtr.Zero, 0);
        if (hColor == IntPtr.Zero || dibBits == IntPtr.Zero)
        {
            throw new InvalidOperationException("아이콘용 DIB 섹션을 만들 수 없습니다.");
        }

        var bgra = new byte[alpha.Length * 4];
        for (var i = 0; i < alpha.Length; i++)
        {
            bgra[(i * 4) + 0] = 255; // B
            bgra[(i * 4) + 1] = 255; // G
            bgra[(i * 4) + 2] = 255; // R
            bgra[(i * 4) + 3] = alpha[i]; // A
        }

        Marshal.Copy(bgra, 0, dibBits, bgra.Length);

        // AND 마스크: 알파가 0인 픽셀을 투명으로 표시
        var stride = ((size + 31) / 32) * 4;
        var mask = new byte[stride * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (alpha[(y * size) + x] == 0)
                {
                    mask[(y * stride) + (x / 8)] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        var maskBits = Marshal.AllocHGlobal(mask.Length);
        try
        {
            Marshal.Copy(mask, 0, maskBits, mask.Length);
            var hMask = CreateBitmap(size, size, 1, 1, maskBits);
            if (hMask == IntPtr.Zero)
            {
                throw new InvalidOperationException("아이콘용 마스크를 만들 수 없습니다.");
            }

            var info = new IconInfo { fIcon = true, hbmMask = hMask, hbmColor = hColor };
            var hIcon = CreateIconIndirect(ref info);

            // CreateIconIndirect 은 비트맵을 복사하므로 즉시 정리해도 된다.
            DeleteObject(hMask);
            DeleteObject(hColor);
            hColor = IntPtr.Zero;

            if (hIcon == IntPtr.Zero)
            {
                throw new InvalidOperationException("아이콘을 만들 수 없습니다.");
            }

            // FromHandle 은 핸들 소유권이 없으므로 복제본을 만든 뒤 원본을 파괴한다.
            using var temporary = Icon.FromHandle(hIcon);
            var icon = (Icon)temporary.Clone();
            DestroyIcon(hIcon);
            return icon;
        }
        finally
        {
            Marshal.FreeHGlobal(maskBits);
            if (hColor != IntPtr.Zero)
            {
                DeleteObject(hColor);
            }
        }
    }

    private static byte[] RenderCrescentAlpha(int size)
    {
        var alpha = new byte[size * size];
        var ax = CrescentCenterX * size;
        var ay = CrescentCenterY * size;
        var ar = CrescentRadius * size;
        var bx = CutoutCenterX * size;
        var by = CutoutCenterY * size;
        var br = CutoutRadius * size;

        var samples = Supersample * Supersample;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var hits = 0;
                for (var sy = 0; sy < Supersample; sy++)
                {
                    for (var sx = 0; sx < Supersample; sx++)
                    {
                        var px = x + ((sx + 0.5) / Supersample);
                        var py = y + ((sy + 0.5) / Supersample);
                        var inside = Distance(px, py, ax, ay) <= ar
                            && Distance(px, py, bx, by) >= br;
                        if (inside)
                        {
                            hits++;
                        }
                    }
                }

                alpha[(y * size) + x] = (byte)((255 * hits) / samples);
            }
        }

        return alpha;
    }

    private static double Distance(double x, double y, double centerX, double centerY)
    {
        var dx = x - centerX;
        var dy = y - centerY;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
