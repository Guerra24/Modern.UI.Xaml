//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Modern.UI.Xaml.XamlApplication;
using static TerraFX.Interop.Windows.SM;
using static TerraFX.Interop.Windows.SW;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.WM;
using static TerraFX.Interop.Windows.WS;

namespace Modern.UI.Xaml;

public class XamlWindow
{
    private HWND window = default;
    private SUBCLASSPROC windowSubclassProc = null!;
    private XamlCompositionSurface xamlSurface = null!, titlebarSurface = null!;

    private string Title;

    public UIElement Content
    {
        get => xamlSurface.Content;
        set => xamlSurface.Content = value;
    }

    public int Width { get; set; }
    public int Height { get; set; }
    public int MinWidth { get; set; } = 250;
    public int MinHeight { get; set; } = 250;

    public XamlWindow(string title) : this(title, CW_USEDEFAULT, CW_USEDEFAULT)
    {
    }

    public XamlWindow(string title, int width, int height)
    {
        Title = title;
        Width = width;
        Height = height;
        Initialize();
        ((XamlApplication)Application.Current).windows.Add(this);
    }

    private unsafe void Initialize()
    {
        var lpszClassName = (char*)Unsafe.AsPointer(ref Unsafe.AsRef(in "Modern.UI.Xaml.XamlWindow".GetPinnableReference()));
        var lpWindowName = (char*)Unsafe.AsPointer(ref Unsafe.AsRef(in Title.GetPinnableReference()));

        var style = BackdropSupported ? WS_EX_NOREDIRECTIONBITMAP : WS_EX_RIGHTSCROLLBAR;

        window = CreateWindowExW((uint)style, lpszClassName, lpWindowName, WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, Width, Height, HWND.NULL, HMENU.NULL, GetModuleHandleW(null), null);

        windowSubclassProc = new SUBCLASSPROC(WindowSubclassProc);
        SetWindowSubclass(window, (delegate* unmanaged<HWND, uint, WPARAM, LPARAM, nuint, nuint, LRESULT>)Marshal.GetFunctionPointerForDelegate(windowSubclassProc), 0, 0);

        InitializeXaml();
    }

    private unsafe void InitializeXaml()
    {
        xamlSurface = new(window);
        xamlSurface.Show();

        titlebarSurface = new(window, () =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(13, -2, 0, 0), FontSize = 12 });
            return panel;
        });
        titlebarSurface.ClickThrough();
        titlebarSurface.Show();
    }

    public void Activate()
    {
        ShowWindow(window, SW_SHOWNORMAL);
    }

    private unsafe LRESULT WindowSubclassProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam, nuint uIdSubclass, nuint dwRefData)
    {
        switch (uMsg)
        {
            case WM_SIZE:
                {
                    var dpi = GetDpiForWindow(hWnd);
                    var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
                    var visualBorder = (int)Math.Ceiling(1 * ((float)dpi / USER_DEFAULT_SCREEN_DPI));
                    var width = (int)LOWORD(lParam);
                    var height = (int)HIWORD(lParam);
                    var y = GetTopBorderMaximized(hWnd, dpi);
                    height -= y;
                    var caption = GetCaptionSize(hWnd, 2, dpi);

                    var hdwp = BeginDeferWindowPos(2);
                    hdwp = titlebarSurface.Resize(hdwp, 0, y + visualBorder, width, GetCaptionSize(hWnd, windowDpi: dpi));
                    hdwp = xamlSurface.Resize(hdwp, 0, y + caption, width, height - caption);
                    EndDeferWindowPos(hdwp);
                    Width = width;
                    Height = height;
                    return 0;
                }
            case WM_GETMINMAXINFO:
                {
                    var dpi = (float)GetDpiForWindow(hWnd) / USER_DEFAULT_SCREEN_DPI;
                    var mmi = (MINMAXINFO*)lParam;
                    mmi->ptMinTrackSize.x = (int)(MinWidth * dpi);
                    mmi->ptMinTrackSize.y = (int)(MinHeight * dpi);
                    return 0;
                }
            case WM_DESTROY:
                xamlSurface.Dispose();
                titlebarSurface.Dispose();
                ((XamlApplication)Application.Current).windows.Remove(this);
                return 0;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

}
