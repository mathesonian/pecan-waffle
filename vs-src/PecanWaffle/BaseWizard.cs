﻿namespace PecanWaffle {
    using EnvDTE;
    using EnvDTE100;
    using EnvDTE80;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.ExtensionManager;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.TemplateWizard;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Management.Automation;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    public abstract class BaseWizard : Component,IWizard {
        private Solution4 _solution { get; set; }
        private DTE2 _dte2 { get; set; }
        private string _templateName;
        private string _projectName;
        private string _templateSourceBranch;
        private object psinstancelock = new object();

        // TODO: How to dispose of the PowerShellInstance?

        public static PowerShell PowerShellInstance
        {
            get;
            private set;
        }
        public Solution4 GetSolution() {
            Solution4 result = null;
            if (_dte2 != null) {
                result = ((Solution4)_dte2.Solution);

                
            }

            return result;
        }

        protected internal virtual string ExtensionInstallDir
        { get; set; }

        public string GetExtensionInstallDir(string extensionId) {
            if(extensionId == null) {
                extensionId = ExtensionInstallDir;
            }

            if (string.IsNullOrWhiteSpace(extensionId)) {
                throw new ApplicationException("ExtensionId is empty");
            }

            var manager = (IVsExtensionManager)GetService(typeof(SVsExtensionManager));
            if (manager != null) {
                var extension = manager.GetInstalledExtension(extensionId);
                if (extension != null) {
                    return extension.InstallPath;
                }
            }
            else {
                throw new ApplicationException("Unable to get an instance of IVsExtensionManager");
            }

            return null;
        }

        public string TemplateName
        {
            get { return _templateName; }
        }
        public string ProjectName
        {
            get { return _projectName; }
        }
        public string TemplateSource
        {
            get; set;
        }
        public string TemplateSourceBranch
        {
            get { return _templateSourceBranch; }
        }

        public string SolutionDirectory
        {
            get; private set;
        }
        public string ExtensionId
        {
            get;private set;
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

            string tsource;
            if (replacementsDictionary.TryGetValue("TemplateSource", out tsource)) {
                TemplateSource = tsource;
            }

            string tbranch;
            if (replacementsDictionary.TryGetValue("TemplateSourceBranch", out tbranch)) {
                _templateSourceBranch = tbranch;
            }

            string slndir;
            if (replacementsDictionary.TryGetValue("$solutiondirectory$", out slndir)) {
                SolutionDirectory = slndir;
            }

            string extensionId;
            if (replacementsDictionary.TryGetValue("ExtensionId", out extensionId)) {
                ExtensionId = extensionId;
            }

            PowerShellInvoker.Instance.EnsureInstallPwScriptInvoked(ExtensionInstallDir);
        }

        public bool ShouldAddProjectItem(string filePath) {
            return false;
        }

        // http://blogs.msdn.com/b/kebab/archive/2014/04/28/executing-powershell-scripts-from-c.aspx
        /// <summary>
        /// Gets the projects in a solution recursively.
        /// </summary>
        /// <returns></returns>
        public IList<Project> GetProjects() {
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
        public static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder) {
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

        public string GetPathToModuleFile() {
            string path = Path.Combine(GetPecanWaffleExtensionInstallDir(), "pecan-waffle.psm1");
            path = new FileInfo(path).FullName;
            if (!File.Exists(path)) {
                // TODO: improve
                throw new ApplicationException(string.Format("Module not found at [{0}]", path));
            }

            return path;
        }

        public string GetPecanWaffleExtensionInstallDir() {
            return (new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName);
        }
        public void AddProjectsUnderPathToSolution(Solution4 solution, string folderPath,string pattern=@"*.*proj") {
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
        public string RemovePlaceholderProjectCreatedByVs(string projectName) {
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