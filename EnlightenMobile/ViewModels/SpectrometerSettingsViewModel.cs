﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using EnlightenMobile.Models;

namespace EnlightenMobile.ViewModels
{
    // This class provides "transformation logic" to render the Model of the
    // EEPROM's ObservableList entries.  
    //
    // Not really; the ObservableList natively uses ViewableSetting objects, and 
    // this class does nothing except provide a "straight-through" copy of each 
    // ViewableSetting as it is rendered into a Cell of the ListView.  
    // 
    // This is the kind of verbose-yet-useless class that makes people hate MVVM.  
    // IF there's a way to obviate it, let me know.
    public class SpectrometerSettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        Spectrometer spec = Spectrometer.getInstance();
        Logger logger = Logger.getInstance();

        public string title
        {
            get => "Spectrometer Settings";
        }

        ////////////////////////////////////////////////////////////////////////
        // BLE Device Info
        ////////////////////////////////////////////////////////////////////////

        public string manufacturerName
        {
            get => spec.bleDeviceInfo.manufacturerName;
        }

        public string softwareRevision
        {
            get => spec.bleDeviceInfo.softwareRevision;
        }

        public string firmwareRevision
        {
            get => spec.bleDeviceInfo.firmwareRevision;
        }

        public string hardwareRevision
        {
            get => spec.bleDeviceInfo.hardwareRevision;
        }

        ////////////////////////////////////////////////////////////////////////
        // EEPROM
        ////////////////////////////////////////////////////////////////////////

        public ViewableSetting ViewableSetting
        {
            get { return _viewableSetting; }
            set { _viewableSetting = value; }
        }
        ViewableSetting _viewableSetting;

        ////////////////////////////////////////////////////////////////////////
        // Util
        ////////////////////////////////////////////////////////////////////////

        // so we can update these from the SpectrometerSettingsView code-behind
        // on display, after changing spectrometers.
        public void refresh()
        {
            logger.debug("refreshing SpectrometerSettingsViewModel");

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(softwareRevision)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(firmwareRevision)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(hardwareRevision)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(manufacturerName)));
        }

        protected void OnPropertyChanged([CallerMemberName] string caller = "")
        {
            logger.debug("SSVM: OnPropertyChanged");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        }
    }
}
