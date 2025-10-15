//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//

using Windows.UI.Xaml.Controls;

namespace Modern.UI.Xaml.Sample;

public sealed partial class App : XamlApplication
{

    private XamlWindow mainWindow = null!;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched()
    {
        mainWindow = new($"Modern.UI.Xaml.Sample");

        var frame = new Frame();
        frame.Navigate(typeof(MainPage));
        mainWindow.Content = frame;
        mainWindow.Activate();
    }
}
