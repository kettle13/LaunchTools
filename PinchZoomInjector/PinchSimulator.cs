namespace PinchZoomInjector;

/// <summary>
/// 中心点を挟んだ2指の仮想タッチ接触点を管理し、Windows Touch Injection API 経由で
/// Direct Manipulation に本物のマルチタッチジェスチャーとして解釈させる。
/// </summary>
internal sealed class PinchSimulator
{
    private const int ContactHalfSize = 4; // rcContact は必須フィールドなので中心±数pxで埋める

    private readonly POINTER_TOUCH_INFO[] _contacts = new POINTER_TOUCH_INFO[2];
    private bool _active;

    public bool IsActive => _active;

    public void Begin(int centerX, int centerY, double radius)
    {
        _contacts[0] = CreateContact(pointerId: 0);
        _contacts[1] = CreateContact(pointerId: 1);

        SetPosition(ref _contacts[0], centerX - (int)radius, centerY);
        SetPosition(ref _contacts[1], centerX + (int)radius, centerY);

        _contacts[0].pointerInfo.pointerFlags = NativeMethods.POINTER_FLAG_DOWN | NativeMethods.POINTER_FLAG_INRANGE | NativeMethods.POINTER_FLAG_INCONTACT;
        _contacts[1].pointerInfo.pointerFlags = NativeMethods.POINTER_FLAG_DOWN | NativeMethods.POINTER_FLAG_INRANGE | NativeMethods.POINTER_FLAG_INCONTACT;

        if (!NativeMethods.InjectTouchInput(2, _contacts))
        {
            throw new InvalidOperationException($"InjectTouchInput(DOWN) failed: 0x{Marshal_GetLastWin32Error():X8}");
        }

        _active = true;
    }

    public void Update(int centerX, int centerY, double radius)
    {
        if (!_active) return;

        SetPosition(ref _contacts[0], centerX - (int)radius, centerY);
        SetPosition(ref _contacts[1], centerX + (int)radius, centerY);

        _contacts[0].pointerInfo.pointerFlags = NativeMethods.POINTER_FLAG_UPDATE | NativeMethods.POINTER_FLAG_INRANGE | NativeMethods.POINTER_FLAG_INCONTACT;
        _contacts[1].pointerInfo.pointerFlags = NativeMethods.POINTER_FLAG_UPDATE | NativeMethods.POINTER_FLAG_INRANGE | NativeMethods.POINTER_FLAG_INCONTACT;

        NativeMethods.InjectTouchInput(2, _contacts);
    }

    public void End()
    {
        if (!_active) return;

        _contacts[0].pointerInfo.pointerFlags = NativeMethods.POINTER_FLAG_UP;
        _contacts[1].pointerInfo.pointerFlags = NativeMethods.POINTER_FLAG_UP;
        NativeMethods.InjectTouchInput(2, _contacts);

        _active = false;
    }

    private static POINTER_TOUCH_INFO CreateContact(uint pointerId) => new()
    {
        pointerInfo = new POINTER_INFO
        {
            pointerType = NativeMethods.PT_TOUCH,
            pointerId = pointerId,
        },
        touchMask = NativeMethods.TOUCH_MASK_CONTACTAREA,
    };

    private static void SetPosition(ref POINTER_TOUCH_INFO contact, int x, int y)
    {
        contact.pointerInfo.ptPixelLocation = new POINT(x, y);
        contact.rcContact = new RECT
        {
            Left = x - ContactHalfSize,
            Top = y - ContactHalfSize,
            Right = x + ContactHalfSize,
            Bottom = y + ContactHalfSize,
        };
    }

    private static int Marshal_GetLastWin32Error() => System.Runtime.InteropServices.Marshal.GetLastWin32Error();
}
