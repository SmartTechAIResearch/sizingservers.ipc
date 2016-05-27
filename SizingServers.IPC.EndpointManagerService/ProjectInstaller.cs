/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace SizingServers.IPC.EndPointManagerService {
    /// <summary>
    /// 
    /// </summary>
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer {
        /// <summary>
        /// 
        /// </summary>
        public ProjectInstaller() {
            InitializeComponent();
        }
        /// <summary>
        /// Set the startup args. (port)
        /// </summary>
        /// <param name="savedState"></param>
        protected override void OnBeforeInstall(IDictionary savedState) {
            Context.Parameters["assemblypath"] = "\"" + Context.Parameters["assemblypath"] + "\" \"" + Service.DEFAULT_TCP_PORT + "\"";
            base.OnBeforeInstall(savedState);
        }
        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e) {
            new ServiceController(serviceInstaller1.ServiceName).Start();
        }
    }
}
