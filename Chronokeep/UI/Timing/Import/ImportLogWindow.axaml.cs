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
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chronokeep.UI.Timing.Import;

public partial class ImportLogWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly LogImporter importer;

    private readonly Event theEvent;
    private int locationId = Constants.Timing.LOCATION_DUMMY;

    [GeneratedRegex("\\d{4}-\\d{2}-\\d{2}")]
    private static partial Regex DateRegex();

    private ImportLogWindow(IMainWindow window, LogImporter importer, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.importer = importer;
        this.database = database;
        theEvent = database.GetCurrentEvent()!;
        List<TimingLocation> locations = database.GetTimingLocations(theEvent.Identifier);
        if (!theEvent.CommonStartFinish)
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }
        Frame.Content = new ImportLogPage1(this, importer, locations);
    }

    public void Update()
    {
        if (Frame.Content!.GetType() != typeof(ImportLogPage1)) return;
        Log.D("UI.Timing.Import.ImportLogWindow", "Updating locations on page.");
        List<TimingLocation> locations = database.GetTimingLocations(theEvent.Identifier);
        if (!theEvent.CommonStartFinish)
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }
        ((ImportLogPage1)Frame.Content).UpdateLocations(locations);
    }

    public static ImportLogWindow NewWindow(IMainWindow window, LogImporter importer, IdbInterface database)
    {
        return new ImportLogWindow(window, importer, database);
    }

    public void Cancel()
    {
        this.Close();
    }

    public void Next(int iLocationId)
    {
        locationId = iLocationId;
        importer.Kind = LogImporter.Type.CUSTOM;
        Frame.Content = new ImportLogPage2(this, importer);
    }

    public async void Import(LogImporter.Type type, int iLocationId, int chipColumn, int timeColumn)
    {
        try
        {
            Log.D("UI.Timing.Import.ImportLogWindow", $"Type is {type} ChipIx {chipColumn} TimeIx {timeColumn}");
            await Task.Run(() =>
            {
                importer.FetchData();
                ImportData data = importer.Data!;
                int chip = chipColumn, time = timeColumn;
                locationId = iLocationId != Constants.Timing.LOCATION_DUMMY ? iLocationId : locationId;
                List<ChipRead> chipReads = [];
                switch (type)
                {
                    case LogImporter.Type.IPICO:
                    {
                        DateTime date = DateTime.ParseExact(data.Headers[1].Substring(20, 12), "yyMMddHHmmss", CultureInfo.InvariantCulture);
                        int.TryParse(data.Headers[1].AsSpan(32, 2), NumberStyles.HexNumber, null, out int milliseconds);
                        milliseconds *= 10;
                        date = date.AddMilliseconds(milliseconds);
                        chipReads.Add(new ChipRead(
                            theEvent.Identifier,
                            locationId,
                            data.Headers[1].Substring(4, 12),
                            date,
                            Convert.ToInt32(data.Headers[1].Substring(2, 2)),
                            data.Headers[1].Length == 36 ? 0 : 1
                        ));
                        int numEntries = data.Data.Count;
                        for (int counter = 0; counter < numEntries; counter++)
                        {
                            date = DateTime.ParseExact(data.Data[counter][1].Substring(20, 12), "yyMMddHHmmss", CultureInfo.InvariantCulture);
                            _ = int.TryParse(data.Data[counter][1].AsSpan(32, 2), NumberStyles.HexNumber, null, out milliseconds);
                            milliseconds *= 10;
                            date = date.AddMilliseconds(milliseconds);
                            chipReads.Add(new ChipRead(
                                theEvent.Identifier,
                                locationId,
                                data.Data[counter][1].Substring(4, 12),
                                date,
                                Convert.ToInt32(data.Data[counter][1].Substring(2, 2)),
                                data.Data[counter][1].Length == 36 ? 0 : 1
                            ));
                        }

                        break;
                    }
                    case LogImporter.Type.CHRONOKEEP:
                        chipReads.AddRange(from object[] line in data.Data
                            select new ChipRead(theEvent.Identifier, // event id
                                locationId, // location id
                                Constants.Timing.CHIPREAD_STATUS_NONE, // status
                                line[2].ToString()!.Trim(), // chip number
                                Convert.ToInt64(line[3]), // seconds
                                Convert.ToInt32(line[4]), // milliseconds
                                Convert.ToInt64(line[5]), // time_seconds
                                Convert.ToInt32(line[6]), // time_milliseconds
                                Convert.ToInt32(line[7]), // antenna
                                line[8].ToString()!, // reader
                                line[9].ToString()!, // box
                                Convert.ToInt32(line[10]), // log_index
                                line[11].ToString()!, // rssi
                                Convert.ToInt32(line[12]), // is_rewind
                                line[13].ToString()!, // reader_time
                                Convert.ToInt64(line[14]), // start_time
                                line[15].ToString()!, // read_bib
                                Convert.ToInt32(line[16]) // placeholder
                            ));
                        break;
                    case LogImporter.Type.RFID:
                    case LogImporter.Type.CUSTOM:
                    default:
                    {
                        if (type == LogImporter.Type.RFID)
                        {
                            if (importer.Data!.Headers.Length < 4)
                            {
                                chip = 1;
                                time = 2;
                            }
                            else
                            {
                                chip = 2;
                                time = 4;
                            }
                        }
                        bool dateIncluded = DateRegex().IsMatch(data.Headers[time]);
                        DateTime date = !dateIncluded ? DateTime.Parse($"{theEvent.Date} {data.Headers[time]}") : DateTime.Parse(data.Headers[time]);
                        chipReads.Add(new ChipRead(theEvent.Identifier, locationId, data.Headers[chip], date));
                        int numEntries = data.Data.Count;
                        for (int counter = 0; counter < numEntries; counter++)
                        {
                            date = DateTime.Parse(!dateIncluded ? $"{theEvent.Date} {data.Data[counter][time]}" : data.Data[counter][time]);
                            chipReads.Add(new ChipRead(theEvent.Identifier, locationId, data.Data[counter][chip], date));
                        }

                        break;
                    }
                }
                database.AddChipReads(chipReads);
            });
            window.NotifyTimingWorker();
            window.UpdateTiming();
            Close();
        }
        catch (Exception)
        {
            Log.D("UI.Timing.Import.ImportLogWindow", "Error importing.");
        }
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        importer.Finish();
        window.WindowFinalize();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
