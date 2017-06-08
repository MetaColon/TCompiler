﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TCompiler.Main;
using TIDE.Coloring.StringFunctions;
using TIDE.Coloring.Types;
using TIDE.Forms.Documentation;
using TIDE.Forms.Tools;
using TIDE.IntelliSense;
using TIDE.Properties;

#endregion

namespace TIDE.Forms
{
    /// <summary>
    ///     The main IDE class for the TIDE
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public partial class TIDE_MainWindow : Form
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        /// <summary>
        ///     The documentation window in which the help is shown
        /// </summary>
        private readonly DocumentationWindow _documentationWindow;

        /// <summary>
        ///     Was the intelliSense form hidden by the user
        /// </summary>
        public bool IntelliSenseCancelled;

        /// <summary>
        ///     Indicates wether multiple characters get automatically typed
        /// </summary>
        private bool _isInMultipleCharacterMode;

        /// <summary>
        ///     Indicates wether a new key got pressed while handling the old one
        /// </summary>
        private bool _newKey;

        /// <summary>
        ///     The path to save the currently opened document
        /// </summary>
        private string _savePath;

        /// <summary>
        ///     The whole text of the current document
        /// </summary>
        private string _wholeText;

        /// <summary>
        ///     The external files used in the project
        /// </summary>
        public readonly List<FileContent> ExternalFiles;

        /// <summary>
        ///     A manager for IntelliSense
        /// </summary>
        private readonly IntelliSenseManager _intelliSenseManager;

        /// <summary>
        ///     Initializes a new TIDE
        /// </summary>
        public TIDE_MainWindow()
        {
            AllocConsole();
            _intelliSenseManager = new IntelliSenseManager(this);

            _documentationWindow = new DocumentationWindow();

            _intelliSenseManager.StopIntelliSenseUpdateThread();

            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            Intellisensing = false;
            Unsaved = false;
            SavePath = null;
            _wholeText = "";
            ExternalFiles = new List<FileContent>();

            IntelliSensePopUp = new IntelliSensePopUp(new Point(0, 0)) { Visible = false };
            IntelliSensePopUp.ItemEntered += IntelliSense_ItemSelected;

            InitializeComponent();
            Focus();

            Editor.SetDoublebuffered(true);
            AssemblerTextBox.SetDoublebuffered(true);
        }

        /// <summary>
        ///     The current IntelliSensePopUp
        /// </summary>
        public IntelliSensePopUp IntelliSensePopUp { get; }

        /// <summary>
        ///     Indicates wether the user didn't save the latest changes
        /// </summary>
        private bool Unsaved { get; set; }

        /// <summary>
        ///     Probably indicates wether the intelliSense window is open. Old: Indicates wether the text is changing because of intelliSense actions
        /// </summary>
        private bool Intellisensing { get; set; }

        /// <summary>
        ///     The path to save the currently opened document
        /// </summary>
        private string SavePath
        {
            get => _savePath;
            set
            {
                var findForm = FindForm();
                if (findForm != null)
                    findForm.Text = value != null ? $@"TIDE - {value.Split('\\', '/').Last()}" : Resources.TIDE;
                _savePath = value;
            }
        }

        /// <summary>
        ///     Saves the current dialogue (if necessary or wanted with dialogue)
        /// </summary>
        /// <param name="showDialogue">Indicates wether to use a dialogue</param>
        private void Save(bool showDialogue)
        {
            if (SavePath == null || showDialogue)
            {
                var dialog = new SaveFileDialog
                {
                    AddExtension = true,
                    OverwritePrompt = true,
                    Title = Resources.Save,
                    Filter = Resources.Type_Ending,
                    DefaultExt = "tc"
                };
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                SavePath = dialog.FileName;
            }
            Unsaved = false;
            File.WriteAllText(SavePath, Editor.Text);
        }

