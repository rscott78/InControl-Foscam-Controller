using MLS.HA.DeviceController.Common.Gui;
using System;
using System.Collections.Generic;
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
            var pwd = txtPassword.Text;

            Thread t = new Thread(() => {
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
        }

        private void btnCancel_click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
