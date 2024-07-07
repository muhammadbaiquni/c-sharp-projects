using OfficeOpenXml;

namespace ExcelMerger;

public partial class ExcelMerger : Form
{
    private List<string> _filesToMerge;

    public ExcelMerger()
    {
        InitializeComponent();

        _filesToMerge = new List<string>();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private void btnSelect_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "Excel Files|*.xlsx";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _filesToMerge.AddRange(openFileDialog.FileNames);
                listFiles.Items.AddRange(openFileDialog.FileNames);
            }
        }
    }

    private async void Merge_Click(object sender, EventArgs e)
    {
        if (!_filesToMerge.Any())
        {
            MessageBox.Show("Please select files to merge.", "No Files Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                Merge.Enabled = false;

                saveFileDialog.Filter = "Excel Files|*.xlsx";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Merge.Text = "Processing...";

                    await MergeExcelFiles(_filesToMerge.ToArray(), saveFileDialog.FileName);
                    MessageBox.Show("Files merged successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred during merge: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Merge.Enabled = true;
            Merge.Text = "Merge";
        }
    }

    private async Task MergeExcelFiles(string[] inputFiles, string outputFile)
    {
        using (var package = new ExcelPackage())
        {
            var outputWorksheet = package.Workbook.Worksheets.Add("MergeSheet");
            int rowOffset = 1;

            foreach (var file in inputFiles)
            {
                using (var inputPackage = new ExcelPackage(new FileInfo(file)))
                {
                    foreach (var worksheet in inputPackage.Workbook.Worksheets)
                    {
                        if (!IWorksheetNotEmpty(worksheet)) continue;

                        if (mergeIntoSingleWorksheet.Checked)
                        {
                            int rows = worksheet.Dimension.End.Row;
                            int cols = worksheet.Dimension.End.Column;

                            for (int row = 1; row <= rows; row++)
                            {
                                for (int col = 1; col <= cols; col++)
                                {
                                    outputWorksheet.Cells[rowOffset + row - 1, col].Value = worksheet.Cells[row, col].Value;
                                }
                            }

                            rowOffset += rows + 1;
                        }
                        else
                        {
                            var newWorksheet = package.Workbook.Worksheets.Add(worksheet.Name, worksheet);
                        }
                    }
                }
            }

            await Task.Run(() => package.SaveAs(new FileInfo(outputFile)));
        }
    }

    private bool IWorksheetNotEmpty(ExcelWorksheet worksheet)
    {
        if (worksheet.Dimension == null) return false;

        for (int row = worksheet.Dimension.Start.Row; row <= worksheet.Dimension.End.Row; row++)
        {
            for (int col = worksheet.Dimension.Start.Column; col <= worksheet.Dimension.End.Column; col++)
            {
                if (worksheet.Cells[row, col].Value != null) return true;
            }
        }

        return false;
    }

    private void btnReset_Click(object sender, EventArgs e)
    {
        _filesToMerge.Clear();
        listFiles.Items.Clear();
    }
}