        /// <summary>
        ///     Compiles the current document
        /// </summary>
        private async Task<string> Compile() => await Task.Run(() =>
        {
            var ex = Main.CompileFile(SavePath, "out.asm", "error.txt");
            var error = File.ReadAllText("error.txt");
            var output = File.ReadAllText("out.asm");
            if (ex != null)
            {
                if (ex.CodeLine?.LineIndex >= 0 && ex.CodeLine?.FileName == SavePath)
                    Editor.HighlightLine(ex.CodeLine.LineIndex, Color.Red);
                MessageBox.Show(error, Resources.Error);
                if (ex.CodeLine?.LineIndex >= 0 && ex.CodeLine?.FileName == SavePath)
                    Editor.HighlightLine(ex.CodeLine.LineIndex, Editor.BackColor);
                return "";
            }

            Invoke(new Action(() =>
            {
                TabControl.SelectTab(AssemblerPage);
                AssemblerTextBox.Text = output;
                AssemblerTextBox.ColorAll(true);
            }));
            return output;
        });

        /// <summary>
        ///     Opens a new document - always opens a new dialogue
        /// </summary>
        private void Open()
        {
            var dialog = new OpenFileDialog
            {
                AddExtension = true,
                Title = Resources.Open,
                Filter = Resources.Type_Ending,
                DefaultExt = "tc"
            };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            SavePath = dialog.FileName;
            Editor.TextChanged -= Editor_TextChanged;
            Editor.Text = File.ReadAllText(SavePath);
            Editor.ColorAll();
            _wholeText = new string(Editor.Text.ToCharArray());
            Editor.TextChanged += Editor_TextChanged;
        }

        #region Eventhandling

        #region ContextMenuHandling

        private void CopyCm(object obj, EventArgs e) => Editor.Copy();

        private void CutCm(object obj, EventArgs e) => Editor.Cut();

        private void PasteCm(object obj, EventArgs e) => Editor.Paste();

        private void UndoCm(object obj, EventArgs e) => Editor.Undo();

        private void RedoCm(object obj, EventArgs e) => Editor.Redo();

        private void SelectAllCm(object obj, EventArgs e) => Editor.SelectAll();

        private void CompileCm(object obj, EventArgs e) => RunButton.PerformClick();

        private void SaveCm(object obj, EventArgs e) => SaveButton.PerformClick();

        private void SaveAsCm(object obj, EventArgs e) => SaveAsButton.PerformClick();

        private void OpenCm(object obj, EventArgs e) => OpenButton.PerformClick();

        private void NewCm(object obj, EventArgs e) => NewButton.PerformClick();

        /// <summary>
        ///     The handler of the color all context menu
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="e"></param>
        private void ColorAllCm(object obj, EventArgs e) => ColorAllButton.PerformClick();

        #endregion

        #region ButtonHandling

        /// <summary>
        ///     Gets fired when the ColorAllButton got clicked and colors the whole document
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void ColorAllButton_Click(object sender, EventArgs e) => Editor.ColorAll();

        /// <summary>
        ///     Gets fired when the help button got clicked and prompts some help
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void HelpButton_Click(object sender, EventArgs e)
        {
            _documentationWindow.ShowDialog();
            //_documentationWindow = new DocumentationWindow();
        }

        /// <summary>
        ///     Gets fired when the new button got pressed and creates a new document
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void NewButton_Click(object sender, EventArgs e)
        {
            if (Unsaved)
            {
                var res = MessageBox.Show(Resources.Do_you_want_to_save_your_changes, Resources.Warning,
                    MessageBoxButtons.YesNoCancel);
                switch (res)
                {
                    case DialogResult.Yes:
                        SaveButton.PerformClick();
                        break;
                    case DialogResult.Cancel:
                        return;
                }
            }
            Editor.Text = "";
            SavePath = null;
        }

        /// <summary>
        ///     Gets fired when the Save as Button got pressed and prompts a new save window
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void SaveAsButton_Click(object sender, EventArgs e) => Save(true);

