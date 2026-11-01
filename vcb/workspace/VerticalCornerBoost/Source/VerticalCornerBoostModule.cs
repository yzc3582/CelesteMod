using System;

namespace Celeste.Mod.VerticalCornerBoost;

public class VerticalCornerBoostModule : EverestModule {
    public static VerticalCornerBoostModule Instance { get; private set; }

    public override Type SettingsType => typeof(VerticalCornerBoostModuleSettings);
    public static VerticalCornerBoostModuleSettings Settings => (VerticalCornerBoostModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(VerticalCornerBoostModuleSession);
    public static VerticalCornerBoostModuleSession Session => (VerticalCornerBoostModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(VerticalCornerBoostModuleSaveData);
    public static VerticalCornerBoostModuleSaveData SaveData => (VerticalCornerBoostModuleSaveData) Instance._SaveData;

    public VerticalCornerBoostModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(VerticalCornerBoostModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(VerticalCornerBoostModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        CornerBoostHooks.Load();
    }

    public override void Unload() {
        CornerBoostHooks.Unload();
    }
}