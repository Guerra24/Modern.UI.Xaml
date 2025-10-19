//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//

using System.Runtime.CompilerServices;
using TerraFX.Interop.Windows;
using TerraFX.Interop.WinRT;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using WinRT;
using static TerraFX.Interop.Windows.GWL;
using static TerraFX.Interop.Windows.SW;
using static TerraFX.Interop.Windows.SWP;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.WS;

namespace Modern.UI.Xaml;

public partial class XamlCompositionSurface : IDisposable
{
    private HWND xamlHwnd = default;

    private DesktopWindowXamlSource desktopWindowXamlSource = null!;

    private ComPtr<IDesktopWindowXamlSourceNative2> nativeSource = default;

    public UIElement Content
    {
        get => desktopWindowXamlSource.Content;
        set => desktopWindowXamlSource.Content = value;
    }

    public XamlCompositionSurface(HWND parent, Func<UIElement>? content = null)
    {
        Initialize(parent, content);
    }

    private unsafe void Initialize(HWND parent, Func<UIElement>? content)
    {
        desktopWindowXamlSource = new();

        ((IUnknown*)((IWinRTObject)desktopWindowXamlSource).NativeObject.ThisPtr)->QueryInterface(__uuidof<IDesktopWindowXamlSourceNative2>(), (void**)nativeSource.GetAddressOf());

        nativeSource.Get()->AttachToWindow(parent);
        nativeSource.Get()->get_WindowHandle((HWND*)Unsafe.AsPointer(ref xamlHwnd));

        if (content != null)
            desktopWindowXamlSource.Content = content();

        ((XamlApplication)Application.Current).surfaces.Add(this);
    }

    public void Resize(int x, int y, int width, int height)
    {
        SetWindowPos(xamlHwnd, HWND.NULL, x, y, width, height, SWP_NOACTIVATE | SWP_NOZORDER);
    }

    internal HDWP Resize(HDWP hdwp, int x, int y, int width, int height)
    {
        return DeferWindowPos(hdwp, xamlHwnd, HWND.NULL, x, y, width, height, SWP_NOACTIVATE | SWP_NOZORDER);
    }

    public void OnSetFocus()
    {
        SetFocus(xamlHwnd);
    }

    public void ClickThrough()
    {
        nint dwExStyle = GetWindowLongPtrW(xamlHwnd, GWL_EXSTYLE);
        dwExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
        SetWindowLongPtrW(xamlHwnd, GWL_EXSTYLE, dwExStyle);
    }

    public void Show()
    {
        ShowWindow(xamlHwnd, SW_SHOW);
    }

    internal unsafe bool PreTranslateMessage(MSG* msg)
    {
        BOOL result = false;

        nativeSource.Get()->PreTranslateMessage(msg, &result);

        return result;
    }

    public void Dispose()
    {
        desktopWindowXamlSource.Dispose();
        ((XamlApplication)Application.Current).surfaces.Remove(this);
    }

}
