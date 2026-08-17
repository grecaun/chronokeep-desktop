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

using Chronokeep.Objects;
using System;

namespace Chronokeep.Interfaces.UI
{
    public interface ITimingPage
    {
        public string GetSearchValue();
        public string GetLocation();
        public string GetReader();
        public SortType GetSortType();
        public PeopleType GetPeopleType();
        public void LoadMainDisplay();
        public void NotifyTimingWorker();
        public void UpdateView();
        public void SetAllTimingSystemsToTime(DateTime date, bool now);
        public void OpenRewindWindow(TimingSystem reader);
        public void CloseRewindWindow();
        public void OpenTimeWindow(TimingSystem reader);
        public void CloseTimeWindow();
        public bool ConnectSystem(TimingSystem reader);
        public bool DisconnectSystem(TimingSystem reader);
        public void RemoveSystem(TimingSystem reader);
        public void SetReaders(string[] readers, bool visible);
    }
}

