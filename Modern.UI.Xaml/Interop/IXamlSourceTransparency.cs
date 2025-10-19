//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Modern.UI.Xaml.Interop;

[GeneratedComInterface]
[Guid("06636C29-5A17-458D-8EA2-2422D997A922")]
public partial interface IXamlSourceTransparency
{
    void GetIids(out int iidCount, out nint iids);
    void GetRuntimeClassName(out nint className);
    void GetTrustLevel(out int trustLevel);
    [return: MarshalAs(UnmanagedType.I1)] bool GetIsBackgroundTransparent();
    void SetIsBackgroundTransparent([MarshalAs(UnmanagedType.I1)] bool isBackgroundTransparent);
}
