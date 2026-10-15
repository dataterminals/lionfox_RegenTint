namespace lionfox_RegenTint
{
    // Saved as ModConfig/lionfoxregentint.json the first time the mod runs. Edit it, then type
    // .regentint in chat to reload it and see the result on the health bar.
    public class RegenTintConfig
    {
        // Fill colour of the "health still to come" segment, as #RRGGBB. Its outline is this
        // colour at 40%, the same rule vanilla uses for the main bar's outline.
        public string Color { get; set; } = "#4DFF73";

        // 0 to 1. Vanilla draws the segment at 0.35, which is most of why it is hard to see.
        public double Opacity { get; set; } = 0.9;
    }
}
