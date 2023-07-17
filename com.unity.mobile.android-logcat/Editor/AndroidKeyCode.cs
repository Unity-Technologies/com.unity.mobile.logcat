namespace Unity.Android.Logcat
{
    internal enum AndroidKeyCode
    {
        /** Soft Left key.
         * Usually situated below the display on phones and used as a multi-function
         * feature key for selecting a software defined function shown on the bottom left
         * of the display. */
        SOFT_LEFT = 1,
        /** Soft Right key.
         * Usually situated below the display on phones and used as a multi-function
         * feature key for selecting a software defined function shown on the bottom right
         * of the display. */
        SOFT_RIGHT = 2,
        /** Home key.
         * This key is handled by the framework and is never delivered to applications. */
        HOME = 3,
        /** Back key. */
        BACK = 4,
        /** Call key. */
        CALL = 5,
        /** End Call key. */
        ENDCALL = 6,
        /** '0' key. */
        _0 = 7,
        /** '1' key. */
        _1 = 8,
        /** '2' key. */
        _2 = 9,
        /** '3' key. */
        _3 = 10,
        /** '4' key. */
        _4 = 11,
        /** '5' key. */
        _5 = 12,
        /** '6' key. */
        _6 = 13,
        /** '7' key. */
        _7 = 14,
        /** '8' key. */
        _8 = 15,
        /** '9' key. */
        _9 = 16,
        /** '*' key. */
        STAR = 17,
        /** '#' key. */
        POUND = 18,
        /** Directional Pad Up key.
         * May also be synthesized from trackball motions. */
        DPAD_UP = 19,
        /** Directional Pad Down key.
         * May also be synthesized from trackball motions. */
        DPAD_DOWN = 20,
        /** Directional Pad Left key.
         * May also be synthesized from trackball motions. */
        DPAD_LEFT = 21,
        /** Directional Pad Right key.
         * May also be synthesized from trackball motions. */
        DPAD_RIGHT = 22,
        /** Directional Pad Center key.
         * May also be synthesized from trackball motions. */
        DPAD_CENTER = 23,
        /** Volume Up key.
         * Adjusts the speaker volume up. */
        VOLUME_UP = 24,
        /** Volume Down key.
         * Adjusts the speaker volume down. */
        VOLUME_DOWN = 25,
        /** Power key. */
        POWER = 26,
        /** Camera key.
         * Used to launch a camera application or take pictures. */
        CAMERA = 27,
        /** Clear key. */
        CLEAR = 28,
        /** 'A' key. */
        A = 29,
        /** 'B' key. */
        B = 30,
        /** 'C' key. */
        C = 31,
        /** 'D' key. */
        D = 32,
        /** 'E' key. */
        E = 33,
        /** 'F' key. */
        F = 34,
        /** 'G' key. */
        G = 35,
        /** 'H' key. */
        H = 36,
        /** 'I' key. */
        I = 37,
        /** 'J' key. */
        J = 38,
        /** 'K' key. */
        K = 39,
        /** 'L' key. */
        L = 40,
        /** 'M' key. */
        M = 41,
        /** 'N' key. */
        N = 42,
        /** 'O' key. */
        O = 43,
        /** 'P' key. */
        P = 44,
        /** 'Q' key. */
        Q = 45,
        /** 'R' key. */
        R = 46,
        /** 'S' key. */
        S = 47,
        /** 'T' key. */
        T = 48,
        /** 'U' key. */
        U = 49,
        /** 'V' key. */
        V = 50,
        /** 'W' key. */
        W = 51,
        /** 'X' key. */
        X = 52,
        /** 'Y' key. */
        Y = 53,
        /** 'Z' key. */
        Z = 54,
        /** ',' key. */
        COMMA = 55,
        /** '.' key. */
        PERIOD = 56,
        /** Left Alt modifier key. */
        ALT_LEFT = 57,
        /** Right Alt modifier key. */
        ALT_RIGHT = 58,
        /** Left Shift modifier key. */
        SHIFT_LEFT = 59,
        /** Right Shift modifier key. */
        SHIFT_RIGHT = 60,
        /** Tab key. */
        TAB = 61,
        /** Space key. */
        SPACE = 62,
        /** Symbol modifier key.
         * Used to enter alternate symbols. */
        SYM = 63,
        /** Explorer special function key.
         * Used to launch a browser application. */
        EXPLORER = 64,
        /** Envelope special function key.
         * Used to launch a mail application. */
        ENVELOPE = 65,
        /** Enter key. */
        ENTER = 66,
        /** Backspace key.
         * Deletes characters before the insertion point, unlike {@link FORWARD_DEL}. */
        DEL = 67,
        /** '`' (backtick) key. */
        GRAVE = 68,
        /** '-'. */
        MINUS = 69,
        /** '=' key. */
        EQUALS = 70,
        /** '[' key. */
        LEFT_BRACKET = 71,
        /** ']' key. */
        RIGHT_BRACKET = 72,
        /** '\' key. */
        BACKSLASH = 73,
        /** ';' key. */
        SEMICOLON = 74,
        /** ''' (apostrophe) key. */
        APOSTROPHE = 75,
        /** '/' key. */
        SLASH = 76,
        /** '@' key. */
        AT = 77,
        /** Number modifier key.
         * Used to enter numeric symbols.
         * This key is not {@link NUM_LOCK}; it is more like {@link ALT_LEFT}. */
        NUM = 78,
        /** Headset Hook key.
         * Used to hang up calls and stop media. */
        HEADSETHOOK = 79,
        /** Camera Focus key.
         * Used to focus the camera. */
        FOCUS = 80,
        /** '+' key. */
        PLUS = 81,
        /** Menu key. */
        MENU = 82,
        /** Notification key. */
        NOTIFICATION = 83,
        /** Search key. */
        SEARCH = 84,
        /** Play/Pause media key. */
        MEDIA_PLAY_PAUSE = 85,
        /** Stop media key. */
        MEDIA_STOP = 86,
        /** Play Next media key. */
        MEDIA_NEXT = 87,
        /** Play Previous media key. */
        MEDIA_PREVIOUS = 88,
        /** Rewind media key. */
        MEDIA_REWIND = 89,
        /** Fast Forward media key. */
        MEDIA_FAST_FORWARD = 90,
        /** Mute key.
         * Mutes the microphone, unlike {@link VOLUME_MUTE}. */
        MUTE = 91,
        /** Page Up key. */
        PAGE_UP = 92,
        /** Page Down key. */
        PAGE_DOWN = 93,
        /** Picture Symbols modifier key.
         * Used to switch symbol sets (Emoji, Kao-moji). */
        PICTSYMBOLS = 94,
        /** Switch Charset modifier key.
         * Used to switch character sets (Kanji, Katakana). */
        SWITCH_CHARSET = 95,
        /** A Button key.
         * On a game controller, the A button should be either the button labeled A
         * or the first button on the bottom row of controller buttons. */
        BUTTON_A = 96,
        /** B Button key.
         * On a game controller, the B button should be either the button labeled B
         * or the second button on the bottom row of controller buttons. */
        BUTTON_B = 97,
        /** C Button key.
         * On a game controller, the C button should be either the button labeled C
         * or the third button on the bottom row of controller buttons. */
        BUTTON_C = 98,
        /** X Button key.
         * On a game controller, the X button should be either the button labeled X
         * or the first button on the upper row of controller buttons. */
        BUTTON_X = 99,
        /** Y Button key.
         * On a game controller, the Y button should be either the button labeled Y
         * or the second button on the upper row of controller buttons. */
        BUTTON_Y = 100,
        /** Z Button key.
         * On a game controller, the Z button should be either the button labeled Z
         * or the third button on the upper row of controller buttons. */
        BUTTON_Z = 101,
        /** L1 Button key.
         * On a game controller, the L1 button should be either the button labeled L1 (or L)
         * or the top left trigger button. */
        BUTTON_L1 = 102,
        /** R1 Button key.
         * On a game controller, the R1 button should be either the button labeled R1 (or R)
         * or the top right trigger button. */
        BUTTON_R1 = 103,
        /** L2 Button key.
         * On a game controller, the L2 button should be either the button labeled L2
         * or the bottom left trigger button. */
        BUTTON_L2 = 104,
        /** R2 Button key.
         * On a game controller, the R2 button should be either the button labeled R2
         * or the bottom right trigger button. */
        BUTTON_R2 = 105,
        /** Left Thumb Button key.
         * On a game controller, the left thumb button indicates that the left (or only)
         * joystick is pressed. */
        BUTTON_THUMBL = 106,
        /** Right Thumb Button key.
         * On a game controller, the right thumb button indicates that the right
         * joystick is pressed. */
        BUTTON_THUMBR = 107,
        /** Start Button key.
         * On a game controller, the button labeled Start. */
        BUTTON_START = 108,
        /** Select Button key.
         * On a game controller, the button labeled Select. */
        BUTTON_SELECT = 109,
        /** Mode Button key.
         * On a game controller, the button labeled Mode. */
        BUTTON_MODE = 110,
        /** Escape key. */
        ESCAPE = 111,
        /** Forward Delete key.
         * Deletes characters ahead of the insertion point, unlike {@link DEL}. */
        FORWARD_DEL = 112,
        /** Left Control modifier key. */
        CTRL_LEFT = 113,
        /** Right Control modifier key. */
        CTRL_RIGHT = 114,
        /** Caps Lock key. */
        CAPS_LOCK = 115,
        /** Scroll Lock key. */
        SCROLL_LOCK = 116,
        /** Left Meta modifier key. */
        META_LEFT = 117,
        /** Right Meta modifier key. */
        META_RIGHT = 118,
        /** Function modifier key. */
        FUNCTION = 119,
        /** System Request / Print Screen key. */
        SYSRQ = 120,
        /** Break / Pause key. */
        BREAK = 121,
        /** Home Movement key.
         * Used for scrolling or moving the cursor around to the start of a line
         * or to the top of a list. */
        MOVE_HOME = 122,
        /** End Movement key.
         * Used for scrolling or moving the cursor around to the end of a line
         * or to the bottom of a list. */
        MOVE_END = 123,
        /** Insert key.
         * Toggles insert / overwrite edit mode. */
        INSERT = 124,
        /** Forward key.
         * Navigates forward in the history stack.  Complement of {@link BACK}. */
        FORWARD = 125,
        /** Play media key. */
        MEDIA_PLAY = 126,
        /** Pause media key. */
        MEDIA_PAUSE = 127,
        /** Close media key.
         * May be used to close a CD tray, for example. */
        MEDIA_CLOSE = 128,
        /** Eject media key.
         * May be used to eject a CD tray, for example. */
        MEDIA_EJECT = 129,
        /** Record media key. */
        MEDIA_RECORD = 130,
        /** F1 key. */
        F1 = 131,
        /** F2 key. */
        F2 = 132,
        /** F3 key. */
        F3 = 133,
        /** F4 key. */
        F4 = 134,
        /** F5 key. */
        F5 = 135,
        /** F6 key. */
        F6 = 136,
        /** F7 key. */
        F7 = 137,
        /** F8 key. */
        F8 = 138,
        /** F9 key. */
        F9 = 139,
        /** F10 key. */
        F10 = 140,
        /** F11 key. */
        F11 = 141,
        /** F12 key. */
        F12 = 142,
        /** Num Lock key.
         * This is the Num Lock key; it is different from {@link NUM}.
         * This key alters the behavior of other keys on the numeric keypad. */
        NUM_LOCK = 143,
        /** Numeric keypad '0' key. */
        NUMPAD_0 = 144,
        /** Numeric keypad '1' key. */
        NUMPAD_1 = 145,
        /** Numeric keypad '2' key. */
        NUMPAD_2 = 146,
        /** Numeric keypad '3' key. */
        NUMPAD_3 = 147,
        /** Numeric keypad '4' key. */
        NUMPAD_4 = 148,
        /** Numeric keypad '5' key. */
        NUMPAD_5 = 149,
        /** Numeric keypad '6' key. */
        NUMPAD_6 = 150,
        /** Numeric keypad '7' key. */
        NUMPAD_7 = 151,
        /** Numeric keypad '8' key. */
        NUMPAD_8 = 152,
        /** Numeric keypad '9' key. */
        NUMPAD_9 = 153,
        /** Numeric keypad '/' key (for division). */
        NUMPAD_DIVIDE = 154,
        /** Numeric keypad '*' key (for multiplication). */
        NUMPAD_MULTIPLY = 155,
        /** Numeric keypad '-' key (for subtraction). */
        NUMPAD_SUBTRACT = 156,
        /** Numeric keypad '+' key (for addition). */
        NUMPAD_ADD = 157,
        /** Numeric keypad '.' key (for decimals or digit grouping). */
        NUMPAD_DOT = 158,
        /** Numeric keypad ',' key (for decimals or digit grouping). */
        NUMPAD_COMMA = 159,
        /** Numeric keypad Enter key. */
        NUMPAD_ENTER = 160,
        /** Numeric keypad '=' key. */
        NUMPAD_EQUALS = 161,
        /** Numeric keypad '(' key. */
        NUMPAD_LEFT_PAREN = 162,
        /** Numeric keypad ')' key. */
        NUMPAD_RIGHT_PAREN = 163,
        /** Volume Mute key.
         * Mutes the speaker, unlike {@link MUTE}.
         * This key should normally be implemented as a toggle such that the first press
         * mutes the speaker and the second press restores the original volume. */
        VOLUME_MUTE = 164,
        /** Info key.
         * Common on TV remotes to show additional information related to what is
         * currently being viewed. */
        INFO = 165,
        /** Channel up key.
         * On TV remotes, increments the television channel. */
        CHANNEL_UP = 166,
        /** Channel down key.
         * On TV remotes, decrements the television channel. */
        CHANNEL_DOWN = 167,
        /** Zoom in key. */
        ZOOM_IN = 168,
        /** Zoom out key. */
        ZOOM_OUT = 169,
        /** TV key.
         * On TV remotes, switches to viewing live TV. */
        TV = 170,
        /** Window key.
         * On TV remotes, toggles picture-in-picture mode or other windowing functions. */
        WINDOW = 171,
        /** Guide key.
         * On TV remotes, shows a programming guide. */
        GUIDE = 172,
        /** DVR key.
         * On some TV remotes, switches to a DVR mode for recorded shows. */
        DVR = 173,
        /** Bookmark key.
         * On some TV remotes, bookmarks content or web pages. */
        BOOKMARK = 174,
        /** Toggle captions key.
         * Switches the mode for closed-captioning text, for example during television shows. */
        CAPTIONS = 175,
        /** Settings key.
         * Starts the system settings activity. */
        SETTINGS = 176,
        /** TV power key.
         * On TV remotes, toggles the power on a television screen. */
        TV_POWER = 177,
        /** TV input key.
         * On TV remotes, switches the input on a television screen. */
        TV_INPUT = 178,
        /** Set-top-box power key.
         * On TV remotes, toggles the power on an external Set-top-box. */
        STB_POWER = 179,
        /** Set-top-box input key.
         * On TV remotes, switches the input mode on an external Set-top-box. */
        STB_INPUT = 180,
        /** A/V Receiver power key.
         * On TV remotes, toggles the power on an external A/V Receiver. */
        AVR_POWER = 181,
        /** A/V Receiver input key.
         * On TV remotes, switches the input mode on an external A/V Receiver. */
        AVR_INPUT = 182,
        /** Red "programmable" key.
         * On TV remotes, acts as a contextual/programmable key. */
        PROG_RED = 183,
        /** Green "programmable" key.
         * On TV remotes, actsas a contextual/programmable key. */
        PROG_GREEN = 184,
        /** Yellow "programmable" key.
         * On TV remotes, acts as a contextual/programmable key. */
        PROG_YELLOW = 185,
        /** Blue "programmable" key.
         * On TV remotes, acts as a contextual/programmable key. */
        PROG_BLUE = 186,
        /** App switch key.
         * Should bring up the application switcher dialog. */
        APP_SWITCH = 187,
        /** Generic Game Pad Button #1.*/
        BUTTON_1 = 188,
        /** Generic Game Pad Button #2.*/
        BUTTON_2 = 189,
        /** Generic Game Pad Button #3.*/
        BUTTON_3 = 190,
        /** Generic Game Pad Button #4.*/
        BUTTON_4 = 191,
        /** Generic Game Pad Button #5.*/
        BUTTON_5 = 192,
        /** Generic Game Pad Button #6.*/
        BUTTON_6 = 193,
        /** Generic Game Pad Button #7.*/
        BUTTON_7 = 194,
        /** Generic Game Pad Button #8.*/
        BUTTON_8 = 195,
        /** Generic Game Pad Button #9.*/
        BUTTON_9 = 196,
        /** Generic Game Pad Button #10.*/
        BUTTON_10 = 197,
        /** Generic Game Pad Button #11.*/
        BUTTON_11 = 198,
        /** Generic Game Pad Button #12.*/
        BUTTON_12 = 199,
        /** Generic Game Pad Button #13.*/
        BUTTON_13 = 200,
        /** Generic Game Pad Button #14.*/
        BUTTON_14 = 201,
        /** Generic Game Pad Button #15.*/
        BUTTON_15 = 202,
        /** Generic Game Pad Button #16.*/
        BUTTON_16 = 203,
        /** Language Switch key.
         * Toggles the current input language such as switching between English and Japanese on
         * a QWERTY keyboard.  On some devices, the same function may be performed by
         * pressing Shift+Spacebar. */
        LANGUAGE_SWITCH = 204,
        /** Manner Mode key.
         * Toggles silent or vibrate mode on and off to make the device behave more politely
         * in certain settings such as on a crowded train.  On some devices, the key may only
         * operate when long-pressed. */
        MANNER_MODE = 205,
        /** 3D Mode key.
         * Toggles the display between 2D and 3D mode. */
        _3D_MODE = 206,
        /** Contacts special function key.
         * Used to launch an address book application. */
        CONTACTS = 207,
        /** Calendar special function key.
         * Used to launch a calendar application. */
        CALENDAR = 208,
        /** Music special function key.
         * Used to launch a music player application. */
        MUSIC = 209,
        /** Calculator special function key.
         * Used to launch a calculator application. */
        CALCULATOR = 210,
        /** Japanese full-width / half-width key. */
        ZENKAKU_HANKAKU = 211,
        /** Japanese alphanumeric key. */
        EISU = 212,
        /** Japanese non-conversion key. */
        MUHENKAN = 213,
        /** Japanese conversion key. */
        HENKAN = 214,
        /** Japanese katakana / hiragana key. */
        KATAKANA_HIRAGANA = 215,
        /** Japanese Yen key. */
        YEN = 216,
        /** Japanese Ro key. */
        RO = 217,
        /** Japanese kana key. */
        KANA = 218,
        /** Assist key.
         * Launches the global assist activity.  Not delivered to applications. */
        ASSIST = 219,
        /** Brightness Down key.
         * Adjusts the screen brightness down. */
        BRIGHTNESS_DOWN = 220,
        /** Brightness Up key.
         * Adjusts the screen brightness up. */
        BRIGHTNESS_UP = 221,
        /** Audio Track key.
         * Switches the audio tracks. */
        MEDIA_AUDIO_TRACK = 222,
        /** Sleep key.
         * Puts the device to sleep.  Behaves somewhat like {@link POWER} but it
         * has no effect if the device is already asleep. */
        SLEEP = 223,
        /** Wakeup key.
         * Wakes up the device.  Behaves somewhat like {@link POWER} but it
         * has no effect if the device is already awake. */
        WAKEUP = 224,
        /** Pairing key.
         * Initiates peripheral pairing mode. Useful for pairing remote control
         * devices or game controllers, especially if no other input mode is
         * available. */
        PAIRING = 225,
        /** Media Top Menu key.
         * Goes to the top of media menu. */
        MEDIA_TOP_MENU = 226,
        /** '11' key. */
        _11 = 227,
        /** '12' key. */
        _12 = 228,
        /** Last Channel key.
         * Goes to the last viewed channel. */
        LAST_CHANNEL = 229,
        /** TV data service key.
         * Displays data services like weather, sports. */
        TV_DATA_SERVICE = 230,
        /** Voice Assist key.
         * Launches the global voice assist activity. Not delivered to applications. */
        VOICE_ASSIST = 231,
        /** Radio key.
         * Toggles TV service / Radio service. */
        TV_RADIO_SERVICE = 232,
        /** Teletext key.
         * Displays Teletext service. */
        TV_TELETEXT = 233,
        /** Number entry key.
         * Initiates to enter multi-digit channel nubmber when each digit key is assigned
         * for selecting separate channel. Corresponds to Number Entry Mode (0x1D) of CEC
         * User Control Code. */
        TV_NUMBER_ENTRY = 234,
        /** Analog Terrestrial key.
         * Switches to analog terrestrial broadcast service. */
        TV_TERRESTRIAL_ANALOG = 235,
        /** Digital Terrestrial key.
         * Switches to digital terrestrial broadcast service. */
        TV_TERRESTRIAL_DIGITAL = 236,
        /** Satellite key.
         * Switches to digital satellite broadcast service. */
        TV_SATELLITE = 237,
        /** BS key.
         * Switches to BS digital satellite broadcasting service available in Japan. */
        TV_SATELLITE_BS = 238,
        /** CS key.
         * Switches to CS digital satellite broadcasting service available in Japan. */
        TV_SATELLITE_CS = 239,
        /** BS/CS key.
         * Toggles between BS and CS digital satellite services. */
        TV_SATELLITE_SERVICE = 240,
        /** Toggle Network key.
         * Toggles selecting broacast services. */
        TV_NETWORK = 241,
        /** Antenna/Cable key.
         * Toggles broadcast input source between antenna and cable. */
        TV_ANTENNA_CABLE = 242,
        /** HDMI #1 key.
         * Switches to HDMI input #1. */
        TV_INPUT_HDMI_1 = 243,
        /** HDMI #2 key.
         * Switches to HDMI input #2. */
        TV_INPUT_HDMI_2 = 244,
        /** HDMI #3 key.
         * Switches to HDMI input #3. */
        TV_INPUT_HDMI_3 = 245,
        /** HDMI #4 key.
         * Switches to HDMI input #4. */
        TV_INPUT_HDMI_4 = 246,
        /** Composite #1 key.
         * Switches to composite video input #1. */
        TV_INPUT_COMPOSITE_1 = 247,
        /** Composite #2 key.
         * Switches to composite video input #2. */
        TV_INPUT_COMPOSITE_2 = 248,
        /** Component #1 key.
         * Switches to component video input #1. */
        TV_INPUT_COMPONENT_1 = 249,
        /** Component #2 key.
         * Switches to component video input #2. */
        TV_INPUT_COMPONENT_2 = 250,
        /** VGA #1 key.
         * Switches to VGA (analog RGB) input #1. */
        TV_INPUT_VGA_1 = 251,
        /** Audio description key.
         * Toggles audio description off / on. */
        TV_AUDIO_DESCRIPTION = 252,
        /** Audio description mixing volume up key.
         * Louden audio description volume as compared with normal audio volume. */
        TV_AUDIO_DESCRIPTION_MIX_UP = 253,
        /** Audio description mixing volume down key.
         * Lessen audio description volume as compared with normal audio volume. */
        TV_AUDIO_DESCRIPTION_MIX_DOWN = 254,
        /** Zoom mode key.
         * Changes Zoom mode (Normal, Full, Zoom, Wide-zoom, etc.) */
        TV_ZOOM_MODE = 255,
        /** Contents menu key.
         * Goes to the title list. Corresponds to Contents Menu (0x0B) of CEC User Control
         * Code */
        TV_CONTENTS_MENU = 256,
        /** Media context menu key.
         * Goes to the context menu of media contents. Corresponds to Media Context-sensitive
         * Menu (0x11) of CEC User Control Code. */
        TV_MEDIA_CONTEXT_MENU = 257,
        /** Timer programming key.
         * Goes to the timer recording menu. Corresponds to Timer Programming (0x54) of
         * CEC User Control Code. */
        TV_TIMER_PROGRAMMING = 258,
        /** Help key. */
        HELP = 259,
        NAVIGATE_PREVIOUS = 260,
        NAVIGATE_NEXT = 261,
        NAVIGATE_IN = 262,
        NAVIGATE_OUT = 263,
        /** Primary stem key for Wear
         * Main power/reset button on watch. */
        STEM_PRIMARY = 264,
        /** Generic stem key 1 for Wear */
        STEM_1 = 265,
        /** Generic stem key 2 for Wear */
        STEM_2 = 266,
        /** Generic stem key 3 for Wear */
        STEM_3 = 267,
        /** Directional Pad Up-Left */
        DPAD_UP_LEFT = 268,
        /** Directional Pad Down-Left */
        DPAD_DOWN_LEFT = 269,
        /** Directional Pad Up-Right */
        DPAD_UP_RIGHT = 270,
        /** Directional Pad Down-Right */
        DPAD_DOWN_RIGHT = 271,
        /** Skip forward media key */
        MEDIA_SKIP_FORWARD = 272,
        /** Skip backward media key */
        MEDIA_SKIP_BACKWARD = 273,
        /** Step forward media key.
         * Steps media forward one from at a time. */
        MEDIA_STEP_FORWARD = 274,
        /** Step backward media key.
         * Steps media backward one from at a time. */
        MEDIA_STEP_BACKWARD = 275,
        /** Put device to sleep unless a wakelock is held. */
        SOFT_SLEEP = 276,
        /** Cut key. */
        CUT = 277,
        /** Copy key. */
        COPY = 278,
        /** Paste key. */
        PASTE = 279,
        /** fingerprint navigation key, up. */
        SYSTEM_NAVIGATION_UP = 280,
        /** fingerprint navigation key, down. */
        SYSTEM_NAVIGATION_DOWN = 281,
        /** fingerprint navigation key, left. */
        SYSTEM_NAVIGATION_LEFT = 282,
        /** fingerprint navigation key, right. */
        SYSTEM_NAVIGATION_RIGHT = 283,
        /** all apps */
        ALL_APPS = 284,
        /** refresh key */
        REFRESH = 285,
        /** Thumbs up key. Apps can use this to let user upvote content. */
        THUMBS_UP = 286,
        /** Thumbs down key. Apps can use this to let user downvote content. */
        THUMBS_DOWN = 287,
        /** Used to switch current account that is consuming content.
         * May be consumed by system to switch current viewer profile. */
        PROFILE_SWITCH = 288
    };
}
