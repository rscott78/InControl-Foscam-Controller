using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MLS.HA.DeviceController;
using MLS.HA.DeviceController.Common.Device;
using MLS.HA.DeviceController.Common.HaControllerInterface;
using MLS.HA.DeviceController.Common;
using System.Threading;
using System.Net;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Configuration;

namespace FoscamController {

    public enum HaCameraCommands {
        setPreset,
        gotoPreset,
        //moveRightStart,
        //moveRightStop,
        //moveLeftStart,
        //moveLeftStop,
        //moveUpStart,
        //moveUpStop,
        //moveDownStart,
        //moveDownStop
    }

    public class FoscamController : HaController, IHaController, IHaDeviceCommand, IHaPoll {

        #region Constants
        public const string SNAPSHOT_URL = "/snapshot.cgi?user={username}&pwd={password}";
        public const string LIVESTREAM_URL = "/videostream.cgi?user={username}&pwd={password}&resolution=32";
        public const string LIVESTREAM_ASF_URL = "/videostream.asf?user={username}&pwd={password}&resolution=32";

        public const string DECODER_URL = "/decoder_control.cgi?command={direction}&user={username}&pwd={password}";

        public const int DECODER_DIRECTION_UP = 0;
        public const int DECODER_DIRECTION_UP_STOP = 1;

        public const int DECODER_DIRECTION_DOWN = 2;
        public const int DECODER_DIRECTION_DOWN_STOP = 3;

        public const int DECODER_DIRECTION_LEFT = 6;
        public const int DECODER_DIRECTION_LEFT_STOP = 5;

        public const int DECODER_DIRECTION_RIGHT = 4;
        public const int DECODER_DIRECTION_RIGHT_STOP = 7;

        public const int DECODER_DIRECTION_SETPRESET_1 = 30;
        public const int DECODER_DIRECTION_GOTOPRESET_1 = 31;

        public const int DECODER_DIRECTION_SETPRESET_2 = 32;
        public const int DECODER_DIRECTION_GOTOPRESET_2 = 33;

        public const int DECODER_DIRECTION_SETPRESET_3 = 34;
        public const int DECODER_DIRECTION_GOTOPRESET_3 = 35;

        public const int DECODER_DIRECTION_SETPRESET_4 = 36;
        public const int DECODER_DIRECTION_GOTOPRESET_4 = 37;

        public const int DECODER_DIRECTION_SETPRESET_5 = 38;
        public const int DECODER_DIRECTION_GOTOPRESET_5 = 39;

        public const string META_SNAPSHOT_URL = "snapshot_url";
        public const string META_MJPEG_URL = "mjpg_url";

        public const string META_RIGHT_URL = "right_url";
        public const string META_RIGHT_STOP_URL = "right_stop_url";

        public const string META_LEFT_URL = "left_url";
        public const string META_LEFT_STOP_URL = "left_stop_url";

        public const string META_UP_URL = "up_url";
        public const string META_UP_STOP_URL = "up_stop_url";

        public const string META_DOWN_URL = "down_url";
        public const string META_DOWN_STOP_URL = "down_stop_url";

        public const string META_SETPRESET1_URL = "setpreset1";
        public const string META_GETPRESET1_URL = "getpreset1";

        public const string META_SETPRESET2_URL = "setpreset2";
        public const string META_GETPRESET2_URL = "getpreset2";

        public const string META_SETPRESET3_URL = "setpreset3";
        public const string META_GETPRESET3_URL = "getpreset3";

        public const string META_SETPRESET4_URL = "setpreset4";
        public const string META_GETPRESET4_URL = "getpreset4";

        public const string META_SETPRESET5_URL = "setpreset5";
        public const string META_GETPRESET5_URL = "getpreset5";


        #endregion

        public static string getControllerName() {
            return "HaFoscamController";
        }

        public string controllerName {
            get {
                return FoscamController.getControllerName();
            }
            set {
                // Does nothing
            }
        }

        Thread tPolldevices;
        bool isRunning = true;
        Dictionary<string, bool> currentSnapshots;
        object dictionaryLockObject = new object();

