﻿using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xamarin.Forms;
using EnlightenMobile.Models;
using System.Threading.Tasks;
using Telerik.XamarinForms.Chart;

namespace EnlightenMobile.ViewModels
{
    // This class provides all the business logic controlling the ScopeView. 
    public class ScopeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // So the ScopeViewModel can float-up Toast events to the ScopeView.
        // This probably could be done using notifications, but I'm not sure I
        // want to make a "public string toastMessage" Property, and I'm not
        // sure what the "best practice" architecture would be.
        public delegate void ToastNotification(string msg);
        public event ToastNotification notifyToast;

        ////////////////////////////////////////////////////////////////////////
        // Private attributes
        ////////////////////////////////////////////////////////////////////////

        Spectrometer spec;
        AppSettings appSettings;
        Logger logger = Logger.getInstance();

        ////////////////////////////////////////////////////////////////////////
        // Lifecycle
        ////////////////////////////////////////////////////////////////////////

        public ScopeViewModel()
        {
            spec = Spectrometer.getInstance();
            appSettings = AppSettings.getInstance();

            appSettings.PropertyChanged += handleAppSettingsChange;
            spec.PropertyChanged += handleSpectrometerChange;

            // bind closures (method calls) to each Command
            acquireCmd = new Command(() => { _ = doAcquireAsync(); });
            refreshCmd = new Command(() => { _ = doAcquireAsync(); }); 
            saveCmd    = new Command(() => { _ = doSave        (); });
            addCmd     = new Command(() => { _ = doAdd         (); });
            clearCmd   = new Command(() => { _ = doClear       (); });

            logger.debug("SVM: instantiating XAxisOptions");
            xAxisOptions = new ObservableCollection<XAxisOption>()
            {
                // these names must match the fields in ChartDataPoint
                new XAxisOption() { name = "pixel", unit = "px" },
                new XAxisOption() { name = "wavelength", unit = "nm" },
                new XAxisOption() { name = "wavenumber", unit = "cm⁻¹" }
            };
            xAxisOption = xAxisOptions[0];

            updateChart();
        }

        ////////////////////////////////////////////////////////////////////////
        //
        //                          Bound Properties
        //
        ////////////////////////////////////////////////////////////////////////

        public string title
        {
            get => "Scope Mode";
        }

        ////////////////////////////////////////////////////////////////////////
        // X-Axis
        ////////////////////////////////////////////////////////////////////////

