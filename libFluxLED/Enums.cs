﻿
namespace FluxLED
{
    public enum BuiltInTimer : byte
    {
        Sunrise = 0xA1,
        Sunset = 0xA2
    }

    public enum PresetPattern : byte
    {
        seven_color_cross_fade = 0x25,
        red_gradual_change = 0x26,
        green_gradual_change = 0x27,
        blue_gradual_change = 0x28,
        yellow_gradual_change = 0x29,
        cyan_gradual_change = 0x2a,
        purple_gradual_change = 0x2b,
        white_gradual_change = 0x2c,
        red_green_cross_fade = 0x2d,
        red_blue_cross_fade = 0x2e,
        green_blue_cross_fade = 0x2f,
        seven_color_strobe_flash = 0x30,
        red_strobe_flash = 0x31,
        green_strobe_flash = 0x32,
        blue_strobe_flash = 0x33,
        yellow_strobe_flash = 0x34,
        cyan_strobe_flash = 0x35,
        purple_strobe_flash = 0x36,
        white_strobe_flash = 0x37,
        seven_color_jumping = 0x38
    }
}
