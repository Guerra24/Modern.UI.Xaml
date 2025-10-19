//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using Windows.UI.Xaml;
using static TerraFX.Interop.Windows.SM;
using static TerraFX.Interop.Windows.SWP;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.WM;

namespace Modern.UI.Xaml;

public partial class XamlApplication
{
    private static HBRUSH LightBackground = CreateSolidBrush(RGB(0xF3, 0xF3, 0xF3));
    private static HBRUSH DarkBackground = CreateSolidBrush(RGB(0x20, 0x20, 0x20));
    private static HBRUSH Background = default;
    private WNDPROC xamlWindowProc = null!;

    internal static bool BackdropSupported = Environment.OSVersion.Version >= new Version(10, 0, 22621, 0);

    private unsafe LRESULT XamlWindowProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        LRESULT dwmResult;
        if (DwmDefWindowProc(hWnd, uMsg, wParam, lParam, &dwmResult))
            return dwmResult;

        if (!BackdropSupported)
            switch (uMsg)
            {
                case WM_ERASEBKGND:
                    PaintBackground(hWnd, (HDC)wParam, Background);
                    return 1;
                case WM_PAINT:
                    PaintClient(hWnd);
                    return 0;
                case WM_WINDOWPOSCHANGING:
                    if ((((WINDOWPOS*)lParam)->flags & SWP_FRAMECHANGED) > 0)
                        AdjustFrame(hWnd);
                    return DefWindowProcW(hWnd, uMsg, wParam, lParam);
            }


        switch (uMsg)
        {
            case WM_CREATE:
                {
                    BOOL val = Current.RequestedTheme == ApplicationTheme.Dark;
                    DwmSetWindowAttribute(hWnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &val, (uint)sizeof(BOOL));
                }
                if (BackdropSupported)
                {
                    var margins = new MARGINS();
                    margins.cxLeftWidth = -1;
                    margins.cxRightWidth = -1;
                    margins.cyBottomHeight = -1;
                    margins.cyTopHeight = -1;
                    DwmExtendFrameIntoClientArea(hWnd, &margins);
                    var type = DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
                    DwmSetWindowAttribute(hWnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &type, sizeof(DWM_SYSTEMBACKDROP_TYPE));
                }
                SetWindowPos(hWnd, HWND.NULL, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                break;
            case WM_SETTINGCHANGE:
            case WM_THEMECHANGED:
                {
                    BOOL val = Current.RequestedTheme == ApplicationTheme.Dark;
                    DwmSetWindowAttribute(hWnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &val, (uint)sizeof(BOOL));
                }
                if (!BackdropSupported)
                    InvalidateRect(hWnd, null, true);
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
            default:
                return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }

        return 0;
    }

    internal static int GetCaptionSize(HWND hWnd, int visualBorderMult = 1, uint? windowDpi = null)
    {
        var dpi = windowDpi ?? GetDpiForWindow(hWnd);
        var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
        var caption = GetSystemMetricsForDpi(SM_CYCAPTION, dpi);
        var visualBorder = (int)Math.Ceiling(1 * ((float)dpi / USER_DEFAULT_SCREEN_DPI));

        return border + caption + visualBorder * visualBorderMult;
    }

    internal static int GetTopBorderMaximized(HWND hWnd, uint dpi)
    {
        if (IsZoomed(hWnd))
        {
            var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
            var visualBorder = (int)Math.Ceiling(1 * ((float)dpi / USER_DEFAULT_SCREEN_DPI));
            return border - visualBorder;
        }
        return 0;
    }

    private static unsafe void PaintBackground(HWND hWnd, HDC hdc, HBRUSH hbr)
    {
        var dpi = GetDpiForWindow(hWnd);
        RECT rc;
        GetClientRect(hWnd, &rc);
        rc.top += GetCaptionSize(hWnd, 2, dpi) + GetTopBorderMaximized(hWnd, dpi);
        FillRect(hdc, &rc, hbr);
    }

    private static unsafe void PaintClient(HWND hWnd)
    {
        PAINTSTRUCT ps;
        var hdc = BeginPaint(hWnd, &ps);

        var dpi = GetDpiForWindow(hWnd);
        var border = GetSystemMetricsForDpi(SM_CXFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
        var visualBorder = (int)Math.Ceiling(1 * ((float)dpi / USER_DEFAULT_SCREEN_DPI));
        RECT rc;
        GetClientRect(hWnd, &rc);
        rc.bottom = GetCaptionSize(hWnd, 2, dpi) + GetTopBorderMaximized(hWnd, dpi);

        HDC memdc = CreateCompatibleDC(hdc);
        BITMAPINFOHEADER bmpInfoHdr = new BITMAPINFOHEADER();
        bmpInfoHdr.biSize = (uint)sizeof(BITMAPINFOHEADER);
        bmpInfoHdr.biWidth = rc.right;
        bmpInfoHdr.biHeight = -rc.bottom;
        bmpInfoHdr.biPlanes = 1;
        bmpInfoHdr.biBitCount = 32;

        HBITMAP hbitmap = CreateDIBSection(memdc, (BITMAPINFO*)&bmpInfoHdr, DIB_RGB_COLORS, null, HANDLE.NULL, 0);
        HGDIOBJ oldbitmap = SelectObject(memdc, hbitmap);

        BitBlt(hdc, 0, 0, rc.right, rc.bottom, memdc, 0, 0, SRCCOPY);
        SelectObject(memdc, oldbitmap);
        DeleteObject(hbitmap);
        DeleteDC(memdc);

        EndPaint(hWnd, &ps);
    }

    private static unsafe void AdjustFrame(HWND hWnd, uint? windowDpi = null)
    {
        var margins = new MARGINS();
        var dpi = windowDpi ?? GetDpiForWindow(hWnd);
        margins.cyTopHeight = GetCaptionSize(hWnd, 2, dpi) + GetTopBorderMaximized(hWnd, dpi);
        DwmExtendFrameIntoClientArea(hWnd, &margins);
    }
}

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
public delegate LRESULT WNDPROC([In] HWND hWnd, [In] uint uMsg, [In] WPARAM wParam, [In] LPARAM lParam);

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
public delegate LRESULT SUBCLASSPROC([In] HWND hWnd, [In] uint uMsg, [In] WPARAM wParam, [In] LPARAM lParam, [In] nuint uIdSubclass, [In] nuint dwRefData);
