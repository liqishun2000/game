using MauiApp.Game.Model;
using MauiApp.Game.World.State;
using SkiaSharp;

namespace MauiApp.Rendering;

internal static class FactionColors
{
    public static SKColor For(GameState state, TileState tile)
    {
        if (tile.IsRebelFixed) return new SKColor(0x8a, 0x8a, 0x8a);
        if (string.IsNullOrEmpty(tile.OwnerFactionId)) return new SKColor(0xb0, 0xb0, 0xb0);

        return state.Factions.TryGetValue(tile.OwnerFactionId, out var f)
            ? f.Kind switch
            {
                FactionKind.Player => new SKColor(0x2f, 0x6f, 0xed),
                FactionKind.Ai => new SKColor(0xd0, 0x3a, 0x3a),
                FactionKind.Rebel => new SKColor(0x8a, 0x8a, 0x8a),
                _ => new SKColor(0x6a, 0x9a, 0x4a),
            }
            : new SKColor(0xb0, 0xb0, 0xb0);
    }
}
