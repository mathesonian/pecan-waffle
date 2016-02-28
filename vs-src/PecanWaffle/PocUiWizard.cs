﻿namespace PecanWaffle {
    using EnvDTE;
    using EnvDTE100;
    using EnvDTE80;
    using Microsoft.VisualStudio.TemplateWizard;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Management.Automation;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using NuGet;

    public class PocUiWizard : BaseWizard {
        public override void RunFinished() {
            try {
                base.RunFinished();

                // show the dialog
                var form = new PocWindow();
                var windowResult = form.ShowDialog();

                if(windowResult.HasValue && windowResult.Value) {

                    Solution4 solution = GetSolution();

                    if (solution != null) {
                        string projectFolder = RemovePlaceholderProjectCreatedByVs(ProjectName);
                        CreateProjectWithPecanWaffle(ProjectName, projectFolder, form.TemplateName, PecanWaffleBranchName, form.TemplatePathOrUrl, form.TemplateBranch);
                        AddProjectsUnderPathToSolution(solution, projectFolder, "*.*proj");
                    }
                    else {
                        // TODO: Improve
                        throw new ApplicationException("Solution is null");
                    }
                }
            }
            catch (Exception ex) {
                // TODO: Improve this
                MessageBox.Show(ex.ToString());
            }
        }
    }
}