namespace ZarkovWindowsService
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.zarkovserviceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.zarkovserviceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // zarkovserviceProcessInstaller
            // 
            this.zarkovserviceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.zarkovserviceProcessInstaller.Password = null;
            this.zarkovserviceProcessInstaller.Username = null;
            this.zarkovserviceProcessInstaller.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.zarkovserviceProcessInstaller_AfterInstall);
            // 
            // zarkovserviceInstaller
            // 
            this.zarkovserviceInstaller.ServiceName = "Zarkov Service DB Schema"; 
            this.zarkovserviceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.zarkovserviceProcessInstaller,
            this.zarkovserviceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller zarkovserviceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller zarkovserviceInstaller;
    }
}