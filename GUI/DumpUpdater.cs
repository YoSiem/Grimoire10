using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grimoire.Configuration;
using Grimoire.Utilities;

namespace Grimoire.GUI
{
    public partial class DumpUpdater : Form
    {
        readonly ConfigManager configMan = GUI.Main.Instance.ConfigMgr;
        readonly List<DumpFileEntry> indexedEntries = new List<DumpFileEntry>();

        string dumpDir;

        sealed class DumpFileEntry
        {
            public string Name { get; init; }
            public string SourceDirectory { get; init; }
            public string DestinationExtension { get; init; }
            public bool ExistsInDump { get; init; }
        }

        public DumpUpdater()
        {
            InitializeComponent();

            grid.Columns[0].Width = 275;
            grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dumpDir = configMan["DumpDirectory", "Grim"];
        }

        private void DumpUpdater_Load(object sender, EventArgs e)
        {
            dumpDirTxtBox.Text = dumpDir;
        }

        private void DumpUpdater_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private async void DumpUpdater_DragDrop(object sender, DragEventArgs e)
        {
            string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            string[] files = ExpandDroppedFiles(dropped);

            statusLb.Text = "Indexing...";
            copyBtn.Enabled = false;
            prgBar.Maximum = 100;
            prgBar.Value = 0;

            List<DumpFileEntry> indexed = await Task.Run(() => IndexFiles(files));

            indexedEntries.Clear();
            indexedEntries.AddRange(indexed);
            BindGrid(indexedEntries);

            statusLb.Text = $"{indexedEntries.Count} files indexed";
            copyBtn.Enabled = indexedEntries.Count > 0;
        }

        static string[] ExpandDroppedFiles(string[] dropped)
        {
            if (dropped is null || dropped.Length == 0)
                return Array.Empty<string>();

            if (dropped.Length == 1 && Directory.Exists(dropped[0]))
                return Directory.GetFiles(dropped[0], "*", SearchOption.TopDirectoryOnly);

            return dropped.Where(File.Exists).ToArray();
        }

        List<DumpFileEntry> IndexFiles(string[] files)
        {
            var result = new List<DumpFileEntry>(files.Length);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file).ToLowerInvariant();
                string source = Path.GetDirectoryName(file);
                string extensionDirectory = GetExtensionDirectory(name);
                string destination = Path.Combine(dumpDir, extensionDirectory, name);

                result.Add(new DumpFileEntry
                {
                    Name = name,
                    SourceDirectory = source,
                    DestinationExtension = extensionDirectory,
                    ExistsInDump = File.Exists(destination)
                });
            }

            return result;
        }

        static string GetExtensionDirectory(string filename)
        {
            string extension = Path.GetExtension(filename);

            if (!string.IsNullOrEmpty(extension))
                return extension.TrimStart('.').ToLowerInvariant();

            return filename.Length >= 3 ? filename.Substring(filename.Length - 3).ToLowerInvariant() : filename.ToLowerInvariant();
        }

        void BindGrid(List<DumpFileEntry> entries)
        {
            grid.SuspendLayout();
            try
            {
                grid.Rows.Clear();

                foreach (DumpFileEntry entry in entries)
                {
                    int rowIndex = grid.Rows.Add(entry.Name, entry.SourceDirectory, entry.DestinationExtension, entry.ExistsInDump ? "Yes" : "No");
                    DataGridViewRow row = grid.Rows[rowIndex];
                    row.DefaultCellStyle.BackColor = entry.ExistsInDump ? Color.FromArgb(255, 124, 124) : Color.PaleGreen;
                }
            }
            finally
            {
                grid.ResumeLayout();
            }
        }

        private async void copyBtn_Click(object sender, EventArgs e)
        {
            List<DumpFileEntry> entriesToCopy = grid.Rows
                .Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .Select(r => new DumpFileEntry
                {
                    Name = r.Cells[0].Value?.ToString() ?? string.Empty,
                    SourceDirectory = r.Cells[1].Value?.ToString() ?? string.Empty,
                    DestinationExtension = r.Cells[2].Value?.ToString() ?? string.Empty,
                    ExistsInDump = string.Equals(r.Cells[3].Value?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase)
                })
                .Where(e => !string.IsNullOrEmpty(e.Name) && !string.IsNullOrEmpty(e.SourceDirectory) && !string.IsNullOrEmpty(e.DestinationExtension))
                .ToList();

            if (entriesToCopy.Count == 0)
                return;

            if (MessageBox.Show("You are about to copy all indexed files into the selected dump directory with overwrite enabled.\n\nDo you want to continue?", "Input Required", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            copyBtn.Enabled = false;
            prgBar.Maximum = entriesToCopy.Count;
            prgBar.Value = 0;
            statusLb.Text = "Copying files...";

            var exportOptions = DataPerformanceConfig.GetExportOptions(configMan);
            int maxWorkers = Math.Max(1, exportOptions.MaxFileWorkers);
            int completed = 0;
            int failed = 0;
            var failures = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(entriesToCopy, new ParallelOptions
            {
                MaxDegreeOfParallelism = maxWorkers
            }, async (entry, ct) =>
            {
                string source = Path.Combine(entry.SourceDirectory, entry.Name);
                string destinationDirectory = Path.Combine(dumpDir, entry.DestinationExtension);
                string destination = Path.Combine(destinationDirectory, entry.Name);

                try
                {
                    Directory.CreateDirectory(destinationDirectory);
                    await Task.Run(() => File.Copy(source, destination, overwrite: true), ct);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    failures.Add($"{entry.Name}: {ex.Message}");
                }

                int done = Interlocked.Increment(ref completed);
                if (done == entriesToCopy.Count || done % 8 == 0)
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            prgBar.Value = Math.Min(done, prgBar.Maximum);
                            statusLb.Text = $"Copying files... {done}/{entriesToCopy.Count}";
                        }));
                    }
                }
            });

            indexedEntries.Clear();
            grid.Rows.Clear();

            prgBar.Maximum = 100;
            prgBar.Value = 0;
            statusLb.Text = string.Empty;

            if (failed > 0)
            {
                MessageBox.Show($"Copied {completed - failed}/{completed} files.\n\n{failed} files failed and were skipped.", "Copy Completed with Warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show($"Copied {completed} files successfully.", "Copy Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            copyBtn.Enabled = false;
        }

        private void grid_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            if (grid.Rows.Count == 0)
                copyBtn.Enabled = false;
        }
    }
}
