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
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using System;
using System.Linq;

namespace Chronokeep.UI
{
    internal static class TypingFilters
    {
        /// <summary>
        /// Runs a filter on typed text before the text box adds it, so a filter that sets Handled
        /// actually keeps the text out. A TextInput handler set in XAML runs after the box has
        /// already added the text, so it can't block anything.
        /// </summary>
        internal static void FilterTyping(this TextBox box, EventHandler<TextInputEventArgs> filter)
        {
            box.AddHandler(InputElement.TextInputEvent, filter, RoutingStrategies.Tunnel);
            // Pasting doesn't go through text input, so put pasted text through the same filter.
            box.AddHandler(TextBox.PastingFromClipboardEvent, async (_, e) =>
            {
                e.Handled = true;
                if (TopLevel.GetTopLevel(box)?.Clipboard is not { } clipboard) return;
                string? text = await clipboard.TryGetTextAsync();
                if (string.IsNullOrEmpty(text)) return;
                string kept = string.Concat(text.Where(c =>
                {
                    TextInputEventArgs args = new() { Text = c.ToString() };
                    filter(box, args);
                    return !args.Handled;
                }));
                box.SelectedText = kept;
            });
        }

        /// <summary>Lets only the digits 0 to 9 be typed.</summary>
        internal static void DigitsOnly(object? sender, TextInputEventArgs e)
            => e.Handled = !(e.Text ?? "").All(char.IsAsciiDigit);
    }
}
