using SnailCommander.Core.Interaction;

namespace SnailCommander.Tests;

internal static class GameViewportTests
{
    public static void Run(Action<bool, string> assert)
    {
        assert(GameViewport.Map(1000, 1000, 540, 960, 500, 500) == (270, 480), "portrait center maps correctly");
        assert(GameViewport.Map(1000, 1000, 540, 960, 0, 500) is null, "left letterbox ignored");
        assert(GameViewport.Map(1000, 1000, 540, 960, 999, 500) is null, "right letterbox ignored");
        assert(GameViewport.Map(1000, 1000, 960, 540, 500, 0) is null, "top letterbox ignored");
        assert(GameViewport.Map(1000, 1000, 960, 540, 500, 999) is null, "bottom letterbox ignored");
        assert(GameViewport.Map(540, 960, 540, 960, 0, 0) == (0, 0), "native origin maps correctly");
        assert(GameViewport.Map(540, 960, 540, 960, 539, 959) == (539, 959), "last native pixel maps correctly");
        assert(GameViewport.Map(540, 960, 540, 960, 540, 960) is null, "exclusive boundary ignored");
        assert(GameViewport.Map(270, 480, 540, 960, 135, 240) == (270, 480), "half size maps correctly");
        assert(GameViewport.Map(1080, 1920, 540, 960, 540, 960) == (270, 480), "double size maps correctly");
        assert(GameViewport.Map(0, 1000, 540, 960, 0, 0) is null, "zero viewport rejected");
        assert(GameViewport.Map(1000, 1000, 0, 960, 0, 0) is null, "zero image rejected");
        assert(GameViewport.Map(1000, 1000, 540, 960, double.NaN, 0) is null, "NaN rejected");
        assert(GameViewport.Map(1000, 1000, 540, 960, 500, double.PositiveInfinity) is null, "infinity rejected");
        assert(GameViewport.Map(540, 960, 540, 960, -1, 500) is null, "negative pointer rejected");
        assert(GameViewport.Map(1000, 1000, 540, 960, 218, 500) is null, "fractional-fit border excluded");
        assert(GameViewport.Map(1000, 1000, 540, 960, 219, 500) == (0, 480), "fractional-fit integer origin matches PictureBox");
        assert(GameViewport.Map(1, 1, 540, 960, 0, 0) is null, "zero-sized fitted image rejected");
    }
}
