using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MinecraftRTXSwitcher
{
    static partial class NvidiaDriverEditor
    {
        private const string ProfileName = "Minecraft";

        private const uint NvAPI_Initialize_ID = 0x0150E828;
        private const uint NvAPI_DRS_CreateSession_ID = 0x0694D52E;
        private const uint NvAPI_DRS_LoadSettings_ID = 0x375DBD6B;
        private const uint NvAPI_DRS_FindProfileByName_ID = 0x7E4A9A0B;
        private const uint NvAPI_DRS_SetSetting_ID = 0x577DD202;
        private const uint NvAPI_DRS_SaveSettings_ID = 0xFCBC7E14;
        private const uint NvAPI_DRS_DestroySession_ID = 0x0DAD9CFF8;
        private const uint NvAPI_EnumPhysicalGPUs_ID = 0xE5AC921F;
        private const uint NvAPI_GetFullName_ID = 0xCEEE8E9F;
        private const uint NvAPI_GetSetting_ID = 0x73BF8338;

        [DllImport("nvapi64", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr nvapi_QueryInterface(uint id);

        private delegate int NvAPI_InitializeDelegate();
        private static NvAPI_InitializeDelegate NvAPI_Initialize;

        private delegate int NvAPI_DRS_CreateSessionDelegate(out IntPtr handle);
        private static NvAPI_DRS_CreateSessionDelegate NvAPI_DRS_CreateSession;

        private delegate int NvAPI_DRS_LoadSettingsDelegate(IntPtr handle);
        private static NvAPI_DRS_LoadSettingsDelegate NvAPI_DRS_LoadSettings;

        private delegate int NvAPI_DRS_FindProfileByNameDelegate(IntPtr handle, NvapiUnicodeString profileName, out IntPtr profileHandle);
        private static NvAPI_DRS_FindProfileByNameDelegate NvAPI_DRS_FindProfileByName;

        private delegate int NvAPI_DRS_SetSettingDelegate(IntPtr handle, IntPtr profileHandle, ref NvdrsSetting setting);
        private static NvAPI_DRS_SetSettingDelegate NvAPI_DRS_SetSetting;

        private delegate int NvAPI_DRS_SaveSettingsDelegate(IntPtr handle);
        private static NvAPI_DRS_SaveSettingsDelegate NvAPI_DRS_SaveSettings;

        private delegate int NvAPI_DRS_DestroySessionDelegate(IntPtr handle);
        private static NvAPI_DRS_DestroySessionDelegate NvAPI_DRS_DestroySession;

        private delegate int NvAPI_EnumPhysicalGPUsDelegate(IntPtr[] gpuHandles, ref uint gpuCount);
        private static NvAPI_EnumPhysicalGPUsDelegate NvAPI_EnumPhysicalGPUs;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NvAPI_GPU_GetFullNameDelegate(IntPtr gpuHandle, StringBuilder name);
        private static NvAPI_GPU_GetFullNameDelegate NvAPI_GPU_GetFullName;


        private delegate int NvAPI_GetSettingDelegate(IntPtr sessionHandle, IntPtr profileHandle, Nvapi settingID, ref NvdrsSetting setting);
        private static NvAPI_GetSettingDelegate NvAPI_GetSetting;

        private static bool _initialized;

        public static event EventHandler<string> Output;

        private enum Nvapi : uint
        {
            RTX_DXR_Enabled = 0X00DE429A
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public readonly struct NvapiUnicodeString
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
            private readonly byte[] _data;

            public NvapiUnicodeString(string text)
            {
                _data = new byte[4096];
                Set(text);
            }

            public string Get()
            {
                string text = Encoding.Unicode.GetString(_data, 0, 4096);
                int index = text.IndexOf('\0');
                if (index > -1) text = text.Remove(index);
                return text;
            }

            public void Set(string text)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(text + '\0');
                Array.Copy(bytes, _data, Math.Min(bytes.Length, 4096));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct NvdrsSetting
        {
            public uint Version;
            public NvapiUnicodeString SettingName;
            public Nvapi SettingId;
            public uint SettingType;
            public uint SettingLocation;
            public uint IsCurrentPredefined;
            public uint IsPredefinedValid;
            public uint PredefinedValue;
            public NvapiUnicodeString PredefinedString;
            public uint CurrentValue;
            public NvapiUnicodeString CurrentString;
        }

        private static void Check(int status)
        {
            if (status != 0)
            {
                throw new Exception(String.Format("NVAPI Error: {0}", status));
            }
        }

        private static void Initialize()
        {
            if (!_initialized)
            {

                NvAPI_Initialize = NvAPI_Delegate<NvAPI_InitializeDelegate>(NvAPI_Initialize_ID);
                Check(NvAPI_Initialize());

                NvAPI_DRS_CreateSession = NvAPI_Delegate<NvAPI_DRS_CreateSessionDelegate>(NvAPI_DRS_CreateSession_ID);
                NvAPI_DRS_LoadSettings = NvAPI_Delegate<NvAPI_DRS_LoadSettingsDelegate>(NvAPI_DRS_LoadSettings_ID);
                NvAPI_DRS_FindProfileByName = NvAPI_Delegate<NvAPI_DRS_FindProfileByNameDelegate>(NvAPI_DRS_FindProfileByName_ID);
                NvAPI_DRS_SetSetting = NvAPI_Delegate<NvAPI_DRS_SetSettingDelegate>(NvAPI_DRS_SetSetting_ID);
                NvAPI_DRS_SaveSettings = NvAPI_Delegate<NvAPI_DRS_SaveSettingsDelegate>(NvAPI_DRS_SaveSettings_ID);
                NvAPI_DRS_DestroySession = NvAPI_Delegate<NvAPI_DRS_DestroySessionDelegate>(NvAPI_DRS_DestroySession_ID);
                NvAPI_EnumPhysicalGPUs = NvAPI_Delegate<NvAPI_EnumPhysicalGPUsDelegate>(NvAPI_EnumPhysicalGPUs_ID);
                NvAPI_GPU_GetFullName = NvAPI_Delegate<NvAPI_GPU_GetFullNameDelegate>(NvAPI_GetFullName_ID);
                NvAPI_GetSetting = NvAPI_Delegate<NvAPI_GetSettingDelegate>(NvAPI_GetSetting_ID);

                _initialized = true;
            }
        }

        private static uint MakeVersion<T>(uint version) where T : struct
        {
            int sizeOfT = Marshal.SizeOf(typeof(T));
            return (uint)sizeOfT | (version << 16);
        }

        private static bool CheckForRTX()
        {
            uint gpuCount = 0;
            IntPtr[] gpuHandles = new IntPtr[32];
            int status = NvAPI_EnumPhysicalGPUs(gpuHandles, ref gpuCount);
            if (status != 0)
            {
                Output?.Invoke(null, "Failed to get GPU handles.");
                return false;
            }

            for (uint i = 0; i < gpuCount; i++)
            {
                StringBuilder gpuName = new StringBuilder(128);
                status = NvAPI_GPU_GetFullName(gpuHandles[i], gpuName);
                if (status != 0)
                {
                    Output?.Invoke(null, string.Format("Failed to get GPU #{0} full name.", i));
                }
                else
                {
                    string gpuModel = gpuName.ToString().TrimEnd('\0');

                    Output?.Invoke(null, gpuModel);
                    if (gpuModel.Contains("RTX"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void ChangeSetting(bool enable)
        {
            Initialize();

            if (!CheckForRTX())
            {
                Output?.Invoke(null, "This app only works on with RTX GPUs!");
                return;
            }

            Output?.Invoke(null, "RTX GPU Found!");

            Check(NvAPI_DRS_CreateSession(out IntPtr sessionHandle));
            Check(NvAPI_DRS_LoadSettings(sessionHandle));
            Check(NvAPI_DRS_FindProfileByName(sessionHandle, new NvapiUnicodeString(ProfileName), out IntPtr profileHandle));

            Output?.Invoke(null, "Minecraft driver profile found!");

            var old = new NvdrsSetting
            {
                Version = MakeVersion<NvdrsSetting>(1),
                SettingId = Nvapi.RTX_DXR_Enabled
            };

            Check(NvAPI_GetSetting(sessionHandle, profileHandle, Nvapi.RTX_DXR_Enabled, ref old));

            bool alreadySet = (enable && old.CurrentValue == 0x00000001) || (!enable && old.CurrentValue == 0x00000000);

            if (alreadySet)
            {
                Output?.Invoke(null, $"RTX is already {(enable ? "enabled" : "disabled")}!");
                Check(NvAPI_DRS_DestroySession(sessionHandle));
                return;
            }


            NvdrsSetting setting = new NvdrsSetting
            {
                Version = MakeVersion<NvdrsSetting>(1),
                SettingId = Nvapi.RTX_DXR_Enabled,
                SettingType = 0,
                SettingLocation = 0,
                CurrentValue = (uint)(enable ? 0x00000001 : 0x00000000),
                PredefinedValue = (uint)(enable ? 0x00000001 : 0x00000000)
            };

            Check(NvAPI_DRS_SetSetting(sessionHandle, profileHandle, ref setting));
            Check(NvAPI_DRS_SaveSettings(sessionHandle));
            Check(NvAPI_DRS_DestroySession(sessionHandle));

            Output?.Invoke(null, "Successfully " + (enable ? "enabled" : "disabled") + " RTX!");
        }

        private static T NvAPI_Delegate<T>(uint id) where T : class
        {
            IntPtr ptr = nvapi_QueryInterface(id);
            if (ptr != IntPtr.Zero)
            {
                return (T)(object)Marshal.GetDelegateForFunctionPointer(ptr, typeof(T));
            }
            return null;
        }
    }
}