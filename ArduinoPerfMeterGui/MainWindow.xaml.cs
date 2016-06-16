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
        private const float checkMailMinutes = 3F;
        private const int freeMemCeiling = (int)65536;  // 100% FreeMEM meter deflection = 64GB RAM
        private const string handshakeMessage = "fish";

        private List<PerformanceCounter> perfCounters;
        private List<double> outputFigures;
        private List<String> allSerialPorts;
        private bool timerLock;
        private SerialPort serialPort;
        private DispatcherTimer dispTimerMetrics = new DispatcherTimer(DispatcherPriority.Normal);
        private DispatcherTimer dispTimerMailChecker = new DispatcherTimer(DispatcherPriority.Background);
        private ProgramState programState;
        private NotesMailChecker mailChecker = new NotesMailChecker();
        private bool mailUpdateQueued = false;

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
            if (dispTimerMetrics.IsEnabled) dispTimerMetrics.IsEnabled = false;
            if (dispTimerMailChecker.IsEnabled) dispTimerMailChecker.IsEnabled = false;
            if (serialPort != null) { serialPort.Close(); }
            programState = ProgramState.End;
        }

        private void RaymondsInit() // Init for app specific stuff
        {
            programState = ProgramState.Start;
            mailUpdateQueued = false;
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

        private void callbackQueueCheckMail(object sender, EventArgs e)
        {
            Debug.WriteLine("[callbackQueueCheckMail] called.");
            mailUpdateQueued = true;
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
                // Send out metrics
                Debug.WriteLine("sync");
                serialPort.WriteLine("sync");
                foreach (double o in outputFigures)
                {
                    string s = o.ToString();
                    s = LeftString(s, 6);
                    Debug.WriteLine(s);
                    serialPort.WriteLine(s);
                }

                // Check unread mail
                if (mailUpdateQueued)
                {
                    int numMail = mailChecker.GetNewUnreadMail();
                    if (numMail > 0)
                    {
                        Debug.WriteLine("email");
                        serialPort.WriteLine("email");
                        mailUpdateQueued = false;
                    }
                    else mailUpdateQueued = false;
                }
            }
            catch (Exception ex)
            {
                programState = ProgramState.TimerDisposed;
                dispTimerMetrics.Stop();
                dispTimerMetrics = null;
                cbSerial.Items.Clear();
                MessageBox.Show(ex.Message, "COM Port Error", MessageBoxButton.OK);
                Debug.WriteLine($"[callbackSendOutFigures]: {ex.Message}");
            }
            timerLock = false;
        }

        private void startTimer()
        {
            if (dispTimerMailChecker != null && !dispTimerMailChecker.IsEnabled)
            {
                dispTimerMailChecker.Interval = TimeSpan.FromMinutes(checkMailMinutes);
                dispTimerMailChecker.Tick += new EventHandler(callbackQueueCheckMail);
                dispTimerMailChecker.Dispatcher.Thread.Priority = ThreadPriority.Normal;
                dispTimerMailChecker.IsEnabled = true;
                mailUpdateQueued = true;    // check mail immediately before the first time timer expires (no wait).
            }
            if (dispTimerMetrics != null && !dispTimerMetrics.IsEnabled)
            {
                dispTimerMetrics.Interval = TimeSpan.FromMilliseconds((double)1000 / serialPortSendHz);
                dispTimerMetrics.Tick += new EventHandler(callbackSendOutFigures);
                dispTimerMetrics.Dispatcher.Thread.Priority = ThreadPriority.Normal;
                dispTimerMetrics.IsEnabled = true;
            }


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
                    dispTimerMetrics.Start();
                    dispTimerMailChecker.Start();
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
                    if (dispTimerMetrics != null) if (dispTimerMetrics.IsEnabled) dispTimerMetrics.Stop();
                    if (dispTimerMailChecker != null) if (dispTimerMailChecker.IsEnabled) dispTimerMailChecker.Stop();
                    break;
            }
        }
        private void resetMailCheck()
        {
            mailChecker.clearMailList();
            mailUpdateQueued = true;
        }

        private void buCheckMail_Click(object sender, RoutedEventArgs e)
        {
            switch (programState)
            {
                case ProgramState.SendingData: resetMailCheck();  break;
                case ProgramState.TimerStop: resetMailCheck(); startTimer(); break;
            }
            Debug.WriteLine($"Mail queued? {mailUpdateQueued.ToString()}");
        }
    }
}