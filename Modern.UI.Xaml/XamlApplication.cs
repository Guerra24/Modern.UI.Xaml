//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//

using Modern.UI.Xaml.Interop;
using MrmPatcher;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using TerraFX.Interop.WinRT;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using WinRT;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.WM;
using static TerraFX.Interop.Windows.WS;
using static TerraFX.Interop.WinRT.WinRT;

namespace Modern.UI.Xaml;

public partial class XamlApplication : Application
{
    private HWND coreHwnd = default, applicationHwnd;
    private WNDPROC wndProc = null!;
    private WindowsXamlManager xamlManager = null!;
    private CoreWindow coreWindow = null!;

    private bool Closing;

    internal List<XamlCompositionSurface> surfaces = new();
    internal List<XamlWindow> windows = new();

    public XamlApplication()
    {
        Initialize();
    }

    private unsafe void Initialize()
    {
        var lpszClassName = (char*)Unsafe.AsPointer(ref Unsafe.AsRef(in "Modern.UI.Xaml.XamlApplication".GetPinnableReference()));

        wndProc = new WNDPROC(WndProc);

        WNDCLASSW wc;
        wc.lpfnWndProc = (delegate* unmanaged<HWND, uint, WPARAM, LPARAM, LRESULT>)Marshal.GetFunctionPointerForDelegate(wndProc);
        wc.hInstance = GetModuleHandleW(null);
        wc.lpszClassName = lpszClassName;
        RegisterClassW(&wc);

        applicationHwnd = CreateWindowExW(WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP, lpszClassName, null, WS_DISABLED, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, wc.hInstance, null);
    }

    private unsafe void InitializeXaml()
    {
        RoInitialize(RO_INIT_TYPE.RO_INIT_SINGLETHREADED);
        // Is this needed anymore? maybe for older builds?
        LoadLibraryW((char*)Unsafe.AsPointer(ref Unsafe.AsRef(in "twinapi.appcore.dll".GetPinnableReference())));
        LoadLibraryW((char*)Unsafe.AsPointer(ref Unsafe.AsRef(in "threadpoolwinrt.dll".GetPinnableReference())));

        // If this is NAOT we need to patch Mrm
        using (new MrmPatcherHelper())
        {
            xamlManager = WindowsXamlManager.InitializeForCurrentThread();
        }

        coreWindow = CoreWindow.GetForCurrentThread();

        using ComPtr<ICoreWindowInterop> interop = default;
        ((IUnknown*)((IWinRTObject)coreWindow).NativeObject.ThisPtr)->QueryInterface(__uuidof<ICoreWindowInterop>(), (void**)interop.GetAddressOf());
        interop.Get()->get_WindowHandle((HWND*)Unsafe.AsPointer(ref coreHwnd));

        SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));

        ((IXamlSourceTransparency)(object)Window.Current).SetIsBackgroundTransparent(true);

        OnLaunched();
    }

    private unsafe bool PreTranslateMessage(MSG* msg)
    {
        foreach (var surface in surfaces)
            if (surface.PreTranslateMessage(msg))
                return true;
        return false;
    }

    protected virtual void OnLaunched()
    {

    }

    public unsafe void Run()
    {
        MSG msg;
        while (GetMessageW(&msg, HWND.NULL, 0, 0))
        {
            bool xamlSourceProcessedMessage = PreTranslateMessage(&msg);
            if (!xamlSourceProcessedMessage)
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
            if (windows.Count == 0 && !Closing)
            {
                Closing = true;
                PostMessageW(coreHwnd, WM_CLOSE, 0, 0);
                PostMessageW(applicationHwnd, WM_CLOSE, 0, 0);
            }
        }
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
                SendMessageW(coreHwnd, uMsg, wParam, lParam);
                break;
            case WM_DESTROY:
                xamlManager.Dispose();
                PostQuitMessage(0);
                break;
            default:
                return DefWindowProcW(hWnd, uMsg, wParam, lParam);
        }
        return 0;
    }

}