        public FoscamController() {
            writeLog("Foscam: starting FoscamController");

            localDevices = new List<HaDevice>();
            currentSnapshots = new Dictionary<string, bool>();

            tPolldevices = new Thread(() => { pollDevices(); });
            tPolldevices.IsBackground = true;
            tPolldevices.Start();

            // TEST - add a local device
            //Thread t = new Thread(() => {
            //    Thread.Sleep(3000);
            //    base.raiseDiscoveredDevice(DeviceProviderTypes.PluginDevice, controllerName, DeviceType.IpCamera, FoscamController.getDeviceName("http://10.4.3.51:8050|admin|mwisvs2m"), "IpCamera");
            //});
            //t.IsBackground = true;
            //t.Start();

            writeLog("Foscam: Startup complete");
        }

        #region Device polling
        /// <summary>
        /// Loops through all tracked devices and gets an image from it.
        /// </summary>
        private void pollDevices() {
            while (isRunning) {

                try {
                    // This is required for some cams that don't follow the rules
                    // It fixes the problem with "The server committed a protocol violation. Section=ResponseHeader Detail=CR must be followed by LF" 
                    if (!ToggleAllowUnsafeHeaderParsing(true)) {
                        // Log if this isn't able to be set
                        writeLog("IPCAM: Unable to set safety header. Some IP cams may not function properly.");
                    }
                } catch (Exception ex) {
                    writeLog("IPCAM: Unable to set safety header", ex);
                }

                try {
                    foreach (var d in localDevices) {
                        if (d.pollDevice) {

                            if (new TimeSpan(DateTime.Now.Ticks - d.lastPollTime.Ticks).TotalSeconds < d.pollDeviceSeconds) {
                                continue;
                            }

                            d.lastPollTime = DateTime.Now;

                            // Get a snapshot from the camera
                            Thread t = new Thread(() => { getSnapshot(d as CameraDevice); });
                            t.IsBackground = true;
                            t.Start();
                        }
                    }
                } catch {
                }
                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Gets an instant camera still.
        /// </summary>
        /// <param name="providerDeviceId"></param>
        /// <returns></returns>
        public byte? pollDevice(object providerDeviceId) {

            try {
                var d = getHaDevice(providerDeviceId);
                getSnapshot(d as CameraDevice);

                return 1;
            } catch { }

            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private bool isCurrentlySnapshotting(CameraDevice device) {
            bool isSnapping = false;
            lock (dictionaryLockObject) {
                if (currentSnapshots.ContainsKey(device.name)) {
                    isSnapping = currentSnapshots[device.name];
                } else {
                    // create the key
                    currentSnapshots.Add(device.name, false);
                }
            }

            return isSnapping;
        }

        // Enable/disable useUnsafeHeaderParsing.
        // See http://o2platform.wordpress.com/2010/10/20/dealing-with-the-server-committed-a-protocol-violation-sectionresponsestatusline/
        public static bool ToggleAllowUnsafeHeaderParsing(bool enable) {
            //Get the assembly that contains the internal class
            Assembly assembly = Assembly.GetAssembly(typeof(SettingsSection));
            if (assembly != null) {
                //Use the assembly in order to get the internal type for the internal class
                Type settingsSectionType = assembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (settingsSectionType != null) {
                    //Use the internal static property to get an instance of the internal settings class.
                    //If the static instance isn't created already invoking the property will create it for us.
                    object anInstance = settingsSectionType.InvokeMember("Section", BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                    if (anInstance != null) {
                        //Locate the private bool field that tells the framework if unsafe header parsing is allowed
                        FieldInfo aUseUnsafeHeaderParsing = settingsSectionType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null) {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, enable);
                            return true;
                        }

                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        private void getSnapshot(CameraDevice device) {
            if (device != null && !isCurrentlySnapshotting(device)) {

                lock (dictionaryLockObject) {
                    currentSnapshots[device.name] = true;
                }

                var url = "";

                if (!string.IsNullOrEmpty(device.snapShotUrl)) {
                    // non foscam
                    url = device.snapShotUrl;
                } else {
                    url = string.Format("http://{0}{1}", device.ip, replaceStringValues(SNAPSHOT_URL, device));
                }

                try {
                    var tempFile = Path.GetTempFileName();

                    using (var c = new WebClient()) {
                        c.DownloadFile(url, tempFile);
                    }

                    // Write the timestamp on the image
                    var message = DateTime.Now.ToString();

                    using (var image = (Bitmap)Image.FromFile(tempFile)) {
                        using (var graphics = Graphics.FromImage(image)) {
                            using (var arialFont = new Font("Arial", 12)) {
                                PointF firstLocation = new PointF(10f, 10f);
                                graphics.DrawString(DateTime.Now.ToString(), arialFont, Brushes.White, firstLocation);
                            }

                            graphics.Flush();
                        }

                        using (MemoryStream ms = new MemoryStream()) {
                            image.Save(ms, ImageFormat.Jpeg);
                            device.b64Image = Convert.ToBase64String(ms.ToArray());
                        }
                    }

                    File.Delete(tempFile);

                } catch (Exception ex) {
                    var msg = string.Format("Foscam: Error getting snapshot from URL: {0}. {1}", url, ex.Message);

                    writeLog("Foscam: Error getting snapshot from URL: " + url, ex);
                    raiseOnDeviceError(device.deviceId, msg);

                    try {
                        //device.b64Image = ex.Message;
                    } catch { }
                } finally {
                    // Tell it there's no longer a snapshot happening
                    lock (dictionaryLockObject) {
                        currentSnapshots[device.name] = false;
                    }
                }
            } else {
                writeLog("Foscam: Existing snapshot detected. Skipping snapshot for device " + device.name);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringToReplace"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static string replaceStringValues(string stringToReplace, CameraDevice device) {
            return stringToReplace.Replace("{password}", device.password).Replace("{username}", device.userName);
        }

        #endregion

        List<HaDevice> localDevices { get; set; }
        public string controllerErrorMessage { get; set; }
        public bool hasControllerError { get; set; }

        public List<HaDevice> getHaDevices() {
            return localDevices;
        }

        /// <summary>
        /// Gets a device by the 
        /// </summary>
        /// <param name="providerId"></param>
        /// <returns></returns>
        public HaDevice getHaDevice(object providerId) {
            foreach (var d in localDevices) {
                if (d.providerDeviceId.ToString() == providerId.ToString()) {
                    return d;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a device by the deviceId.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public HaDevice getHaDevice(Guid deviceId) {
            foreach (var d in localDevices) {
                if (d.deviceId == deviceId) {
                    return d;
                }
            }

            return null;
        }

        /// <summary>
        /// Called when the server pulls a saved device from the db.
        /// </summary>
        /// <param name="dbDevice"></param>
        /// <returns></returns>
        public HaDevice trackDevice(HaDeviceDto dbDevice) {

            var haDev = new CameraDevice() {
                deviceId = dbDevice.deviceId,
                providerDeviceId = dbDevice.uniqueName,
                deviceName = dbDevice.deviceName
            };

            try {

                // See if there is meta data available for snapshot/mjpg url's
                var snapShotUrl = getDeviceMetadata(haDev.deviceId, META_SNAPSHOT_URL);
                var mjpegUrl = getDeviceMetadata(haDev.deviceId, META_MJPEG_URL);

                if (!string.IsNullOrEmpty(snapShotUrl)) {

                    haDev.liveStreamUrl = mjpegUrl;
                    haDev.snapShotUrl = snapShotUrl;

                    Thread t = new Thread(() => { setupMetaPropertiesForOtherCam(haDev, snapShotUrl, mjpegUrl); });
                    t.IsBackground = true;
                    t.Start();

                } else {
                    // Foscam only
                    // Get the ip, username and password from the devicename
                    try {
                        var split = dbDevice.uniqueName.Split('|');
                        haDev.ip = split[0].Replace("http://", "");
                        haDev.userName = split[1];
                        haDev.password = split[2];
                    } catch { }

                    haDev.liveStreamUrl = string.Format("http://{0}{1}", haDev.ip, replaceStringValues(LIVESTREAM_URL, haDev));

                    Thread t = new Thread(() => { setupMetaProperties(haDev); });
                    t.IsBackground = true;
                    t.Start();
                }
            } catch { }

            localDevices.Add(haDev);

            return haDev;
        }

        private void setupMetaPropertiesForOtherCam(CameraDevice cameraDevice, string snapShotUrl, string mjpegUrl) {
            try {
                var externalIp = getExternalIp();

                // Get the external ip of this camera
                var snapShotUri = new Uri(snapShotUrl);
                cameraDevice.externalIp = snapShotUri.Host + ":" + snapShotUri.Port;

            } catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        private void setupMetaProperties(CameraDevice cameraDevice) {
            try {
                var externalIp = getExternalIp();

                // Replace the internal ip with the external
                if (cameraDevice.ip.Contains(":")) {
                    var splitStr = cameraDevice.ip.Split(':');
                    cameraDevice.externalIp = externalIp + ":" + splitStr[splitStr.Length - 1];
                } else {
                    cameraDevice.externalIp = externalIp;
                }

                cameraDevice.liveStreamExternalUrl = string.Format("http://{0}{1}", cameraDevice.externalIp, replaceStringValues(LIVESTREAM_URL, cameraDevice));

                cameraDevice.panDownUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_DOWN_URL), cameraDevice);
                cameraDevice.panDownStopUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_DOWN_STOP_URL), cameraDevice);

                cameraDevice.panUpUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_UP_URL), cameraDevice);
                cameraDevice.panUpStopUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_UP_STOP_URL), cameraDevice);

                cameraDevice.panLeftUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_LEFT_URL), cameraDevice);
                cameraDevice.panLeftStopUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_LEFT_STOP_URL), cameraDevice);

                cameraDevice.panRightUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_RIGHT_URL), cameraDevice);
                cameraDevice.panRightStopUrl = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_RIGHT_STOP_URL), cameraDevice);

                cameraDevice.setPreset1Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_SETPRESET1_URL), cameraDevice);
                cameraDevice.gotoPreset1Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_GETPRESET1_URL), cameraDevice);

                cameraDevice.setPreset2Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_SETPRESET2_URL), cameraDevice);
                cameraDevice.gotoPreset2Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_GETPRESET2_URL), cameraDevice);

                cameraDevice.setPreset3Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_SETPRESET3_URL), cameraDevice);
                cameraDevice.gotoPreset3Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_GETPRESET3_URL), cameraDevice);

                cameraDevice.setPreset4Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_SETPRESET4_URL), cameraDevice);
                cameraDevice.gotoPreset4Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_GETPRESET4_URL), cameraDevice);

                cameraDevice.setPreset5Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_SETPRESET5_URL), cameraDevice);
                cameraDevice.gotoPreset5Url = replaceStringValues(base.getDeviceMetadata(cameraDevice.deviceId, META_GETPRESET5_URL), cameraDevice);

            } catch (Exception ex) {
                writeLog("Problem setting up device meta data", ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private string getExternalIp() {

            if (this.lookedUpExternalIp == null) {

                var a4 = "";
                try {
                    // Get the external ip address on this network
                    var client = new WebClient();
                    var response = client.DownloadString("http://checkip.dyndns.org");

                    string[] a = response.Split(':');
                    string a2 = a[1].Substring(1);
                    string[] a3 = a2.Split('<');
                    a4 = a3[0];
                } catch (Exception ex) {
                    writeLog("Error during getExternalIp()", ex);
                }

                lookedUpExternalIp = a4;
            }

            return lookedUpExternalIp;
        }

        public void finishedTracking() {
            // na
        }

        public void setLevel(object providerDeviceId, int newLevel) {
            // Does nothing for a camera
        }

        public void setPower(object providerDeviceId, bool powered) {
            // Does nothing for a campera
        }

        public void executeSpecialCommand(object providerDeviceId, SpecialCommand command, object value) {
            // na
        }

        public HaDeviceDetails getDeviceDetails(object providerDeviceId) {
            // na
            return new HaDeviceDetails() { };
        }

        public ControllerTestResult testController() {
            // na
            return new ControllerTestResult() {
                result = false,
                message = "Not supported"
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        private void doHtmlGet(string url, string ipAddress) {
            using (var c = new WebClient()) {
                try {
                    url = string.Format(url, ipAddress);
                    writeLog("gotoPreset URL " + url);
                    c.DownloadStringAsync(new Uri(url));
                } catch (Exception ex) {
                    writeLog("Error during camera html command", ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="commandData"></param>
        private void gotoPreset(Guid deviceId, Dictionary<string, object> commandData, HaDeviceCommandResult result) {
            // Get data from the commanddata
            if (commandData != null && commandData.ContainsKey("presetIndex")) {
                int presetIndex;
                if (int.TryParse(commandData["presetIndex"].ToString(), out presetIndex)) {
                    var device = getHaDevice(deviceId) as CameraDevice;
                    if (device != null) {
                        var url = device.getPresetUrl(presetIndex);
                        doHtmlGet(url, device.ip);
                    } else {
                        result.success = false;
                        result.message = "Device not found.";
                    }
                } else {
                    result.success = false;
                    result.message = "presetIndex is not an integer!";
                }
            } else {
                result.success = false;
                result.message = "presetIndex data not found!";
            }
        }

        public string getAsfVideoFeedUrl(Guid deviceId) {
            var cameraDevice = getHaDevice(deviceId) as CameraDevice;
            return string.Format("http://{0}{1}", cameraDevice.externalIp, replaceStringValues(LIVESTREAM_ASF_URL, cameraDevice));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="commandData"></param>
        /// <param name="result"></param>
        private void setPreset(Guid deviceId, Dictionary<string, object> commandData, HaDeviceCommandResult result) {
            // Get data from the commanddata
            if (commandData != null && commandData.ContainsKey("presetIndex")) {
                int presetIndex;
                if (int.TryParse(commandData["presetIndex"].ToString(), out presetIndex)) {
                    var device = getHaDevice(deviceId) as CameraDevice;
                    if (device != null) {
                        var url = device.getPresetSetUrl(presetIndex);
                        doHtmlGet(url, device.ip);
                    } else {
                        result.success = false;
                        result.message = "Device not found.";
                    }
                } else {
                    result.success = false;
                    result.message = "presetIndex is not an integer!";
                }
            } else {
                result.success = false;
                result.message = "presetIndex data not found!";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="jsonCommandData"></param>
        /// <returns></returns>
        public HaDeviceCommandResult processCommand(Guid deviceId, HaDeviceCommand jsonCommandData) {

            var result = new HaDeviceCommandResult() {
                success = true
            };

            HaCameraCommands cameraCommand;

            // Make sure it's a valid commandType
            if (Enum.TryParse<HaCameraCommands>(jsonCommandData.commandType, out cameraCommand)) {

                switch (cameraCommand) {
                    case HaCameraCommands.gotoPreset:
                        gotoPreset(deviceId, jsonCommandData.commandData, result);
                        break;
                    case HaCameraCommands.setPreset:
                        setPreset(deviceId, jsonCommandData.commandData, result);
                        break;
                }

            } else {
                result.success = false;
                result.message = "Invalid command type " + jsonCommandData.commandType;
            }

            return result;
        }

        public void stop() {
            isRunning = false;
        }

        public static string getDeviceName(string ipAddress, string username, string password) {
            return string.Format("http://{0}|{1}|{2}", ipAddress, username, password);
        }

        public static string getDeviceName(string snapshotUrl, string streamUrl) {

            // By default the name is going to be the host:ip
            try {
                var uri = new Uri(snapshotUrl);
                return string.Format("{0}:{1}", uri.Host, uri.Port);
            } catch { }

            // Failsafe
            return Guid.NewGuid().ToString();
        }

        public string lookedUpExternalIp { get; set; }
    }
}
