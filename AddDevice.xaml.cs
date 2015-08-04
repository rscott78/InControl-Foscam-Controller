using MLS.HA.DeviceController.Common.Gui;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FoscamController {

    /// <summary>
    /// Interaction logic for AddDevice.xaml
    /// </summary>
    public partial class AddDevice : PluginGuiWindow {
        public AddDevice() {
            InitializeComponent();
        }

        private void btnAdd_click(object sender, RoutedEventArgs e) {

            var ip = txtIpAddress.Text;
            var user = txtUsername.Text;
            var pwd = txtPassword.Password;

            // If anything is entered for snapshot/mjpg url, use those instead
            var snapshotUrl = txtSnapshotUrl.Text;
            var mjpegUrl = txtMjpegUrl.Text;

            if (!string.IsNullOrEmpty(snapshotUrl)) {
                verifyOtherSetup(snapshotUrl, mjpegUrl);
            } else {
                verifyFoscamSetup(ip, user, pwd);
            }
        }

        /// <summary>
        /// Verifies that the snapshot and/or mjpegUrl is valid. Saves the devices and closes the window if so.
        /// </summary>
        /// <param name="snapshotUrl"></param>
        /// <param name="mjpegUrl"></param>
        private void verifyOtherSetup(string snapshotUrl, string mjpegUrl) {
            Thread t = new Thread(() => {
                showStatus();
                try {
                    var tempFile = System.IO.Path.GetTempFileName();

                    using (var c = new WebClient()) {
                        c.DownloadFile(snapshotUrl, tempFile);
                    }
                    using (var image = (Bitmap)System.Drawing.Image.FromFile(tempFile)) {
                        using (MemoryStream ms = new MemoryStream()) {
                            image.Save(ms, ImageFormat.Jpeg);
                        }
                    }
                    File.Delete(tempFile);

                    // If we made it to here, there wasn't any problem detected
                    var deviceId = base.addDevice(MLS.HA.DeviceController.Common.DeviceProviderTypes.PluginDevice, FoscamController.getControllerName(), MLS.HA.DeviceController.Common.DeviceType.IpCamera, FoscamController.getDeviceName(snapshotUrl, mjpegUrl), "IpCamera");
                    base.setDeviceMetaData(deviceId, FoscamController.META_SNAPSHOT_URL, snapshotUrl);
                    base.setDeviceMetaData(deviceId, FoscamController.META_MJPEG_URL, mjpegUrl);

                    Dispatcher.Invoke(new Action(() => {
                        Close();
                    }));

                } catch (Exception ex) {
                    hideStatus();
                    Dispatcher.Invoke(new Action(() => {
                        MessageBox.Show("Unable to retrieve an image your Ip camera. Please verify the Snapshot Url and try again.\r\n" + ex.Message, "No Image", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }));
                } finally {
                    //// Tell it there's no longer a snapshot happening
                    //lock (dictionaryLockObject) {
                    //    currentSnapshots[device.name] = false;
                    //}
                }
            });
            t.IsBackground = true;
            t.Start();

        }

        /// <summary>
        /// Verifies that the foscam setup is correct and saves, then closes the window if so.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="user"></param>
        /// <param name="pwd"></param>
        private void verifyFoscamSetup(string ip, string user, string pwd) {
            Thread t = new Thread(() => {
                showStatus();
                var foundCamera = false;
                try {
                    // Attempt to verify that the camera IP is correct
                    var testUrl = string.Format("http://{0}/check_user.cgi?user={1}&pwd={2}", ip, user, pwd);
                    using (var client = new WebClient()) {
                        var result = client.DownloadString(testUrl);
                        if (result.Contains("var pri=")) {
                            foundCamera = true;
                        }
                    }
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message);
                    foundCamera = false;
                    hideStatus();
                }

                if (!foundCamera) {
                    if (MessageBox.Show("It appears that there is no camera at the address specified. Do you want to add this camera anyway?", "Camera not found", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                        var deviceId = base.addDevice(MLS.HA.DeviceController.Common.DeviceProviderTypes.PluginDevice, FoscamController.getControllerName(), MLS.HA.DeviceController.Common.DeviceType.IpCamera, FoscamController.getDeviceName(ip, user, pwd), "IpCamera");
                        setupMetaData(deviceId);

                        Dispatcher.Invoke(new Action(() => {
                            Close();
                        }));
                    }
                } else {
                    var deviceId = base.addDevice(MLS.HA.DeviceController.Common.DeviceProviderTypes.PluginDevice, FoscamController.getControllerName(), MLS.HA.DeviceController.Common.DeviceType.IpCamera, FoscamController.getDeviceName(ip, user, pwd), "IpCamera");
                    setupMetaData(deviceId);

                    Dispatcher.Invoke(new Action(() => {
                        Close();
                    }));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void showStatus() {
            Dispatcher.Invoke(new Action(() => {
                btnAdd.Visibility = System.Windows.Visibility.Collapsed;
                btnCancel.Visibility = System.Windows.Visibility.Collapsed;
                btnHelp.Visibility = System.Windows.Visibility.Collapsed;

                pbStatus.Visibility = System.Windows.Visibility.Visible;
            }));
        }

        private void hideStatus() {
            Dispatcher.Invoke(new Action(() => {
                btnAdd.Visibility = System.Windows.Visibility.Visible;
                btnCancel.Visibility = System.Windows.Visibility.Visible;
                btnHelp.Visibility = System.Windows.Visibility.Visible;

                pbStatus.Visibility = System.Windows.Visibility.Collapsed;
            }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        private void setupMetaData(Guid deviceId) {

            // Setup the direction control Url's for Foscam
            // Stored as http://{0}/decoder_control.cgi?command={direction}&user={username}&pwd={password}                    

            base.setDeviceMetaData(deviceId, FoscamController.META_RIGHT_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_RIGHT.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_RIGHT_STOP_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_RIGHT_STOP.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_LEFT_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_LEFT.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_LEFT_STOP_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_LEFT_STOP.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_UP_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_UP.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_UP_STOP_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_UP_STOP.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_DOWN_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_DOWN.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_DOWN_STOP_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_DOWN_STOP.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_SETPRESET1_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_SETPRESET_1.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_GETPRESET1_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_GOTOPRESET_1.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_SETPRESET2_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_SETPRESET_2.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_GETPRESET2_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_GOTOPRESET_2.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_SETPRESET3_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_SETPRESET_3.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_GETPRESET3_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_GOTOPRESET_3.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_SETPRESET4_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_SETPRESET_4.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_GETPRESET4_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_GOTOPRESET_4.ToString())));

            base.setDeviceMetaData(deviceId, FoscamController.META_SETPRESET5_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_SETPRESET_5.ToString())));
            base.setDeviceMetaData(deviceId, FoscamController.META_GETPRESET5_URL, string.Format("http://{{0}}{0}", FoscamController.DECODER_URL.Replace("{direction}", FoscamController.DECODER_DIRECTION_GOTOPRESET_5.ToString())));

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_click(object sender, RoutedEventArgs e) {
            Close();
        }

        /// <summary>
        /// Shows help about camera URLs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e) {
            // Open web browser
            try {
                System.Diagnostics.Process.Start("http://store.incontrolzwave.com/boards/topic/305/adding-mjpeg-ip-cameras-to-incontrol");
            } catch {
                MessageBox.Show("There was a problem starting your web browser. Please visit http://store.incontrolzwave.com/boards/topic/305/adding-mjpeg-ip-cameras-to-incontrol for upgrade information.");
            }
        }
    }
}
