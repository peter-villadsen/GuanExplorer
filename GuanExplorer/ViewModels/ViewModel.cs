using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using GuanExplorer.Properties;
using Guan.Logic;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit;
using System.Windows.Controls;

namespace DatalogExplorer.ViewModels
{
    internal class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Command implementations
        public static ICommand ApplicationExitCommand => new RelayCommand(
            _ =>
            {
                Application.Current.Shutdown();
            });

        public static ICommand OpenFileCommand => new RelayCommand(
            p =>
            {
                MainWindow view = (MainWindow)p!;
                var openFileDialog = new OpenFileDialog()
                {
                    CheckFileExists = true,
                    AddExtension = true,
                    DefaultExt = "dl",
                    Filter = "Datalog|*.dl|All|*.*",
                    Multiselect = false,
                    Title = "Select datalog document to open",
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var source = File.ReadAllText(openFileDialog.FileName);
                    view.FactsAndRulesEditor.Text = source;
                }

            });

        public static ICommand SaveFileCommand => new RelayCommand(
            p =>
            {
                MainWindow view = (MainWindow)p!;
                var saveFileDialog = new SaveFileDialog()
                {
                    AddExtension = true,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    DefaultExt = "dl",
                    Filter = "Datalog|*.dl|All|*.*",
                    OverwritePrompt = true,
                    Title = "Save datalog document",
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, view.FactsAndRulesEditor.Text);
                }
            });

        public static ICommand AboutCommand => new RelayCommand(_ =>
        {
            var aboutBox = new DatalogExplorer.Views.AboutBox();
            aboutBox.Show();

        });

        public static ICommand IncreaseQueryFontSizeCommand => new RelayCommand(
            target =>
            {
                var view = (MainWindow)target!;
                if (Settings.Default.FontSize < 32)
                {
                    var fontSize = Settings.Default.FontSize + 2;
                    view.FactsAndRulesEditor.FontSize = fontSize;
                    view.TranscriptEditor.FontSize = fontSize;
                    Settings.Default.FontSize = fontSize;
                }
            },
            canExecute => Settings.Default.FontSize < 32);

        public static ICommand DecreaseQueryFontSizeCommand => new RelayCommand(
            target =>
            {
                var view = (MainWindow)target!;
                if (Settings.Default.FontSize > 5)
                {
                    var fontSize = Settings.Default.FontSize - 2;
                    view.FactsAndRulesEditor.FontSize = fontSize;
                    view.TranscriptEditor.FontSize = fontSize;
                    Settings.Default.FontSize = fontSize;
                }
            },
            canExecute => Settings.Default.FontSize > 5);

        private string GetQueryText(TextEditor editor)
        {
            // If there is a selection, use the line where the 
            // selection starts and the end of the line where it
            // ends.
            if (!string.IsNullOrWhiteSpace(editor.SelectedText))
            {
                // We have a selection. Now find that line and column
                //var startLine = editor.Document.GetLineByNumber(editor.SelectionStart);
                //var endLine = editor.Document.GetLineByNumber(editor.SelectionStart + editor.SelectionLength);

                // Get the text from start of the first line till the end of the last line.
                var txt = editor.SelectedText;
                return txt;
            }

            return editor.Text;
        }

        public ICommand ExecuteCommand => new RelayCommand(async p =>
        {
            var view = (MainWindow)p!;
            string rulesText = view.FactsAndRulesEditor.Text;


            Stopwatch sw = new();
            sw.Start();
            
            try
            {
                // Compile the facts and rules.
                // Guan only offers an API that deals with parsing one rule at the time. This
                // makes it quite useless for this kind of application. For now, just assume
                // that each rule is on a separate line.
                var rules = rulesText.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                var module = Module.Parse("Source", rules, null);

                ModuleProvider moduleProvider = new ModuleProvider();
                moduleProvider.Add(module);

                // Compile the queries:
                QueryContext queryContext = new QueryContext(moduleProvider);

                // The Query instance that will be used to execute the supplied query expression over the related rules.

                // Get rid of comments with double dash
                var queryText = GetQueryText(view.TranscriptEditor);
                queryText = queryText.Trim();

                Query query = Query.Create(queryText, queryContext);

                // Execute the query. 
                // Gets multiple results, if possible, up to supplied maxResults value.
                List<Term> results = await query.GetResultsAsync(10000);

                UpdateProgramView(module, results, view);
            }
            catch (Exception ex)
            {
                view.TranscriptEditor.AppendText(Environment.NewLine + ex.Message);
            }
            finally
            {
                sw.Stop();
                this.Message = $"{sw.ElapsedMilliseconds}ms elapsed";
            }
        });

        #endregion

        private void UpdateProgramView(Module module, IList<Term> results, MainWindow view)
        {
            view.ProgramTree.Items.Clear();
            var rootItem = new TreeViewItem() { Header = $"{module.Name}" };
            rootItem.ItemsSource = module.GetPublicTypes().Select(n => n.Name + "(" + string.Join(",", n.RequiredArguments) + ")");
            rootItem.IsExpanded = true;

            // Copy the results to the worksheet, below the query text
            var result = $"{string.Join(Environment.NewLine, results)}{Environment.NewLine}";

            view.ProgramTree.Items.Add(rootItem);
            view.TranscriptEditor.AppendText(result);

            // Update the Program view with the information
        }

        private string result = string.Empty;
        public string Result
        {
            get => this.result;
            set
            {
                this.result = value;
                this.OnPropertyChanged(nameof(Result));
            }
        }

        public ViewModel()
        {
            Settings.Default.PropertyChanged += (object? sender, PropertyChangedEventArgs e) =>
            {
                // Save all the user's settings when they change.
                Settings.Default.Save();
            };
        }
        #region properties

        private string? _caretPositionString;
        public string? CaretPositionString
        {
            get { return this._caretPositionString;  }
            set 
            {
                this._caretPositionString = value;
                this.OnPropertyChanged(nameof(CaretPositionString));
            }
        }

        private string? _message;
        public string? Message
        {
            get { return this._message; }
            set { this._message = value; this.OnPropertyChanged(nameof(Message));  }

        }
        int _fontSize = 12;
        public int FontSize 
        { 
            get { return _fontSize; }
            set
            {
                if (value != _fontSize)
                {
                    this._fontSize = value;
                    this.OnPropertyChanged(nameof(FontSize));
                }
            }
        }

        bool _showLineNumbers = true;
        public bool ShowLineNumbers
        {
            get { return _showLineNumbers; }
            set
            {
                if (value != _showLineNumbers)
                {
                    this._showLineNumbers = value;
                    this.OnPropertyChanged(nameof(ShowLineNumbers));
                }
            }
        }
        #endregion
    }
}
