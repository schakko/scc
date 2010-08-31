using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceProcess;
using System.Threading;

namespace ServiceConsoleControl
{
    /// <summary>
    /// Main entry class
    /// </summary>
    class Program
    {
        private static String NAME = "scc";

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                ShowUsageAndExit(null);
            }

            try
            {
                Command cmd = Command.Factory(args[0]);
                cmd.SetServiceName(args[1]);
                cmd.Execute();
            }
            catch (Exception e)
            {
                ShowUsageAndExit("Error: " + e.Message);
            }
        }
        /// <summary>
        /// Shows the usage of this program and exits with code 0
        /// </summary>
        /// <param name="_line">additional line to print before usage is shown</param>
        public static void ShowUsageAndExit(String _line)
        {
            if (_line != null)
            {
                Program.LogLine(_line);
            }

            Program.LogLine(Program.NAME + " starts, stops and restarts services with all dependencies to other services");
            Program.LogLine("Usage: " + Program.NAME + " [" + Command.CMD_START + ", " + Command.CMD_STOP + ", " + Command.CMD_RESTART + "] [Service]");
            Environment.Exit(0);
        }

        /// <summary>
        /// Write string to console
        /// </summary>
        /// <param name="_msg">string to write</param>
        public static void LogLine(String _msg)
        {
            Console.WriteLine(_msg);
        }
    }

    /// <summary>
    /// Interface for executable service commands
    /// </summary>
    interface IServiceCommand
    {
        /// <summary>
        /// Execute the command
        /// </summary>
        void Execute();

        /// <summary>
        /// Sets name of service
        /// </summary>
        /// <param name="_name">name of service</param>
        void SetServiceName(String _name);

        /// <summary>
        /// Retrieves name of service
        /// </summary>
        /// <returns>name of service</returns>
        string GetServiceName();
    }

    /// <summary>
    /// Abstract class for commands, implements IServiceCommand
    /// </summary>
    abstract class Command : IServiceCommand
    {
        private String _name;
        
        protected ServiceController sc = new ServiceController();

        public static String CMD_STOP = "stop";
        
        public static String CMD_START = "start";
        
        public static String CMD_RESTART = "restart";

        /// <summary>
        /// Factory method for command.
        /// Given command will be lowered.
        /// If the command is not supported, an exception will be thrown
        /// </summary>
        /// <param name="_cmd">Command to execute - this can be one of this.CMD_*</param>
        /// <returns>Instance of Command</returns>
        public static Command Factory(String _cmd)
        {
            _cmd = _cmd.ToLower();

            if (_cmd.Equals(CMD_RESTART))
            {
                return new RestartCommand();
            }
            else if (_cmd.Equals(CMD_START))
            {
                return new StartCommand();
            }
            else if (_cmd.Equals(CMD_STOP))
            {
                return new StopCommand();
            }

            throw new Exception("Unsupported command: " + _cmd);
        }

        /// <summary>
        /// Sets service name, required by IServiceCommand
        /// </summary>
        /// <param name="_serviceName">Service name to set</param>
        public void SetServiceName(String _serviceName)
        {
            _name = _serviceName;
            sc.ServiceName = _serviceName;
        }

        /// <summary>
        /// Gets service name, required by IServiceCommand
        /// </summary>
        /// <returns>Service name</returns>
        public String GetServiceName()
        {
            return _name;
        }

        /// <summary>
        /// Abstract execute command which is required by IServiceCommand
        /// </summary>
        abstract public void Execute();
    }

    /// <summary>
    /// Abstract class for recursive commands, can be used for solving dependencies between services
    /// </summary>
    abstract class RecursiveCommand : Command
    {
        /// <summary>
        /// Depth of dependency
        /// </summary>
        private int _depth = 0;

        /// <summary>
        /// Stringbuilder with prefixing spaces
        /// </summary>
        private StringBuilder sb;

        /// <summary>
        /// Timespan to wait for service
        /// </summary>
        protected TimeSpan tsWait = new System.TimeSpan(10000000000000);

        /// <summary>
        /// Abstract method which retrieves the recursive command
        /// </summary>
        /// <returns>RecursiveCommand</returns>
        abstract protected RecursiveCommand GetCommand();

        /// <summary>
        /// Depth value
        /// </summary>
        public int Depth
        {
            set { _depth = value; }
            get { return _depth; }
        }

        /// <summary>
        /// Return depth as spaces
        /// </summary>
        /// <returns>String with depth as spaces</returns>
        protected String GetSpaces()
        {
            if (sb == null)
            {
                sb = new StringBuilder();

                for (int i = 0; i < _depth; i++)
                {
                    sb.Append("  ");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Executes GetCommand() on all given ServiceController[]
        /// </summary>
        /// <param name="scs">Array of ServiceController on which the GetCommand() will be executed</param>
        protected void ExecuteCommandOnServiceControllers(ServiceController[] _scs)
        {
            Program.LogLine(GetSpaces() + "Service dependencies: " + _scs.Length);

            for (int i = 0, m = _scs.Length; i < m; i++)
            {
                RecursiveCommand rc = this.GetCommand();
                rc.Depth = this.Depth + 1;
                rc.SetServiceName(_scs[i].ServiceName);
                rc.Execute();
            }
        }

        /// <summary>
        /// Returns true if service is of ServiceType Win32ShareProcess or Win32OwnProcess. Other types cannot be killed
        /// </summary>
        protected bool IsKillable
        {
            get
            {
                if (sc.ServiceType.Equals(ServiceType.Win32ShareProcess) || sc.ServiceType.Equals(ServiceType.Win32OwnProcess))
                {
                    return true;
                }

                Program.LogLine(GetSpaces() + this.DisplayName + " is of type " + sc.ServiceType.ToString() + ". These services can not be processed.");
                return false;
            }
        }

        /// <summary>
        /// Returns display name
        /// </summary>
        public String DisplayName
        {
            get
            {
                return "Service " + sc.DisplayName + " (" + sc.ServiceName + ")";
            }

        }
    }

    /// <summary>
    /// Command for restarting a service
    /// </summary>
    class RestartCommand : Command
    {
        /// <summary>
        /// Stops the service and then restarts
        /// </summary>
        public override void Execute()
        {
            IServiceCommand stopCmd = new StopCommand();
            IServiceCommand startCmd = new StartCommand();
            stopCmd.SetServiceName(this.GetServiceName());
            startCmd.SetServiceName(this.GetServiceName());

            stopCmd.Execute();
            startCmd.Execute();
        }

        public override String ToString()
        {
            return CMD_RESTART;
        }
    }

    /// <summary>
    /// Command for starting a service with all its dependencies
    /// </summary>
    class StartCommand : RecursiveCommand
    {
        public override String ToString()
        {
            return CMD_START;
        }

        /// <summary>
        /// Retrieve a new instance of myself
        /// </summary>
        /// <returns></returns>
        protected override RecursiveCommand GetCommand()
        {
            return new StartCommand();
        }

        /// <summary>
        /// Starts a service. The service could only be started if it is Win32[Own|Share]Process. KernelDriver or other types of services are not allowed.
        /// At first all services will be started which this service depends on. At second the service itself will be started.
        /// After that all services are started which depends on this service.
        /// </summary>
        public override void Execute()
        {
            if (this.IsKillable)
            {
                if (sc.Status.Equals(ServiceControllerStatus.Stopped))
                {
                    Program.LogLine(GetSpaces() + "Starting " + this.DisplayName);
                    Program.LogLine(GetSpaces() + "Starting all services that " + this.DisplayName + " depends on ...");
                    ExecuteCommandOnServiceControllers(sc.ServicesDependedOn);
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, tsWait);
                    Program.LogLine(GetSpaces() + "Service is now in status " + sc.Status.ToString());
                    Program.LogLine(GetSpaces() + "Starting all services that depends on " + this.DisplayName + " ... ");
                    ExecuteCommandOnServiceControllers(sc.DependentServices);
                }
                else
                {
                    Program.LogLine(GetSpaces() + this.DisplayName + " has status " + sc.Status.ToString() + " - nothing to do");
                }
            }
        }
    }
    
    /// <summary>
    /// Command for stopping a service with all its dependencies
    /// </summary>
    class StopCommand : RecursiveCommand
    {
        public override String ToString()
        {
            return CMD_STOP;
        }

        /// <summary>
        /// Returns an instance of myself
        /// </summary>
        /// <returns>New instance of StopCommand</returns>
        protected override RecursiveCommand GetCommand()
        {
            return new StopCommand();
        }

        /// <summary>
        /// Executes the command. The service could only be stopped if it is Win32[Own|Share]Process. KernelDriver or other types of services are not allowed.
        /// At first all services will be stopped that depends on this service. Then this service is stopped.
        /// </summary>
        public override void Execute()
        {
            if (this.IsKillable)
            {
                if (sc.Status.Equals(ServiceControllerStatus.Running))
                {
                    Program.LogLine(GetSpaces() + "Stopping " + this.DisplayName);
                    Program.LogLine(GetSpaces() + "Stopping all services that depends on " + this.DisplayName + " ...");
                    ExecuteCommandOnServiceControllers(sc.DependentServices);
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, tsWait);
                    Program.LogLine(GetSpaces() + "Service is now in status " + sc.Status.ToString());
                }
                else
                {
                    Program.LogLine(GetSpaces() + this.DisplayName + " has status " + sc.Status.ToString() + " - nothing to do");
                }
            }
        }
    }
}