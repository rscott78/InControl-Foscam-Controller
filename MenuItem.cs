using MLS.HA.DeviceController.Common.Gui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoscamController {
    public class MenuItem : IPluginMenuItem {

        public string mainMenuName() {
            return "Ip Camera";
        }

        /// <summary>
        /// Gets all sub-menu items.
        /// </summary>
        /// <returns></returns>
        public List<PluginSubMenuItem> subMenus() {
            var subs = new List<PluginSubMenuItem>();

            var menuItem = new PluginSubMenuItem();
            menuItem.menuName = "_Add MJPEG IP Camera";
            menuItem.onMenuItemClicked += addFoscamAddDeviceClicked;
            subs.Add(menuItem);

            return subs;
        }

        /// <summary>
        /// Display the Add Dialog
        /// </summary>
        void addFoscamAddDeviceClicked(System.Windows.Window windowOwner) {
            //MessageBox.Show("You clicked the menu!");
            var frm = new AddDevice();
            frm.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            frm.Owner = windowOwner;
            frm.ShowDialog();
        }

    }
}