        public ObservableCollection<XAxisOption> xAxisOptions { get; set; }
        public XAxisOption xAxisOption
        {
            get => _xAxisOption;
            set
            {
                logger.debug($"xAxisOption -> {value}");
                _xAxisOption = value;
                updateChart();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(xAxisLabelFormat)));
            }
        }
        XAxisOption _xAxisOption;

        public double xAxisMinimum
        {
            get => _xAxisMinimum;
            set
            {
                _xAxisMinimum = value;
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(xAxisMinimum)));
            }
        }
        double _xAxisMinimum;

        public double xAxisMaximum
        {
            get => _xAxisMaximum;
            set
            {
                _xAxisMaximum = value;
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(xAxisMaximum)));
            }
        }
        double _xAxisMaximum;

        public string xAxisLabelFormat
        {
            get => xAxisOption.name == "pixel" ? "F0" : "F2";
        }

        ////////////////////////////////////////////////////////////////////////
        // integrationTimeMS
        ////////////////////////////////////////////////////////////////////////

        public string integrationTimeMS 
        {
            get => spec.integrationTimeMS.ToString();
            set { }
        }

        // the ScopeView's code-behind has registered that a final value has
        // been entered into the Entry (hit return), so latch it
        public void setIntegrationTimeMS(string s)
        {
            if (UInt32.TryParse(s, out UInt32 value))
                spec.integrationTimeMS = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(integrationTimeMS)));
        }

        ////////////////////////////////////////////////////////////////////////
        // gainDb
        ////////////////////////////////////////////////////////////////////////

        public string gainDb
        {
            get => spec.gainDb.ToString();
            set { }
        }

        // the ScopeView's code-behind has registered that a final value has
        // been entered into the Entry (hit return), so latch it
        public void setGainDb(string s)
        {
            if (float.TryParse(s, out float value))
                spec.gainDb = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(gainDb)));
        }

        ////////////////////////////////////////////////////////////////////////
        // scansToAverage
        ////////////////////////////////////////////////////////////////////////

        public string scansToAverage
        {
            get => spec.scansToAverage.ToString();
            set { }
        }

        // the ScopeView's code-behind has registered that a final value has
        // been entered into the Entry (hit return), so latch it
        public void setScansToAverage(string s)
        {
            if (ushort.TryParse(s, out ushort value))
                spec.scansToAverage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(scansToAverage)));
        }

        ////////////////////////////////////////////////////////////////////////
        // misc acquisition parameters
        ////////////////////////////////////////////////////////////////////////

        public bool darkEnabled
        {
            get => spec.dark != null;
            set => spec.toggleDark();
        }

        public string note
        {
            get => spec.note;
            set => spec.note = value;
        }

        ////////////////////////////////////////////////////////////////////////
        // Laser Shenanigans
        ////////////////////////////////////////////////////////////////////////

        public bool laserEnabled
        {
            get => spec.laserEnabled;
            set => spec.laserEnabled = value;
        }

        public bool ramanModeEnabled
        {
            get => spec.ramanModeEnabled;
            set
            {
                logger.debug($"SVM.ramanModeEnabled: setting Spectrometer.ramanModeEnabled = {value}");
                spec.ramanModeEnabled = value;

                // as this is based partly on Raman Mode...
                updateLaserAvailable();
            }
        }

        // Provided so the "Laser Enable" Switch is disabled if we're in Raman
        // Mode (or battery is low).
        public bool laserIsAvailable
        {
            get
            {
                var available = !ramanModeEnabled && spec.battery.level >= 5;
                if (!available)
                    logger.debug($"laser not available because ramanModeEnabled ({ramanModeEnabled}) or bettery < 5 ({spec.battery.level})");
                return available;
            }
        }

        // Provided so the View can only show/enable certain controls if we're
        // logged-in.
        public bool isAuthenticated
        {
            get => AppSettings.getInstance().authenticated;
        }

        // Provided so any changes to AppSettings.authenticated will immediately
        // take effect on our View.
        void handleAppSettingsChange(object sender, PropertyChangedEventArgs e)
        {
            logger.debug($"SVM.handleAppSettingsChange: received notification from {sender}, so refreshing isAuthenticated");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(isAuthenticated)));

            updateLaserAvailable();
        }

        public void updateLaserAvailable()
        { 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(laserIsAvailable)));
        }

        ////////////////////////////////////////////////////////////////////////
        // Refresh
        ////////////////////////////////////////////////////////////////////////

        public bool isRefreshing
        {
            get => _isRefreshing;
            set 
            {
                logger.debug($"SVM: isRefreshing -> {value}");
                _isRefreshing = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(isRefreshing)));
            }
        }
        bool _isRefreshing;

        // invoked by ScopeView when the user pulls-down on the Scope Options grid
        // @todo consider whether this feature should user-configurable, as an 
        //       accidental acquisition could be destructive of both data and 
        //       health (as the laser could auto-fire)
        public Command refreshCmd { get; }

        ////////////////////////////////////////////////////////////////////////
        // Status Bar
        ////////////////////////////////////////////////////////////////////////

        public string spectrumMax
        { 
            get => string.Format("Max: {0:f2}", spec.measurement.max);
        }

        public string batteryState 
        { 
            get => spec.battery.ToString();
        }

        public string batteryColor
        { 
            get => spec.battery.level > 20 ? "#eee" : "#f33";
        }

        void updateBatteryProperties()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(batteryState)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(batteryColor)));
        }

        ////////////////////////////////////////////////////////////////////////
        // Acquire Command
        ////////////////////////////////////////////////////////////////////////

        public string acquireButtonColor
        {
            get
            {
                // should somehow get this into the XAML itself, or perhaps the
                // code-behind (which could also set .IsEnabled)
                return spec.acquiring ? "#ba0a0a" : "#ccc";
            }
        }

        // invoked by ScopeView when the user clicks "Acquire" 
        public Command acquireCmd { get; }

        // the user clicked the "Acquire" button on the Scope View
        async Task<bool> doAcquireAsync()
        {
            if (spec.acquiring)
                return false;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(acquireButtonColor)));

            // take a fresh Measurement
            var startTime = DateTime.Now;
            var ok = await spec.takeOneAveragedAsync(showProgress);
            if (ok)
            {
                // info-level logging so we can QC timing w/o verbose logging
                var elapsedMS = (DateTime.Now - startTime).TotalMilliseconds;
                logger.info($"Completed acquisition in {elapsedMS} ms");

                updateChart();

                // later we could decide not to graph bad measurements, or not log
                // elapsed time, but this is fine for now
                _ = isGoodMeasurement();
            }
            else
            {
                notifyToast?.Invoke("Error reading spectrum");
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(acquireButtonColor)));
            updateBatteryProperties();
            showProgress(0);
            isRefreshing = false;

            updateLaserAvailable();

            return ok;
        }

        // This is a callback (delegate) passed down into Spectrometer so it can
        // update our acquisitionProgress property while reading BLE packets.
        void showProgress(double progress) => acquisitionProgress = progress; 

        // this is a floating-point "percentage completion" backing the 
        // ProgressBar on the ScopeView
        public double acquisitionProgress
        {
            get => _acquisitionProgress;
            set 
            {
                _acquisitionProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(acquisitionProgress)));
            }
        }
        double _acquisitionProgress;

        bool isGoodMeasurement()
        {
            Measurement m = spec.measurement;
            if (m is null || m.raw is null)
                return false;

            var allZero = true;                
            var allHigh = true;                
            for (int i = 0; i < m.raw.Length; i++)
            {
                if (m.raw[i] !=     0) allZero = false;
                if (m.raw[i] != 65535) allHigh = false;

                // no point checking beyond this point
                if (!allHigh && !allZero)
                    return true;
            }

            if (allZero)
                notifyToast?.Invoke("ERROR: spectrum is all zero");
            else if (allHigh)
                notifyToast?.Invoke("ERROR: spectrum is all 0xff");
            return !(allZero || allHigh);
        }

        ////////////////////////////////////////////////////////////////////////
        // Chart
        ////////////////////////////////////////////////////////////////////////

        public Command addCmd { get; }
        public Command clearCmd { get; }

        public RadCartesianChart theChart;

        public ObservableCollection<ChartDataPoint> chartData { get; set; } = new ObservableCollection<ChartDataPoint>();

        // declare statically for now; these are individual Properties because 
        // I don't think I can use databinding against array elements
        public ObservableCollection<ChartDataPoint> trace0 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace1 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace2 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace3 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace4 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace5 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace6 { get; set; } = new ObservableCollection<ChartDataPoint>();
        public ObservableCollection<ChartDataPoint> trace7 { get; set; } = new ObservableCollection<ChartDataPoint>();

        double[] xAxis;
        string lastAxisType;
        int nextTrace = 0;
        const int MAX_TRACES = 8;

        ObservableCollection<ChartDataPoint> getTraceData(int trace)
        {
            switch(trace)
            {
                case 0: return trace0;
                case 1: return trace1;
                case 2: return trace2;
                case 3: return trace3;
                case 4: return trace4;
                case 5: return trace5;
                case 6: return trace6;
                case 7: return trace7;
            }
            return trace0;
        }

        string getTraceName(int trace)
        {
            switch(trace)
            {
                case 0: return nameof(trace0);
                case 1: return nameof(trace1);
                case 2: return nameof(trace2); 
                case 3: return nameof(trace3); 
                case 4: return nameof(trace4); 
                case 5: return nameof(trace5); 
                case 6: return nameof(trace6); 
                case 7: return nameof(trace7); 
            }
            return nameof(trace0);
        }

        void updateChart()
        {
            refreshChartData();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(chartData)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(spectrumMax)));
        }

        void updateTrace(int trace) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(getTraceName(trace)));

        private void refreshChartData()
        {
            // use last Measurement from the Spectrometer
            uint pixels = spec.pixels;
            double[] intensities = spec.measurement.processed;

            // pick our x-axis
            if (lastAxisType != null && lastAxisType == xAxisOption.name)
            {
                // re-use previous axis
            }
            else 
            { 
                xAxis = null;
                if (xAxisOption.name == "wavelength")
                    xAxis = spec.wavelengths;
                else if (xAxisOption.name == "wavenumber")
                    xAxis = spec.wavenumbers;
                else
                    xAxis = spec.xAxisPixels;

                lastAxisType = xAxisOption.name;
            }

            if (intensities is null || xAxis is null)
                return;

            logger.info("populating ChartData");
            chartData.Clear();
            for (int i = 0; i < pixels; i++)
                chartData.Add(new ChartDataPoint() { intensity = intensities[i], xValue = xAxis[i] });

            xAxisMinimum = xAxis[0];
            xAxisMaximum = xAxis[pixels-1];
        }

        bool doAdd()
        { 
            logger.debug("Add button pressed");

            var name = getTraceName(nextTrace);
            logger.debug($"Populating trace {name}");

            var data = getTraceData(nextTrace);
            data.Clear();
            foreach (var orig in chartData)
                data.Add(new ChartDataPoint() { xValue = orig.xValue, intensity = orig.intensity });

            updateTrace(nextTrace);

            nextTrace = (nextTrace + 1) % MAX_TRACES;

            return true;
        }

        bool doClear()
        {
            logger.debug("Clear button pressed");

            for (int i = 0; i < MAX_TRACES; i++)
            {
                getTraceData(i).Clear();
                updateTrace(i);
            }

            nextTrace = 0;

            return true;
        }

        ////////////////////////////////////////////////////////////////////////
        // Save Command
        ////////////////////////////////////////////////////////////////////////

        // invoked by ScopeView when the user clicks "Save" 
        public Command saveCmd { get; }

        // the user clicked the "Save" button on the Scope View
        bool doSave()
        {
            var ok = spec.measurement.save();
            if (ok)
                notifyToast?.Invoke($"saved {spec.measurement.filename}");
            return ok;
        }

        // This is required, but I don't remember how / why
        protected void OnPropertyChanged([CallerMemberName] string caller = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        }

        // Provided so Spectrometer notifications will immediately take effect on our View.
        void handleSpectrometerChange(object sender, PropertyChangedEventArgs e)
        {
            var name = e.PropertyName;
            logger.debug($"SVM.handleSpectrometerChange: received notification from {sender} that {name} changed");

            if (name == "batteryStatus")
            {
                updateBatteryProperties();
            }
            else if (name == "laserState")
            {
                // Is there anything useful to do with this information?
                //
                // Basically, if the laser timed-out (in manual mode "Advanced"), 
                // this should flip the switch back to the proper "off" position.
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(laserEnabled)));
            }
        }
    }
}
