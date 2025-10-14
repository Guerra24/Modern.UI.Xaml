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
using static TerraFX.Interop.Windows.SM;
using static TerraFX.Interop.Windows.SW;
using static TerraFX.Interop.Windows.SWP;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.WM;
using static TerraFX.Interop.Windows.WS;

namespace Modern.UI.Xaml;

public class XamlWindow
{
    private WNDPROC wndProc = null!;
    private HWND hwnd = default;
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
        var lpszClassName = (char*)Unsafe.AsPointer(ref Unsafe.AsRef(in $"XamlWindow_{Random.Shared.Next()}".GetPinnableReference()));
        var lpWindowName = (char*)Unsafe.AsPointer(ref Unsafe.AsRef(in Title.GetPinnableReference()));

        wndProc = new WNDPROC(WndProc);

        WNDCLASSW wc;
        wc.lpfnWndProc = (delegate* unmanaged<HWND, uint, WPARAM, LPARAM, LRESULT>)Marshal.GetFunctionPointerForDelegate(wndProc);
        wc.hInstance = GetModuleHandleW(null);
        wc.lpszClassName = lpszClassName;
        RegisterClassW(&wc);

        hwnd = CreateWindowExW(WS_EX_NOREDIRECTIONBITMAP, lpszClassName, lpWindowName, WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, Width, Height, HWND.NULL, HMENU.NULL, wc.hInstance, null);

        InitializeXaml();

        ChangeTheme(Application.Current.RequestedTheme == ApplicationTheme.Dark);

        var margins = new MARGINS();
        margins.cxLeftWidth = -1;
        margins.cxRightWidth = -1;
        margins.cyBottomHeight = -1;
        margins.cyTopHeight = -1;
        DwmExtendFrameIntoClientArea(hwnd, &margins);

        if (Environment.OSVersion.Version >= new Version(10, 0, 22621, 0))
        {
            var type = DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &type, sizeof(DWM_SYSTEMBACKDROP_TYPE));
        }

        SetWindowPos(hwnd, HWND.NULL, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private unsafe void InitializeXaml()
    {
        xamlSurface = new(hwnd);

        titlebarSurface = new(hwnd, () =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(13, -2, 0, 0), FontSize = 12 });
            return panel;
        });
        titlebarSurface.ClickThrough();
    }

    public void Activate()
    {
        ShowWindow(hwnd, SW_SHOWNORMAL);
    }

    public unsafe void ChangeTheme(bool darkmode)
    {
        BOOL val = darkmode;
        DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &val, (uint)sizeof(BOOL));
    }

    private unsafe LRESULT WndProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        switch (uMsg)
        {
            case WM_CREATE:
                InitializeXaml();
                break;
            case WM_SETTINGCHANGE:
            case WM_THEMECHANGED:
                ChangeTheme(Application.Current.RequestedTheme == ApplicationTheme.Dark);
                break;
            case WM_SIZE:
                {
                    var dpi = GetDpiForWindow(hWnd);
                    var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
                    var caption = GetSystemMetricsForDpi(SM_CYCAPTION, dpi);
                    var visualBorder = (int)Math.Ceiling(1 * ((float)dpi / USER_DEFAULT_SCREEN_DPI));
                    var y = 0;
                    var width = (int)LOWORD(lParam);
                    var height = (int)HIWORD(lParam);
                    if (IsZoomed(hWnd))
                    {
                        y = border - visualBorder;
                        height -= border - visualBorder;
                    }
                    titlebarSurface.Resize(0, y + visualBorder, width, caption + border + visualBorder);

                    xamlSurface.Resize(0, y + border + caption + visualBorder * 2, width, height - border - caption - visualBorder * 2);

                    Width = width;
                    Height = height;
                }
                break;
            case WM_GETMINMAXINFO:
                {
                    var dpi = (float)GetDpiForWindow(hWnd) / USER_DEFAULT_SCREEN_DPI;
                    var mmi = (MINMAXINFO*)lParam;
                    mmi->ptMinTrackSize.x = (int)(MinWidth * dpi);
                    mmi->ptMinTrackSize.y = (int)(MinHeight * dpi);
                }
                break;
            case WM_NCCALCSIZE:
                if (wParam == 1)
                {
                    var dpi = GetDpiForWindow(hWnd);
                    var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
                    //var visualBorder = GetSystemMetricsForDpi(SM_CYBORDER, dpi);
                    NCCALCSIZE_PARAMS* param = (NCCALCSIZE_PARAMS*)lParam;
                    param->rgrc[0].left += border;
                    //param->rgrc[0].top += IsZoomed(hWnd) ? border - visualBorder : 0;
                    param->rgrc[0].top += 0;
                    param->rgrc[0].right -= border;
                    param->rgrc[0].bottom -= border;
                    break;
                }
                else
                {
                    return DefWindowProcW(hWnd, uMsg, wParam, lParam);
                }
            case WM_NCHITTEST:
                {
                    LRESULT dwmResult;
                    if (DwmDefWindowProc(hWnd, uMsg, wParam, lParam, &dwmResult))
                        return dwmResult;

                    var x = LOWORD(lParam);
                    var y = HIWORD(lParam);
                    var ret = DefWindowProcW(hWnd, uMsg, wParam, lParam);

                    if (ret == HTCLIENT)
                    {
                        RECT rc;
                        GetWindowRect(hWnd, &rc);
                        var dpi = GetDpiForWindow(hWnd);
                        var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
                        if (y < rc.top + border)
                            return HTTOP;
                        else
                            return HTCAPTION;
                    }
                    return ret;
                }
            case WM_DESTROY:
                xamlSurface.Dispose();
                titlebarSurface.Dispose();
                ((XamlApplication)Application.Current).windows.Remove(this);
                break;
            default:
                return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }

        return 0;
    }
}


[UnmanagedFunctionPointer(CallingConvention.Winapi)]
public delegate LRESULT WNDPROC([In] HWND hWnd, [In] uint uMsg, [In] WPARAM wParam, [In] LPARAM lParam);