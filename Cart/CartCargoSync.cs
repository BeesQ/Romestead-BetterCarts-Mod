using System;
using System.Collections.Generic;
using System.Text;

namespace BetterCarts;

// the ONE entity parameter Better Carts writes. Vanilla c1..c5 and Iron Cart c6..c8 are one key per slot; a single packed key keeps the save footprint to one entry per Cart and cannot collide with a future mod's cN
internal static class CartCargoSync {
    internal const string CargoKey = "bc_cargo";
    internal const int MaxExtras = 128;

    private const char Separator = ',';

    internal static string Pack(List<Guid> extras) {
        if (extras == null || extras.Count == 0) {
            return string.Empty;
        }
        StringBuilder builder = new StringBuilder();
        foreach (Guid id in extras) {
            if (builder.Length > 0) {
                builder.Append(Separator);
            }
            builder.Append(id.ToString());
        }
        return builder.ToString();
    }

    // the caller owns seen: on a host the server and client halves run on different threads, so one shared set here would race
    internal static void Unpack(string raw, List<Guid> into, HashSet<Guid> seen) {
        into.Clear();
        seen.Clear();
        if (string.IsNullOrEmpty(raw)) {
            return;
        }
        ReadOnlySpan<char> remaining = raw.AsSpan();
        while (!remaining.IsEmpty) {
            int separatorIndex = remaining.IndexOf(Separator);
            ReadOnlySpan<char> part = separatorIndex >= 0 ? remaining.Slice(0, separatorIndex) : remaining;
            if (Guid.TryParse(part, out Guid id) && seen.Add(id)) {
                into.Add(id);
            }
            if (separatorIndex < 0) {
                break;
            }
            remaining = remaining.Slice(separatorIndex + 1);
        }
    }
}