        /// <summary>
        ///     Gets fired when the Run button got pressed and compiles and runs the current document
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private async void RunButton_Click(object sender, EventArgs e)
        {
            SaveButton.PerformClick();
            if (SavePath == null)
            {
                MessageBox.Show(Resources.You_have_to_save_first, Resources.Error);
                return;
            }

            var processName = "8051SimulatorAsm.jar";

            var compiled = await Compile();
            if (string.IsNullOrEmpty(compiled))
                return;
            Clipboard.SetText(compiled);
            if (!File.Exists(processName))
            {
                MessageBox.Show(Resources.LostTheSimulatorFileInfoText, Resources.Error);
                return;
            }
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(processName)
            };
            process.Start();
        }

        /// <summary>
        ///     Gets fired when the Save button got pressed and saves the current document
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void SaveButton_Click(object sender, EventArgs e) => Save(false);

        /// <summary>
        ///     Gets fired when the Open button is pressed and opens a new document
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void OpenButton_Click(object sender, EventArgs e)
        {
            if (Unsaved)
            {
                var res = MessageBox.Show(Resources.Do_you_want_to_save_your_changes, Resources.Warning,
                    MessageBoxButtons.YesNoCancel);
                switch (res)
                {
                    case DialogResult.Yes:
                        SaveButton.PerformClick();
                        break;
                    case DialogResult.Cancel:
                        return;
                }
            }
            Open();
        }

        /// <summary>
        ///     Gets fired when the ParseToAssembler button is pressed and parses the document to assembler code
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private async void ParseToAssemblerButton_Click(object sender, EventArgs e)
        {
            SaveButton.PerformClick();
            if (SavePath == null)
            {
                MessageBox.Show(Resources.You_have_to_save_first, Resources.Error);
                return;
            }
            await Compile();
        }

        #endregion

        /// <summary>
        ///     Gets fired when an item from intelliSense is selected and inserts the selected item
        /// </summary>
        private void IntelliSense_ItemSelected(object sender, ItemSelectedEventArgs e)
        {
            _intelliSenseManager.HideIntelliSense();
            //Intellisensing = true;
            var res = GetCurrent.GetCurrentWord(Editor.SelectionStart, Editor)?.Value;
            var s = e.SelectedItem.Substring(e.SelectedItem.Length >= (res?.Length ?? 0) ? res?.Length ?? 0 : 0) + " ";
            Focus();
            InsertMultiplecharacters(s);
        }

        /// <summary>
        ///     Inserts multiple characters at the current cursorPosition
        /// </summary>
        /// <param name="s">The characters as a string</param>
        private void InsertMultiplecharacters(string s)
        {
            Editor.BeginUpdate();
            _isInMultipleCharacterMode = true;
            var lengthBefore = Editor.TextLength;
            SendKeys.Flush();
            for (var i = 0; i < Editor.TextLength - lengthBefore; i++)
                SendKeys.SendWait("\b");    //Shut up - it works like that and I can't get the Tab out of the windows message queue...

            foreach (var c in s)
            {
                SendKeys.SendWait(c.ToString()); //Because this is hilarious
                Editor_TextChanged();
            }
            _isInMultipleCharacterMode = false;
            Editor.EndUpdate();
        }

        private async void AddExternalFileContent(string path) => await Task.Run(() =>
        {
            if (ExternalFiles.Any(file => file.Path.Equals(path)))
                return;

            var fileContent = new FileContent(path);
            if (fileContent.Content == null)
                return;
            ExternalFiles.Add(fileContent);
            foreach (var line in fileContent.Content.Split('\n').Where(s => s.StartsWith("include ")))
                AddExternalFileContent(line.Substring("include ".Length));
        });

        private async void RemoveOldExternalFileContent(string oldPath) => await Task.Run(() =>
        {
            var fileContent = ExternalFiles.FirstOrDefault(file => file.Path.Equals(oldPath));
            if (fileContent?.Content == null)
                return;

            ExternalFiles.Remove(fileContent);
            foreach (var line in fileContent.Content.Split('\n').Where(s => s.StartsWith("include ")))
                RemoveOldExternalFileContent(line.Substring("include ".Length));
        });

        /// <summary>
        ///     Gets fired when the TextBox changed
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void Editor_TextChanged(object sender = null, EventArgs e = null)
        {
            var removed = StringFunctions.GetRemoved(_wholeText, Editor.Text);
            var added = StringFunctions.GetAdded(_wholeText, Editor.Text);

            if (added.Count > 0 && !char.IsLetter(added.LastOrDefault()) || removed.Count > 0 && !char.IsLetter(removed.FirstOrDefault()))
            {
                IntelliSenseCancelled = false;
                Intellisensing = false;
                _intelliSenseManager.HideIntelliSense();
            }
            else if (!Intellisensing && !IntelliSenseCancelled && char.IsLetter(added.LastOrDefault()) && !_isInMultipleCharacterMode)
            {
                Intellisensing = true;
                _intelliSenseManager.ShowIntelliSense();
            }

            var currentLine = Editor.CurrentLine().Trim();
            if (currentLine.StartsWith("include ", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Editor.SelectionStart < _wholeText.Length)
                    RemoveOldExternalFileContent(_wholeText.Split('\n')[
                        Editor.GetLineFromCharIndex(Editor.SelectionStart)].
                    Substring("include ".Length));

                AddExternalFileContent(currentLine.Substring("include ".Length));
            }

            _newKey = false;
            if (Editor.Text.Length - _wholeText.Length == 0)
                return;
            if (Editor.Text.Length - _wholeText.Length > 1)
            {
                Editor.Format();
                Editor_FontChanged();
            }
            else
            {
                if (removed.Contains(';') && Editor.Text.Length > 0)
                {
                    Editor.ColorCurrentLine();
                }
                else
                {
                    var cChar = GetCurrent.GetCurrentCharacter(Editor.SelectionStart, Editor);
                    if (!string.IsNullOrEmpty(cChar?.Value.ToString()) && cChar.Value == ';')
                    {
                        Editor.ColorCurrentLine();
                    }
                    else
                    {
                        Editor.BeginUpdate();
                        var word = GetCurrent.GetCurrentWord(Editor.SelectionStart, Editor);
                        Coloring.Coloring.WordActions(word, Editor);
                        Coloring.Coloring.CharActions(cChar, Editor);
                        Editor.EndUpdate();
                    }
                }
            }
            Unsaved = true;
            _wholeText = new string(Editor.Text.ToCharArray());

            if (_newKey)
                return;
            _intelliSenseManager.UpdateIntelliSense();
        }

        /// <summary>
        ///     Gets fired when the TIDE has loaded
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void TIDE_Load(object sender, EventArgs e)
        {
            IntelliSensePopUp.Show();
            _intelliSenseManager.UpdateIntelliSense();
            _intelliSenseManager.HideIntelliSense();
            Editor.Focus();

            Editor.ContextMenu = new ContextMenu(new List<MenuItem>
            {
                new MenuItem("Copy", CopyCm),
                new MenuItem("Cut", CutCm),
                new MenuItem("Paste", PasteCm),
                new MenuItem("Undo", UndoCm),
                new MenuItem("Redo", RedoCm),
                new MenuItem("Select all", SelectAllCm),
                new MenuItem("Compile", CompileCm),
                new MenuItem("Save", SaveCm),
                new MenuItem("Save as", SaveAsCm),
                new MenuItem("Open", OpenCm),
                new MenuItem("New", NewCm),
                new MenuItem("Color all", ColorAllCm)
            }.ToArray());
        }

        /// <summary>
        ///     Gets fired when the cursor position has changed
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="eventArgs">Useless</param>
        public async void Editor_SelectionChanged(object sender, EventArgs eventArgs)
            => await Task.Run(() =>
            {
                if (!IntelliSensePopUp.Visible)
                    return;
                Editor.Invoke(new Action(() =>
                {
                    PositionLabel.Text = string.Format(Resources.Line_Column,
                        Editor.GetLineFromCharIndex(Editor.SelectionStart),
                        Editor.SelectionStart - Editor.GetFirstCharIndexOfCurrentLine());
                    IntelliSensePopUp.Location = _intelliSenseManager.GetIntelliSensePosition();
                }));
            });

        /// <summary>
        ///     Gets fired when the TIDE is closing and eventually prompts the user for saving
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void TIDE_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Unsaved)
                return;
            var res = MessageBox.Show(Resources.Do_you_want_to_save_your_changes, Resources.Warning,
                MessageBoxButtons.YesNoCancel);
            switch (res)
            {
                case DialogResult.Yes:
                    SaveButton.PerformClick();
                    break;
                case DialogResult.Cancel:
                    e.Cancel = true;
                    return;
            }
        }

        /// <summary>
        ///     Gets fired before the user has pressed a key. Is there to prevent tab from doing something else
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Provides information about the pressed key</param>
        private void Editor_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            _newKey = true;
        }

        /// <summary>
        ///     Gets fired when the user has pressed a key.
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Provides information about the pressed key</param>
        private void TIDE_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
                switch (e.KeyCode)
                {
                    case Keys.S:
                        if (e.Shift)
                            SaveAsButton.PerformClick();
                        else
                            SaveButton.PerformClick();
                        break;
                    case Keys.O:
                        OpenButton.PerformClick();
                        break;
                    case Keys.N:
                        NewButton.PerformClick();
                        break;
                    case Keys.Space:
                        _intelliSenseManager.ShowIntelliSense();
                        break;
                    case Keys.F5:
                        RunButton.PerformClick();
                        break;
                    case Keys.F:
                        FormatButton.PerformClick();
                        break;
                    default:
                        return;
                }
            else
                switch (e.KeyCode)
                {
                    case Keys.F5:
                        ParseToAssemblerButton.PerformClick();
                        break;
                    case Keys.Escape:
                        _intelliSenseManager.HideIntelliSense();
                        IntelliSenseCancelled = true;
                        break;
                    case Keys.Tab:
                        if (!IntelliSensePopUp.Visible)
                        {
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                            InsertMultiplecharacters(new string(' ', 4));
                            break;
                        }
                        IntelliSensePopUp.EnterItem();
                        break;
                    case Keys.Enter:
                        if (!IntelliSensePopUp.Visible)
                        {
                            var lineIndex = Editor.GetLineFromCharIndex(Editor.SelectionStart);
                            var line = Editor.Lines.Length > lineIndex ? Editor.Lines[lineIndex] : null;
                            if (line == null)
                                return;
                            InsertMultiplecharacters("\n" + new string(' ', line.TakeWhile(c => c == ' ').Count()));
                            break;
                        }
                        IntelliSensePopUp.EnterItem();
                        break;
                    case Keys.Down:
                        if (!IntelliSensePopUp.Visible || e.Shift)
                            return;
                        IntelliSensePopUp.ScrollDown();
                        break;
                    case Keys.Up:
                        if (!IntelliSensePopUp.Visible || e.Shift)
                            return;
                        IntelliSensePopUp.ScrollUp();
                        break;
                    case Keys.Space:
                        return;
                    default:
                        return;
                }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        /// <summary>
        ///     Makes sure that the font can't get changed.
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void Editor_FontChanged(object sender = null, EventArgs e = null)
        {
            if (!_isInMultipleCharacterMode)
                Editor.BeginUpdate();
            var oldSelection = Editor.SelectionStart;
            Editor.SelectAll();
            Editor.SelectionFont = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Editor.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Editor.Select(oldSelection, 0);
            if (!_isInMultipleCharacterMode)
                Editor.EndUpdate();
        }

        /// <summary>
        ///     Same as TIDE_KeyDown
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Information about the key</param>
        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            //TIDE_KeyDown(sender, e);
        }

        /// <summary>
        ///     Same as TIDE_KeyDown
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Information about the key</param>
        private void ToolBar_KeyDown(object sender, KeyEventArgs e)
        {
            //TIDE_KeyDown(sender, e);
        }

        /// <summary>
        ///     Same as TIDE_KeyDown
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Information about the key</param>
        private void TabControl_KeyDown(object sender, KeyEventArgs e) => TIDE_KeyDown(sender, e);

        /// <summary>
        ///     Gets fired when the window has resized, because the IntelliSense window has to be moved.
        /// </summary>
        /// <param name="sender">Useless</param>
        /// <param name="e">Useless</param>
        private void TIDE_ResizeEnd(object sender, EventArgs e)
            => IntelliSensePopUp.Location = _intelliSenseManager.GetIntelliSensePosition();

        /// <summary>
        ///     Formats the whole text
        /// </summary>
        /// <param name="sender">The button that was clicked</param>
        /// <param name="e">Useless</param>
        private void FormatButton_Click(object sender, EventArgs e)
        {
            if (Editor.SelectionLength > 0)
                Editor.Format(Editor.GetSelectedLines());
            else
                Editor.Format();
        }

        #endregion
    }
}