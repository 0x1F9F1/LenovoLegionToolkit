﻿using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Utils
{
    public static class Compatibility
    {
        private static readonly string _allowedVendor = "LENOVO";
        private static readonly string[] _allowedModels = new[]
        {
            "16ACHg6", // Legion 7 - AMD, nVidia
            "16ITHg6", // Legion 7 - Intel, nVidia
            "15IMHg05", // Legion 7 - Intel, nVidia
            "16ACH6", // Legion 5 Pro - AMD, nVidia
            "R7000P2020H", // Legion 5 Pro - AMD, nVidia (CN)
            "Y9000K2020H", // Legioin 7i - Intel, nVidia (CN)
            "Y9000K2021H", // Legioin 7i - Intel, nVidia (CN)
            "16ITH6", // Legion 5 Pro - Intel, nVidia
            "16IAH7", // Legion 5 Pro - Intel, nVidia
            "17ACH6", // Legion 5 - AMD, nVidia
            "17ITH6", // Legion 5 - Intel, nVidia
            "15ACH6", // Legion 5 - AMD, nVidia
            "15ARH05", // Legion 5 - AMD, nVidia
            "15IMH05", // Legion 5 - Intel, nVidia
            "15ITH6", // Legion 5 - Intel, nVidia
            "15IAH7H", // Legion 5 - Intel, nVidia
            "17ARH05", // Legion 5 - AMD, nVidia
            "17IMH05", // Legion 5 - Intel, nVidia
            "R7000P2021H", // Legion 5 - AMD, nVidia (CN)
            "R9000P2021H", // Legion 5 Pro - AMD, nVidia (CN)
            "15ACH6", // Legion S7 - AMD, nVidia

            // Limited compatibility
            "17IRHg", // Legion Y740 - Intel, nVidia
            "15IR", // Legion Y740 - Intel, nVidia
            "15IRH", // Legion Y540 - Intel, nVidia
            "17IRH", // Legion Y540 - Intel, nVidia
        };

        private static MachineInformation? _machineInformation;

        public static async Task<MachineInformation> GetMachineInformation()
        {
            if (!_machineInformation.HasValue)
            {
                var (vendor, machineType, model, serialNumber) = await GetModelDataAsync().ConfigureAwait(false);
                var biosVersion = await GetBIOSVersionAsync().ConfigureAwait(false);
                var modelYear = GetModelYear();

                _machineInformation = new()
                {
                    Vendor = vendor,
                    MachineType = machineType,
                    Model = model,
                    SerialNumber = serialNumber,
                    BIOSVersion = biosVersion,
                    ModelYear = modelYear,
                    Properties = new()
                    {
                        ShouldFlipFnLock = GetShouldFlipFnLock(modelYear),
                        SupportsGodMode = GetSupportsGodMode(biosVersion),
                        SupportsACDetection = await GetSupportsACDetection(),
                    }
                };
            }

            return _machineInformation.Value;
        }

        public static async Task<(bool isCompatible, MachineInformation machineInformation)> IsCompatibleAsync()
        {
            var machineInformation = await GetMachineInformation().ConfigureAwait(false);

            if (!machineInformation.Vendor.Equals(_allowedVendor, StringComparison.InvariantCultureIgnoreCase))
                return (false, machineInformation);

            foreach (var allowedModel in _allowedModels)
                if (machineInformation.Model.Contains(allowedModel, StringComparison.InvariantCultureIgnoreCase))
                    return (true, machineInformation);

            return (false, machineInformation);
        }

        private static bool GetShouldFlipFnLock(ModelYear modelYear)
        {
            return modelYear == ModelYear.MY2020OrEarlier;
        }

        private static bool GetSupportsGodMode(string biosVersion)
        {
            if (biosVersion.StartsWith("GKCN") && int.TryParse(biosVersion.Replace("GKCN", null).Replace("WW", null), out int rev1) && rev1 >= 49)
                return true;

            if (biosVersion.StartsWith("HHCN") && int.TryParse(biosVersion.Replace("HHCN", null).Replace("WW", null), out int rev2) && rev2 >= 23)
                return true;

            return false;
        }

        private static async Task<bool> GetSupportsACDetection()
        {
            return await Power.IsACFitForOC().ConfigureAwait(false) != null;
        }

        private static async Task<(string, string, string, string)> GetModelDataAsync()
        {
            var result = await WMI.ReadAsync("root\\CIMV2",
                                $"SELECT * FROM Win32_ComputerSystemProduct",
                                pdc =>
                                {
                                    var machineType = (string)pdc["Name"].Value;
                                    var vendor = (string)pdc["Vendor"].Value;
                                    var model = (string)pdc["Version"].Value;
                                    var serialNumber = (string)pdc["IdentifyingNumber"].Value;
                                    return (vendor, machineType, model, serialNumber);
                                }).ConfigureAwait(false);
            return result.First();
        }

        private static async Task<string> GetBIOSVersionAsync()
        {
            var result = await WMI.ReadAsync("root\\CIMV2",
                                $"SELECT * FROM Win32_BIOS",
                                pdc => (string)pdc["Name"].Value).ConfigureAwait(false);
            return result.First();
        }

        private static ModelYear GetModelYear()
        {
            if (CheckIf2020OrEarlier())
                return ModelYear.MY2020OrEarlier;

            return ModelYear.MY2021OrLater;
        }

        private static bool CheckIf2020OrEarlier()
        {
            try
            {
                uint inBuffer = 0x2;
                if (!Native.DeviceIoControl(Devices.GetBattery(), 0x831020E8, ref inBuffer, sizeof(uint), out uint outBuffer, sizeof(uint), out _, IntPtr.Zero))
                {
                    var error = Marshal.GetLastWin32Error();

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"DeviceIoControl returned 0, last error: {error}");

                    throw new InvalidOperationException($"DeviceIoControl returned 0, last error: {error}");
                }

                outBuffer = outBuffer.ReverseEndianness();
                return outBuffer.GetNthBit(19);
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }
}
