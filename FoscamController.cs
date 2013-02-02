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

namespace FoscamController {
    public class FoscamController : HaController, IHaController {

        #region Constants
        public const string SNAPSHOT_URL = "/snapshot.cgi?user={username}&pwd={password}";
        public const string LIVESTREAM_URL = "/videostream.cgi?user={username}&pwd={password}&resolution=32";

        public const string DECODER_URL = "/decoder_control.cgi?command={direction}&user={username}&pwd={password}";

        public const int DECODER_DIRECTION_UP = 0;
        public const int DECODER_DIRECTION_UP_STOP = 1;

        public const int DECODER_DIRECTION_DOWN = 2;
        public const int DECODER_DIRECTION_DOWN_STOP = 3;

        public const int DECODER_DIRECTION_LEFT = 4;
        public const int DECODER_DIRECTION_LEFT_STOP = 5;

        public const int DECODER_DIRECTION_RIGHT = 6;
        public const int DECODER_DIRECTION_RIGHT_STOP = 7;

        public const string META_RIGHT_URL = "right_url";
        public const string META_RIGHT_STOP_URL = "right_stop_url";

        public const string META_LEFT_URL = "left_url";
        public const string META_LEFT_STOP_URL = "left_stop_url";

        public const string META_UP_URL = "up_url";
        public const string META_UP_STOP_URL = "up_stop_url";

        public const string META_DOWN_URL = "down_url";
        public const string META_DOWN_STOP_URL = "down_stop_url";
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
            }
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

        /// <summary>
        /// 
        /// </summary>
        private void getSnapshot(CameraDevice device) {
            if (device != null && !isCurrentlySnapshotting(device)) {

                lock (dictionaryLockObject) {
                    currentSnapshots[device.name] = true;
                }

                var url = string.Format("http://{0}{1}", device.ip, replaceStringValues(SNAPSHOT_URL, device));
                //var tempFile = Path.Combine(Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location), "");

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
                    writeLog("Foscam: Error getting snapshot", ex);

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
                // Get the ip, username and password from the devicename
                var split = dbDevice.uniqueName.Split('|');
                haDev.ip = split[0].Replace("http://", "");
                haDev.userName = split[1];
                haDev.password = split[2];
            } catch { }

            haDev.liveStreamUrl = string.Format("http://{0}{1}", haDev.ip, replaceStringValues(LIVESTREAM_URL, haDev));

            Thread t = new Thread(() => { setupMetaProperties(haDev); });
            t.IsBackground = true;
            t.Start();

            localDevices.Add(haDev);

            return haDev;
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
            } catch (Exception ex) {
                writeLog("Problem setting up device meta data", ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private string getExternalIp() {
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

            return a4;
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

        public void stop() {
            isRunning = false;
        }

        public static string getDeviceName(string ipAddress, string username, string password) {
            return string.Format("http://{0}|{1}|{2}", ipAddress, username, password);
        }

    }
}
