using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using InfluxDB.Collector;
using System.Diagnostics;
using System.IO;
using DotNetSiemensPLCToolBoxLibrary.Communication;
using DotNetSiemensPLCToolBoxLibrary.DataTypes;
using System.Threading;
using System.Timers;

namespace Test_InfluxDB
{
    public partial class Form1 : Form
    {
        //SPS Handler
        private List<PLCConnection> plcConnection; //Objekt für die SPS-Verbindung
        private string[] conn = new string[] { "MultipleSPSExample_Connection_1" }; //Name der SPS-Konfiguration
        List<PLCTag> tags_read = new List<PLCTag>(); //Daten die von der SPS gelsen werden

        public static AbortableBackgroundWorker worker;
        System.Timers.Timer timer_cycle = new System.Timers.Timer();

        public Form1()
        {
            InitializeComponent();
            ReadSPSItems();

            timer_cycle = new System.Timers.Timer(60000);
            timer_cycle.Elapsed += new ElapsedEventHandler(timer_cycle_Tick);

            worker = new AbortableBackgroundWorker();
            worker.DoWork += worker_DoWork;
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            ConnectPLC();

            plcConnection[0].ReadValues(tags_read);

            var process = Process.GetCurrentProcess();

            Metrics.Collector = new CollectorConfiguration()
                .Tag.With("host", Environment.GetEnvironmentVariable("COMPUTERNAME"))
                .Tag.With("os", Environment.GetEnvironmentVariable("OS"))
                .Tag.With("process", Path.GetFileName(process.MainModule.FileName))
                .Batch.AtInterval(TimeSpan.FromSeconds(2))
                .WriteTo.InfluxDB("http://10.8.0.10:8086", "tempdb", "root", "root")
                .CreateCollector();

            //Jeden Wert in das Datenbanksystem einfügen
            foreach (PLCTag tempTag in tags_read)
            {
                //Intressanter Part
                Metrics.Write(tempTag.S7FormatAddress,
                    new Dictionary<string, object>
                {
                    { "value", tempTag.Value }
                });
            }
            Console.WriteLine("Daten in die InflixDB hinzugefügt....");

            DisconnectPLC();
        }

        private void timer_cycle_Tick(object sender, ElapsedEventArgs e)
        {
            if (!worker.IsBusy)
                worker.RunWorkerAsync();
        }

        private void ReadSPSItems()
        {
            tags_read.Add(new PLCTag("DB42.DBD508", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD500", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD504", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD516", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD524", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD528", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD532", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD536", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD540", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD544", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD564", TagDataType.Float));
            tags_read.Add(new PLCTag("DB42.DBD576", TagDataType.Float));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            timer_cycle.Enabled = true;
        }

        private void ConnectPLC()
        {
            plcConnection = new List<PLCConnection>();
            try
            {
                PLCConnection akConn = new PLCConnection(conn[0]);

                plcConnection.Add(akConn);
                akConn.Connect();
            }
            catch (Exception ex)
            {
                DisconnectPLC();
                return;
            }
        }

        private void DisconnectPLC()
        {
            if (plcConnection != null)
                foreach (PLCConnection plcConnectioni in plcConnection)
                {
                    plcConnectioni.Dispose();
                }
            plcConnection = null;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Configuration.ShowConfiguration(conn[0], true);
        }
    }

    public class AbortableBackgroundWorker : BackgroundWorker
    {

        private Thread workerThread;

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            workerThread = Thread.CurrentThread;
            try
            {
                base.OnDoWork(e);
            }
            catch (ThreadAbortException)
            {
                e.Cancel = true; //We must set Cancel property to true!
                Thread.ResetAbort(); //Prevents ThreadAbortException propagation
            }
        }


        public void Abort()
        {
            if (workerThread != null)
            {
                workerThread.Abort();
                workerThread = null;
            }
        }
    }
}
