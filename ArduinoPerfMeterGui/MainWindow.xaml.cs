using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ArduinoPerfMeterGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    // Dummy Comment
    public partial class MainWindow : Window
    {
        private enum ProgramState
        {
            Start,
            Initializing,
            InitComFound,
            InitComNotFound,
            SendingData,
            TimerStop,
            TimerDisposed,
            End
        }

        private const float serialPortSendHz = 15;
        private const int freeMemCeiling = (int)65536;  // 100% FreeMEM meter deflection = 64GB RAM
        private const string handshakeMessage = "fish";

        private List<PerformanceCounter> perfCounters;
        private List<double> outputFigures;
        private List<String> allSerialPorts;
        private bool timerLock;
        private SerialPort serialPort;

        private DispatcherTimer dispTimer = new DispatcherTimer(DispatcherPriority.SystemIdle);

        private ProgramState programState;

        public MainWindow()
        {
            InitializeComponent();
            RaymondsInit();
        }

        private void buExit_Click(object sender, RoutedEventArgs e)
        {
            RaymondsExit();
            this.Close();
        }

        private void RaymondsExit()
        {
            if (dispTimer.IsEnabled) dispTimer.IsEnabled = false;
            if (serialPort != null) { serialPort.Close(); }
            programState = ProgramState.End;
        }

        private void RaymondsInit() // Init for app specific stuff
        {
            programState = ProgramState.Start;

            InitSerialPort();

            // Init Performance Counters
            perfCounters = InitPerfCounters();
            outputFigures = updateOutputFigures(perfCounters);

            // Start sending data if IsOpen
            if (programState == ProgramState.InitComFound)
            {
                startTimer();
            }
        }

        private void InitSerialPort()
        {
            // SerialPort serialPort = null;
            programState = ProgramState.InitComNotFound;

            allSerialPorts = GetAllPorts();
            Debug.WriteLine($"GetAllPorts: Found COM ports: {allSerialPorts.Count}");
            cbSerial.Items.Clear();

            foreach (string serialPortName in allSerialPorts)
                cbSerial.Items.Add(serialPortName);

            foreach (string serialPortName in allSerialPorts)
            {
                try
                {
                    Debug.WriteLine("Serial port found: " + serialPortName);
                    if (serialPort != null) while (serialPort.IsOpen) { serialPort.Dispose(); }
                    serialPort = new SerialPort(serialPortName, 115200, Parity.None, 8, StopBits.One);
                    while (!serialPort.IsOpen) { serialPort.Open(); }
                    serialPort.RtsEnable = true;
                    serialPort.ReadTimeout = 250;
                    serialPort.WriteTimeout = 250;
                    serialPort.NewLine = ((char)10).ToString();

                    Debug.WriteLine("fish");
                    serialPort.WriteLine("fish");
                    serialPort.BaseStream.Flush();
                    String s = serialPort.ReadLine().Trim();
                    if (s.Contains("chips"))
                    {
                        Debug.Write("Connection made: ");
                        Debug.WriteLine(serialPortName);
                        programState = ProgramState.InitComFound;
                        cbSerial.SelectedValue = serialPortName;
                        break;
                    }
                    else
                    {
                        programState = ProgramState.InitComNotFound;
                        serialPort.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (serialPort != null) { serialPort.Dispose(); }
                    Debug.WriteLine("Expected timeout.");
                }
            }
        }

        private List<string> GetAllPorts()
        {
            Debug.WriteLine("Calling: GetAllPorts()");

            List<String> allPorts = new List<String>();
            foreach (String portName in System.IO.Ports.SerialPort.GetPortNames())
            {
                allPorts.Add(portName);
            }
            return allPorts;
        }

        private List<PerformanceCounter> InitPerfCounters()
        {
            List<PerformanceCounter> p = new List<PerformanceCounter>();
            p.Add(new PerformanceCounter("Processor", "% Processor Time", "0"));
            p.Add(new PerformanceCounter("Processor", "% Processor Time", "1"));
            //p.Add(new PerformanceCounter("Memory", "Free & Zero Page List Bytes"));
            p.Add(new PerformanceCounter("Memory", "Available MBytes"));
            PerformanceCounterCategory _pcCat = new PerformanceCounterCategory("PhysicalDisk");
            p.Add(new PerformanceCounter("PhysicalDisk", "% Disk Time", _pcCat.GetInstanceNames()[0]));     // Disk% of the first physical disk
            return p;
        }

        private List<double> updateOutputFigures(List<PerformanceCounter> allPc)
        {
            List<double> o = new List<double>();
            o.Add(allPc[0].NextValue());
            o.Add(allPc[1].NextValue());
            o.Add(allPc[2].NextValue());
            o.Add(allPc[3].NextValue());
            return o;
        }

        private void callbackSendOutFigures(object sender, EventArgs e)
        //  public void callbackSendOutFigures(object status)
        {
            programState = ProgramState.SendingData;
            if (timerLock) return;
            timerLock = true;
            if (serialPort == null) return;
            if (!serialPort.IsOpen) return;
            // Main:-
            outputFigures = updateOutputFigures(perfCounters);

            try
            {
                Debug.WriteLine("sync");
                serialPort.WriteLine("sync");
                foreach (double o in outputFigures)
                {
                    string s = o.ToString();
                    s = LeftString(s, 6);
                    Debug.WriteLine(s);
                    serialPort.WriteLine(s);
                }
            }
            catch (Exception ex)
            {
                programState = ProgramState.TimerDisposed;
                dispTimer.Stop();
                dispTimer = null;
                cbSerial.Items.Clear();
                MessageBox.Show(ex.Message, "COM Port Error", MessageBoxButton.OK);
                Debug.WriteLine($"[callbackSendOutFigures]: {ex.Message}");
            }

            timerLock = false;
        }

        private void startTimer()
        {
            if (dispTimer == null) return;
            if (dispTimer.IsEnabled) return;
            dispTimer.Interval = TimeSpan.FromMilliseconds((double)1000 / serialPortSendHz);
            dispTimer.Tick += new EventHandler(callbackSendOutFigures);
            dispTimer.Dispatcher.Thread.Priority = ThreadPriority.Highest;
            dispTimer.IsEnabled = true;
        }

        private void buStart_Click(object sender, RoutedEventArgs e)
        {
            switch (programState)
            {
                case ProgramState.Start:
                    // undefined
                    break;

                case ProgramState.Initializing:
                    // undefined
                    break;

                case ProgramState.InitComFound:
                    startTimer();
                    break;

                case ProgramState.InitComNotFound:
                    InitSerialPort();
                    if (programState == ProgramState.InitComFound)
                    {
                        startTimer();
                    }
                    break;

                case ProgramState.SendingData:
                    // Do nothing
                    break;

                case ProgramState.TimerStop:
                    dispTimer.Start();
                    break;

                case ProgramState.TimerDisposed:
                    InitSerialPort();
                    if (programState == ProgramState.InitComFound)
                    {
                        startTimer();
                    }
                    break;

                case ProgramState.End:
                    // undefined
                    break;
            }
        }

        private string LeftString(string str, int len)
        {
            return str.Substring(0, Math.Min(6, str.Length));
        }

        private void buStop_Click(object sender, RoutedEventArgs e)
        {
            switch (programState)
            {
                case ProgramState.SendingData:
                    programState = ProgramState.TimerStop;
                    if (dispTimer == null) return;
                    if (dispTimer.IsEnabled) dispTimer.Stop();
                    break;
            }
        }
    }
}