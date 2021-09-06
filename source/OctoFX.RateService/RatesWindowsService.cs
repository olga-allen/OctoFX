using NHibernate;
using OctoFX.Core.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace OctoFX.RateService
{
    partial class RatesWindowsService : ServiceBase
    {
        readonly ISessionFactory sessionFactory;
        readonly IMarketExchangeRateProvider rateProvider;
        readonly Timer timer;

        public RatesWindowsService(ISessionFactory sessionFactory, IMarketExchangeRateProvider rateProvider)
        {
            InitializeComponent();

            this.sessionFactory = sessionFactory;
            this.rateProvider = rateProvider;
            timer = new Timer(5000) { AutoReset = true };
            timer.Elapsed += (sender, eventArgs) => GenerateNewRates();
        }

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            //eventLog1.WriteEntry("In OnStart.");
            // Set up a timer that triggers every minute.
            Timer timer = new Timer();
            timer.Interval = 60000; // 60 seconds
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {
            timer.Stop();
        }
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.
           // eventLog1.WriteEntry("Monitoring the System", EventLogEntryType.Information, eventId++);
        }

        void GenerateNewRates()
        {
            try
            {
                using (var session = sessionFactory.OpenSession())
                using (var transaction = session.BeginTransaction())
                {
                    var rates = session.QueryOver<ExchangeRate>()
                        .List();

                    foreach (var rate in rates)
                    {
                        rate.Rate = rateProvider.GetCurrentRate(rate.SellBuyCurrencyPair);
                        Console.WriteLine("Rate for {0}: {1:n4}", rate.SellBuyCurrencyPair, rate.Rate);
                        session.Update(rate);
                    }

                    session.Flush();
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
        }
        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

    }
}
