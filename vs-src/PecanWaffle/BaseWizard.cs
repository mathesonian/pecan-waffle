﻿namespace PecanWaffle {
    using EnvDTE;
    using EnvDTE100;
    using EnvDTE80;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.TemplateWizard;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Management.Automation;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    public abstract class BaseWizard : IWizard {
        private Solution4 _solution { get; set; }
        private DTE2 _dte2 { get; set; }
        private string _templateName;
        private string _projectName;
        private string _pecanWaffleBranchName;
        private string _templateSource;
        private string _templateSourceBranch;
        private object psinstancelock = new object();

        // TODO: How to dispose of the PowerShellInstance?

        protected internal static PowerShell PowerShellInstance
        {
            get;
            private set;
        }
        protected internal Solution4 GetSolution() {
            Solution4 result = null;
            if (_dte2 != null) {
                result = ((Solution4)_dte2.Solution);
            }

            return result;
        }
        protected internal string TemplateName
        {
            get { return _templateName; }
        }
        protected internal string ProjectName
        {
            get { return _projectName; }
        }
        protected internal string PecanWaffleBranchName
        {
            get {
                string result = "master";
                if (!string.IsNullOrWhiteSpace(_pecanWaffleBranchName)) {
                    result = _pecanWaffleBranchName;
                }

                return result;
            }
        }
        protected internal string TemplateSource
        {
            get { return _templateSource; }
        }
        protected internal string TemplateSourceBranch
        {
            get { return _templateSourceBranch; }
        }

        protected internal string ExtensionInstallDir
        {
            get
            {
                return (new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName);
            }
        }

        public virtual void BeforeOpeningFile(ProjectItem projectItem) { }

        public virtual void ProjectFinishedGenerating(Project project) {

        }

        public virtual void ProjectItemFinishedGenerating(ProjectItem projectItem) { }

        public virtual void RunFinished() {
            if (_dte2 != null) {
                _solution = (Solution4)_dte2.Solution;
            }
        }
        
        public virtual void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            _dte2 = automationObject as DTE2;

            string projName;
            if (replacementsDictionary.TryGetValue("$safeprojectname$", out projName)) {
                _projectName = projName;
            }

            string tname;
            if (replacementsDictionary.TryGetValue("TemplateName", out tname)) {
                _templateName = tname;
            }

            string pwbranch;
            if (replacementsDictionary.TryGetValue("PecanWaffleInstallBranch", out pwbranch)) {
                _pecanWaffleBranchName = pwbranch;
            }

            string tsource;
            if (replacementsDictionary.TryGetValue("TemplateSource", out tsource)) {
                _templateSource = tsource;
            }

            string tbranch;
            if (replacementsDictionary.TryGetValue("TemplateSourceBranch", out tbranch)) {
                _templateSourceBranch = tbranch;
            }

            PowerShellInvoker.Instance.EnsureInstallPwScriptInvoked(PecanWaffleBranchName);
        }

        public bool ShouldAddProjectItem(string filePath) {
            return false;
        }


        protected internal string GetStringFrom(Collection<PSObject> invokeResult) {
            if (invokeResult == null) { throw new ArgumentNullException(nameof(invokeResult)); }
            StringBuilder sb = new StringBuilder();
            foreach(var result in invokeResult) {
                sb.AppendLine(result.ToString());
            }
            return sb.ToString();
        }
        protected internal void WriteToOutputWindow(string message) {
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            Guid customGuid = new Guid("5e2e5362-86e1-466e-956b-391841275c59");
            string customTitle = "pecan-waffle";
            outWindow.CreatePane(ref customGuid, customTitle, 1, 1);

            IVsOutputWindowPane customPane;
            outWindow.GetPane(ref customGuid, out customPane);

            customPane.OutputString(message);
            customPane.Activate();
        }
        // http://blogs.msdn.com/b/kebab/archive/2014/04/28/executing-powershell-scripts-from-c.aspx
        /// <summary>
        /// Gets the projects in a solution recursively.
        /// </summary>
        /// <returns></returns>
        protected internal IList<Project> GetProjects() {
            var projects = _solution.Projects;
            var list = new List<Project>();
            var item = projects.GetEnumerator();

            while (item.MoveNext()) {
                var project = item.Current as Project;
                if (project == null) {
                    continue;
                }

                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder) {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else {
                    list.Add(project);
                }
            }
            return list;
        }

        /// <summary>
        /// Gets the solution folder projects.
        /// </summary>
        /// <param name="solutionFolder">The solution folder.</param>
        /// <returns></returns>
        protected internal static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder) {
            var list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++) {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null) {
                    continue;
                }

                // If this is another solution folder, do a recursive call, otherwise add
                if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder) {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                }
                else {
                    list.Add(subProject);
                }
            }
            return list;
        }

        protected internal string GetPathToModuleFile() {
            string path = Path.Combine(GetPecanWaffleExtensionInstallDir(), "pecan-waffle.psm1");
            path = new FileInfo(path).FullName;
            if (!File.Exists(path)) {
                // TODO: improve
                throw new ApplicationException(string.Format("Module not found at [{0}]", path));
            }

            return path;
        }

        protected internal string GetPecanWaffleExtensionInstallDir() {
            return (new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName);
        }
        protected internal void AddProjectsUnderPathToSolution(Solution4 solution, string folderPath,string pattern=@"*.*proj") {
            string[] projFiles = Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories);

            bool hadErrors = false;
            StringBuilder errorsb = new StringBuilder();
            foreach (string path in projFiles) {
                // TODO: Check to see if the project is already added to the solution
                try {
                    Project projectAdded = solution.AddFromFile(path, false);
                    // ProjectHelper.UpdatePackagesPathInProject(projectAdded,GetSolution());
                }
                catch(Exception ex) {
                    errorsb.AppendLine(ex.ToString());
                }
            }

            if (hadErrors) {
                MessageBox.Show(errorsb.ToString());
            }
        }
        protected internal string RemovePlaceholderProjectCreatedByVs(string projectName) {
            bool foundProjToRemove = false;
            var projects = GetProjects();
            Project removedProject = null;
            string projectFolder = null;
            foreach (var proj in projects) {
                if (string.Compare(projectName, proj.Name, StringComparison.OrdinalIgnoreCase) == 0) {
                    string removedProjPath = proj.FullName;
                    GetSolution().Remove(proj);
                    if (File.Exists(removedProjPath)) {
                        File.Delete(removedProjPath);
                    }
                    projectFolder = new FileInfo(removedProjPath).Directory.FullName;
                    foundProjToRemove = true;
                    removedProject = proj;
                    break;
                }
            }

            if (!foundProjToRemove) {
                // TODO: Improve
                MessageBox.Show("project to remove was null");
            }

            return projectFolder;
        }
    }
}