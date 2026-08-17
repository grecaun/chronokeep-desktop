/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Avalonia.Controls;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using Chronokeep.IO;
using Chronokeep.UI.Parts;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Import;

public partial class ImportFilePage1 : UserControl
{
    private readonly IDataImporter importer;

    public ImportFilePage1(IDataImporter importer)
    {
        InitializeComponent();
        this.importer = importer;
        for (int i = 1; i < importer.Data!.GetNumHeaders(); i++)
        {
            HeaderListBox.Items.Add(new HeaderPart(importer.Data.Headers[i], i));
        }
    }

    internal List<string> RequiredNotFound()
    {
        Log.D("UI.ImportFilePage1", "Checking for required fields.");
        List<string> output = [];
        bool first = false, last = false;
        foreach (HeaderPart item in HeaderListBox.Items.Cast<HeaderPart>())
        {
            int val = item.HeaderBox.SelectedIndex;
            switch (val)
            {
                case ImportFileWindow.FIRST:
                    first = true;
                    break;
                case ImportFileWindow.LAST:
                    last = true;
                    break;
            }
        }
        if (!first && !last)
        {
            output = ["First and/or Last Name"];
        }
        return output;
    }

    internal List<string> RepeatHeaders()
    {
        Log.D("UI.ImportFilePage1", "Checking for repeat headers in user selection.");
        int[] check = new int[ImportFileWindow.HUMAN_FIELDS.Length];
        bool repeat = false;
        List<string> output = [];
        foreach (HeaderPart item in HeaderListBox.Items.Cast<HeaderPart>())
        {
            int val = item.HeaderBox.SelectedIndex;
            if (val <= 0) continue;
            if (check[val] > 0)
            {
                output.Add(item.HeaderBox.SelectedItem!.ToString()!);
                repeat = true;
            }
            else
            {
                check[val] = 1;
            }
        }
        return repeat ? output : [];
    }

    internal HeaderPart[] GetListBoxItems()
    {
        HeaderPart[] output = new HeaderPart[HeaderListBox.Items.Count];
        for (int i = 0; i < HeaderListBox.Items.Count; i++)
        {
            output[i] = (HeaderPart)HeaderListBox.Items[i]!;
        }
        return output;
    }

    internal void UpdateSheetNo(int selection)
    {
        Log.D("ImportFilePage1", $"Changing sheet to {selection}");
        ExcelImporter excelImporter = (ExcelImporter)importer;
        excelImporter.ChangeSheet(selection);
        excelImporter.FetchHeaders();
        HeaderListBox.Items.Clear();
        for (int i = 1; i < importer.Data!.GetNumHeaders(); i++)
        {
            HeaderListBox.Items.Add(new HeaderPart(importer.Data.Headers[i], i));
        }
    }
}
