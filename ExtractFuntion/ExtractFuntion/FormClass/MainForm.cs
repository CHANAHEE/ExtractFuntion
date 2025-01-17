﻿using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.ObjectModelRemoting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Operations;

namespace ExtractFuntion
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            Init();
        }

        public void Init()
        {
            this.MaximizeBox = false;
        }

        private void button_FindSolution_Click(object sender, EventArgs e)
        {            
            using (OpenFileDialog FileDialog = new OpenFileDialog())
            {
                FileDialog.InitialDirectory = "D:\\Project\\ExtractFunctionProject\\ExtractFuntion";
                FileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                FileDialog.FilterIndex = 2;
                FileDialog.RestoreDirectory = true;

                if (FileDialog.ShowDialog() == DialogResult.OK)
                {
                    this.textBox_SolutionPath.Text = FileDialog.FileName;
                }
            }
        }

        private async void button_StartExtract_Click(object sender, EventArgs e)
        {
            string SolutionPath = this.textBox_SolutionPath.Text;

            SolutionFile Solution = SolutionFile.Parse(SolutionPath);
            IEnumerable<ProjectInSolution> ProjectList = Solution.ProjectsInOrder;

            this.progressBar_Progress.Minimum = 0;
            this.progressBar_Progress.Maximum = ProjectList.Count();

            int CurrentProgressPoint = 0;

            foreach (var Project in ProjectList)
            {
                await ExtractFunction_Process(Project, SolutionPath);

                CurrentProgressPoint++;

                this.progressBar_Progress.Value++;
                this.label_ProgressPercent.Text = ((int)(((double)CurrentProgressPoint / (double)ProjectList.Count()) * 100)).ToString() + " %";
            }
        }

        private Task ExtractFunction_Process(ProjectInSolution Project, string SolutionPath)
        {
            return Task.Run(() =>
            {
                string ProjectFileName = Project.RelativePath.Replace($"{Project.ProjectName}\\", "");
                string ProjectFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath), Project.AbsolutePath);
                string ProjectFolderPath = ProjectFilePath.Replace($"\\{ProjectFileName}", "");

                // 시트 생성
                ExcelManager.Instance.Make_ExcelSheet(ProjectFileName.Replace(".csproj", ""));

                // Excel UI 초기 작업
                ExcelManager.Instance.Init_UI();

                ExtractClassFile_All(ProjectFolderPath);
            });
        }

        private void ExtractClassFile_All(string ProjectPath)
        {
            var files = Directory.GetFiles(ProjectPath, "*.cs", SearchOption.AllDirectories).
                                                Where(s => s.Contains("\\bin\\") == false).
                                                Where(s => s.Contains("\\obj\\") == false).
                                                Where(s => s.Contains("\\Config\\") == false).
                                                Where(s => s.Contains(".Designer") == false).
                                                Where(s => s.Contains("\\Properties\\") == false).
                                                Where(s => s.Contains("\\Design\\") == false).
                                                Where(s => s.Contains("\\RuleDefine\\") == false);

            foreach (var file in files)
            {                
                ExtractMethod_All(file);
            }

            // 클래스 파일, 함수 이름 삽입 후, UI 작업
            ExcelManager.Instance.Make_UI(ExcelManager.Instance.CELL_INDEX);

            // 클래스 파일 별 함수 개수 구분을 위한 변수 초기화
            ExcelManager.Instance.CELL_INDEX = 0;
        }

        private void ExtractMethod_All(string ClassFile)
        {
            string CodeScript = File.ReadAllText(ClassFile);
            SyntaxTree Tree = CSharpSyntaxTree.ParseText(CodeScript);

            try
            {
                var Method = Tree.GetRoot().DescendantNodes()
                         .OfType<MethodDeclarationSyntax>();

                Console.WriteLine($"=============== [ClassFile] {ClassFile}");

                //해당 cs 파일 정보를 삽입
                bool IsNotNullMethod = ExcelManager.Instance.Make_ClassFile_CellValue(ClassFile, Method.Count(), ExcelManager.Instance.CELL_INDEX);

                if(IsNotNullMethod == false)
                {
                    return;
                }

                //해당 cs 파일의 모든 메소드의 정보를 삽입
                for (int CurrentIndex = 0; CurrentIndex < Method.Count(); CurrentIndex++)
                {
                    ExcelManager.Instance.Make_Function_CellValue(Method, ExcelManager.Instance.CELL_INDEX, CurrentIndex);
                    Console.WriteLine($"Method{CurrentIndex} :{Method.ElementAt(CurrentIndex).Modifiers} {Method.ElementAt(CurrentIndex).ReturnType} {Method.ElementAt(CurrentIndex).Identifier} {Method.ElementAt(CurrentIndex).ParameterList}");

                    ExcelManager.Instance.CELL_INDEX++;
                }
            }
            catch(Exception ex) 
            {
                Console.WriteLine($"[Error] {ex.Message}");
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            ExcelManager.Instance.ReleaseMemory();
            Process.GetCurrentProcess().Kill();
        }
    }
}
