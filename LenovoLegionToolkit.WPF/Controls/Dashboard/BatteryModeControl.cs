﻿using LenovoLegionToolkit.Lib;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard
{
    public class BatteryModeControl : AbstractComboBoxFeatureCardControl<BatteryState>
    {
        public BatteryModeControl()
        {
            Icon = SymbolRegular.BatteryCharge24;
            Title = "Battery Mode";
            Subtitle = "Choose how the battery is charged.";
        }
    }
}
