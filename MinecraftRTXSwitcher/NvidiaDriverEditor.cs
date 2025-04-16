using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MinecraftRTXSwitcher
{
    static partial class NvidiaDriverEditor
    {
        #region Constants
        private const string PROFILE_NAME = "Minecraft";

        // NVAPI Function IDs
        private const uint NVAPI_INITIALIZE_ID = 0x0150E828;
        private const uint NVAPI_DRS_CREATE_SESSION_ID = 0x0694D52E;
        private const uint NVAPI_DRS_LOAD_SETTINGS_ID = 0x375DBD6B;
        private const uint NVAPI_DRS_FIND_PROFILE_BY_NAME_ID = 0x7E4A9A0B;
        private const uint NVAPI_DRS_SET_SETTING_ID = 0x577DD202;
        private const uint NVAPI_DRS_SAVE_SETTINGS_ID = 0xFCBC7E14;
        private const uint NVAPI_DRS_DESTROY_SESSION_ID = 0x0DAD9CFF8;
        private const uint NVAPI_ENUM_PHYSICAL_GPUS_ID = 0xE5AC921F;
        private const uint NVAPI_GET_FULL_NAME_ID = 0xCEEE8E9F;
        private const uint NVAPI_GET_SETTING_ID = 0x73BF8338;
        private const uint NVAPI_GET_ERROR_MESSAGE_ID = 0x6C2D048C;
        #endregion

        #region NVAPI Interface
        [DllImport("nvapi64", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr nvapi_QueryInterface(uint id);
        #endregion

        #region Delegates
        private delegate int NvAPI_InitializeDelegate();
        private delegate int NvAPI_DRS_CreateSessionDelegate(out IntPtr handle);
        private delegate int NvAPI_DRS_LoadSettingsDelegate(IntPtr handle);
        private delegate int NvAPI_DRS_FindProfileByNameDelegate(IntPtr handle, NvapiUnicodeString profileName, out IntPtr profileHandle);
        private delegate int NvAPI_DRS_SetSettingDelegate(IntPtr handle, IntPtr profileHandle, ref NvdrsSetting setting);
        private delegate int NvAPI_DRS_SaveSettingsDelegate(IntPtr handle);
        private delegate int NvAPI_DRS_DestroySessionDelegate(IntPtr handle);
        private delegate int NvAPI_EnumPhysicalGPUsDelegate(IntPtr[] gpuHandles, ref uint gpuCount);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NvAPI_GPU_GetFullNameDelegate(IntPtr gpuHandle, StringBuilder name);
        private delegate int NvAPI_GetSettingDelegate(IntPtr sessionHandle, IntPtr profileHandle, Nvapi settingID, ref NvdrsSetting setting);
        private delegate int NvAPI_GetErrorMessageDelegate(int status, StringBuilder message);
        #endregion

        #region Function Pointers
        private static NvAPI_InitializeDelegate NvAPI_Initialize;
        private static NvAPI_DRS_CreateSessionDelegate NvAPI_DRS_CreateSession;
        private static NvAPI_DRS_LoadSettingsDelegate NvAPI_DRS_LoadSettings;
        private static NvAPI_DRS_FindProfileByNameDelegate NvAPI_DRS_FindProfileByName;
        private static NvAPI_DRS_SetSettingDelegate NvAPI_DRS_SetSetting;
        private static NvAPI_DRS_SaveSettingsDelegate NvAPI_DRS_SaveSettings;
        private static NvAPI_DRS_DestroySessionDelegate NvAPI_DRS_DestroySession;
        private static NvAPI_EnumPhysicalGPUsDelegate NvAPI_EnumPhysicalGPUs;
        private static NvAPI_GPU_GetFullNameDelegate NvAPI_GPU_GetFullName;
        private static NvAPI_GetSettingDelegate NvAPI_GetSetting;
        private static NvAPI_GetErrorMessageDelegate NvAPI_GetErrorMessage;
        #endregion

        private static bool _initialized;
        public static event EventHandler<string> Output;

        #region Enums and Structs
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
        #endregion

        #region Helper Methods
        private static void LogMessage(string message)
        {
            Output?.Invoke(null, message);
        }

        private static void CheckApiStatus(int status)
        {
            if (status != 0)
            {
                StringBuilder message = new StringBuilder(512);
                if (NvAPI_GetErrorMessage != null)
                {
                    NvAPI_GetErrorMessage(status, message);
                    throw new Exception($"NVAPI Error: {status} Details: {message}");
                }
                else
                {
                    throw new Exception($"NVAPI Error: {status}");
                }
            }
        }

        private static void Initialize()
        {
            if (_initialized) return;

            // Load and initialize API functions
            NvAPI_Initialize = GetNvApiDelegate<NvAPI_InitializeDelegate>(NVAPI_INITIALIZE_ID);
            CheckApiStatus(NvAPI_Initialize());

            // Load other function pointers
            NvAPI_DRS_CreateSession = GetNvApiDelegate<NvAPI_DRS_CreateSessionDelegate>(NVAPI_DRS_CREATE_SESSION_ID);
            NvAPI_DRS_LoadSettings = GetNvApiDelegate<NvAPI_DRS_LoadSettingsDelegate>(NVAPI_DRS_LOAD_SETTINGS_ID);
            NvAPI_DRS_FindProfileByName = GetNvApiDelegate<NvAPI_DRS_FindProfileByNameDelegate>(NVAPI_DRS_FIND_PROFILE_BY_NAME_ID);
            NvAPI_DRS_SetSetting = GetNvApiDelegate<NvAPI_DRS_SetSettingDelegate>(NVAPI_DRS_SET_SETTING_ID);
            NvAPI_DRS_SaveSettings = GetNvApiDelegate<NvAPI_DRS_SaveSettingsDelegate>(NVAPI_DRS_SAVE_SETTINGS_ID);
            NvAPI_DRS_DestroySession = GetNvApiDelegate<NvAPI_DRS_DestroySessionDelegate>(NVAPI_DRS_DESTROY_SESSION_ID);
            NvAPI_EnumPhysicalGPUs = GetNvApiDelegate<NvAPI_EnumPhysicalGPUsDelegate>(NVAPI_ENUM_PHYSICAL_GPUS_ID);
            NvAPI_GPU_GetFullName = GetNvApiDelegate<NvAPI_GPU_GetFullNameDelegate>(NVAPI_GET_FULL_NAME_ID);
            NvAPI_GetSetting = GetNvApiDelegate<NvAPI_GetSettingDelegate>(NVAPI_GET_SETTING_ID);
            NvAPI_GetErrorMessage = GetNvApiDelegate<NvAPI_GetErrorMessageDelegate>(NVAPI_GET_ERROR_MESSAGE_ID);

            _initialized = true;
        }

        private static uint MakeVersion<T>(uint version) where T : struct
        {
            int sizeOfT = Marshal.SizeOf(typeof(T));
            return (uint)sizeOfT | (version << 16);
        }

        private static T GetNvApiDelegate<T>(uint id) where T : class
        {
            IntPtr ptr = nvapi_QueryInterface(id);
            if (ptr != IntPtr.Zero)
            {
                return (T)(object)Marshal.GetDelegateForFunctionPointer(ptr, typeof(T));
            }
            return null;
        }
        #endregion

        #region GPU Detection and RTX Settings
        private static bool HasRtxGpu()
        {
            uint gpuCount = 0;
            IntPtr[] gpuHandles = new IntPtr[32];
            int status = NvAPI_EnumPhysicalGPUs(gpuHandles, ref gpuCount);
            if (status != 0)
            {
                LogMessage("Failed to get GPU handles.");
                return false;
            }

            for (uint i = 0; i < gpuCount; i++)
            {
                StringBuilder gpuName = new StringBuilder(128);
                status = NvAPI_GPU_GetFullName(gpuHandles[i], gpuName);
                if (status != 0)
                {
                    LogMessage($"Failed to get GPU #{i} full name.");
                }
                else
                {
                    string gpuModel = gpuName.ToString().TrimEnd('\0');
                    LogMessage(gpuModel);

                    if (gpuModel.Contains("RTX"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsRtxSettingAlreadySet(IntPtr sessionHandle, IntPtr profileHandle, bool enableRtx)
        {
            try
            {
                var setting = new NvdrsSetting
                {
                    Version = MakeVersion<NvdrsSetting>(1),
                    SettingId = Nvapi.RTX_DXR_Enabled
                };

                CheckApiStatus(NvAPI_GetSetting(sessionHandle, profileHandle, Nvapi.RTX_DXR_Enabled, ref setting));

                bool currentlyEnabled = setting.CurrentValue == 0x00000001;
                bool alreadySet = (enableRtx && currentlyEnabled) || (!enableRtx && !currentlyEnabled);

                if (alreadySet)
                {
                    LogMessage($"RTX is already {(enableRtx ? "enabled" : "disabled")}!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message.Split(new string[] { "Details: " }, StringSplitOptions.None).LastOrDefault();
                if (msg == "NVAPI_PROFILE_NOT_FOUND") return false;

                throw;
            }
            return false;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Changes the RTX setting for Minecraft
        /// </summary>
        /// <param name="enable">True to enable RTX, false to disable</param>
        public static void ChangeSetting(bool enable)
        {
            Initialize();

            if (!HasRtxGpu())
            {
                LogMessage("This app only works with RTX GPUs!");
                return;
            }

            LogMessage("RTX GPU Found!");

            IntPtr sessionHandle = IntPtr.Zero;

            try
            {
                // Create session and find profile
                CheckApiStatus(NvAPI_DRS_CreateSession(out sessionHandle));
                CheckApiStatus(NvAPI_DRS_LoadSettings(sessionHandle));
                CheckApiStatus(NvAPI_DRS_FindProfileByName(sessionHandle, new NvapiUnicodeString(PROFILE_NAME), out IntPtr profileHandle));

                LogMessage("Minecraft driver profile found!");

                if (IsRtxSettingAlreadySet(sessionHandle, profileHandle, enable))
                {
                    return;
                }

                // Update RTX setting
                NvdrsSetting setting = new NvdrsSetting
                {
                    Version = MakeVersion<NvdrsSetting>(1),
                    SettingId = Nvapi.RTX_DXR_Enabled,
                    SettingType = 0,
                    SettingLocation = 0,
                    CurrentValue = (uint)(enable ? 0x00000001 : 0x00000000),
                    PredefinedValue = (uint)(enable ? 0x00000001 : 0x00000000)
                };

                CheckApiStatus(NvAPI_DRS_SetSetting(sessionHandle, profileHandle, ref setting));
                CheckApiStatus(NvAPI_DRS_SaveSettings(sessionHandle));

                LogMessage($"Successfully {(enable ? "enabled" : "disabled")} RTX!");
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                if (sessionHandle != IntPtr.Zero)
                {
                    try { NvAPI_DRS_DestroySession(sessionHandle); } catch { }
                }
            }
        }
        #endregion
    }
}